// Products is handled in memory to keep the project simple and focused on core logic.
// This can be replaced with persistence later (DB or file storage).
using StockControl.Models;

namespace StockControl.Data{
public class ProductPersistence
{
        private readonly List<Product> products = new();

    public IReadOnlyList<Product> GetAll()
    {
        return products;
    }
    public Product? GetByID(int id)
        {
            return products.FirstOrDefault(p => p.Id == id);
        }
    public Product? GetByBarcode(string barcode)
        {
            return products.FirstOrDefault(p => p.Barcode == barcode);
        }
    public void Add(Product product)
    {
        product.Id = products.Count()+1;
        products.Add(product);
    }
    public void Remove(Product product)
    {
        products.Remove(product);
    }

    public void Update(Product product)
    {
        var existing = products.FirstOrDefault(p => p.Id == product.Id);
        if (existing == null)
            throw new Exception("Producto no encontrado");

        existing.Name = product.Name;
        existing.Price = product.Price;
        existing.Stock = product.Stock;
        existing.Unit = product.Unit;
    }
    public void UpdateStock(int id, decimal quantity)
    {
        var product = products.FirstOrDefault(p => p.Id == id);
        if (product == null)
            throw new Exception("Producto no encontrado");

        product.Stock += quantity;
    }
}
}