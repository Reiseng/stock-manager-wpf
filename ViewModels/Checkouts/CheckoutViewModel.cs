using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using StockControl.Enums;
using StockControl.Models;
using StockControl.Services;
using StockControl.ViewModels.Clients;
using StockControl.ViewModels.Products;
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
        public Array InvoiceTypes { get; }
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
        public RelayCommand RemoveItemCommand { get; }

        public decimal SubTotal => Checkout?.SubTotal ?? 0;
        public decimal Total => Checkout?.Total * (1+ (_company?.tax/100 ?? 0)) ?? 0;

        public CheckoutViewModel(CheckoutService checkoutService)
        {
            _company = AppServices.CompanyService.GetCompanyInfo();
            InvoiceTypes = Enum.GetValues(typeof(InvoiceType));
            _checkoutService = checkoutService;
            ConfirmCheckoutCommand = new RelayCommand(
                _ => ConfirmCheckout(),
                _ => Checkout.Items.Any()
            );
            AddClientCommand = new RelayCommand(_ => OpenAddClient());
            AddProductCommand = new RelayCommand(_ => OpenAddProduct());
            RemoveItemCommand = new RelayCommand(item => RemoveItem((CheckoutItemDto)item));
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
        private void LoadCheckout()
        {
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
                _checkoutService.ConfirmCheckout();
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
            ClientDto? selectedClientDto = ClientDto.FromModel(selectedClient);
            if (selectedClient != null)
            {
                _checkout.Client = selectedClientDto;
            }
                OnPropertyChanged(nameof(Checkout));
                OnPropertyChanged(nameof(ClientFullName));
                OnPropertyChanged(nameof(ClientDocument));
        }
        private void OpenAddProduct()
        {
            var vm = new AddProductViewModel(AppServices.ProductService);
            var view = new AddProductWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            vm.ShowSuccessAction = () =>
            {
                new SuccessWindow
                {
                    Owner = view
                }.Show();
            };
            vm.CloseAction = () => view.Close();
            view.ShowDialog();
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
        private bool IsBarcode(string text)
        {
            return text.All(char.IsDigit);
        }

    }
}
