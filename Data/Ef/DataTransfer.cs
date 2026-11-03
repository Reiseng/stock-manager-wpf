using Microsoft.EntityFrameworkCore;
using StockControl.Config.DataBaseConfig;

namespace StockControl.Data.Ef
{
    // Copies ALL the data from one database to another, keeping the ids
    // (sales keep pointing to their products and clients). Works between any
    // two engines: SQLite -> PostgreSQL (moving a store to a server),
    // PostgreSQL -> SQLite (backup files of a server) and back.
    //
    // The target must already have the schema. Whatever it had is replaced,
    // inside one transaction: if anything fails, the target is left as it was.
    public static class DataTransfer
    {
        private const int BatchSize = 2000;

        public record Summary(int Products, int Clients, int Users, int Checkouts);

        public static Summary CopyAll(DatabaseContext source, DatabaseContext target)
        {
            using var from = source.CreateDbContext();
            using var to = target.CreateDbContext();
            to.ChangeTracker.AutoDetectChangesEnabled = false;
            using var transaction = to.Database.BeginTransaction();

            // Children first
            to.CheckoutItems.ExecuteDelete();
            to.Checkouts.ExecuteDelete();
            to.UserSessions.ExecuteDelete(); // saved sessions belong to the old database
            to.Users.ExecuteDelete();
            to.Products.ExecuteDelete();
            to.Clients.ExecuteDelete();
            to.Company.ExecuteDelete();

            // Parents first
            Copy(from.Company, to, r => r.Id);
            var users = Copy(from.Users, to, r => r.Id);
            var clients = Copy(from.Clients, to, r => r.Id);
            var products = Copy(from.Products, to, r => r.Id);
            var checkouts = Copy(from.Checkouts, to, r => r.Id);
            Copy(from.CheckoutItems, to, r => r.Id);

            if (target.Provider == DatabaseProvider.PostgreSql)
                ResetPostgresSequences(to);

            transaction.Commit();
            return new Summary(products, clients, users, checkouts);
        }

        // True when the database has no business data yet (only what a new install creates)
        public static bool IsEmpty(DatabaseContext db)
        {
            using var ctx = db.CreateDbContext();
            return !ctx.Products.Any() && !ctx.Checkouts.Any() && ctx.Clients.Count() <= 1 && ctx.Users.Count() <= 1;
        }

        private static int Copy<T>(DbSet<T> source, StockDbContext to, System.Linq.Expressions.Expression<Func<T, int>> key) where T : class
        {
            int copied = 0;
            while (true)
            {
                var batch = source.AsNoTracking().OrderBy(key).Skip(copied).Take(BatchSize).ToList();
                if (batch.Count == 0)
                    break;
                to.Set<T>().AddRange(batch);
                to.SaveChanges();
                to.ChangeTracker.Clear();
                copied += batch.Count;
            }
            return copied;
        }

        // Ids were copied as they were: the next new row must continue after the highest one
        private static void ResetPostgresSequences(StockDbContext ctx)
        {
            foreach (var table in new[] { "CompanyInfo", "Users", "Clients", "Products", "Checkouts", "CheckoutItems", "UserSessions" })
            {
                ctx.Database.ExecuteSqlRaw(
                    $@"SELECT setval(pg_get_serial_sequence('""{table}""', 'Id'), COALESCE(MAX(""Id""), 1), MAX(""Id"") IS NOT NULL) FROM ""{table}"";");
            }
        }
    }
}
