// Client is an individual or organization that purchases goods or services from a business.
// Client data persistence involves storing and retrieving client information from a database.
using StockControl.Config.DataBaseConfig;
using StockControl.Models;

namespace StockControl.Data
{
    public class ClientPersistence
    {
        private readonly DatabaseContext db;
        public ClientPersistence(DatabaseContext dbContext)
        {
            db = dbContext;
        }
        public List<Client> GetAll(bool includeInactive = false)
        {
            var clients = new List<Client>();
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ID, Dni, Name, LastName, Phone, Email, Address, IsActive, TaxCondition
                FROM Clients
                ";
            if (!includeInactive)
            {
                command.CommandText += " WHERE IsActive = 1";
            }
            command.CommandText += ";";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var client = new Client(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetInt32(7) == 1 ? true : false
                );
                client.ID = reader.GetInt32(0);
                client.TaxCondition = (StockControl.Enums.TaxCondition)reader.GetInt32(8);
                clients.Add(client);
            }
            return clients;
        }

        public Client? GetByDni(string dni, bool includeInactive = false)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ID, Dni, Name, LastName, Phone, Email, Address, IsActive, TaxCondition
                FROM Clients
                WHERE Dni = @dni";
            if (!includeInactive)
            {
                command.CommandText += " AND IsActive = 1";
            }
            command.CommandText += ";";
            command.Parameters.AddWithValue("@dni", dni);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var client = new Client(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetInt16(7) == 1 ? true : false
                );
                client.ID = reader.GetInt32(0);
                client.TaxCondition = (StockControl.Enums.TaxCondition)reader.GetInt32(8);
                return client;
            }
            return null;
        }
        public Client? GetByID(int id, bool includeInactive = false)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ID, Dni, Name, LastName, Phone, Email, Address, IsActive, TaxCondition
                FROM Clients
                WHERE ID = @id
                ";
            if (!includeInactive)
            {
                command.CommandText += " AND IsActive = 1";
            }
            command.CommandText += ";";
            command.Parameters.AddWithValue("@id", id);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var client = new Client(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetInt32(7) == 1
                );
                client.ID = reader.GetInt32(0);
                client.TaxCondition = (StockControl.Enums.TaxCondition)reader.GetInt32(8);
                return client;
            }
            return null;
        }
        public void Add(Client client)
        {   
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Clients (Dni, Name, LastName, Phone, Email, Address, IsActive, TaxCondition)
                VALUES (@dni, @name, @lastName, @phone, @email, @address, @isActive, @taxCondition);
                ";
            command.Parameters.AddWithValue("@dni", client.Dni);
            command.Parameters.AddWithValue("@name", client.Name);
            command.Parameters.AddWithValue("@lastName", client.LastName);
            command.Parameters.AddWithValue("@phone", 
                client.Phone ?? (object)DBNull.Value);

            command.Parameters.AddWithValue("@email", 
                client.Email ?? (object)DBNull.Value);

            command.Parameters.AddWithValue("@address", 
                client.Address ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isActive", client.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@taxCondition", (int)client.TaxCondition);
            command.ExecuteNonQuery();
        }
        public void Remove(Client client)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Clients
                SET IsActive = 0
                WHERE ID = @id;
                ";
            command.Parameters.AddWithValue("@id", client.ID);
            command.ExecuteNonQuery();
        }
        public void Update(Client _client)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Clients
                SET Dni = @dni,
                    Name = @name,
                    LastName = @lastName,
                    Phone = @phone,
                    Email = @email,
                    Address = @address,
                    IsActive = @isActive,
                    TaxCondition = @taxCondition
                WHERE ID = @id;
                ";
            command.Parameters.AddWithValue("@dni", _client.Dni);
            command.Parameters.AddWithValue("@name", _client.Name);
            command.Parameters.AddWithValue("@lastName", _client.LastName);
            command.Parameters.AddWithValue("@phone", _client.Phone ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@email", _client.Email ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@address", _client.Address ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isActive", _client.IsActive ? 1 : 0);
            command.Parameters.AddWithValue("@taxCondition", (int)_client.TaxCondition);
            command.Parameters.AddWithValue("@id", _client.ID);
            command.ExecuteNonQuery();
        }
    }
}