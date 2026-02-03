// Products is any item or good that a business offers for sale to customers.
// Product data persistence involves storing and retrieving product information from a database.
using StockControl.Models;
using StockControl.Enums;
using StockControl.Config.DataBaseConfig;

namespace StockControl.Data{
public class ProductPersistence
{
    private readonly DatabaseContext db;
    public ProductPersistence(DatabaseContext dbContext)
    {
        db = dbContext;
    }
    public List<Product> GetAll(bool includeInactive)
    {
        var products = new List<Product>();
        using var connection = db.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Barcode, Brand, Name, Price, Stock, Unit, IsActive
                FROM Products
                ";
            if (!includeInactive)
            {
                command.CommandText += " WHERE IsActive = 1";
            }
            command.CommandText += ";";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var product = new Product(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetDecimal(4),
                    reader.GetDecimal(5),
                    (UnitType)reader.GetInt32(6),
                    reader.GetBoolean(7)
                );
                product.Id = reader.GetInt32(0);
                products.Add(product);
            }
            return products;
    }
    public Product? GetByID(int id, bool includeInactive)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Barcode, Brand, Name, Price, Stock, Unit, IsActive
                FROM Products
                WHERE Id = @id
                ";
            if (!includeInactive)
            {
                command.CommandText += " AND IsActive = 1";
            }
            command.CommandText += ";";
            command.Parameters.AddWithValue("@id", id);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var product = new Product(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetDecimal(4),
                    reader.GetDecimal(5),
                    (UnitType)reader.GetInt32(6),
                    reader.GetBoolean(7)
                );
                product.Id = reader.GetInt32(0);
                return product;
            }
            return null;
        }
    public Product? GetByBarcode(string barcode, bool includeInactive)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Barcode, Brand, Name, Price, Stock, Unit, IsActive
                FROM Products
                WHERE Barcode = @barcode
                ";
            if (!includeInactive)
            {
                command.CommandText += " AND IsActive = 1";
            }
            command.CommandText += ";";
            command.Parameters.AddWithValue("@barcode", barcode);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var product = new Product(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetDecimal(4),
                    reader.GetDecimal(5),
                    (UnitType)reader.GetInt32(6),
                    reader.GetBoolean(7)
                );
                product.Id = reader.GetInt32(0);
                return product;
            }
            return null;
        }
    public void Add(Product product)
    {
        using var connection = db.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Products (Name, Barcode, Brand, Price, Stock, Unit, IsActive)
                VALUES (@name, @barcode, @brand, @price, @stock, @unitType, @isActive);
                ";
            command.Parameters.AddWithValue("@name", product.Name);
            command.Parameters.AddWithValue("@barcode", product.Barcode);
            command.Parameters.AddWithValue("@brand", product.Brand);
            command.Parameters.AddWithValue("@price", product.Price);
            command.Parameters.AddWithValue("@stock", product.Stock);
            command.Parameters.AddWithValue("@unitType", (int)product.Unit);
            command.Parameters.AddWithValue("@isActive", product.IsActive ? 1 : 0);
            command.ExecuteNonQuery();
    }
    public void Remove(Product product)
    {
        using var connection = db.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Products
                SET IsActive = 0
                WHERE Id = @id;
                ";
            command.Parameters.AddWithValue("@id", product.Id);
            command.ExecuteNonQuery();
    }

    public void Update(Product product)
    {
        using var connection = db.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Products
                SET Name = @name,
                    Barcode = @barcode,
                    Brand = @brand,
                    Price = @price,
                    Stock = @stock,
                    Unit = @unitType,
                    IsActive = @isActive
                WHERE Id = @id;
                ";
            command.Parameters.AddWithValue("@name", product.Name);
            command.Parameters.AddWithValue("@barcode", product.Barcode);
            command.Parameters.AddWithValue("@brand", product.Brand);
            command.Parameters.AddWithValue("@price", product.Price);
            command.Parameters.AddWithValue("@stock", product.Stock);
            command.Parameters.AddWithValue("@unitType", (int)product.Unit);
            command.Parameters.AddWithValue("@isActive", product.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@id", product.Id);
            command.ExecuteNonQuery();
    }
    public void UpdateStock(int id, decimal quantity)
    {
        using var connection = db.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Products
                SET Stock = @stock
                WHERE Id = @id;
                ";
            command.Parameters.AddWithValue("@stock", quantity);
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
    }
}
}