using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StockControl.Services;
using System.Windows;
using StockControl.Enums;
using StockControl.Views.Windows;

namespace StockControl.ViewModels.Settings
{
    public class SettingsViewModel : INotifyPropertyChanged
    {

        private readonly CompanyService _companyService;
        private readonly Company? _company;

        public Action? ShowSuccessAction { get; set; }
        public Array TaxConditions { get; }

        public RelayCommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public Action? CloseAction { get; set; }
        private bool _isSuccess;
        public bool IsSuccess
        {
            get => _isSuccess;
            set { _isSuccess = value; OnPropertyChanged(); }
        }

        public SettingsViewModel(CompanyService companyService)
        {
            _companyService = companyService;
            _company = companyService.GetCompanyInfo();
            Name = _company?.Name ?? "";
            CUIT = _company?.CUIT ?? "";
            SelectedTaxCondition = _company?.taxCondition ?? TaxCondition.FinalConsumer;
            Tax = _company?.tax ?? 0;
            Phone = _company?.Phone ?? "";
            Email = _company?.Email ?? "";
            Address = _company?.Address ?? "";
            TaxConditions = Enum.GetValues(typeof(TaxCondition));
            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke());
        }

        private string? _name;
        public string? Name
        {
            get => _name;
            set { _name = value; 
                    OnPropertyChanged();
                }
        }

        private string? _cuit;
        public string? CUIT
        {
            get => _cuit;
            set { _cuit = value; OnPropertyChanged();}
        }

        private TaxCondition _selectedTaxCondition;
        public TaxCondition SelectedTaxCondition
        {
            get => _selectedTaxCondition;
            set { _selectedTaxCondition = value; OnPropertyChanged(); }
        }

        private decimal _tax;
        public decimal Tax
        {
            get => _tax;
            set { _tax = value; OnPropertyChanged();}
        }

        private string _phone;
        public string Phone
        {
            get => _phone;
            set { _phone = value; OnPropertyChanged(); }
        }

        private string _email;
        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        private string _address;
        public string Address
        {
            get => _address;
            set { _address = value; OnPropertyChanged(); }
        }



private void Save()
{
    try{
            _company.CUIT = CUIT;
            _company.Name = Name;
            _company.taxCondition = SelectedTaxCondition;
            _company.tax = Tax;
            _company.Phone = Phone;
            _company.Email = Email;
            _company.Address = Address;
            _companyService.SetCompanyInfo(_company);

            ShowSuccessAction = () =>
            {
                new SuccessWindow
                {
                    Owner = Application.Current.MainWindow,
                    Message = "La información de la empresa se ha guardado correctamente."
                }.Show();
            };
            ShowSuccessAction?.Invoke();
                    Task.Delay(1200).ContinueWith(_ =>
                        Application.Current.Dispatcher.Invoke(() =>
                            CloseAction?.Invoke()));
        }
    catch(Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
}

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Name)
                   && !string.IsNullOrWhiteSpace(CUIT)
                   && Tax >= 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
