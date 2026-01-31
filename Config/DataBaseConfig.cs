using Microsoft.Data.Sqlite;

namespace StockControl.Config.DataBaseConfig
{
    public class DatabaseContext
        {
            public string ConnectionString { get; }

            public DatabaseContext(string connectionString)
            {
                ConnectionString = connectionString;
            }

            public SqliteConnection CreateConnection()
                => new SqliteConnection(ConnectionString);
        }
}