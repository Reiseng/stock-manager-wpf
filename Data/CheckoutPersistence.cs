// Checkout is handled in memory to keep the project simple and focused on core logic.
// This can be replaced with persistence later (DB or file storage).
using StockControl.Models;

namespace StockControl.Data
{
    public class CheckoutPersistence
    {
        private readonly List<Checkout> checkouts = new();

        public IReadOnlyList<Checkout> GetAll()
        {
            return checkouts;
        }
        public Checkout GetByID(int id)
        {
            return checkouts.FirstOrDefault(c => c.ID == id);
        }
        public void Add(Checkout _checkout)
        {
            _checkout.ID = checkouts.Count()+1;
            checkouts.Add(_checkout);
        }
    }
}