using System.IO;
using System.Net.Http;
using StockControl.Data;
using StockControl.Enums;
using StockControl.Models;
using StockControl.Utils;

namespace StockControl.Services.Arca
{
    // Everything about sending sales to ARCA.
    //
    // A sale marked "Facturar en ARCA" is saved first (stock and record are never
    // lost) with status Pending, and then the CAE is requested:
    //   - approved  -> Authorized (CAE + due date)
    //   - rejected  -> Rejected, with ARCA's message (can be fixed and re-sent)
    //   - no answer -> stays Pending and is retried later (on startup and after each sale)
    // If ARCA did authorize it but the answer got lost, the next attempt finds it
    // with FECompConsultar instead of issuing a second invoice.
    public class FiscalService
    {
        private static readonly HttpClient DefaultHttp = new() { Timeout = TimeSpan.FromSeconds(30) };
        private readonly HttpClient Http;
        private readonly CheckoutPersistence _checkouts;
        private readonly CompanyService _company;
        private readonly SemaphoreSlim _lock = new(1, 1);

        // http: only the tests pass one (a fake ARCA)
        public FiscalService(CheckoutPersistence checkouts, CompanyService company, HttpClient? http = null)
        {
            Http = http ?? DefaultHttp;
            _checkouts = checkouts;
            _company = company;
        }

        public static ArcaConfig Config => ConfigService.Config.Arca;
        public bool IsEnabled => Config.Enabled;

        public static string EnvironmentFolder(bool production)
            => Path.Combine(ConfigService.DataFolder, "arca", production ? "produccion" : "homologacion");

        public ArcaCertificateStore Certificates => new(EnvironmentFolder(Config.Production));

        // ---- Before saving a sale: clear message if it can't be invoiced ----
        public (char Letter, int VoucherType) Prepare(Checkout checkout)
        {
            if (!IsEnabled)
                throw new Exception("La facturación electrónica está desactivada (Configuración > Facturación electrónica).");
            var company = _company.GetCompanyInfo();
            if (IssuerCuit(company).Length != 11)
                throw new Exception("Cargá el CUIT del negocio (11 dígitos) en Configuración > Datos del negocio.");
            var cert = Certificates.Info
                ?? throw new Exception("Falta el certificado de ARCA. Cargalo en Configuración > Facturación electrónica, o destildá \"Facturar en ARCA\".");
            if (cert.Expired)
                throw new Exception($"El certificado de ARCA venció el {cert.ValidUntil:dd/MM/yyyy}. Hay que generar uno nuevo.");
            if (Config.PointOfSale <= 0)
                throw new Exception("Configurá el punto de venta de ARCA.");

            var letter = FiscalRules.Letter(company.taxCondition, checkout.Client?.TaxCondition ?? TaxCondition.FinalConsumer);
            var type = FiscalRules.VoucherType(letter, checkout.operationType);
            var doc = FiscalRules.Document(checkout.Client);
            var amounts = Amounts(checkout, letter);
            FiscalRules.Validate(letter, doc, amounts.Total, checkout.operationType);

            if (checkout.operationType == OperationType.CreditNote || checkout.operationType == OperationType.DebitNote)
            {
                var related = checkout.RelatedCheckoutId is int rid ? _checkouts.GetByID(rid) : null;
                if (related == null || related.FiscalStatus != FiscalStatus.Authorized)
                    throw new Exception("La factura original no tiene CAE de ARCA: esta nota también tiene que ser interna. Destildá \"Facturar en ARCA\".");
                if (related.FiscalVoucherType != FiscalRules.VoucherType(letter, OperationType.Sale))
                    throw new Exception($"La nota tiene que ser de la misma letra que la factura original ({FiscalRules.VoucherTypeName(related.FiscalVoucherType ?? 0)}). " +
                                        "Revisá la condición frente al IVA del cliente.");
            }
            return (letter, type);
        }

        // ---- Request the CAE for a saved sale ----
        public async Task<Checkout> AuthorizeAsync(int checkoutId)
        {
            await _lock.WaitAsync();
            try
            {
                var c = _checkouts.GetByID(checkoutId) ?? throw new Exception("No se encontró la venta.");
                if (c.FiscalStatus == FiscalStatus.Authorized)
                    return c;

                try
                {
                    var (letter, type) = Prepare(c);
                    var company = _company.GetCompanyInfo();
                    var cuit = IssuerCuit(company);
                    var doc = FiscalRules.Document(c.Client);
                    var amounts = Amounts(c, letter);
                    var production = Config.Production;
                    var pointOfSale = Config.PointOfSale;

                    // If the point of sale / type / environment changed since a previous
                    // attempt, that attempted number means nothing anymore
                    if (c.FiscalPointOfSale != pointOfSale || c.FiscalVoucherType != type || c.FiscalProduction != production)
                        c.FiscalVoucherNumber = null;
                    c.FiscalPointOfSale = pointOfSale;
                    c.FiscalVoucherType = type;
                    c.FiscalProduction = production;
                    c.invoiceType = FiscalRules.InvoiceTypeFor(letter);

                    var (certificate, key) = Certificates.Load() ?? throw new Exception("Falta el certificado de ARCA.");
                    using (certificate)
                    using (key)
                    {
                        var wsaa = new WsaaClient(Http, production, EnvironmentFolder(production));
                        var wsfe = new WsfeClient(Http, production);
                        var ticket = await wsaa.GetTicketAsync(certificate, key);
                        var last = await wsfe.LastAuthorizedAsync(ticket, cuit, pointOfSale, type);

                        // Recovery: a previous attempt may have been authorized without us getting the answer
                        if (c.FiscalVoucherNumber is long tried && tried <= last)
                        {
                            var found = await wsfe.ConsultAsync(ticket, cuit, pointOfSale, type, tried);
                            if (found?.Cae != null && found.Total == amounts.Total && found.DocNumber == doc.DocNumber)
                            {
                                c.FiscalStatus = FiscalStatus.Authorized;
                                c.Cae = found.Cae;
                                c.CaeDueDate = found.CaeDueDate;
                                c.FiscalMessage = "CAE recuperado de un envío anterior.";
                                _checkouts.UpdateFiscal(c);
                                return c;
                            }
                        }

                        var number = last + 1;
                        c.FiscalVoucherNumber = number;
                        c.FiscalStatus = FiscalStatus.Pending;
                        c.FiscalMessage = "Enviado a ARCA, esperando respuesta.";
                        _checkouts.UpdateFiscal(c); // remember the number BEFORE sending (for recovery)

                        AssociatedVoucher? associated = null;
                        if (c.RelatedCheckoutId is int rid && _checkouts.GetByID(rid) is Checkout original
                            && original.FiscalStatus == FiscalStatus.Authorized
                            && (c.operationType == OperationType.CreditNote || c.operationType == OperationType.DebitNote))
                        {
                            associated = new AssociatedVoucher(original.FiscalVoucherType ?? 0, original.FiscalPointOfSale ?? 0,
                                original.FiscalVoucherNumber ?? 0, cuit, original.Date);
                        }

                        var request = new VoucherRequest(pointOfSale, type, number, FiscalRules.Concept(c.Items),
                            doc.DocType, doc.DocNumber, DateTime.Today, amounts,
                            FiscalRules.ReceiverConditionId(c.Client?.TaxCondition ?? TaxCondition.FinalConsumer), associated);

                        var result = await wsfe.RequestCaeAsync(ticket, cuit, request);
                        if (result.Approved)
                        {
                            c.FiscalStatus = FiscalStatus.Authorized;
                            c.Cae = result.Cae;
                            c.CaeDueDate = result.CaeDueDate;
                            c.FiscalMessage = result.Observations.Count > 0 ? result.Summary : null;
                        }
                        else
                        {
                            c.FiscalStatus = FiscalStatus.Rejected;
                            c.FiscalVoucherNumber = null; // a rejected number is not used in ARCA
                            c.FiscalMessage = string.IsNullOrWhiteSpace(result.Summary) ? "ARCA rechazó el comprobante." : result.Summary;
                        }
                    }
                }
                catch (ArcaException ex)
                {
                    // Connection problems and configuration problems: stays pending and is retried
                    c.FiscalStatus = FiscalStatus.Pending;
                    c.FiscalMessage = ex.Message;
                    if (!ex.IsConnectionProblem)
                        ErrorLog.Write("ARCA", ex);
                }
                catch (Exception ex)
                {
                    c.FiscalStatus = FiscalStatus.Pending;
                    c.FiscalMessage = ex.Message;
                    ErrorLog.Write("ARCA", ex);
                }

                _checkouts.UpdateFiscal(c);
                return c;
            }
            finally
            {
                _lock.Release();
            }
        }

        // Internal or rejected sale -> send it to ARCA now ("Facturar en ARCA" from the history)
        public async Task<Checkout> SendAsync(int checkoutId)
        {
            var c = _checkouts.GetByID(checkoutId) ?? throw new Exception("No se encontró la venta.");
            if (c.FiscalStatus == FiscalStatus.Authorized)
                return c;
            Prepare(c); // clear message if it can't be invoiced
            c.FiscalStatus = FiscalStatus.Pending;
            c.FiscalMessage = null;
            _checkouts.UpdateFiscal(c);
            return await AuthorizeAsync(checkoutId);
        }

        // Retries every pending sale. Stops at the first connection problem (no point insisting).
        public async Task<int> RetryPendingAsync()
        {
            if (!IsEnabled)
                return 0;
            int done = 0;
            foreach (var id in _checkouts.GetPendingFiscalIds())
            {
                var c = await AuthorizeAsync(id);
                if (c.FiscalStatus == FiscalStatus.Pending)
                    break;
                done++;
            }
            return done;
        }

        // "Probar conexión" in Configuración: checks every step and says which one fails
        public async Task<string> TestConnectionAsync()
        {
            var production = Config.Production;
            var env = production ? "producción" : "homologación (prueba)";
            var wsfe = new WsfeClient(Http, production);

            var status = await wsfe.DummyAsync();
            var company = _company.GetCompanyInfo();
            var cuit = IssuerCuit(company);
            if (cuit.Length != 11)
                return $"ARCA responde ({env}): {status}.\nFalta el CUIT del negocio en Datos del negocio.";

            var loaded = Certificates.Load();
            if (loaded == null)
                return $"ARCA responde ({env}): {status}.\nFalta cargar el certificado.";

            var (certificate, key) = loaded.Value;
            using (certificate)
            using (key)
            {
                var ticket = await new WsaaClient(Http, production, EnvironmentFolder(production)).GetTicketAsync(certificate, key);
                var letter = FiscalRules.Letter(company.taxCondition, TaxCondition.FinalConsumer);
                var type = FiscalRules.VoucherType(letter, OperationType.Sale);
                var last = await wsfe.LastAuthorizedAsync(ticket, cuit, Config.PointOfSale, type);
                return $"Todo en orden ({env}).\nCertificado aceptado y servicio autorizado.\n" +
                       $"Último {FiscalRules.VoucherTypeName(type)} en el punto de venta {Config.PointOfSale}: N° {last}.";
            }
        }

        private static VoucherAmounts Amounts(Checkout c, char letter)
            => FiscalRules.Amounts(c.Items.Select(i => (i.Quantity, i.UnitPrice, i.product.Tax)), letter);

        public static string IssuerCuit(Company company)
            => new string((company.CUIT ?? "").Where(char.IsDigit).ToArray());
    }
}
