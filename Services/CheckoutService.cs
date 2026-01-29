using StockControl.Data;
using StockControl.Models;
using StockControl.Enums;

namespace StockControl.Services
{
    public class CheckoutService
    {
        private Checkout _currentCheckout;
        private readonly CheckoutPersistence _repository;
        ProductService productService;
        public CheckoutService(CheckoutPersistence repository)
        {
            _repository = repository;
            productService = AppServices.ProductService;
            StartCheckout();
        }
        public IReadOnlyList<Checkout> GetCheckouts()
        {
            return _repository.GetAll();
        }
        public Checkout GetCheckoutByID(string ID)
        {
            int id = Convert.ToInt16(ID);
            if (id <= 0)
                throw new Exception("Id inválida");
            Checkout checkout = _repository.GetByID(id);
            if (checkout == null)
                throw new Exception("El cliente no existe");
            return checkout;
        }
        public void StartCheckout()
        {
            _currentCheckout = new Checkout
            {
                Items = new List<CheckoutItem>(),
                Date = DateTime.Now
            };
        }
        public void AddProduct(Product product, decimal quantity)
        {
            if (product == null)
                throw new Exception("Producto inválido");

            if (product.Unit == UnitType.Unit && quantity % 1 != 0)
                throw new Exception("Este producto se vende por unidad");

            var item = _currentCheckout.Items
                .FirstOrDefault(i => i.product.Id == product.Id);

            if (item != null)
            {
                item.Quantity += quantity;
            }
            else
            {
                _currentCheckout.Items.Add(new CheckoutItem
                {
                    product = product,
                    Quantity = quantity,
                    UnitPrice = product.Price
                });
            }
        }
        public void RemoveProduct(CheckoutItem item)
        {
            if (item == null)
                throw new Exception("Producto inválido");
            _currentCheckout.Items.Remove(item);
        }
        public void RemoveOneUnit(CheckoutItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (item.Quantity > 1)
                item.Quantity--;
            else
                _currentCheckout.Items.Remove(item);
        }
        public void SetClient(Client client)
        {
            _currentCheckout.Client = client;
        }
        public void SetInvoiceType(InvoiceType type)
        {
            _currentCheckout.invoiceType = type;
        }
        public void ConfirmCheckout()
        {
            if (_currentCheckout.Items.Count == 0)
                throw new Exception("El carrito está vacío");

            if (_currentCheckout.invoiceType == InvoiceType.AFacture ||
                _currentCheckout.invoiceType == InvoiceType.BFacture)
            {
                if (_currentCheckout.Client == null)
                {
                    throw new Exception("Debe seleccionar un cliente para este tipo de factura");
                }
            }
            else if (_currentCheckout.Client == null)
            {
                _currentCheckout.Client = new Client
                {
                    Name = "Consumidor Final"
                };
            }
            foreach (var item in _currentCheckout.Items)
            {
                if (item.product.Stock < item.Quantity)
                    throw new Exception(
                        $"Stock insuficiente para {item.product.Name}"
                    );
            }

            foreach (var item in _currentCheckout.Items)
            {
                productService.DecreaseStock(
                    item.product.Id,
                    item.Quantity
                );
            }

            _repository.Add(_currentCheckout);

            StartCheckout();
        }
        public Checkout GetCurrentCheckout()
        {
            if (_currentCheckout == null)
                StartCheckout();

            return _currentCheckout;
        }
    }
}