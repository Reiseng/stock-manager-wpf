using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace StockControl.Services.Arca
{
    // An error ARCA answered (or couldn't be reached). Message is ready to show.
    public class ArcaException : Exception
    {
        public bool IsConnectionProblem { get; }
        public ArcaException(string message, bool isConnectionProblem = false, Exception? inner = null)
            : base(message, inner)
        {
            IsConnectionProblem = isConnectionProblem;
        }
    }

    public static class ArcaEndpoints
    {
        public static string Wsaa(bool production) => production
            ? "https://wsaa.afip.gov.ar/ws/services/LoginCms"
            : "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";

        public static string Wsfe(bool production) => production
            ? "https://servicios1.afip.gov.ar/wsfev1/service.asmx"
            : "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";
    }

    public record AccessTicket(string Token, string Sign, DateTimeOffset ExpiresAt);

    // ===================== WSAA: authentication =====================
    // Signs a "ticket request" (TRA) with the certificate and exchanges it for a
    // Token + Sign valid for 12 hours. The ticket is cached on disk: ARCA refuses
    // a new one while the previous is still valid.
    public class WsaaClient
    {
        private readonly HttpClient _http;
        private readonly bool _production;
        private readonly string _cachePath;

        public WsaaClient(HttpClient http, bool production, string cacheFolder)
        {
            _http = http;
            _production = production;
            _cachePath = Path.Combine(cacheFolder, "ticket-wsfe.json");
        }

        public async Task<AccessTicket> GetTicketAsync(X509Certificate2 certificate, RSA key, CancellationToken ct = default)
        {
            var cached = ReadCache();
            if (cached != null && cached.ExpiresAt > DateTimeOffset.Now.AddMinutes(10))
                return cached;

            var tra = BuildTra(DateTimeOffset.Now, "wsfe");
            var cms = Convert.ToBase64String(SignTra(tra, certificate, key));
            var envelope =
                "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:wsaa=\"http://wsaa.view.sua.dvadac.desein.afip.gov\">" +
                "<soapenv:Header/><soapenv:Body><wsaa:loginCms><wsaa:in0>" + cms + "</wsaa:in0></wsaa:loginCms></soapenv:Body></soapenv:Envelope>";

            string response;
            try
            {
                using var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPAction", "\"\"");
                using var result = await _http.PostAsync(ArcaEndpoints.Wsaa(_production), content, ct);
                response = await result.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                throw new ArcaException("No se pudo conectar con ARCA (autenticación). Revisá la conexión a internet.", true, ex);
            }

            var ticket = ParseLoginResponse(response);
            WriteCache(ticket);
            return ticket;
        }

        public static string BuildTra(DateTimeOffset now, string service)
        {
            string Fmt(DateTimeOffset d) => d.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
            return new XElement("loginTicketRequest", new XAttribute("version", "1.0"),
                new XElement("header",
                    new XElement("uniqueId", now.ToUnixTimeSeconds()),
                    new XElement("generationTime", Fmt(now.AddMinutes(-10))),
                    new XElement("expirationTime", Fmt(now.AddMinutes(10)))),
                new XElement("service", service)).ToString(SaveOptions.DisableFormatting);
        }

        // CMS (PKCS#7) with the TRA inside, signed with the certificate
        public static byte[] SignTra(string tra, X509Certificate2 certificate, RSA key)
        {
            var cms = new SignedCms(new ContentInfo(Encoding.UTF8.GetBytes(tra)), detached: false);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate, key)
            {
                IncludeOption = X509IncludeOption.EndCertOnly,
                DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1") // SHA-256
            };
            cms.ComputeSignature(signer);
            return cms.Encode();
        }

        public static AccessTicket ParseLoginResponse(string soap)
        {
            XDocument doc;
            try { doc = XDocument.Parse(soap); }
            catch (Exception ex) { throw new ArcaException("ARCA devolvió una respuesta inválida al autenticar.", true, ex); }

            var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value;
            if (fault != null)
            {
                if (fault.Contains("alreadyAuthenticated", StringComparison.OrdinalIgnoreCase) ||
                    fault.Contains("ya posee un TA valido", StringComparison.OrdinalIgnoreCase))
                    throw new ArcaException("ARCA indica que ya hay un acceso vigente para este certificado, pero el programa no lo tiene guardado " +
                                            "(por ejemplo, se usó el mismo certificado en otra PC). Esperá unos minutos y volvé a intentar; " +
                                            "como máximo se libera a las 12 horas.");
                if (fault.Contains("notAuthorized", StringComparison.OrdinalIgnoreCase) ||
                    fault.Contains("no autorizado", StringComparison.OrdinalIgnoreCase))
                    throw new ArcaException("El certificado no está autorizado para Facturación Electrónica (wsfe). " +
                                            "Falta crear la autorización/relación del servicio en ARCA (ver la guía).");
                if (fault.Contains("cms.cert", StringComparison.OrdinalIgnoreCase) || fault.Contains("expired", StringComparison.OrdinalIgnoreCase))
                    throw new ArcaException($"ARCA rechazó el certificado: {fault}");
                throw new ArcaException($"ARCA rechazó la autenticación: {fault}");
            }

            var inner = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "loginCmsReturn")?.Value
                ?? throw new ArcaException("ARCA no devolvió el acceso (loginCmsReturn).");
            var ticket = XDocument.Parse(inner);
            string Get(string name) => ticket.Descendants(name).FirstOrDefault()?.Value
                ?? throw new ArcaException($"Falta '{name}' en la respuesta de ARCA.");
            return new AccessTicket(Get("token"), Get("sign"),
                DateTimeOffset.Parse(Get("expirationTime"), CultureInfo.InvariantCulture));
        }

        private AccessTicket? ReadCache()
        {
            try
            {
                return File.Exists(_cachePath) ? JsonSerializer.Deserialize<AccessTicket>(File.ReadAllText(_cachePath)) : null;
            }
            catch (Exception) { return null; }
        }

        private void WriteCache(AccessTicket ticket)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
                File.WriteAllText(_cachePath, JsonSerializer.Serialize(ticket));
            }
            catch (Exception) { }
        }

        public void ClearCache()
        {
            try { if (File.Exists(_cachePath)) File.Delete(_cachePath); } catch (Exception) { }
        }
    }

    // ===================== WSFEv1: electronic invoices =====================

    public record AssociatedVoucher(int Type, int PointOfSale, long Number, string IssuerCuit, DateTime Date);

    public record VoucherRequest(
        int PointOfSale,
        int VoucherType,
        long Number,
        int Concept,
        int DocType,
        long DocNumber,
        DateTime Date,
        VoucherAmounts Amounts,
        int ReceiverConditionId,
        AssociatedVoucher? Associated);

    public record ArcaMessage(int Code, string Text)
    {
        public override string ToString() => $"{Code}: {Text}";
    }

    public record CaeResult(bool Approved, string? Cae, DateTime? CaeDueDate, IReadOnlyList<ArcaMessage> Errors, IReadOnlyList<ArcaMessage> Observations)
    {
        public string Summary => string.Join(" | ", Errors.Concat(Observations).Select(m => m.ToString()));
    }

    public record ConsultedVoucher(long Number, decimal Total, long DocNumber, string? Cae, DateTime? CaeDueDate);

    public class WsfeClient
    {
        public const string Ns = "http://ar.gov.afip.dif.FEV1/";
        private static readonly XNamespace X = Ns;
        private readonly HttpClient _http;
        private readonly bool _production;

        public WsfeClient(HttpClient http, bool production)
        {
            _http = http;
            _production = production;
        }

        // Service status (doesn't need a certificate)
        public async Task<string> DummyAsync(CancellationToken ct = default)
        {
            var doc = await CallAsync("FEDummy", new XElement(X + "FEDummy"), ct);
            string V(string n) => doc.Descendants().FirstOrDefault(e => e.Name.LocalName == n)?.Value ?? "?";
            return $"AppServer {V("AppServer")}, DbServer {V("DbServer")}, AuthServer {V("AuthServer")}";
        }

        public async Task<long> LastAuthorizedAsync(AccessTicket ticket, string cuit, int pointOfSale, int voucherType, CancellationToken ct = default)
        {
            var doc = await CallAsync("FECompUltimoAutorizado", new XElement(X + "FECompUltimoAutorizado",
                Auth(ticket, cuit),
                new XElement(X + "PtoVta", pointOfSale),
                new XElement(X + "CbteTipo", voucherType)), ct);
            ThrowOnErrors(doc);
            var nro = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "CbteNro")?.Value;
            return long.TryParse(nro, out var n) ? n : 0;
        }

        public async Task<CaeResult> RequestCaeAsync(AccessTicket ticket, string cuit, VoucherRequest request, CancellationToken ct = default)
        {
            var doc = await CallAsync("FECAESolicitar", BuildCaeRequest(ticket, cuit, request), ct);
            return ParseCaeResponse(doc);
        }

        // Looks up a voucher already sent (to recover a CAE whose answer got lost)
        public async Task<ConsultedVoucher?> ConsultAsync(AccessTicket ticket, string cuit, int pointOfSale, int voucherType, long number, CancellationToken ct = default)
        {
            var doc = await CallAsync("FECompConsultar", new XElement(X + "FECompConsultar",
                Auth(ticket, cuit),
                new XElement(X + "FeCompConsReq",
                    new XElement(X + "CbteTipo", voucherType),
                    new XElement(X + "CbteNro", number),
                    new XElement(X + "PtoVta", pointOfSale))), ct);
            var result = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ResultGet");
            if (result == null)
                return null; // 602 = no existe
            string? V(string n) => result.Elements().FirstOrDefault(e => e.Name.LocalName == n)?.Value;
            return new ConsultedVoucher(
                long.Parse(V("CbteDesde") ?? "0"),
                decimal.Parse(V("ImpTotal") ?? "0", CultureInfo.InvariantCulture),
                long.Parse(V("DocNro") ?? "0"),
                V("CodAutorizacion"),
                ParseDate(V("FchVto")));
        }

        // ---------- XML ----------
        private static XElement Auth(AccessTicket ticket, string cuit) => new(X + "Auth",
            new XElement(X + "Token", ticket.Token),
            new XElement(X + "Sign", ticket.Sign),
            new XElement(X + "Cuit", cuit));

        private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        private static string Date(DateTime value) => value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        // Element order follows the WSFEv1 WSDL (it matters)
        public static XElement BuildCaeRequest(AccessTicket ticket, string cuit, VoucherRequest r)
        {
            var a = r.Amounts;
            var detail = new XElement(X + "FECAEDetRequest",
                new XElement(X + "Concepto", r.Concept),
                new XElement(X + "DocTipo", r.DocType),
                new XElement(X + "DocNro", r.DocNumber),
                new XElement(X + "CbteDesde", r.Number),
                new XElement(X + "CbteHasta", r.Number),
                new XElement(X + "CbteFch", Date(r.Date)),
                new XElement(X + "ImpTotal", Money(a.Total)),
                new XElement(X + "ImpTotConc", "0.00"),
                new XElement(X + "ImpNeto", Money(a.Net)),
                new XElement(X + "ImpOpEx", "0.00"),
                new XElement(X + "ImpTrib", "0.00"),
                new XElement(X + "ImpIVA", Money(a.Vat)));

            if (r.Concept != 1) // services: period and payment due date are mandatory
            {
                detail.Add(new XElement(X + "FchServDesde", Date(r.Date)));
                detail.Add(new XElement(X + "FchServHasta", Date(r.Date)));
                detail.Add(new XElement(X + "FchVtoPago", Date(r.Date)));
            }

            detail.Add(new XElement(X + "MonId", "PES"));
            detail.Add(new XElement(X + "MonCotiz", "1"));
            detail.Add(new XElement(X + "CondicionIVAReceptorId", r.ReceiverConditionId));

            if (r.Associated != null)
            {
                var asoc = r.Associated;
                detail.Add(new XElement(X + "CbtesAsoc",
                    new XElement(X + "CbteAsoc",
                        new XElement(X + "Tipo", asoc.Type),
                        new XElement(X + "PtoVta", asoc.PointOfSale),
                        new XElement(X + "Nro", asoc.Number),
                        new XElement(X + "Cuit", asoc.IssuerCuit),
                        new XElement(X + "CbteFch", Date(asoc.Date)))));
            }

            if (a.VatLines.Count > 0)
            {
                detail.Add(new XElement(X + "Iva", a.VatLines.Select(l =>
                    new XElement(X + "AlicIva",
                        new XElement(X + "Id", l.ArcaId),
                        new XElement(X + "BaseImp", Money(l.Net)),
                        new XElement(X + "Importe", Money(l.Vat))))));
            }

            return new XElement(X + "FECAESolicitar",
                Auth(ticket, cuit),
                new XElement(X + "FeCAEReq",
                    new XElement(X + "FeCabReq",
                        new XElement(X + "CantReg", 1),
                        new XElement(X + "PtoVta", r.PointOfSale),
                        new XElement(X + "CbteTipo", r.VoucherType)),
                    new XElement(X + "FeDetReq", detail)));
        }

        public static CaeResult ParseCaeResponse(XDocument doc)
        {
            List<ArcaMessage> Messages(string container, string item) => doc.Descendants()
                .Where(e => e.Name.LocalName == container)
                .SelectMany(c => c.Elements().Where(e => e.Name.LocalName == item))
                .Select(e => new ArcaMessage(
                    int.TryParse(e.Elements().FirstOrDefault(x => x.Name.LocalName == "Code")?.Value, out var c) ? c : 0,
                    e.Elements().FirstOrDefault(x => x.Name.LocalName == "Msg")?.Value ?? ""))
                .ToList();

            var errors = Messages("Errors", "Err");
            var observations = Messages("Observaciones", "Obs");
            var det = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "FECAEDetResponse");
            string? V(string n) => det?.Elements().FirstOrDefault(e => e.Name.LocalName == n)?.Value;

            var approved = V("Resultado") == "A" && !string.IsNullOrWhiteSpace(V("CAE"));
            return new CaeResult(approved, approved ? V("CAE") : null, approved ? ParseDate(V("CAEFchVto")) : null, errors, observations);
        }

        private static DateTime? ParseDate(string? value)
            => DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        private static void ThrowOnErrors(XDocument doc)
        {
            var err = doc.Descendants().Where(e => e.Name.LocalName == "Err").Select(e =>
                $"{e.Elements().FirstOrDefault(x => x.Name.LocalName == "Code")?.Value}: {e.Elements().FirstOrDefault(x => x.Name.LocalName == "Msg")?.Value}").ToList();
            if (err.Count > 0)
                throw new ArcaException("ARCA respondió con error: " + string.Join(" | ", err));
        }

        private async Task<XDocument> CallAsync(string operation, XElement body, CancellationToken ct)
        {
            var envelope = new XElement(XName.Get("Envelope", "http://schemas.xmlsoap.org/soap/envelope/"),
                new XAttribute(XNamespace.Xmlns + "soap", "http://schemas.xmlsoap.org/soap/envelope/"),
                new XElement(XName.Get("Body", "http://schemas.xmlsoap.org/soap/envelope/"), body));

            string text;
            try
            {
                using var content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPAction", $"\"{Ns}{operation}\"");
                using var response = await _http.PostAsync(ArcaEndpoints.Wsfe(_production), content, ct);
                text = await response.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                throw new ArcaException("No se pudo conectar con ARCA (facturación). Revisá la conexión a internet.", true, ex);
            }

            XDocument doc;
            try { doc = XDocument.Parse(text); }
            catch (Exception ex) { throw new ArcaException("ARCA devolvió una respuesta inválida.", true, ex); }

            var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value;
            if (fault != null)
                throw new ArcaException($"ARCA rechazó el pedido: {fault}");
            return doc;
        }
    }
}
