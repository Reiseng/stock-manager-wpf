// Company is a business or organization that provides goods or services to customers.
// Company data persistence involves storing and retrieving company information from a database.
using StockControl.Config.DataBaseConfig;
using StockControl.Data.Ef;
using StockControl.Enums;

namespace StockControl.Data
{
    public class CompanyPersistence
    {
        private readonly DatabaseContext db;

        public CompanyPersistence(DatabaseContext dbContext)
        {
            db = dbContext;
        }

        public Company? GetCompany()
        {
            using var ctx = db.CreateDbContext();
            var r = ctx.Company.OrderBy(c => c.Id).FirstOrDefault();
            if (r == null)
                return null;
            return new Company(r.CUIT, r.Name, r.Address, r.Phone, r.Email, (TaxCondition)r.TaxCondition) { Id = r.Id };
        }

        public void AddCompany(Company company)
        {
            using var ctx = db.CreateDbContext();
            var row = new CompanyRow();
            Copy(company, row);
            ctx.Company.Add(row);
            ctx.SaveChanges();
            company.Id = row.Id;
        }

        public void UpdateCompany(Company company)
        {
            using var ctx = db.CreateDbContext();
            var row = new CompanyRow { Id = company.Id };
            Copy(company, row);
            ctx.Company.Update(row);
            ctx.SaveChanges();
        }

        private static void Copy(Company company, CompanyRow row)
        {
            row.CUIT = company.CUIT ?? "";
            row.Name = company.Name ?? "";
            row.Address = company.Address ?? "";
            row.Phone = company.Phone;
            row.Email = company.Email;
            row.TaxCondition = (int)company.taxCondition;
        }
    }
}
