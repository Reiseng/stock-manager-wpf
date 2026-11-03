// Checkout is the process of finalizing a sale or transaction, typically involving payment and receipt generation.
using Microsoft.EntityFrameworkCore;
using StockControl.Config.DataBaseConfig;
using StockControl.Data.Ef;
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

        // The services are no longer needed (clients and products come in the same query),
        // the parameters stay so nothing else has to change.
        public CheckoutPersistence(DatabaseContext dbContext, ClientService? ClientService = null, ProductService? ProductService = null)
        {
            db = dbContext;
        }

        public List<Checkout> GetAll()
        {
            using var ctx = db.CreateDbContext();
            return WithDetails(ctx).OrderBy(c => c.Id).AsSplitQuery().AsEnumerable().Select(ToModel).ToList();
        }

        public Checkout? GetByID(int id)
        {
            using var ctx = db.CreateDbContext();
            var row = WithDetails(ctx).AsSplitQuery().FirstOrDefault(c => c.Id == id);
            return row == null ? null : ToModel(row);
        }

        // Saves everything about the ARCA side of a sale (status, number, CAE, message)
        public void UpdateFiscal(Checkout c)
        {
            using var ctx = db.CreateDbContext();
            var status = (int)c.FiscalStatus;
            var invoiceType = (int)c.invoiceType;
            DateTime? due = c.CaeDueDate?.Date;
            ctx.Checkouts.Where(x => x.Id == c.ID).ExecuteUpdate(s => s
                .SetProperty(x => x.FiscalStatus, status)
                .SetProperty(x => x.FiscalPointOfSale, c.FiscalPointOfSale)
                .SetProperty(x => x.FiscalVoucherType, c.FiscalVoucherType)
                .SetProperty(x => x.FiscalVoucherNumber, c.FiscalVoucherNumber)
                .SetProperty(x => x.Cae, c.Cae)
                .SetProperty(x => x.CaeDueDate, due)
                .SetProperty(x => x.FiscalMessage, c.FiscalMessage)
                .SetProperty(x => x.FiscalProduction, c.FiscalProduction)
                .SetProperty(x => x.InvoiceType, invoiceType));
        }

        // Sales waiting for a CAE (no internet, ARCA down...).
        // pointOfSale: only the ones of that register (several registers share one
        // database, each with its own point of sale; the other registers' sales
        // are theirs to send). Sales without one (older versions) are anyone's.
        public List<int> GetPendingFiscalIds(int? pointOfSale = null)
        {
            using var ctx = db.CreateDbContext();
            var pending = (int)FiscalStatus.Pending;
            var query = ctx.Checkouts.Where(c => c.FiscalStatus == pending);
            if (pointOfSale is int pos)
                query = query.Where(c => c.FiscalPointOfSale == pos || c.FiscalPointOfSale == null);
            return query.OrderBy(c => c.Id).Select(c => c.Id).ToList();
        }

        // Sum of quantities already returned through credit notes, per product
        public Dictionary<int, decimal> GetCreditedQuantities(int relatedCheckoutId)
        {
            using var ctx = db.CreateDbContext();
            var creditNote = (int)OperationType.CreditNote;
            return ctx.CheckoutItems
                .Where(i => ctx.Checkouts.Any(c => c.Id == i.CheckoutId && c.RelatedCheckoutId == relatedCheckoutId && c.OperationType == creditNote))
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToDictionary(x => x.ProductId, x => x.Quantity);
        }

        // Stock and the sale are saved together or not at all
        public Checkout Add(Checkout checkout, IEnumerable<StockChange> stockChanges)
        {
            using var ctx = db.CreateDbContext();
            using var transaction = ctx.Database.BeginTransaction();

            foreach (var change in stockChanges)
            {
                var id = change.ProductId;
                var qty = Math.Abs(change.Delta);
                int updated = change.Delta < 0
                    // Conditional update: the stock check and the decrement are one atomic
                    // statement, so two registers can't sell the last unit at the same time
                    ? ctx.Products.Where(p => p.Id == id && p.Stock >= qty)
                        .ExecuteUpdate(s => s.SetProperty(p => p.Stock, p => p.Stock - qty))
                    : ctx.Products.Where(p => p.Id == id)
                        .ExecuteUpdate(s => s.SetProperty(p => p.Stock, p => p.Stock + qty));
                if (updated == 0)
                    throw new Exception($"Stock insuficiente para {change.ProductName}");
            }

            var row = new CheckoutRow
            {
                ClientId = checkout.Client?.ID,
                InvoiceType = (int)checkout.invoiceType,
                OperationType = (int)checkout.operationType,
                Total = checkout.Total,
                CreatedAt = checkout.Date,
                RelatedCheckoutId = checkout.RelatedCheckoutId,
                FiscalStatus = (int)checkout.FiscalStatus,
                FiscalProduction = checkout.FiscalProduction,
                FiscalPointOfSale = checkout.FiscalPointOfSale,
                Items = checkout.Items.Select(i => new CheckoutItemRow
                {
                    ProductId = i.product.Id,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                }).ToList(),
            };
            ctx.Checkouts.Add(row);
            ctx.SaveChanges();
            transaction.Commit();

            checkout.ID = row.Id;
            return checkout;
        }

        private static IQueryable<CheckoutRow> WithDetails(StockDbContext ctx)
            => ctx.Checkouts
                .Include(c => c.Client)
                .Include(c => c.Items.OrderBy(i => i.Id)).ThenInclude(i => i.Product);

        private static Checkout ToModel(CheckoutRow r)
        {
            var items = r.Items.Select(i => new CheckoutItem
            {
                product = i.Product != null ? ProductPersistence.ToModel(i.Product) : null!,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
            }).ToList();

            return new Checkout(
                r.Client != null ? ClientPersistence.ToModel(r.Client) : null,
                items,
                (InvoiceType)r.InvoiceType,
                (OperationType)r.OperationType,
                r.CreatedAt,
                r.Total,
                r.RelatedCheckoutId)
            {
                ID = r.Id,
                FiscalStatus = (FiscalStatus)r.FiscalStatus,
                FiscalPointOfSale = r.FiscalPointOfSale,
                FiscalVoucherType = r.FiscalVoucherType,
                FiscalVoucherNumber = r.FiscalVoucherNumber,
                Cae = r.Cae,
                CaeDueDate = r.CaeDueDate,
                FiscalMessage = r.FiscalMessage,
                FiscalProduction = r.FiscalProduction,
            };
        }
    }
}
