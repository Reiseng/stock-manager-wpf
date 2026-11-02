using System.Security.Cryptography;

namespace StockControl.Utils.PasswordHelper{
public static class PasswordHelper
    {
        // Stored format: "iterations|salt|hash".
        // Old hashes ("salt|hash") used 10.000 iterations and are still accepted;
        // they are upgraded on the next successful login (see NeedsRehash).
        private const int Iterations = 600_000;
        private const int LegacyIterations = 10_000;

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
            return $"{Iterations}|{Convert.ToBase64String(salt)}|{Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return false;

            var parts = stored.Split('|');
            int iterations;
            string saltPart, hashPart;
            if (parts.Length == 3 && int.TryParse(parts[0], out iterations))
            {
                saltPart = parts[1];
                hashPart = parts[2];
            }
            else if (parts.Length == 2)
            {
                iterations = LegacyIterations;
                saltPart = parts[0];
                hashPart = parts[1];
            }
            else
            {
                return false;
            }

            byte[] salt, storedHash;
            try
            {
                salt = Convert.FromBase64String(saltPart);
                storedHash = Convert.FromBase64String(hashPart);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] newHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, storedHash.Length);
            return CryptographicOperations.FixedTimeEquals(newHash, storedHash);
        }

        public static bool NeedsRehash(string stored)
            => !stored.StartsWith($"{Iterations}|");
    }
}
