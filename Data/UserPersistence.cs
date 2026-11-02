using StockControl.Config.DataBaseConfig;
using StockControl.Enums;
using StockControl.Models;

namespace StockControl.Data
{
    public class UserPersistence
    {
        private readonly DatabaseContext db;
        public UserPersistence(DatabaseContext dbContext)
        {
            db = dbContext;
        }

        public List<User> GetAll(bool includeInactive = false)
        {
            var users = new List<User>();
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ID, Username, PasswordHash, Role, IsActive
                FROM Users
                ";
            if (!includeInactive)
            {
                command.CommandText += " WHERE IsActive = 1";
            }
            command.CommandText += ";";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var user = new User(
                    reader.GetString(1),
                    reader.GetString(2),
                    (RoleType)reader.GetInt32(3),
                    reader.GetInt32(4) == 1 ? true : false
                );
                user.ID = reader.GetInt32(0);
                users.Add(user);
            }
            return users;
        }
        public User? GetByID(int id, bool includeInactive = false)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ID, Username, PasswordHash, Role, IsActive
                FROM Users
                WHERE ID = @id";
            if (!includeInactive)
            {
                command.CommandText += " AND IsActive = 1";
            }
            command.CommandText += ";";
            command.Parameters.AddWithValue("@id", id);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var user = new User(
                    reader.GetString(1),
                    reader.GetString(2),
                    (RoleType)reader.GetInt32(3),
                    reader.GetInt32(4) == 1 ? true : false
                );
                user.ID = reader.GetInt32(0);
                return user;
            }
            return null;
        }
        public User? GetByUsername(string username, bool includeInactive = false) 
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ID, Username, PasswordHash, Role, IsActive
                FROM Users
                WHERE Username = @username";
            if (!includeInactive)
            {
                command.CommandText += " AND IsActive = 1";
            }
            command.CommandText += ";";
            command.Parameters.AddWithValue("@username", username);
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var user = new User(
                    reader.GetString(1),
                    reader.GetString(2),
                    (RoleType)reader.GetInt32(3),
                    reader.GetInt32(4) == 1 ? true : false
                );
                user.ID = reader.GetInt32(0);
                return user;
            }
            return null;
        }
        public void Add(User user)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Users (Username, Passwordhash, Role, IsActive)
                VALUES (@username, @passwordhash, @role, @isActive);
                ";
            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@passwordhash", user.Password);
            command.Parameters.AddWithValue("@role", (int)user.Role);
            command.Parameters.AddWithValue("@isActive",user.IsActive ? 1 : 0);
            command.ExecuteNonQuery();
        }
        public void Remove(int id)
        {
            using var connection = db.CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users
                SET IsActive = 0
                WHERE ID = @id;
                ";
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }
        // ---- Saved sessions ("mantener sesión iniciada") ----
        public void AddSession(int userId, string tokenHash, string deviceName, DateTime now)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO UserSessions (UserId, TokenHash, DeviceName, CreatedAt, LastUsedAt)
                VALUES (@userId, @tokenHash, @deviceName, @now, @now);
                ";
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@tokenHash", tokenHash);
            command.Parameters.AddWithValue("@deviceName", deviceName);
            command.Parameters.AddWithValue("@now", now.ToString("yyyy-MM-dd HH:mm:ss"));
            command.ExecuteNonQuery();
        }
        public UserSession? GetSessionByTokenHash(string tokenHash)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, UserId, LastUsedAt
                FROM UserSessions
                WHERE TokenHash = @tokenHash;
                ";
            command.Parameters.AddWithValue("@tokenHash", tokenHash);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return null;
            return new UserSession(
                reader.GetInt32(0),
                reader.GetInt32(1),
                DateTime.ParseExact(reader.GetString(2), "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        }
        public void TouchSession(int sessionId, DateTime now)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE UserSessions SET LastUsedAt = @now WHERE Id = @id;";
            command.Parameters.AddWithValue("@now", now.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@id", sessionId);
            command.ExecuteNonQuery();
        }
        public void DeleteSessionByTokenHash(string tokenHash)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM UserSessions WHERE TokenHash = @tokenHash;";
            command.Parameters.AddWithValue("@tokenHash", tokenHash);
            command.ExecuteNonQuery();
        }
        public void DeleteSessionsForUser(int userId)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM UserSessions WHERE UserId = @userId;";
            command.Parameters.AddWithValue("@userId", userId);
            command.ExecuteNonQuery();
        }
        public void Update(User user)
        {
            using var connection = db.CreateConnection();
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE Users
                    SET Username = @username,
                        Passwordhash = @passwordhash,
                        Role = @role,
                        IsActive = @isActive
                    WHERE ID = @id;
                    ";
                command.Parameters.AddWithValue("@username", user.Username);
                command.Parameters.AddWithValue("@passwordhash", user.Password);
                command.Parameters.AddWithValue("@role", (int)user.Role);
                command.Parameters.AddWithValue("@isActive", user.IsActive ? 1 : 0);
                command.Parameters.AddWithValue("@id", user.ID);
                command.ExecuteNonQuery();
        }
    }
}