using StockControl.Enums;
namespace StockControl.Models
{
    public class Product
    {
    public int Id { get; set; }               // interno
    public string Barcode { get; set; }       // lector
    public string Name { get; set; }
    public string Brand {get; set; }
    public decimal Stock { get; set; }
    public decimal Price { get; set; }
    public UnitType Unit { get; set; }


    public override string ToString()
        => $"{Name} ({Stock}) - ${Price}";

        public Product(string barcode, string brand, string name, decimal stock, decimal price, UnitType unitType)
        {
            Barcode = barcode;
            Brand = brand;
            Name = name;
            Stock = stock;
            Price = price;
            Unit = unitType;
        }
}
}