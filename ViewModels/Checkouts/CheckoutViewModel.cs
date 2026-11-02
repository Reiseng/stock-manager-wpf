using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using StockControl.Dtos;
using StockControl.Enums;
using StockControl.Models;
using StockControl.Services;
using StockControl.ViewModels.Clients;
using StockControl.ViewModels.Products;
using StockControl.Views;
using StockControl.Views.Clients;
using StockControl.Views.Products;
using StockControl.Views.Windows;

namespace StockControl.ViewModels.Checkouts
{
    public class CheckoutViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private ObservableCollection<Product> _searchResults = new();
        private string _ProductSearchText;
        public string ProductSearchText { get => _ProductSearchText;
            set
            {
                _ProductSearchText = value;
                OnPropertyChanged();
            }
        }
        private string _RelatedCheckoutIdText;
        public string RelatedCheckoutIdText { get => _RelatedCheckoutIdText;
            set
            {
                _RelatedCheckoutIdText = value;
                OnPropertyChanged();
            }
        }
        public RoleMode ViewMode { get; }
        public User CurrentUser{ get; }
        public Array InvoiceTypes { get; }
        public Array OperationTypes { get; }
        public Checkout lastCheckout {get ; set; }
        public ObservableCollection<CheckoutItemDto> Items
            => Checkout.Items;
        private readonly CheckoutService _checkoutService;
        public string ClientFullName =>
            Checkout.Client != null
                ? $"{Checkout.Client.Name} {Checkout.Client.LastName}"
                : "Consumidor final";

        public string ClientDocument =>
            Checkout.Client?.Dni?? "";
        private Company? _company;
        private CheckoutDto _checkout;
        public CheckoutDto Checkout
        {
            get => _checkout;
            set
            {
                _checkout = value;
                OnPropertyChanged(nameof(Checkout));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(ClientFullName));
                OnPropertyChanged(nameof(ClientDocument));
                OnPropertyChanged(nameof(Checkout.Items));
            }
        }
        public InvoiceType SelectedInvoiceType
        {
            get => Checkout.InvoiceType;
            set
            {
                if (Checkout.InvoiceType != value)
                {
                    Checkout.InvoiceType = value;
                    _checkoutService.SetInvoiceType(value);
                    OnPropertyChanged();
                }
            }
        }
        public OperationType SelectedOperationType
        {
            get => Checkout.OperationType;
            set
            {
                if (Checkout.OperationType != value)
                {
                    Checkout.OperationType = value;
                    _checkoutService.SetOperationType(value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RequiresRelatedCheckout));
                }
            }
        }
        public ObservableCollection<Product> SearchResults
        {
            get => _searchResults;
            set
            {
                _searchResults = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSearchResults));
            }
        }
        // Several products matched the typed name: the cashier picks one from a list
        public bool HasSearchResults => SearchResults.Count > 0;

        // Feedback under the search box (no modal popups while scanning)
        private string? _searchMessage;
        public string? SearchMessage
        {
            get => _searchMessage;
            set { _searchMessage = value; OnPropertyChanged(); }
        }

        // Credit and debit notes must point to an existing sale
        public bool RequiresRelatedCheckout =>
            SelectedOperationType == OperationType.CreditNote || SelectedOperationType == OperationType.DebitNote;
        public RelayCommand AddProductCommand { get; }
        public RelayCommand AddClientCommand { get; }
        public RelayCommand ConfirmCheckoutCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand RemoveItemCommand { get; }
        public RelayCommand LoggoutCommand { get; }
        public RelayCommand AddSearchResultCommand { get; }
        public RelayCommand ClearSearchResultsCommand { get; }

        public decimal Total => Checkout?.Total ?? 0;

        public CheckoutViewModel()
        {
            _checkoutService = AppServices.CheckoutService;
            LoadCheckout();
            CurrentUser = AppServices.UserService.CurrentUser;
            if (CurrentUser.Role == RoleType.Admin){
                ViewMode = RoleMode.AdminOnly;
            }
            if (CurrentUser.Role == RoleType.Cashier){
                ViewMode = RoleMode.CashierOnly;
            }
            if (CurrentUser.Role == RoleType.Repository){
                ViewMode = RoleMode.RepositoryOnly;
            }
            _company = AppServices.CompanyService.GetCompanyInfo();
            InvoiceTypes = Enum.GetValues(typeof(InvoiceType));
            OperationTypes = Enum.GetValues(typeof(OperationType));
            ConfirmCheckoutCommand = new RelayCommand(
                _ => ConfirmCheckout(),
                _ => Checkout.Items.Any()
            );
            CancelCommand = new RelayCommand(
                _=>ClearCheckout()
            );
            AddClientCommand = new RelayCommand(_ => OpenAddClient());
            AddProductCommand = new RelayCommand(_ => OpenAddProduct());
            RemoveItemCommand = new RelayCommand(item => RemoveItem((CheckoutItemDto)item));
            AddSearchResultCommand = new RelayCommand(item => AddSearchResult(item as Product));
            ClearSearchResultsCommand = new RelayCommand(_ => ClearSearchResults());
            LoggoutCommand = new RelayCommand(_ =>
            {
            if (MessageBox.Show(
                                $"¿Seguro de cerrar sesión?",
                                "Confirmar",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                                return;
                AppServices.UserService.Loggout();

                var loginVm = new LogginWindowViewModel();
                var loginView = new LogginWindow
                {
                    DataContext = loginVm
                };

                loginView.Show();

                Application.Current.MainWindow.Close();
                Application.Current.MainWindow = loginView;
            });
            
            
        }
        private void RemoveItem(CheckoutItemDto item)
        {
            try{
                if (item != null)
                {
                    Checkout.Items.Remove(item);
                    CheckoutItem? modelItem = _checkoutService.GetCurrentCheckout().Items
                        .FirstOrDefault(i => i.product.Id == item.ProductId);
                    if (modelItem != null)
                    {
                        _checkoutService.RemoveProduct(modelItem);
                    }
                    OnPropertyChanged(nameof(Total));
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public void LoadCheckout()
        {
            if (Checkout != null)
            {
                foreach(var item in Checkout.Items)
                {
                    _checkoutService.SetQuantity(item.ProductId, item.Quantity);
                    _checkoutService.SetProductPrice(item.ProductId, item.UnitPrice);
                }
            }
            var checkout = _checkoutService.GetCurrentCheckout();

            if (Checkout == null)
            {
                Checkout = CheckoutDto.FromModel(checkout);
                return;
            }
            Checkout.Items.Clear();
            foreach (var item in checkout.Items)

                Checkout.Items.Add(CheckoutItemDto.FromModel(item));

            OnPropertyChanged(nameof(Total));
            ConfirmCheckoutCommand.RaiseCanExecuteChanged();
            HighlightedItem = Checkout.Items.FirstOrDefault(i => i.ProductId == _lastProductId);
        }
        private void ConfirmCheckout()
        {
            try
            {

                if (Checkout.Client != null)
                {
                    var client = AppServices.ClientService.GetClientByID(Checkout.Client.ID);
                    if (client != null)
                    {
                        _checkoutService.SetClient(client);
                    }
                }else
                {
                    _checkoutService.SetClient(AppServices.ClientService.GetFinalConsumer());
                }
                lastCheckout = _checkoutService.ConfirmCheckout();
                OpenActions();
                _checkoutService.StartCheckout();
                Checkout = CheckoutDto.FromModel(_checkoutService.GetCurrentCheckout());
                LoadCheckout();
                OnPropertyChanged(nameof(ClientFullName));
                OnPropertyChanged(nameof(ClientDocument));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));}
        private void OpenAddClient()
        {
            Client? selectedClient = null;

            var vm = new ClientsViewModel(
                AppServices.ClientService,
                canEditClients: false
            );

            var view = new ClientWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            vm.CloseAction = SelectedClient =>
            {
                selectedClient = vm.SelectedClient;
                new SuccessWindow
                {
                    Owner = view
                }.ShowDialog();

                view.Close();
            };
            view.ShowDialog();
            if (selectedClient != null)
            {
                ClientDto? selectedClientDto = ClientDto.FromModel(selectedClient);
                _checkout.Client = selectedClientDto;
                // Keep the service in sync too: before, scanning a barcode afterwards
                // rebuilt the cart from the service and the client went back to "Consumidor Final"
                _checkoutService.SetClient(selectedClient);
            }
                OnPropertyChanged(nameof(Checkout));
                OnPropertyChanged(nameof(ClientFullName));
                OnPropertyChanged(nameof(ClientDocument));
        }
        private void OpenAddProduct()
        {
            Product? selectedProduct = null;

            var vm = new ProductsViewModel(
                AppServices.ProductService,
                CanEditOrDelete: false
            );

            var view = new ProductWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            vm.CloseAction = SelectedClient =>
            {
                selectedProduct = vm.SelectedProduct;
                new SuccessWindow
                {
                    Owner = view
                }.ShowDialog();

                view.Close();
            };

            view.ShowDialog();

            if (selectedProduct != null)
                AddToCart(selectedProduct, 1);
        }
        public void SetRelatedCheckoutIdFromText()
        {
            try{
            if (int.TryParse(RelatedCheckoutIdText, out int parsedValue))
            {
                _checkoutService.GetCheckoutByID(RelatedCheckoutIdText);
                Checkout.RelatedCheckoutId = parsedValue;
                _checkoutService.GetCurrentCheckout().RelatedCheckoutId = parsedValue;
            }
            else
            {
                Checkout.RelatedCheckoutId = null;
                _checkoutService.GetCurrentCheckout().RelatedCheckoutId = null;
            }
        }        catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
             finally
            {
                RelatedCheckoutIdText = "";
                OnPropertyChanged(nameof(Checkout));
            }
        }
        // ---- Keyboard selling ----
        // "3*7790001"  adds 3 units of that product (also works with a name: "2*yerba")
        // "1,5*queso"  decimals for products sold by weight
        // "3*"         changes the quantity of the last product added to 3
        // + / -        (with the search box empty) add or remove one unit of the last product
        private static readonly System.Text.RegularExpressions.Regex QuantityPrefix =
            new(@"^\s*(\d+(?:[.,]\d+)?)\s*\*\s*(.*)$");

        private int? _lastProductId;
        private decimal _pendingQuantity = 1;

        // Row highlighted in the grid (the last product added or changed)
        private CheckoutItemDto? _highlightedItem;
        public CheckoutItemDto? HighlightedItem
        {
            get => _highlightedItem;
            set { _highlightedItem = value; OnPropertyChanged(); }
        }

        private void ClearSearchResults()
        {
            SearchResults = new ObservableCollection<Product>();
        }
        private void AddSearchResult(Product? product)
        {
            if (product == null)
                return;
            AddToCart(product, _pendingQuantity);
            _pendingQuantity = 1;
            ClearSearchResults();
        }
        private void AddToCart(Product product, decimal quantity)
        {
            try
            {
                _checkoutService.AddProduct(product, quantity);
                _lastProductId = product.Id;
                RefreshFromService();
            }
            catch (Exception ex)
            {
                SearchMessage = ex.Message;
            }
        }

        // Rebuilds the grid from the service WITHOUT copying the grid back first.
        // (LoadCheckout copies grid -> service, which is right after editing a cell
        // but undid programmatic changes: e.g. adding by name a product already in
        // the cart didn't increase its quantity.)
        private void RefreshFromService()
        {
            var checkout = _checkoutService.GetCurrentCheckout();
            Checkout.Items.Clear();
            foreach (var item in checkout.Items)
                Checkout.Items.Add(CheckoutItemDto.FromModel(item));
            OnPropertyChanged(nameof(Total));
            ConfirmCheckoutCommand.RaiseCanExecuteChanged();
            HighlightedItem = Checkout.Items.FirstOrDefault(i => i.ProductId == _lastProductId);
        }

        private CheckoutItem? LastItem()
        {
            var items = _checkoutService.GetCurrentCheckout().Items;
            return items.FirstOrDefault(i => i.product.Id == _lastProductId) ?? items.LastOrDefault();
        }

        private static bool TryParseQuantity(string text, out decimal quantity)
            => decimal.TryParse(text.Replace('.', ','), System.Globalization.NumberStyles.Number,
                   new System.Globalization.CultureInfo("es-AR"), out quantity) && quantity > 0 && quantity <= 100000;

        // + / - keys on the empty search box
        public void ChangeLastQuantity(decimal delta)
        {
            SearchMessage = null;
            var last = LastItem();
            if (last == null)
                return;
            _lastProductId = last.product.Id;
            _checkoutService.SetQuantity(last.product.Id, last.Quantity + delta); // 0 or less removes it
            RefreshFromService();
        }

        private void SetLastQuantity(decimal quantity)
        {
            var last = LastItem();
            if (last == null)
            {
                SearchMessage = "Primero agregá un producto.";
                return;
            }
            if (last.product.Unit == UnitType.Unit && quantity % 1 != 0)
            {
                SearchMessage = $"{last.product.Name} se vende por unidad: usá un número entero.";
                return;
            }
            _lastProductId = last.product.Id;
            _checkoutService.SetQuantity(last.product.Id, quantity);
            RefreshFromService();
        }

        public void AddProductFromSearch()
        {
            SearchMessage = null;
            ClearSearchResults();
            _pendingQuantity = 1;
            var text = ProductSearchText?.Trim() ?? "";
            if (text.Length == 0)
                return;

            // 0. Quantity prefix: "3*..." / "3*"
            decimal quantity = 1;
            var match = QuantityPrefix.Match(text);
            if (match.Success)
            {
                if (!TryParseQuantity(match.Groups[1].Value, out quantity))
                {
                    SearchMessage = "Cantidad inválida.";
                    return;
                }
                text = match.Groups[2].Value.Trim();
                if (text.Length == 0)
                {
                    SetLastQuantity(quantity);
                    return;
                }
            }

            // 1. Barcode search
            if (IsBarcode(text))
            {
                try
                {
                    var product = AppServices.ProductService.GetProductByBarcode(text);
                    AddToCart(product, quantity);
                    return;
                }
                catch (Exception)
                {
                    // Not a known barcode: try it as a name below
                }
            }

            // 2. Search by name
            var results = AppServices.ProductService.SearchByName(text);

            // 3. If only one result, add it directly; several => show the list
            if (results.Count == 1)
            {
                AddToCart(results[0], quantity);
            }
            else if (results.Count == 0)
            {
                SearchMessage = $"No hay productos con el código o nombre «{text}».";
            }
            else
            {
                _pendingQuantity = quantity;
                SearchResults = new ObservableCollection<Product>(results);
            }
        }
        public void OpenActions()
        {
            InvoiceDto invoice = new InvoiceDto(
                lastCheckout.ID.ToString(),
                lastCheckout.Date,
                lastCheckout.invoiceType,
                lastCheckout.operationType,
                _company!,
                Checkout.Client!,
                Checkout.Items.ToList(),
                Total
            );
            var vm = new PrintActionViewModel(invoice);
            var view = new PrintActionWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };
            vm.CloseAction = success =>
            {
                new SuccessWindow
                {
                    Owner = view
                }.ShowDialog();

                view.Close();
            };
            view.ShowDialog();
        }
        private bool IsBarcode(string text)
        {
            return text.All(char.IsDigit);
        }
        public void ClearCheckout()
        {
            _checkoutService.ClearCheckout();
            LoadCheckout();
        }

    }
}
