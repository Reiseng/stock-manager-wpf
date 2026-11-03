// Client is an individual or organization that purchases goods or services from a business.
// Client data persistence involves storing and retrieving client information from a database.
using Microsoft.EntityFrameworkCore;
using StockControl.Config.DataBaseConfig;
using StockControl.Data.Ef;
using StockControl.Enums;
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
            using var ctx = db.CreateDbContext();
            return Query(ctx, includeInactive).OrderBy(c => c.Id).AsEnumerable().Select(ToModel).ToList();
        }

        public Client? GetByDni(string dni, bool includeInactive = false)
        {
            using var ctx = db.CreateDbContext();
            var row = Query(ctx, includeInactive).FirstOrDefault(c => c.Dni == dni);
            return row == null ? null : ToModel(row);
        }

        public Client? GetByID(int id, bool includeInactive = false)
        {
            using var ctx = db.CreateDbContext();
            var row = Query(ctx, includeInactive).FirstOrDefault(c => c.Id == id);
            return row == null ? null : ToModel(row);
        }

        public void Add(Client client)
        {
            using var ctx = db.CreateDbContext();
            var row = new ClientRow();
            Copy(client, row);
            ctx.Clients.Add(row);
            ctx.SaveChanges();
            client.ID = row.Id;
        }

        public void Remove(Client client)
        {
            using var ctx = db.CreateDbContext();
            ctx.Clients.Where(c => c.Id == client.ID)
                .ExecuteUpdate(s => s.SetProperty(c => c.IsActive, false));
        }

        public void Update(Client client)
        {
            using var ctx = db.CreateDbContext();
            var row = new ClientRow { Id = client.ID };
            Copy(client, row);
            ctx.Clients.Update(row);
            ctx.SaveChanges();
        }

        private static IQueryable<ClientRow> Query(StockDbContext ctx, bool includeInactive)
            => includeInactive ? ctx.Clients : ctx.Clients.Where(c => c.IsActive);

        private static void Copy(Client client, ClientRow row)
        {
            row.Dni = client.Dni;
            row.Name = client.Name;
            row.LastName = client.LastName;
            row.Phone = client.Phone;
            row.Email = client.Email;
            row.Address = client.Address;
            row.IsActive = client.IsActive;
            row.TaxCondition = (int)client.TaxCondition;
        }

        internal static Client ToModel(ClientRow r)
            => new(r.Dni, r.Name, r.LastName, r.Phone!, r.Email!, r.Address!, r.IsActive)
            {
                ID = r.Id,
                TaxCondition = (TaxCondition)r.TaxCondition,
            };
    }
}
