// Checkout is the process of finalizing a sale or transaction, typically involving payment and receipt generation.
using Microsoft.Data.Sqlite;
using StockControl.Config.DataBaseConfig;
using StockControl.Enums;
using StockControl.Models;
using StockControl.Services;

namespace StockControl.Data
{
    // Delta < 0 takes stock out (sale), Delta > 0 puts it back (credit note)
    public record StockChange(int ProductId, string ProductName, decimal Delta);

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
                SELECT Id, ClientId, InvoiceType, OperationType, Total, CreatedAt, RelatedCheckoutId
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
                    (OperationType)reader.GetInt32(3),
                    DateTime.Parse(reader.GetString(5)),
                    reader.GetDecimal(4),
                    reader.IsDBNull(6) ? null : reader.GetInt32(6)
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
                    SELECT Id, ClientId, InvoiceType, OperationType, Total, CreatedAt, RelatedCheckoutId
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
                        (OperationType)reader.GetInt32(3),
                        DateTime.Parse(reader.GetString(5)),
                        reader.GetDecimal(4),
                        reader.IsDBNull(6) ? null : reader.GetInt32(6)
                    );
                    checkout.ID = checkoutId;
                    return checkout;
                }
            }
        return null;
        }
        // Sum of quantities already returned through credit notes, per product
        public Dictionary<int, decimal> GetCreditedQuantities(int relatedCheckoutId)
        {
            var result = new Dictionary<int, decimal>();
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ci.ProductId, SUM(ci.Quantity)
                FROM CheckoutItems ci
                JOIN Checkouts c ON c.Id = ci.CheckoutId
                WHERE c.RelatedCheckoutId = @relatedId AND c.OperationType = @creditNote
                GROUP BY ci.ProductId;
            ";
            command.Parameters.AddWithValue("@relatedId", relatedCheckoutId);
            command.Parameters.AddWithValue("@creditNote", (int)OperationType.CreditNote);
            using var reader = command.ExecuteReader();
            while (reader.Read())
                result[reader.GetInt32(0)] = reader.GetDecimal(1);
            return result;
        }
        public Checkout Add(Checkout _checkout, IEnumerable<StockChange> stockChanges)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            // Everything below commits together or not at all
            using var transaction = connection.BeginTransaction();

            foreach (var change in stockChanges)
            {
                using var stockCommand = connection.CreateCommand();
                if (change.Delta < 0)
                {
                    // Conditional update: the stock check and the decrement are one atomic
                    // statement, so two registers can't sell the last unit at the same time
                    stockCommand.CommandText = @"
                        UPDATE Products SET Stock = Stock - @qty
                        WHERE Id = @id AND Stock >= @qty;";
                }
                else
                {
                    stockCommand.CommandText = @"
                        UPDATE Products SET Stock = Stock + @qty
                        WHERE Id = @id;";
                }
                stockCommand.Parameters.Add("@qty", SqliteType.Real).Value = (double)Math.Abs(change.Delta);
                stockCommand.Parameters.AddWithValue("@id", change.ProductId);
                if (stockCommand.ExecuteNonQuery() == 0)
                    throw new Exception($"Stock insuficiente para {change.ProductName}");
            }

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Checkouts (ClientId, InvoiceType, OperationType, Total, CreatedAt, RelatedCheckoutId)
                VALUES (@clientId, @invoiceType, @operationType, @total, @createdAt, @relatedCheckoutId);
                SELECT last_insert_rowid();
                ";
            command.Parameters.AddWithValue("@clientId", _checkout.Client?.ID ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@invoiceType", (int)_checkout.invoiceType);
            command.Parameters.AddWithValue("@operationType", (int)_checkout.operationType);
            command.Parameters.AddWithValue("@total", _checkout.Total);
            command.Parameters.AddWithValue("@createdAt", _checkout.Date.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@relatedCheckoutId", _checkout.RelatedCheckoutId ?? (object)DBNull.Value);
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
            transaction.Commit();
            _checkout.ID = (int)checkoutId;
            return _checkout;
        }
    }
}