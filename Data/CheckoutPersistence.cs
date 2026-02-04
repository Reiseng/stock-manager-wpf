// Checkout is the process of finalizing a sale or transaction, typically involving payment and receipt generation.
using StockControl.Config.DataBaseConfig;
using StockControl.Enums;
using StockControl.Models;
using StockControl.Services;

namespace StockControl.Data
{
    public class CheckoutPersistence
    {
        private readonly DatabaseContext db;
        private readonly ClientService clientService;
        private readonly ProductService productService;
        public CheckoutPersistence(DatabaseContext dbContext, ClientService ClientService, ProductService ProductService)
        {
            db = dbContext;
            clientService = ClientService;
            productService = ProductService;
        }

        public List<Checkout> GetAll()
        {
        var checkouts = new List<Checkout>();
        var itemsByCheckout = new Dictionary<int, List<CheckoutItem>>();

        using var connection = db.CreateConnection();
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT Id, CheckoutId, ProductId, Quantity, UnitPrice
                FROM CheckoutItems;
            ";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var checkoutId = reader.GetInt32(1);

                var item = new CheckoutItem
                {
                    product = productService.GetProductByID(reader.GetInt32(2), includeInactive: true),
                    Quantity = reader.GetDecimal(3),
                    UnitPrice = reader.GetDecimal(4)
                };

                if (!itemsByCheckout.ContainsKey(checkoutId))
                    itemsByCheckout[checkoutId] = new List<CheckoutItem>();

                itemsByCheckout[checkoutId].Add(item);
            }
        }
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT Id, ClientId, InvoiceType, Total, CreatedAt
                FROM Checkouts;
            ";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                int checkoutId = reader.GetInt32(0);
                Client client = clientService.GetClientByID(reader.GetInt32(1), includeInactive: true);
                var checkout = new Checkout(
                    client,
                    itemsByCheckout.ContainsKey(checkoutId)
                        ? itemsByCheckout[checkoutId]
                        : new List<CheckoutItem>(),
                    (InvoiceType)reader.GetInt32(2),
                    DateTime.Parse(reader.GetString(4)),
                    reader.GetDecimal(3)
                );
                checkout.ID = checkoutId;
                checkouts.Add(checkout);
            }
}       return checkouts;
    }
        public Checkout GetByID(int id)
        {
            var itemsByCheckout = new Dictionary<int, List<CheckoutItem>>();
            using var connection = db.CreateConnection();
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT Id, CheckoutId, ProductId, Quantity, UnitPrice
                    FROM CheckoutItems WHERE CheckoutId = @id;
                ";

                command.Parameters.AddWithValue("@id", id);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var checkoutId = reader.GetInt32(1);

                    var item = new CheckoutItem
                    {
                        product = productService.GetProductByID(reader.GetInt32(2), includeInactive: true),
                        Quantity = reader.GetDecimal(3),
                        UnitPrice = reader.GetDecimal(4)
                    };

                    if (!itemsByCheckout.ContainsKey(checkoutId))
                        itemsByCheckout[checkoutId] = new List<CheckoutItem>();

                    itemsByCheckout[checkoutId].Add(item);
                }
            }
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT Id, ClientId, InvoiceType, Total, CreatedAt
                    FROM Checkouts WHERE Id = @id;
                ";

                command.Parameters.AddWithValue("@id", id);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int checkoutId = reader.GetInt32(0);
                    Client client = clientService.GetClientByID(reader.GetInt32(1), includeInactive: true);
                    var checkout = new Checkout(
                        client,
                        itemsByCheckout.ContainsKey(checkoutId)
                            ? itemsByCheckout[checkoutId]
                            : new List<CheckoutItem>(),
                        (InvoiceType)reader.GetInt32(2),
                        DateTime.Parse(reader.GetString(4)),
                        reader.GetDecimal(3)
                    );
                    checkout.ID = checkoutId;
                    return checkout;
                }
            }
        return null;
        }
        public Checkout Add(Checkout _checkout)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Checkouts (ClientId, InvoiceType, Total, CreatedAt)
                VALUES (@clientId, @invoiceType, @total, @createdAt);
                SELECT last_insert_rowid();
                ";
            command.Parameters.AddWithValue("@clientId", _checkout.Client?.ID ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@invoiceType", (int)_checkout.invoiceType);
            command.Parameters.AddWithValue("@total", _checkout.Total);
            command.Parameters.AddWithValue("@createdAt", _checkout.Date.ToString("yyyy-MM-dd HH:mm:ss"));
            var checkoutId = (long)command.ExecuteScalar()!;

            foreach (var item in _checkout.Items)
            {
                using var itemCommand = connection.CreateCommand();
                itemCommand.CommandText = @"
                    INSERT INTO CheckoutItems (CheckoutId, ProductId, Quantity, UnitPrice)
                    VALUES (@checkoutId, @productId, @quantity, @unitPrice);
                    ";
                itemCommand.Parameters.AddWithValue("@checkoutId", checkoutId);
                itemCommand.Parameters.AddWithValue("@productId", item.product.Id);
                itemCommand.Parameters.AddWithValue("@quantity", item.Quantity);
                itemCommand.Parameters.AddWithValue("@unitPrice", item.UnitPrice);
                itemCommand.ExecuteNonQuery();
            }
            _checkout.ID = (int)checkoutId;
            return _checkout;
        }
    }
}