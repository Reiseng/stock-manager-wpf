using StockControl.Enums;
namespace StockControl.Models{
public class CheckoutItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; }
    public UnitType Unit { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total => Quantity * UnitPrice;
    public static CheckoutItemDto FromModel(CheckoutItem model)
        {
            return new CheckoutItemDto
            {
                ProductName = model.product.Name,
                Quantity = model.Quantity,
                UnitPrice = model.UnitPrice
            };
        }
}
}