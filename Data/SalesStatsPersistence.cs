using System.Globalization;
using StockControl.Config.DataBaseConfig;
using StockControl.Enums;

namespace StockControl.Data
{
    public record SaleRow(DateTime Date, decimal Total, OperationType Operation, FiscalStatus Fiscal);
    public record ProductSales(string Name, decimal Quantity, decimal Amount);

    // Read-only queries for the sales charts (budgets are not sales and are left out)
    public class SalesStatsPersistence
    {
        private const string DateFormat = "yyyy-MM-dd HH:mm:ss";
        private readonly DatabaseContext db;

        public SalesStatsPersistence(DatabaseContext dbContext)
        {
            db = dbContext;
        }

        // Every sale / credit note / debit note in [from, to)
        public List<SaleRow> GetSales(DateTime from, DateTime to)
        {
            var rows = new List<SaleRow>();
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT CreatedAt, Total, OperationType, FiscalStatus
                FROM Checkouts
                WHERE CreatedAt >= @from AND CreatedAt < @to AND OperationType IN (0, 2, 3);";
            command.Parameters.AddWithValue("@from", from.ToString(DateFormat));
            command.Parameters.AddWithValue("@to", to.ToString(DateFormat));
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new SaleRow(
                    ParseDate(reader.GetString(0)),
                    Convert.ToDecimal(reader.GetDouble(1)),
                    (OperationType)reader.GetInt32(2),
                    (FiscalStatus)reader.GetInt32(3)));
            }
            return rows;
        }

        // Best-selling products in [from, to) by amount (final price), credit notes subtracted
        public List<ProductSales> GetTopProducts(DateTime from, DateTime to, int top)
        {
            var list = new List<ProductSales>();
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT p.Name,
                       SUM(CASE WHEN c.OperationType = 2 THEN -i.Quantity ELSE i.Quantity END) AS Qty,
                       SUM(CASE WHEN c.OperationType = 2 THEN -1 ELSE 1 END * i.Quantity * i.UnitPrice * (1 + p.Tax / 100.0)) AS Amount
                FROM CheckoutItems i
                JOIN Checkouts c ON c.Id = i.CheckoutId
                JOIN Products p ON p.Id = i.ProductId
                WHERE c.CreatedAt >= @from AND c.CreatedAt < @to AND c.OperationType IN (0, 2, 3)
                GROUP BY p.Id, p.Name
                HAVING Amount > 0
                ORDER BY Amount DESC
                LIMIT @top;";
            command.Parameters.AddWithValue("@from", from.ToString(DateFormat));
            command.Parameters.AddWithValue("@to", to.ToString(DateFormat));
            command.Parameters.AddWithValue("@top", top);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ProductSales(
                    reader.GetString(0),
                    Convert.ToDecimal(reader.GetDouble(1)),
                    Math.Round(Convert.ToDecimal(reader.GetDouble(2)), 2)));
            }
            return list;
        }

        private static DateTime ParseDate(string text)
            => DateTime.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d
                : DateTime.Parse(text, CultureInfo.InvariantCulture);
    }
}
