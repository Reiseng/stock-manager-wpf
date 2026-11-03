using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StockControl.Config.DataBaseConfig;

namespace StockControl.Services.Database
{
    // One schema change. Version numbers must be consecutive and never reused.
    public record Migration(int Version, string Description, string Sql);

    // How migrations work:
    // SQLite keeps an integer inside the database file ("PRAGMA user_version").
    // It says which migrations were already applied to THAT file. On startup we
    // run, in order, every migration with a higher number, each one inside a
    // transaction, and then update user_version. So an old database (or an old
    // backup being imported) is brought up to date automatically.
    //
    // To change the schema in the future: ADD a new Migration at the end of the
    // list. Never edit a migration that was already released, because databases
    // that already ran it won't run it again.
    public class DatabaseInitializer
    {
        public static readonly IReadOnlyList<Migration> Migrations = new List<Migration>
        {
            new(1, "Esquema inicial", @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    Role INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS Clients (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Dni TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    LastName TEXT NOT NULL,
                    Email TEXT,
                    Phone TEXT,
                    Address TEXT,
                    IsActive INTEGER NOT NULL DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Barcode TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    Brand TEXT NOT NULL,
                    Price REAL NOT NULL,
                    Stock REAL NOT NULL,
                    Unit INTEGER NOT NULL,
                    Tax REAL NOT NULL DEFAULT 21.0,
                    IsActive INTEGER NOT NULL DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS Checkouts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ClientId INTEGER,
                    RelatedCheckoutId INTEGER,
                    InvoiceType INTEGER NOT NULL,
                    OperationType INTEGER NOT NULL,
                    Total REAL NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY(ClientId) REFERENCES Clients(Id)
                );
                CREATE TABLE IF NOT EXISTS CheckoutItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CheckoutId INTEGER NOT NULL,
                    ProductId INTEGER NOT NULL,
                    Quantity INTEGER NOT NULL,
                    UnitPrice REAL NOT NULL,
                    FOREIGN KEY(CheckoutId) REFERENCES Checkouts(Id),
                    FOREIGN KEY(ProductId) REFERENCES Products(Id)
                );
                CREATE TABLE IF NOT EXISTS CompanyInfo (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CUIT TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    Address TEXT NOT NULL,
                    Phone TEXT,
                    Email TEXT,
                    TaxCondition INTEGER NOT NULL
                );"),

            new(2, "Corregir teléfono/email invertidos en clientes", @"
                UPDATE Clients
                SET Phone = Email, Email = Phone
                WHERE (Email IS NOT NULL AND Email <> '' AND Email NOT LIKE '%@%')
                   OR (Phone LIKE '%@%');"),

            new(3, "Índices para historial y notas de crédito", @"
                CREATE INDEX IF NOT EXISTS IX_CheckoutItems_CheckoutId ON CheckoutItems(CheckoutId);
                CREATE INDEX IF NOT EXISTS IX_Checkouts_RelatedCheckoutId ON Checkouts(RelatedCheckoutId);"),

            new(4, "Sesiones guardadas (mantener sesión iniciada)", @"
                CREATE TABLE IF NOT EXISTS UserSessions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    TokenHash TEXT NOT NULL UNIQUE,
                    DeviceName TEXT,
                    CreatedAt TEXT NOT NULL,
                    LastUsedAt TEXT NOT NULL,
                    FOREIGN KEY(UserId) REFERENCES Users(Id)
                );
                CREATE INDEX IF NOT EXISTS IX_UserSessions_UserId ON UserSessions(UserId);"),

            new(5, "Facturación electrónica (ARCA)", @"
                ALTER TABLE Checkouts ADD COLUMN FiscalStatus INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE Checkouts ADD COLUMN FiscalPointOfSale INTEGER;
                ALTER TABLE Checkouts ADD COLUMN FiscalVoucherType INTEGER;
                ALTER TABLE Checkouts ADD COLUMN FiscalVoucherNumber INTEGER;
                ALTER TABLE Checkouts ADD COLUMN Cae TEXT;
                ALTER TABLE Checkouts ADD COLUMN CaeDueDate TEXT;
                ALTER TABLE Checkouts ADD COLUMN FiscalMessage TEXT;
                ALTER TABLE Checkouts ADD COLUMN FiscalProduction INTEGER NOT NULL DEFAULT 0;
                CREATE INDEX IF NOT EXISTS IX_Checkouts_FiscalStatus ON Checkouts(FiscalStatus);
                -- Condición frente al IVA del cliente (3 = consumidor final)
                ALTER TABLE Clients ADD COLUMN TaxCondition INTEGER NOT NULL DEFAULT 3;"),
        };

        public static int LatestVersion => Migrations[^1].Version;

        // Same schema for PostgreSQL (a shared server for several registers).
        // Its own numbering: version 1 is everything SQLite has up to version 5.
        // Names are quoted to keep the same capitalisation EF Core uses.
        public static readonly IReadOnlyList<Migration> PostgresMigrations = new List<Migration>
        {
            new(1, "Esquema inicial (equivale a la versión 5 de SQLite)", @"
                CREATE TABLE IF NOT EXISTS ""Users"" (
                    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    ""Username"" text NOT NULL UNIQUE,
                    ""PasswordHash"" text NOT NULL,
                    ""Role"" integer NOT NULL,
                    ""IsActive"" boolean NOT NULL DEFAULT TRUE
                );
                CREATE TABLE IF NOT EXISTS ""Clients"" (
                    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    ""Dni"" text NOT NULL UNIQUE,
                    ""Name"" text NOT NULL,
                    ""LastName"" text NOT NULL,
                    ""Email"" text,
                    ""Phone"" text,
                    ""Address"" text,
                    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
                    ""TaxCondition"" integer NOT NULL DEFAULT 3
                );
                CREATE TABLE IF NOT EXISTS ""Products"" (
                    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    ""Barcode"" text NOT NULL UNIQUE,
                    ""Name"" text NOT NULL,
                    ""Brand"" text NOT NULL,
                    ""Price"" numeric(18,4) NOT NULL,
                    ""Stock"" numeric(18,4) NOT NULL,
                    ""Unit"" integer NOT NULL,
                    ""Tax"" numeric(9,4) NOT NULL DEFAULT 21,
                    ""IsActive"" boolean NOT NULL DEFAULT TRUE
                );
                CREATE TABLE IF NOT EXISTS ""Checkouts"" (
                    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    ""ClientId"" integer REFERENCES ""Clients""(""Id""),
                    ""RelatedCheckoutId"" integer,
                    ""InvoiceType"" integer NOT NULL,
                    ""OperationType"" integer NOT NULL,
                    ""Total"" numeric(18,4) NOT NULL,
                    ""CreatedAt"" timestamp NOT NULL,
                    ""FiscalStatus"" integer NOT NULL DEFAULT 0,
                    ""FiscalPointOfSale"" integer,
                    ""FiscalVoucherType"" integer,
                    ""FiscalVoucherNumber"" bigint,
                    ""Cae"" text,
                    ""CaeDueDate"" timestamp,
                    ""FiscalMessage"" text,
                    ""FiscalProduction"" boolean NOT NULL DEFAULT FALSE
                );
                CREATE TABLE IF NOT EXISTS ""CheckoutItems"" (
                    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    ""CheckoutId"" integer NOT NULL REFERENCES ""Checkouts""(""Id""),
                    ""ProductId"" integer NOT NULL REFERENCES ""Products""(""Id""),
                    ""Quantity"" numeric(18,4) NOT NULL,
                    ""UnitPrice"" numeric(18,4) NOT NULL
                );
                CREATE TABLE IF NOT EXISTS ""CompanyInfo"" (
                    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    ""CUIT"" text NOT NULL UNIQUE,
                    ""Name"" text NOT NULL,
                    ""Address"" text NOT NULL,
                    ""Phone"" text,
                    ""Email"" text,
                    ""TaxCondition"" integer NOT NULL
                );
                CREATE TABLE IF NOT EXISTS ""UserSessions"" (
                    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                    ""UserId"" integer NOT NULL REFERENCES ""Users""(""Id""),
                    ""TokenHash"" text NOT NULL UNIQUE,
                    ""DeviceName"" text,
                    ""CreatedAt"" timestamp NOT NULL,
                    ""LastUsedAt"" timestamp NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ""IX_CheckoutItems_CheckoutId"" ON ""CheckoutItems""(""CheckoutId"");
                CREATE INDEX IF NOT EXISTS ""IX_Checkouts_RelatedCheckoutId"" ON ""Checkouts""(""RelatedCheckoutId"");
                CREATE INDEX IF NOT EXISTS ""IX_Checkouts_FiscalStatus"" ON ""Checkouts""(""FiscalStatus"");
                CREATE INDEX IF NOT EXISTS ""IX_Checkouts_CreatedAt"" ON ""Checkouts""(""CreatedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_UserSessions_UserId"" ON ""UserSessions""(""UserId"");"),
        };

        public static int LatestPostgresVersion => PostgresMigrations[^1].Version;

        private readonly DatabaseContext db;
        private readonly BackupService? backupService;

        public DatabaseInitializer(DatabaseContext dbContext, BackupService? backupService = null)
        {
            db = dbContext;
            this.backupService = backupService;
        }

        public void Initialize()
        {
            if (!db.IsSqlite)
            {
                InitializePostgres();
                return;
            }

            using var connection = db.CreateConnection();
            connection.Open();

            int current = GetSchemaVersion(connection);
            if (current > LatestVersion)
                throw new Exception(
                    $"La base de datos es de una versión más nueva del programa (esquema {current}, " +
                    $"este programa llega hasta {LatestVersion}). Actualizá el programa.");

            var pending = Migrations.Where(m => m.Version > current).OrderBy(m => m.Version).ToList();

            // Safety copy before touching an existing database
            if (pending.Count > 0 && HasTables(connection))
                backupService?.CreateAutomaticBackup($"antes-de-migrar-v{current}");

            foreach (var migration in pending)
            {
                using var transaction = connection.BeginTransaction();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = migration.Sql;
                    command.ExecuteNonQuery();
                }
                using (var command = connection.CreateCommand())
                {
                    // PRAGMA can't take parameters; Version is our own int, not user input
                    command.CommandText = $"PRAGMA user_version = {migration.Version};";
                    command.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            Seed(connection);
        }

        // PostgreSQL: the version lives in a small table. Several registers may start
        // at the same time: a lock makes only one of them apply the migrations.
        private void InitializePostgres()
        {
            using var ctx = db.CreateDbContext();
            using var transaction = ctx.Database.BeginTransaction();
            ctx.Database.ExecuteSqlRaw("SELECT pg_advisory_xact_lock(7421001);");
            ctx.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""SchemaInfo"" (""Version"" integer NOT NULL);");
            var current = ctx.Database.SqlQueryRaw<int>(@"SELECT COALESCE(MAX(""Version""), 0) AS ""Value"" FROM ""SchemaInfo""").AsEnumerable().First();
            if (current > LatestPostgresVersion)
                throw new Exception(
                    $"La base de datos del servidor es de una versión más nueva del programa (esquema {current}, " +
                    $"este programa llega hasta {LatestPostgresVersion}). Actualizá el programa en esta PC.");

            foreach (var migration in PostgresMigrations.Where(m => m.Version > current).OrderBy(m => m.Version))
            {
                ctx.Database.ExecuteSqlRaw(migration.Sql);
                ctx.Database.ExecuteSqlRaw(@"DELETE FROM ""SchemaInfo""; INSERT INTO ""SchemaInfo"" (""Version"") VALUES (" + migration.Version + ");");
            }

            ctx.Database.ExecuteSqlRaw(@"
                INSERT INTO ""Users"" (""Username"", ""PasswordHash"", ""Role"", ""IsActive"")
                SELECT 'Admin', 'ZBGIDpYmoqYbL6pnHumsXg==|hf+NGcBFeOjGLSHlO8IL2P/ZldaxLZzKWAvddXyKb0U=', 0, TRUE
                WHERE NOT EXISTS (SELECT 1 FROM ""Users"");

                INSERT INTO ""Clients"" (""Dni"", ""Name"", ""LastName"", ""Email"", ""Phone"", ""Address"", ""IsActive"", ""TaxCondition"")
                SELECT '00000000', 'Consumidor', 'Final', NULL, NULL, NULL, TRUE, 3
                WHERE NOT EXISTS (SELECT 1 FROM ""Clients"" WHERE ""Dni"" = '00000000');

                INSERT INTO ""CompanyInfo"" (""CUIT"", ""Name"", ""Address"", ""Phone"", ""Email"", ""TaxCondition"")
                SELECT '00000000', 'Empresa', 'Dirección de la empresa', NULL, NULL, 1
                WHERE NOT EXISTS (SELECT 1 FROM ""CompanyInfo"");");
            transaction.Commit();
        }

        public static int GetSchemaVersion(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static bool HasTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }

        // Base data the program needs. Idempotent: runs on every startup.
        private static void Seed(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                -- Default admin (Admin / Admin123) only for a brand-new database
                INSERT INTO Users (Username, PasswordHash, Role, IsActive)
                SELECT 'Admin', 'ZBGIDpYmoqYbL6pnHumsXg==|hf+NGcBFeOjGLSHlO8IL2P/ZldaxLZzKWAvddXyKb0U=', 0, 1
                WHERE NOT EXISTS (SELECT 1 FROM Users);

                INSERT INTO Clients (Dni, Name, LastName, Email, Phone, Address, IsActive)
                SELECT '00000000', 'Consumidor', 'Final', NULL, NULL, NULL, 1
                WHERE NOT EXISTS (SELECT 1 FROM Clients WHERE Dni = '00000000');

                INSERT INTO CompanyInfo (CUIT, Name, Address, Phone, Email, TaxCondition)
                SELECT '00000000', 'Empresa', 'Dirección de la empresa', NULL, NULL, 1
                WHERE NOT EXISTS (SELECT 1 FROM CompanyInfo);
            ";
            command.ExecuteNonQuery();
        }
    }
}
