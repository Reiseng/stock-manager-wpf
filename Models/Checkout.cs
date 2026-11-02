using StockControl.Enums;

namespace StockControl.Models
{
    public class Checkout
        {
            public int ID { get; set; }
            public Client? Client { get; set; } // opcional
            public List<CheckoutItem> Items { get; set; } = new();
            public DateTime Date { get; set; }
            public InvoiceType invoiceType { get; set; }
            public OperationType operationType { get; set; }
            public int? RelatedCheckoutId { get; set; }
            public decimal Total {get; set;}

            // ---- ARCA ----
            public FiscalStatus FiscalStatus { get; set; } = FiscalStatus.Internal;
            public int? FiscalPointOfSale { get; set; }
            public int? FiscalVoucherType { get; set; }
            public long? FiscalVoucherNumber { get; set; }
            public string? Cae { get; set; }
            public DateTime? CaeDueDate { get; set; }
            public string? FiscalMessage { get; set; }
            public bool FiscalProduction { get; set; }

            // Text for the history list: "Factura B 0001-00000023", "Pendiente", "Interno"...
            public string FiscalSummary => FiscalStatus switch
            {
                FiscalStatus.Authorized => $"{StockControl.Services.Arca.FiscalRules.VoucherTypeName(FiscalVoucherType ?? 0)} {StockControl.Services.Arca.FiscalRules.FormatNumber(FiscalPointOfSale ?? 0, FiscalVoucherNumber ?? 0)}{(FiscalProduction ? "" : " (prueba)")}",
                FiscalStatus.Pending => "Pendiente de CAE",
                FiscalStatus.Rejected => "Rechazado por ARCA",
                _ => "Interno"
            };
            public Checkout(Client? client, List<CheckoutItem> items, InvoiceType invoiceType,OperationType operationType, DateTime date, decimal total, int? relatedCheckoutId = null)
            {
                Client = client;
                Items = items;
                this.invoiceType = invoiceType;
                this.operationType = operationType;
                Date = date;
                Total = total;
                RelatedCheckoutId = relatedCheckoutId;
            }
            public Checkout() { }
        }
}