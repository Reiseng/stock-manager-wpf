namespace StockControl.Models
{
    public class CheckoutItem
        {
            public Product product { get; set; }
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Total => Quantity * UnitPrice;
        }
}