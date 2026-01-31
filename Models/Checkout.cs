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
            public decimal Total {get; set;}
            public Checkout(Client? client, List<CheckoutItem> items, InvoiceType invoiceType, DateTime date, decimal total)
            {
                Client = client;
                Items = items;
                this.invoiceType = invoiceType;
                Date = date;
                Total = total;
            }
            public Checkout() { }
        }
}