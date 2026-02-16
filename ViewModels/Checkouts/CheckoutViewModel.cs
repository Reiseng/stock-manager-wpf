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
        public RoleMode ViewMode { get; }
        public User CurrentUser{ get; }
        public Array InvoiceTypes { get; }
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
                OnPropertyChanged(nameof(SubTotal));
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
                    OnPropertyChanged();
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
            }
        }
        public RelayCommand AddProductCommand { get; }
        public RelayCommand AddClientCommand { get; }
        public RelayCommand ConfirmCheckoutCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand RemoveItemCommand { get; }
        public RelayCommand LoggoutCommand { get; }

        public decimal SubTotal => Checkout?.SubTotal ?? 0;
        public decimal Total => Checkout?.SubTotal * (1+ (_company?.tax/100 ?? 0)) ?? 0;

        public CheckoutViewModel()
        {
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
            _checkoutService = AppServices.CheckoutService;
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
            
            LoadCheckout();
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
                    OnPropertyChanged(nameof(SubTotal));
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
            OnPropertyChanged(nameof(SubTotal));
            ConfirmCheckoutCommand.RaiseCanExecuteChanged();
        }
        private void ConfirmCheckout()
        {
            try
            {
                _checkoutService.SetInvoiceType(Checkout.InvoiceType);
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
                Checkout = null;
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
            {
                ProductDto? selectedProductDto = ProductDto.FromModel(selectedProduct);
                _checkoutService.AddProduct(selectedProduct, 1);
            }
                OnPropertyChanged(nameof(Checkout));
                OnPropertyChanged(nameof(Checkout.Items));
            LoadCheckout();
        }

        public void AddProductFromSearch()
        {
            if (string.IsNullOrWhiteSpace(ProductSearchText))
                return;

            // 1. Barcode search
            if (IsBarcode(ProductSearchText))
            {
                try
                {
                    var product = AppServices.ProductService.GetProductByBarcode(ProductSearchText);
                    if (product == null)
                        return;

                    _checkoutService.AddProduct(product, 1);
                    Checkout = CheckoutDto.FromModel(_checkoutService.GetCurrentCheckout());
                    LoadCheckout();
                    SearchResults.Clear();
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            // 2. Search by name
            var results = AppServices.ProductService.SearchByName(ProductSearchText);
            SearchResults = new ObservableCollection<Product>(results);

            // 3. If only one result, add it directly
            if (SearchResults.Count == 1)
            {
                _checkoutService.AddProduct(SearchResults[0], 1);
                LoadCheckout();
                SearchResults.Clear();
            }
        }
        public void OpenActions()
        {
            InvoiceDto invoice = new InvoiceDto(
                lastCheckout.ID.ToString(),
                lastCheckout.Date,
                lastCheckout.invoiceType,
                _company!,
                Checkout.Client!,
                Checkout.Items.ToList(),
                SubTotal,
                _company?.tax ?? 0,
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
