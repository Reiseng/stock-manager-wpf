using System.Security.Cryptography;

namespace StockControl.Utils.PasswordHelper{
public static class PasswordHelper
    {
        public static string HashPassword(string password)
        {
            using var rng = RandomNumberGenerator.Create();

            byte[] salt = new byte[16];
            rng.GetBytes(salt);

            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);

            byte[] hash = pbkdf2.GetBytes(32);

            return Convert.ToBase64String(salt) + "|" + Convert.ToBase64String(hash);
        }

        public static bool Verify(string password, string stored)
        {
            var parts = stored.Split('|');

            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] storedHash = Convert.FromBase64String(parts[1]);

            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);

            byte[] newHash = pbkdf2.GetBytes(32);

            return CryptographicOperations.FixedTimeEquals(newHash, storedHash);
        }
    }
}