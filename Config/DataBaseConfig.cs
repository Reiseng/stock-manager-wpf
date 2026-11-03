using Microsoft.Data.Sqlite;
using StockControl.Data.Ef;

namespace StockControl.Config.DataBaseConfig
{
    // Database engines the program can use
    public enum DatabaseProvider
    {
        Sqlite = 0,      // a file on this PC (one register)
        PostgreSql = 1,  // a server shared by several registers
    }

    // Where the data lives. Every query goes through CreateDbContext() (EF Core),
    // so the same code works with every engine.
    public class DatabaseContext
    {
        public string ConnectionString { get; }
        public DatabaseProvider Provider { get; }

        public DatabaseContext(string connectionString, DatabaseProvider provider = DatabaseProvider.Sqlite)
        {
            ConnectionString = connectionString;
            Provider = provider;
        }

        public bool IsSqlite => Provider == DatabaseProvider.Sqlite;

        public StockDbContext CreateDbContext() => new(this);

        // SQLite only: schema migrations and file backups work on the file directly
        public SqliteConnection CreateConnection()
        {
            if (!IsSqlite)
                throw new InvalidOperationException("Esta operación es solo para bases SQLite.");
            return new SqliteConnection(ConnectionString);
        }
    }
}
