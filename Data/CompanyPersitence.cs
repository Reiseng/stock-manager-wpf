// Company is a business or organization that provides goods or services to customers.
// Company data persistence involves storing and retrieving company information from a database.

using StockControl.Models;
using StockControl.Enums;
using StockControl.Config.DataBaseConfig;

namespace StockControl.Data{
public class CompanyPersistence
    {
        private  readonly DatabaseContext db;
        public CompanyPersistence(DatabaseContext dbContext)
        {
            db = dbContext;
        }
        public Company? GetCompany()
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, CUIT, Name, Address, Phone, Email, TaxCondition, Tax
                FROM CompanyInfo
                LIMIT 1;
                ";
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var company = new Company(
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    (TaxCondition)reader.GetInt32(6),
                    reader.GetDecimal(7)
                );
                company.Id = reader.GetInt32(0);
                return company;
            }
            return null;
        }
        public void AddCompany(Company company)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO CompanyInfo (CUIT, Name, Address, Phone, Email, TaxCondition, Tax)
                    VALUES (@cuit, @name, @address, @phone, @email, @taxCondition, @tax);
                    ";
            command.Parameters.AddWithValue("@cuit", company.CUIT);
            command.Parameters.AddWithValue("@name", company.Name);
            command.Parameters.AddWithValue("@address", (object?)company.Address ?? DBNull.Value);
            command.Parameters.AddWithValue("@phone", (object?)company.Phone ?? DBNull.Value);
            command.Parameters.AddWithValue("@email", (object?)company.Email ?? DBNull.Value);
            command.Parameters.AddWithValue("@taxCondition", (int)company.taxCondition);
            command.Parameters.AddWithValue("@tax", company.tax);
            command.ExecuteNonQuery();
        }
        public void UpdateCompany(Company company)
        {
            using var connection = db.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE CompanyInfo
                    SET CUIT = @cuit,
                        Name = @name,
                        Address = @address,
                        Phone = @phone,
                        Email = @email,
                        TaxCondition = @taxCondition,
                        Tax = @tax
                    WHERE Id = @id;
                    ";
            command.Parameters.AddWithValue("@cuit", company.CUIT);
            command.Parameters.AddWithValue("@name", company.Name);
            command.Parameters.AddWithValue("@address", (object?)company.Address ?? DBNull.Value);
            command.Parameters.AddWithValue("@phone", (object?)company.Phone ?? DBNull.Value);
            command.Parameters.AddWithValue("@email", (object?)company.Email ?? DBNull.Value);
            command.Parameters.AddWithValue("@taxCondition", (int)company.taxCondition);
            command.Parameters.AddWithValue("@tax", company.tax);
            command.Parameters.AddWithValue("@id", company.Id);
            command.ExecuteNonQuery();
        }
    }
}