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
            }
        }
        public RelayCommand DetailCommand { get; }

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

            return Checkout.Client.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
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
            var vm = new CheckoutDetailViewModel(_checkoutService, checkoutDto);
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