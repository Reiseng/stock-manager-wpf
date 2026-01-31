// Client is handled in memory to keep the project simple and focused on core logic.
// This can be replaced with persistence later (DB or file storage).
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
        public List<Client> GetAll()
        {
            var clients = new List<Client>();
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ID, Dni, Name, LastName, Phone, Email, Address
                FROM Clients;
                ";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var client = new Client(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6)
                );
                client.ID = reader.GetInt32(0);
                clients.Add(client);
            }
            return clients;
        }

        public Client? GetByDni(string dni)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ID, Dni, Name, LastName, Phone, Email, Address
                FROM Clients
                WHERE Dni = @dni;
                ";
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
                    reader.IsDBNull(6) ? null : reader.GetString(6)
                );
                client.ID = reader.GetInt32(0);
                return client;
            }
            return null;
        }

        public Client? GetByID(int id)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ID, Dni, Name, LastName, Phone, Email, Address
                FROM Clients
                WHERE ID = @id;
                ";
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
                    reader.IsDBNull(6) ? null : reader.GetString(6)
                );
                client.ID = reader.GetInt32(0);
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
                INSERT INTO Clients (Dni, Name, LastName, Phone, Email, Address)
                VALUES (@dni, @name, @lastName, @phone, @email, @address);
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
            command.ExecuteNonQuery();
        }
        public void Remove(Client client)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM Clients
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
                    Address = @address
                WHERE ID = @id;
                ";
            command.Parameters.AddWithValue("@dni", _client.Dni);
            command.Parameters.AddWithValue("@name", _client.Name);
            command.Parameters.AddWithValue("@lastName", _client.LastName);
            command.Parameters.AddWithValue("@phone", _client.Phone ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@email", _client.Email ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@address", _client.Address ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@id", _client.ID);
            command.ExecuteNonQuery();
        }
    }
}