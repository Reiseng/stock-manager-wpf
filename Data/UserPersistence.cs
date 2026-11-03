using Microsoft.EntityFrameworkCore;
using StockControl.Config.DataBaseConfig;
using StockControl.Data.Ef;
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
            using var ctx = db.CreateDbContext();
            return Query(ctx, includeInactive).OrderBy(u => u.Id).AsEnumerable().Select(ToModel).ToList();
        }

        public User? GetByID(int id, bool includeInactive = false)
        {
            using var ctx = db.CreateDbContext();
            var row = Query(ctx, includeInactive).FirstOrDefault(u => u.Id == id);
            return row == null ? null : ToModel(row);
        }

        public User? GetByUsername(string username, bool includeInactive = false)
        {
            using var ctx = db.CreateDbContext();
            var row = Query(ctx, includeInactive).FirstOrDefault(u => u.Username == username);
            return row == null ? null : ToModel(row);
        }

        public void Add(User user)
        {
            using var ctx = db.CreateDbContext();
            var row = new UserRow();
            Copy(user, row);
            ctx.Users.Add(row);
            ctx.SaveChanges();
            user.ID = row.Id;
        }

        public void Remove(int id)
        {
            using var ctx = db.CreateDbContext();
            ctx.Users.Where(u => u.Id == id)
                .ExecuteUpdate(s => s.SetProperty(u => u.IsActive, false));
        }

        public void Update(User user)
        {
            using var ctx = db.CreateDbContext();
            var row = new UserRow { Id = user.ID };
            Copy(user, row);
            ctx.Users.Update(row);
            ctx.SaveChanges();
        }

        // ---- Saved sessions ("mantener sesión iniciada") ----
        public void AddSession(int userId, string tokenHash, string deviceName, DateTime now)
        {
            using var ctx = db.CreateDbContext();
            ctx.UserSessions.Add(new UserSessionRow
            {
                UserId = userId,
                TokenHash = tokenHash,
                DeviceName = deviceName,
                CreatedAt = Seconds(now),
                LastUsedAt = Seconds(now),
            });
            ctx.SaveChanges();
        }

        public UserSession? GetSessionByTokenHash(string tokenHash)
        {
            using var ctx = db.CreateDbContext();
            var row = ctx.UserSessions.FirstOrDefault(s => s.TokenHash == tokenHash);
            return row == null ? null : new UserSession(row.Id, row.UserId, row.LastUsedAt);
        }

        public void TouchSession(int sessionId, DateTime now)
        {
            using var ctx = db.CreateDbContext();
            var when = Seconds(now);
            ctx.UserSessions.Where(s => s.Id == sessionId)
                .ExecuteUpdate(s => s.SetProperty(x => x.LastUsedAt, when));
        }

        public void DeleteSessionByTokenHash(string tokenHash)
        {
            using var ctx = db.CreateDbContext();
            ctx.UserSessions.Where(s => s.TokenHash == tokenHash).ExecuteDelete();
        }

        public void DeleteSessionsForUser(int userId)
        {
            using var ctx = db.CreateDbContext();
            ctx.UserSessions.Where(s => s.UserId == userId).ExecuteDelete();
        }

        private static IQueryable<UserRow> Query(StockDbContext ctx, bool includeInactive)
            => includeInactive ? ctx.Users : ctx.Users.Where(u => u.IsActive);

        private static void Copy(User user, UserRow row)
        {
            row.Username = user.Username;
            row.PasswordHash = user.Password;
            row.Role = (int)user.Role;
            row.IsActive = user.IsActive;
        }

        private static User ToModel(UserRow r)
            => new(r.Username, r.PasswordHash, (RoleType)r.Role, r.IsActive) { ID = r.Id };

        // Sessions are stored to the second (same as before)
        private static DateTime Seconds(DateTime d) => new(d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second, d.Kind);
    }
}
