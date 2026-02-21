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