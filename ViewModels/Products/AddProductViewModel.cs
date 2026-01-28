using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StockControl.Services;
using System.Windows;
using StockControl.Enums;
using StockControl.Models;

namespace StockControl.ViewModels.Products
{
    public class AddProductViewModel : INotifyPropertyChanged
    {

        private readonly ProductService _productService;
        private readonly ProductDto? _Product;
        public bool IsEditMode { get; private set; }
        public string Title => IsEditMode ? "Editar producto" : "Agregar producto";

        public Action? ShowSuccessAction { get; set; }

        public Action? CloseAction { get; set; }
        private bool _isSuccess;
        public bool IsSuccess
        {
            get => _isSuccess;
            set { _isSuccess = value; OnPropertyChanged(); }
        }

        public AddProductViewModel(ProductService productService)
        {
            IsEditMode = false;
            _productService = productService;
            _Product = new ProductDto();
            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke());

            UnitTypes = Enum.GetValues(typeof(UnitType));
        }
        // EDITAR
        public AddProductViewModel(ProductService productService, ProductDto product)
            : this(productService)
        {
            IsEditMode = true;
            _Product = product;
            Name = product.Name;
            Brand = product.Brand;
            Barcode = product.Barcode;
            Price = product.Price.Value;
            Stock = product.Stock.Value;
            SelectedUnitType = product.Unit.Value;
        }
        private string _barcode;
        public string Barcode
        {
            get => _barcode;
            set { _barcode = value; 
                    OnPropertyChanged(); 
                    SaveCommand.RaiseCanExecuteChanged();
                }
        }

        private string _brand;
        public string Brand
        {
            get => _brand;
            set { _brand = value; OnPropertyChanged(); }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; 
                    OnPropertyChanged(); 
                    SaveCommand.RaiseCanExecuteChanged();
                }
        }

        private decimal _stock;
        public decimal Stock
        {
            get => _stock;
            set { _stock = value; OnPropertyChanged(); }
        }

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set { _price = value; 
                    OnPropertyChanged(); 
                    SaveCommand.RaiseCanExecuteChanged();
                }
        }

        private UnitType _selectedUnitType;
        public UnitType SelectedUnitType
        {
            get => _selectedUnitType;
            set { _selectedUnitType = value; OnPropertyChanged(); }
        }

        public Array UnitTypes { get; }

        public RelayCommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

private void Save()
{
        _Product.Name = Name;
        _Product.Brand = Brand;
        _Product.Barcode = Barcode;
        _Product.Price = Price;
        _Product.Stock = Stock;
        _Product.Unit = SelectedUnitType;
    if (IsEditMode)
    {
        _productService.UpdateProduct(_Product);
    }
    else
    {
        _productService.AddProduct(_Product);
    }

    ShowSuccessAction?.Invoke();
                Task.Delay(600).ContinueWith(_ =>
                    Application.Current.Dispatcher.Invoke(() =>
                        CloseAction?.Invoke()));
}

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Name)
                   && !string.IsNullOrWhiteSpace(Barcode)
                   && Price > 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
