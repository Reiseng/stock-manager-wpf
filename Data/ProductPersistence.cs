// Products is any item or good that a business offers for sale to customers.
// Product data persistence involves storing and retrieving product information from a database.
using Microsoft.EntityFrameworkCore;
using StockControl.Config.DataBaseConfig;
using StockControl.Data.Ef;
using StockControl.Enums;
using StockControl.Models;

namespace StockControl.Data
{
    public class ProductPersistence
    {
        private readonly DatabaseContext db;

        public ProductPersistence(DatabaseContext dbContext)
        {
            db = dbContext;
        }

        public List<Product> GetAll(bool includeInactive)
        {
            using var ctx = db.CreateDbContext();
            return Query(ctx, includeInactive).OrderBy(p => p.Id).AsEnumerable().Select(ToModel).ToList();
        }

        public Product? GetByID(int id, bool includeInactive)
        {
            using var ctx = db.CreateDbContext();
            var row = Query(ctx, includeInactive).FirstOrDefault(p => p.Id == id);
            return row == null ? null : ToModel(row);
        }

        public Product? GetByBarcode(string barcode, bool includeInactive)
        {
            using var ctx = db.CreateDbContext();
            var row = Query(ctx, includeInactive).FirstOrDefault(p => p.Barcode == barcode);
            return row == null ? null : ToModel(row);
        }

        public void Add(Product product)
        {
            using var ctx = db.CreateDbContext();
            var row = new ProductRow();
            Copy(product, row);
            ctx.Products.Add(row);
            ctx.SaveChanges();
            product.Id = row.Id;
        }

        // Products are never deleted (old sales point to them): they are deactivated
        public void Remove(Product product)
        {
            using var ctx = db.CreateDbContext();
            ctx.Products.Where(p => p.Id == product.Id)
                .ExecuteUpdate(s => s.SetProperty(p => p.IsActive, false));
        }

        public void Update(Product product)
        {
            using var ctx = db.CreateDbContext();
            var row = new ProductRow { Id = product.Id };
            Copy(product, row);
            ctx.Products.Update(row);
            ctx.SaveChanges();
        }

        public void UpdateStock(int id, decimal quantity)
        {
            using var ctx = db.CreateDbContext();
            ctx.Products.Where(p => p.Id == id)
                .ExecuteUpdate(s => s.SetProperty(p => p.Stock, quantity));
        }

        private static IQueryable<ProductRow> Query(StockDbContext ctx, bool includeInactive)
            => includeInactive ? ctx.Products : ctx.Products.Where(p => p.IsActive);

        private static void Copy(Product product, ProductRow row)
        {
            row.Barcode = product.Barcode;
            row.Name = product.Name;
            row.Brand = product.Brand;
            row.Price = product.Price;
            row.Stock = product.Stock;
            row.Unit = (int)product.Unit;
            row.Tax = product.Tax;
            row.IsActive = product.IsActive;
        }

        internal static Product ToModel(ProductRow r)
            => new(r.Barcode, r.Brand, r.Name, r.Price, r.Stock, (UnitType)r.Unit, r.Tax, r.IsActive) { Id = r.Id };
    }
}
