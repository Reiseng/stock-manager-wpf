using StockControl.Enums;
using StockControl.Models;
namespace StockControl.Dtos
{
    public class ProductDto
    {
        public int Id { get; set; }               // interno
        public string? Barcode { get; set; }       // lector
        public string? Brand {get; set; }
        public string? Name { get; set; }
        public decimal? Stock { get; set; }
        public decimal? Price { get; set; }
        public UnitType? Unit { get; set; }
        public ProductDto(int id, string? barcode, string? brand, string? name, decimal? stock, decimal? price, UnitType? unitType)
        {
            Id = id;
            Barcode = barcode;
            Brand = brand;
            Name = name;
            Stock = stock;
            Price = price;
            Unit = unitType;
        }
        public ProductDto(){}

        internal static ProductDto? FromModel(Product product)
        {
            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Barcode = product.Barcode,
                Brand = product.Barcode,
                Stock = product.Stock,
                Price = product.Price,
                Unit = product.Unit
            };
        }
    }
}