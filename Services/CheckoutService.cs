using StockControl.Data;
using StockControl.Models;
using StockControl.Enums;
using StockControl.Services.Arca;

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
            if (!int.TryParse(ID, out int id) || id <= 0)
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
        // true = request a CAE when confirming ("Facturar en ARCA")
        public void SetInvoiceInArca(bool value)
        {
            _currentCheckout.FiscalStatus = value ? FiscalStatus.Pending : FiscalStatus.Internal;
        }
        public Checkout ConfirmCheckout()
        {
            if (_currentCheckout.Items.Count == 0)
                throw new Exception("El carrito está vacío");
            var operation = _currentCheckout.operationType;
            if (operation == OperationType.CreditNote ||
                operation == OperationType.DebitNote)
            {
                ValidateRelatedCheckout();
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
            // "Facturar en ARCA": check everything BEFORE saving (clear message instead of
            // a sale saved that can't be invoiced). The CAE is requested after saving.
            if (_currentCheckout.FiscalStatus == FiscalStatus.Pending)
            {
                var (letter, _) = AppServices.FiscalService.Prepare(_currentCheckout);
                _currentCheckout.invoiceType = FiscalRules.InvoiceTypeFor(letter);
                _currentCheckout.FiscalProduction = FiscalService.Config.Production;
            }

            // Stock movements: a sale takes stock out, a credit note puts it back.
            // Budgets and debit notes don't touch stock. Services never have stock.
            var stockChanges = new List<StockChange>();
            if (operation == OperationType.Sale || operation == OperationType.CreditNote)
            {
                var sign = operation == OperationType.Sale ? -1 : 1;
                stockChanges = _currentCheckout.Items
                    .Where(i => i.product.Unit != UnitType.Service)
                    .GroupBy(i => i.product.Id)
                    .Select(g => new StockChange(g.Key, g.First().product.Name, sign * g.Sum(i => i.Quantity)))
                    .ToList();
            }
            // Same rounding as ARCA (per VAT rate, 2 decimals): receipt, history and ARCA match
            _currentCheckout.Total = FiscalRules.SaleTotal(_currentCheckout.Items);
            _currentCheckout.Date = DateTime.Now;
            // Stock check, stock update and invoice insert run in ONE transaction:
            // either everything is saved or nothing is (also safe with several registers)
            var lastCheckout = _repository.Add(_currentCheckout, stockChanges);
            return lastCheckout;
        }
        private void ValidateRelatedCheckout()
        {
            if (_currentCheckout.RelatedCheckoutId == null)
                throw new Exception("Debe vincularse a una factura existente");

            var related = _repository.GetByID(_currentCheckout.RelatedCheckoutId.Value);
            if (related == null)
                throw new Exception("La factura vinculada no existe");
            if (related.operationType != OperationType.Sale)
                throw new Exception("Las notas de crédito y débito deben vincularse a una venta");

            if (_currentCheckout.operationType != OperationType.CreditNote)
                return;

            // A credit note can only return what was sold, minus what was already returned
            var alreadyCredited = _repository.GetCreditedQuantities(related.ID);
            foreach (var group in _currentCheckout.Items.GroupBy(i => i.product.Id))
            {
                var name = group.First().product.Name;
                var requested = group.Sum(i => i.Quantity);
                var sold = related.Items.Where(i => i.product.Id == group.Key).Sum(i => i.Quantity);
                if (sold == 0)
                    throw new Exception($"{name} no figura en la factura N° {related.ID}");

                alreadyCredited.TryGetValue(group.Key, out var credited);
                var available = sold - credited;
                if (requested > available)
                    throw new Exception(
                        $"De {name} solo se pueden devolver {available:0.###} (vendido {sold:0.###}, ya devuelto {credited:0.###})");
            }
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
            else if (item.product.Unit == UnitType.Unit && quantity % 1 != 0)
                return; // invalid for products sold by unit: keep the previous quantity
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

            // Called while the grid is being edited: throwing here crashed the app.
            // An invalid price is ignored and the grid shows the previous one again.
            if (price > 0)
                item.UnitPrice = price;
        }
    }
}