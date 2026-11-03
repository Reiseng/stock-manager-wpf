using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StockControl.Config.DataBaseConfig;

namespace StockControl.Data.Ef
{
    // Rows exactly as they are stored. The *Persistence classes turn them into
    // the program's models (Product, Client, Checkout...), so nothing outside
    // Data/ knows about EF Core.
    public class ProductRow
    {
        public int Id { get; set; }
        public string Barcode { get; set; } = "";
        public string Name { get; set; } = "";
        public string Brand { get; set; } = "";
        public decimal Price { get; set; }
        public decimal Stock { get; set; }
        public int Unit { get; set; }
        public decimal Tax { get; set; }
        public bool IsActive { get; set; }
    }

    public class ClientRow
    {
        public int Id { get; set; }
        public string Dni { get; set; } = "";
        public string Name { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public int TaxCondition { get; set; }
    }

    public class UserRow
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public int Role { get; set; }
        public bool IsActive { get; set; }
    }

    public class UserSessionRow
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string TokenHash { get; set; } = "";
        public string? DeviceName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUsedAt { get; set; }
    }

    public class CompanyRow
    {
        public int Id { get; set; }
        public string CUIT { get; set; } = "";
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public int TaxCondition { get; set; }
    }

    public class CheckoutRow
    {
        public int Id { get; set; }
        public int? ClientId { get; set; }
        public ClientRow? Client { get; set; }
        public int? RelatedCheckoutId { get; set; }
        public int InvoiceType { get; set; }
        public int OperationType { get; set; }
        public decimal Total { get; set; }
        public DateTime CreatedAt { get; set; }
        public int FiscalStatus { get; set; }
        public int? FiscalPointOfSale { get; set; }
        public int? FiscalVoucherType { get; set; }
        public long? FiscalVoucherNumber { get; set; }
        public string? Cae { get; set; }
        public DateTime? CaeDueDate { get; set; }
        public string? FiscalMessage { get; set; }
        public bool FiscalProduction { get; set; }
        public List<CheckoutItemRow> Items { get; set; } = new();
    }

    public class CheckoutItemRow
    {
        public int Id { get; set; }
        public int CheckoutId { get; set; }
        public int ProductId { get; set; }
        public ProductRow? Product { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class StockDbContext : DbContext
    {
        private readonly DatabaseContext _db;

        public StockDbContext(DatabaseContext db)
        {
            _db = db;
        }

        public DbSet<ProductRow> Products => Set<ProductRow>();
        public DbSet<ClientRow> Clients => Set<ClientRow>();
        public DbSet<UserRow> Users => Set<UserRow>();
        public DbSet<UserSessionRow> UserSessions => Set<UserSessionRow>();
        public DbSet<CompanyRow> Company => Set<CompanyRow>();
        public DbSet<CheckoutRow> Checkouts => Set<CheckoutRow>();
        public DbSet<CheckoutItemRow> CheckoutItems => Set<CheckoutItemRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            switch (_db.Provider)
            {
                case DatabaseProvider.Sqlite:
                    options.UseSqlite(_db.ConnectionString);
                    break;
                case DatabaseProvider.PostgreSql:
                    options.UseNpgsql(_db.ConnectionString);
                    break;
                default:
                    throw new NotSupportedException($"Motor de base de datos no soportado: {_db.Provider}");
            }
            // Reads are mapped to models right away: no change tracking needed
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configuration)
        {
            // SQLite has no decimal type: money and quantities are stored as REAL
            // (as they always were). With this EF can also compare, sort and sum them.
            if (_db.IsSqlite)
                configuration.Properties<decimal>().HaveConversion<double>();
            else
            {
                // PostgreSQL: exact decimals and dates without time zone (local time, as always)
                configuration.Properties<decimal>().HavePrecision(18, 4);
                configuration.Properties<DateTime>().HaveColumnType("timestamp without time zone");
            }
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ProductRow>().ToTable("Products");
            model.Entity<ClientRow>().ToTable("Clients");
            model.Entity<UserRow>().ToTable("Users");
            model.Entity<UserSessionRow>().ToTable("UserSessions");
            model.Entity<CompanyRow>().ToTable("CompanyInfo");

            model.Entity<CheckoutRow>(e =>
            {
                e.ToTable("Checkouts");
                e.HasOne(c => c.Client).WithMany().HasForeignKey(c => c.ClientId);
                e.HasMany(c => c.Items).WithOne().HasForeignKey(i => i.CheckoutId);
            });
            model.Entity<CheckoutItemRow>(e =>
            {
                e.ToTable("CheckoutItems");
                e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId);
            });

            if (_db.IsSqlite)
            {
                // SQLite keeps dates as text in a fixed format ("2026-11-02 17:30:00"):
                // it sorts and compares correctly as text.
                var dateTime = new ValueConverter<DateTime, string>(d => FormatDateTime(d), s => ParseDate(s));
                var date = new ValueConverter<DateTime, string>(d => FormatDate(d), s => ParseDate(s));
                model.Entity<CheckoutRow>().Property(c => c.CreatedAt).HasConversion(dateTime);
                model.Entity<CheckoutRow>().Property(c => c.CaeDueDate).HasConversion(date);
                model.Entity<UserSessionRow>().Property(s => s.CreatedAt).HasConversion(dateTime);
                model.Entity<UserSessionRow>().Property(s => s.LastUsedAt).HasConversion(dateTime);
            }
        }

        public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        public static string FormatDateTime(DateTime d) => d.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        public static string FormatDate(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Tolerant: older versions may have saved dates in other formats
        public static DateTime ParseDate(string text)
        {
            if (DateTime.TryParseExact(text, new[] { DateTimeFormat, "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss.FFFFFFF" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
                return exact;
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant))
                return invariant;
            return DateTime.Parse(text, CultureInfo.GetCultureInfo("es-AR"));
        }
    }
}
