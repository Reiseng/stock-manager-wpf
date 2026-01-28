using System.Collections.ObjectModel;
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
            public decimal Total
                {
                    get => Items.Sum(i => i.Total);
                }
        }
}