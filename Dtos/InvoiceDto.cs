using StockControl.Enums;
using StockControl.Models;

namespace StockControl.Dtos
{
    public class InvoiceDto
    {
        public string invoiceNumber { get; set; }
        public DateTime date { get; set; }
        public InvoiceType invoiceType {get; set; }
        public OperationType operationType { get; set; }
        public Company seller { get; set; }
        public ClientDto client { get; set; }
        public List<CheckoutItemDto> items { get; set; }
        public decimal subtotal { get; set; }
        public decimal tax { get; set; }
        public decimal total { get; set; }
        public InvoiceDto(string InvoiceNumber, DateTime Date,InvoiceType InvoiceType, OperationType OperationType, Company Seller, ClientDto Client, List<CheckoutItemDto> Items, decimal Subtotal, decimal Tax, decimal Total)
        {
            invoiceNumber = InvoiceNumber;
            date = Date;
            invoiceType = InvoiceType;
            operationType = OperationType;
            seller = Seller;
            client = Client;
            items = Items;
            subtotal = Subtotal;
            tax = Tax;
            total = Total;
        }
    }
}