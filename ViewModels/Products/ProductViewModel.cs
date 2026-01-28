using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using StockControl.Models;
using StockControl.Services;
using System.Runtime.CompilerServices;
using System.Windows;
using StockControl.Views.Products;
using StockControl.Views.Windows;


namespace StockControl.ViewModels.Products
{
    public class ProductsViewModel : INotifyPropertyChanged
    {
        private readonly ProductService _productService;
        private readonly ICollectionView _productsView;
        public event PropertyChangedEventHandler PropertyChanged;

        private Product? _selectedProduct;

        public ObservableCollection<Product> Products { get; }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                _productsView.Refresh();
            }
        }
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged();

                EditCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand AddCommand { get; }

        public ProductsViewModel(ProductService productService)
        {
            _productService = productService;

            Products = new ObservableCollection<Product>(_productService.GetProducts());

            _productsView = CollectionViewSource.GetDefaultView(Products);
            _productsView.Filter = FilterProducts;
            LoadProducts();
            AddCommand = new RelayCommand(_ => OpenAddProduct());

            EditCommand = new RelayCommand(
                _ => EditProduct(SelectedProduct),
                _ => SelectedProduct != null);

            DeleteCommand = new RelayCommand(
                _ => DeleteProduct(SelectedProduct),
                _ => SelectedProduct != null);
        }
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
        private void LoadProducts()
        {
            Products.Clear();

            foreach (var product in _productService.GetProducts())
                Products.Add(product);
        }
        private bool FilterProducts(object obj)
        {
            if (obj is not Product product)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            return product.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || product.Barcode.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private void EditProduct(Product product)
        {
            ProductDto _product = new ProductDto(product.Id, product.Barcode, product.Brand, product.Name, product.Stock, product.Price, product.Unit);
            var vm = new AddProductViewModel(_productService, _product);
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

            LoadProducts();
        }
        private void DeleteProduct(object parameter)
        {
            if (parameter is not Product product)
                return;
            if (MessageBox.Show(
                $"¿Eliminar {product.Name}?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            _productService.RemoveProduct(product.Id);
            Products.Remove(product);
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

            LoadProducts();
            _productsView.Refresh();
        }
    }
}
