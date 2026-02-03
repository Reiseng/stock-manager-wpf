using Microsoft.Data.Sqlite;
using StockControl.Config.DataBaseConfig;

namespace StockControl.Services.Database
{
    public class DatabaseInitializer
    {
        private readonly DatabaseContext db;

        public DatabaseInitializer(DatabaseContext dbContext)
        {
            db = dbContext;
        }

        public void Initialize()
        {
            using var connection = db.CreateConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText =
            @"
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
                INSERT INTO Clients (Dni, Name, LastName, Email, Phone, Address, IsActive)
                SELECT '00000000', 'Consumidor', 'Final', NULL, NULL, NULL, 1
                WHERE NOT EXISTS (
                    SELECT 1 FROM Clients WHERE Dni = '00000000'
                );
                CREATE TABLE IF NOT EXISTS Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Barcode TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    Brand TEXT NOT NULL,
                    Price REAL NOT NULL,
                    Stock REAL NOT NULL,
                    Unit INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS Checkouts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ClientId INTEGER,
                    InvoiceType INTEGER NOT NULL,
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
                    TaxCondition INTEGER NOT NULL,
                    Tax REAL NOT NULL DEFAULT 21.0
                );
                INSERT INTO CompanyInfo (CUIT, Name, Address, Phone, Email, TaxCondition, Tax)
                SELECT '00000000', 'Empresa', 'Dirección de la empresa', NULL, NULL, 1, 21.0
                WHERE NOT EXISTS (
                    SELECT 1 FROM CompanyInfo WHERE CUIT = '00000000'
                );
            ";
            command.ExecuteNonQuery();
        }
    }
}