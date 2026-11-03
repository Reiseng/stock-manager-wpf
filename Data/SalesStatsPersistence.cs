using StockControl.Config.DataBaseConfig;
using StockControl.Enums;

namespace StockControl.Data
{
    public record SaleRow(DateTime Date, decimal Total, OperationType Operation, FiscalStatus Fiscal);
    public record ProductSales(string Name, decimal Quantity, decimal Amount);

    // Read-only queries for the sales charts (budgets are not sales and are left out)
    public class SalesStatsPersistence
    {
        private static readonly int[] SaleOperations = { (int)OperationType.Sale, (int)OperationType.CreditNote, (int)OperationType.DebitNote };
        private readonly DatabaseContext db;

        public SalesStatsPersistence(DatabaseContext dbContext)
        {
            db = dbContext;
        }

        // Every sale / credit note / debit note in [from, to)
        public List<SaleRow> GetSales(DateTime from, DateTime to)
        {
            using var ctx = db.CreateDbContext();
            return ctx.Checkouts
                .Where(c => c.CreatedAt >= from && c.CreatedAt < to && SaleOperations.Contains(c.OperationType))
                .Select(c => new { c.CreatedAt, c.Total, c.OperationType, c.FiscalStatus })
                .AsEnumerable()
                .Select(c => new SaleRow(c.CreatedAt, c.Total, (OperationType)c.OperationType, (FiscalStatus)c.FiscalStatus))
                .ToList();
        }

        // Best-selling products in [from, to) by amount (final price), credit notes subtracted
        public List<ProductSales> GetTopProducts(DateTime from, DateTime to, int top)
        {
            using var ctx = db.CreateDbContext();
            var creditNote = (int)OperationType.CreditNote;
            var start = from;   // "from" is a keyword inside a query expression
            var end = to;
            var lines =
                from i in ctx.CheckoutItems
                join c in ctx.Checkouts on i.CheckoutId equals c.Id
                join p in ctx.Products on i.ProductId equals p.Id
                where c.CreatedAt >= start && c.CreatedAt < end && SaleOperations.Contains(c.OperationType)
                select new Line
                {
                    Id = p.Id,
                    Name = p.Name,
                    Sign = c.OperationType == creditNote ? -1m : 1m,
                    Quantity = i.Quantity,
                    Amount = i.Quantity * i.UnitPrice * (100m + p.Tax) / 100m,
                };

            // PostgreSQL keeps every decimal of a division, more than a C# decimal can
            // hold: there the amount is rounded inside the query (SQLite works with REAL).
            if (!db.IsSqlite)
                lines = lines.Select(l => new Line { Id = l.Id, Name = l.Name, Sign = l.Sign, Quantity = l.Quantity, Amount = Math.Round(l.Amount, 4) });

            return lines
                .GroupBy(l => new { l.Id, l.Name })
                .Select(g => new
                {
                    g.Key.Name,
                    Quantity = g.Sum(l => l.Sign * l.Quantity),
                    Amount = g.Sum(l => l.Sign * l.Amount),
                })
                .Where(x => x.Amount > 0)
                .OrderByDescending(x => x.Amount)
                .Take(top)
                .AsEnumerable()
                .Select(x => new ProductSales(x.Name, x.Quantity, Math.Round(x.Amount, 2)))
                .ToList();
        }

        // A class with settable members (not a positional record) so EF can group by it
        private class Line
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public decimal Sign { get; set; }
            public decimal Quantity { get; set; }
            public decimal Amount { get; set; }
        }
    }
}
