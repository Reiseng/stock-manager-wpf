// Client is handled in memory to keep the project simple and focused on core logic.
// This can be replaced with persistence later (DB or file storage).
using StockControl.Models;

namespace StockControl.Data
{
    public class ClientPersistence
    {
        private readonly List<Client> clients = new();

        public IReadOnlyList<Client> GetAll()
        {
            return clients;
        }

        public Client? GetByDni(string dni)
        {
            return clients.FirstOrDefault(c => c.Dni == dni);
        }

        public Client? GetByID(int id)
        {
            return clients.FirstOrDefault(c => c.ID == id);
        }
        public void Add(Client client)
        {   
            client.ID = clients.Count()+1;
            clients.Add(client);
        }
        public void Remove(Client client)
        {
            clients.Remove(client);
        }
        public void Update(Client _client)
        {
        }
    }
}