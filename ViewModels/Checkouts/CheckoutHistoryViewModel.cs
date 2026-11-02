using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using StockControl.Models;
using StockControl.Services;
using System.Runtime.CompilerServices;
using StockControl.Views.Checkouts;
using System.Windows;
using StockControl.Dtos;

namespace StockControl.ViewModels.Checkouts
{
    public class CheckoutsHistoryViewModel : INotifyPropertyChanged
    {
        private readonly CheckoutService _checkoutService;
        private readonly ICollectionView _checkoutView;
        private Checkout? _selectedCheckout;

        public ObservableCollection<Checkout> Checkouts { get; }
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                _checkoutView.Refresh();
            }
        }
        public Checkout? SelectedCheckout
        {
            get => _selectedCheckout;
            set
            {
                _selectedCheckout = value;
                OnPropertyChanged();

                DetailCommand.RaiseCanExecuteChanged();
                SendToArcaCommand.RaiseCanExecuteChanged();
            }
        }
        public RelayCommand DetailCommand { get; }

        // ---- ARCA ----
        public bool ArcaEnabled => AppServices.FiscalService.IsEnabled;
        public RelayCommand SendToArcaCommand { get; }

        private bool _isSending;
        public bool IsSending
        {
            get => _isSending;
            set { _isSending = value; OnPropertyChanged(); SendToArcaCommand.RaiseCanExecuteChanged(); }
        }

        // Internal, pending or rejected sale -> ask for the CAE now
        private async Task SendToArcaAsync(Checkout checkout)
        {
            var name = checkout.operationType == Enums.OperationType.Sale ? "la venta" : "el comprobante";
            if (MessageBox.Show(
                    $"¿Facturar en ARCA {name} N° {checkout.ID} por {checkout.Total:C2}?\n\n" +
                    "Se pide el CAE con la fecha de hoy.",
                    "Facturar en ARCA", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            IsSending = true;
            try
            {
                var result = await AppServices.FiscalService.SendAsync(checkout.ID);
                var message = result.FiscalStatus switch
                {
                    Enums.FiscalStatus.Authorized => $"{result.FiscalSummary}\nCAE {result.Cae}, vence el {result.CaeDueDate:dd/MM/yyyy}.",
                    Enums.FiscalStatus.Rejected => $"ARCA rechazó el comprobante:\n{result.FiscalMessage}",
                    _ => $"Quedó pendiente:\n{result.FiscalMessage}"
                };
                MessageBox.Show(message, "Facturar en ARCA", MessageBoxButton.OK,
                    result.FiscalStatus == Enums.FiscalStatus.Authorized ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Facturar en ARCA", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                IsSending = false;
                LoadCheckouts();
                _checkoutView.Refresh();
            }
        }

        public CheckoutsHistoryViewModel(CheckoutService checkoutService)
        {
            _checkoutService = checkoutService;

            Checkouts = new ObservableCollection<Checkout>(_checkoutService.GetCheckouts());

            _checkoutView = CollectionViewSource.GetDefaultView(Checkouts);
            _checkoutView.Filter = FilterCheckouts;
            LoadCheckouts();
            DetailCommand = new RelayCommand(
                _ => OpenCheckoutDetail(SelectedCheckout),
                _ => SelectedCheckout!= null);
            SendToArcaCommand = new RelayCommand(
                async _ => await SendToArcaAsync(SelectedCheckout!),
                _ => ArcaEnabled && !IsSending && SelectedCheckout != null
                     && SelectedCheckout.FiscalStatus != Enums.FiscalStatus.Authorized
                     && SelectedCheckout.operationType != Enums.OperationType.Budget);
        }
        private void LoadCheckouts()
        {
            Checkouts.Clear();

            foreach (var Checkout in _checkoutService.GetCheckouts())
                Checkouts.Add(Checkout);
        }
        private bool FilterCheckouts(object obj)
        {
            if (obj is not Checkout Checkout)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            return Checkout.FiscalSummary.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || Checkout.Client.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) 
                || Checkout.Client.LastName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || Checkout.ID.ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        private void OpenCheckoutDetail(Checkout _checkout)
        {
            CheckoutDto checkoutDto = CheckoutDto.FromModel(_checkout);
            var vm = new CheckoutDetailViewModel(_checkoutService, checkoutDto, _checkout);
            var view = new CheckoutDetailWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            vm.CloseAction = success => view.Close();
            view.ShowDialog();

            LoadCheckouts();
            _checkoutView.Refresh();
        }
    }
}