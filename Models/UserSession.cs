namespace StockControl.Models
{
    // A "keep me signed in" session. Only the SHA-256 of the token is stored in
    // the database; the token itself lives encrypted on the PC (SessionStore).
    public record UserSession(int Id, int UserId, DateTime LastUsedAt);
}
