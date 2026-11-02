using StockControl.Enums;
using StockControl.Models;

namespace StockControl.Services.Arca
{
    // One VAT rate inside a voucher (WSFEv1 "AlicIva")
    public record VatLine(int ArcaId, decimal Rate, decimal Net, decimal Vat);

    // Amounts exactly as they are sent to ARCA (rounded to 2 decimals and consistent:
    // Total = Net + Vat). The sale total is calculated the same way so the receipt,
    // the history and ARCA always show the same number.
    public record VoucherAmounts(decimal Net, decimal Vat, decimal Total, IReadOnlyList<VatLine> VatLines);

    // Pure rules (no network, no database), so they can be tested.
    // ARCA codes come from the WSFEv1 parameter tables.
    public static class FiscalRules
    {
        // From this amount on, a final consumer must be identified (RG 5824/2026)
        public const decimal FinalConsumerIdThreshold = 10_000_000m;

        public const string FinalConsumerDni = "00000000";

        // ---- Voucher letter ----
        public static char Letter(TaxCondition issuer, TaxCondition receiver)
        {
            switch (issuer)
            {
                case TaxCondition.ResponsibleRegistered:
                    // Monotributistas receive "A" since 2021 (Ley 27.618)
                    return receiver == TaxCondition.ResponsibleRegistered || receiver == TaxCondition.Monotax ? 'A' : 'B';
                case TaxCondition.Monotax:
                case TaxCondition.Exempt:
                case TaxCondition.NotResponsible:
                    return 'C';
                default:
                    throw new Exception("Configurá la condición frente al IVA del negocio (Configuración > Datos del negocio). " +
                                        "\"Consumidor final\" no puede emitir facturas.");
            }
        }

        // ---- Voucher type code (CbteTipo) ----
        public static int VoucherType(char letter, OperationType operation)
        {
            int baseCode = letter switch { 'A' => 1, 'B' => 6, 'C' => 11, _ => throw new ArgumentException("Letra inválida") };
            return operation switch
            {
                OperationType.Sale => baseCode,         // Factura
                OperationType.DebitNote => baseCode + 1, // Nota de débito
                OperationType.CreditNote => baseCode + 2, // Nota de crédito
                _ => throw new Exception("Los presupuestos no se informan a ARCA.")
            };
        }

        public static string VoucherTypeName(int type) => type switch
        {
            1 => "Factura A", 2 => "Nota de débito A", 3 => "Nota de crédito A",
            6 => "Factura B", 7 => "Nota de débito B", 8 => "Nota de crédito B",
            11 => "Factura C", 12 => "Nota de débito C", 13 => "Nota de crédito C",
            _ => $"Comprobante {type}"
        };

        public static InvoiceType InvoiceTypeFor(char letter) => letter switch
        {
            'A' => InvoiceType.AFacture,
            'B' => InvoiceType.BFacture,
            _ => InvoiceType.CFacture
        };

        // "0001-00000023"
        public static string FormatNumber(int pointOfSale, long number) => $"{pointOfSale:D4}-{number:D8}";

        // ---- Receiver document (DocTipo / DocNro) ----
        public static (int DocType, long DocNumber) Document(Client? client)
        {
            var dni = client?.Dni?.Trim() ?? "";
            if (dni.Length == 0 || dni == FinalConsumerDni)
                return (99, 0);                 // Consumidor final sin identificar
            if (!long.TryParse(dni, out var number))
                throw new Exception($"El documento del cliente ({dni}) tiene caracteres inválidos.");
            return dni.Length == 11 ? (80, number)   // CUIT
                                    : (96, number);  // DNI
        }

        // ---- Receiver VAT condition (CondicionIVAReceptorId, mandatory from 1/12/2026, RG 5616) ----
        public static int ReceiverConditionId(TaxCondition condition) => condition switch
        {
            TaxCondition.ResponsibleRegistered => 1,
            TaxCondition.Exempt => 4,
            TaxCondition.FinalConsumer => 5,
            TaxCondition.Monotax => 6,
            TaxCondition.NotResponsible => 15, // IVA no alcanzado
            _ => 5
        };

        // ---- VAT rate -> ARCA id ----
        public static int VatId(decimal rate) => rate switch
        {
            0m => 3,
            2.5m => 9,
            5m => 8,
            10.5m => 4,
            21m => 5,
            27m => 6,
            _ => throw new Exception($"ARCA no acepta una alícuota de IVA de {rate:0.##}%. Las válidas son 0, 2,5, 5, 10,5, 21 y 27%.")
        };

        // ---- Amounts ----
        // Items carry the price WITHOUT VAT and their VAT rate.
        // letter 'C': the monotributista doesn't discriminate VAT, the whole amount is "neto".
        public static VoucherAmounts Amounts(IEnumerable<(decimal Quantity, decimal UnitPrice, decimal Rate)> items, char letter)
        {
            var list = items.ToList();
            var lines = list
                .GroupBy(i => i.Rate)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var net = Round(g.Sum(i => i.Quantity * i.UnitPrice));
                    var vat = Round(net * g.Key / 100m);
                    return new VatLine(letter == 'A' || letter == 'B' ? VatId(g.Key) : 0, g.Key, net, vat);
                })
                .ToList();

            var netTotal = lines.Sum(l => l.Net);
            var vatTotal = lines.Sum(l => l.Vat);
            var total = netTotal + vatTotal;

            if (letter == 'C')
                return new VoucherAmounts(total, 0m, total, Array.Empty<VatLine>());
            return new VoucherAmounts(netTotal, vatTotal, total, lines);
        }

        // Same calculation for any sale (fiscal or not), so totals always match.
        // ('-' = no letter: same rounding, without checking that the rates are ARCA rates)
        public static decimal SaleTotal(IEnumerable<CheckoutItem> items)
            => Amounts(items.Select(i => (i.Quantity, i.UnitPrice, i.product.Tax)), '-').Total;

        public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        // ---- Concept: 1 products, 2 services, 3 both ----
        public static int Concept(IEnumerable<CheckoutItem> items)
        {
            var units = items.Select(i => i.product.Unit).ToList();
            bool services = units.Any(u => u == UnitType.Service);
            bool goods = units.Any(u => u != UnitType.Service);
            return services && goods ? 3 : services ? 2 : 1;
        }

        // ---- Checks before asking for a CAE (clear messages instead of ARCA error codes) ----
        public static void Validate(char letter, (int DocType, long DocNumber) doc, decimal total, OperationType operation)
        {
            if (operation == OperationType.Budget)
                throw new Exception("Los presupuestos no se informan a ARCA. Destildá \"Facturar en ARCA\".");
            if (letter == 'A' && doc.DocType != 80)
                throw new Exception("Para emitir Factura A el cliente tiene que tener CUIT (11 dígitos).");
            if (doc.DocType == 99 && total >= FinalConsumerIdThreshold)
                throw new Exception($"Desde {FinalConsumerIdThreshold:C0} hay que identificar al cliente con DNI o CUIL. Elegí o cargá el cliente.");
        }
    }
}
