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
        ClientService clientService;
        CompanyService companyService;

        public CheckoutService(CheckoutPersistence repository)
        {
            _repository = repository;
            productService = AppServices.ProductService;
            companyService = AppServices.CompanyService;
            clientService = AppServices.ClientService;
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
                throw new Exception("No se encontró una factura con ese ID");
            return checkout;
        }
        public void StartCheckout()
        {
            _currentCheckout = new Checkout
            {
                Client = clientService.GetFinalConsumer(),
                Items = new List<CheckoutItem>(),
                Date = DateTime.Now
            };
        }
        public void AddProduct(Product product, decimal quantity)
        {
            if (product == null)
                throw new Exception("Producto inválido");

            if (product.Unit == UnitType.Unit && quantity % 1 != 0)
                throw new Exception("Este producto no se vende por unidad");

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
        public void SetOperationType(OperationType type)
        {
            _currentCheckout.operationType = type;
        }
        public Checkout ConfirmCheckout()
        {
            if (_currentCheckout.Items.Count == 0)
                throw new Exception("El carrito está vacío");
            if (_currentCheckout.operationType == OperationType.CreditNote ||
                _currentCheckout.operationType == OperationType.DebitNote)
            {
                if (_currentCheckout.RelatedCheckoutId == null)
                    throw new Exception("Debe vincularse a una factura existente");
            }
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
                _currentCheckout.Client = clientService.GetFinalConsumer();
            }
            if (_currentCheckout.operationType == OperationType.Sale){
            foreach (var item in _currentCheckout.Items)
                {
                    if (item.product.Stock < item.Quantity)
                        throw new Exception(
                            $"Stock insuficiente para {item.product.Name}"
                        );
                }
                foreach (var item in _currentCheckout.Items)
                {
                    if (item.product.Unit != UnitType.Service){
                        productService.DecreaseStock(
                            item.product.Id,
                            item.Quantity
                        );
                    }
                }
            }
            if (_currentCheckout.operationType == OperationType.CreditNote){
                foreach (var item in _currentCheckout.Items)
                {
                    if (item.product.Unit != UnitType.Service){
                        productService.IncreaseStock(
                            item.product.Id,
                            item.Quantity
                        );
                    }
                }
            }
            _currentCheckout.Total = _currentCheckout.Items.Sum(i => i.Total)*(1+(companyService.GetCompanyInfo().tax/100));
            var lastCheckout = _repository.Add(_currentCheckout);
            return lastCheckout;
        }
        public Checkout GetCurrentCheckout()
        {
            if (_currentCheckout == null)
                StartCheckout();

            return _currentCheckout;
        }
        public void SetQuantity(int productId, decimal quantity)
        {
            var item = _currentCheckout.Items
                .FirstOrDefault(i => i.product.Id == productId);

            if (item == null) return;

            if (quantity <= 0)
                _currentCheckout.Items.Remove(item);
            else
                item.Quantity = quantity;
        }
        internal void ClearCheckout()
        {
            StartCheckout();
        }
        public void SetProductPrice(int productId, decimal price)
        {
            var item = _currentCheckout.Items
                .FirstOrDefault(i => i.product.Id == productId);

            if (item == null) return;

            if (price <= 0)
                throw new Exception("El precio debe ser mayor a 0");
            else
                item.UnitPrice = price;
        }
    }
}