using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using StockControl.Models;
using StockControl.Services;
using System.Runtime.CompilerServices;
using System.Windows;
using StockControl.Views.Products;
using StockControl.Views.Windows;
using StockControl.Dtos;


namespace StockControl.ViewModels.Products
{
    public class ProductsViewModel : INotifyPropertyChanged
    {
        private readonly ProductService _productService;
        private readonly ICollectionView _productsView;
        public event PropertyChangedEventHandler PropertyChanged;
        private Product? _selectedProduct;
        public Action<Product?>? CloseAction { get; set; }
        public ViewMode ViewMode { get; }
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
                ConfirmCommand.RaiseCanExecuteChanged();
            }
        }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand ConfirmCommand { get; }

        public ProductsViewModel(ProductService productService, bool CanEditOrDelete)
        {
            _productService = productService;
            if (CanEditOrDelete)
            {
                ViewMode = ViewMode.Editable;
            }
            else
            {
                ViewMode = ViewMode.SelectOnly;
            }
            Products = new ObservableCollection<Product>(_productService.GetProducts(false));

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
            ConfirmCommand = new RelayCommand(
                _ => CloseAction?.Invoke(SelectedProduct),
                _ => SelectedProduct != null);
        }
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
        private void LoadProducts()
        {
            Products.Clear();

            foreach (var product in _productService.GetProducts(false))
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
            vm.CloseAction = success => view.Close();

            view.ShowDialog();

            LoadProducts();
        }
        private void DeleteProduct(object parameter)
        {
            try{
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
            catch(Exception ex){
                MessageBox.Show(ex.Message);
            }
        }
        private void OpenAddProduct()
        {
            ProductDto? createdProduct = null;
            var vm = new AddProductViewModel(_productService);
            var view = new AddProductWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            vm.CloseAction = success =>
            {
                if (success)
                {
                    createdProduct = vm.CreatedProduct;
                    new SuccessWindow
                    {
                        Owner = view
                    }.ShowDialog();
                }

                view.Close();
            };

            view.ShowDialog();

            if (createdProduct != null)
            {
                LoadProducts();

                SelectedProduct = Products.FirstOrDefault(p => p.Id == createdProduct.Id);
             if (ViewMode == ViewMode.SelectOnly && SelectedProduct != null)
                {
                    CloseAction?.Invoke(SelectedProduct);
                }
            }

            _productsView.Refresh();
        }

    }
}
