using StockControl.Services;
using StockControl.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using StockControl.Dtos;
using QuestPDF.Fluent;
using StockControl.Utils;

namespace StockControl.ViewModels.Checkouts
{
    public class CheckoutDetailViewModel : INotifyPropertyChanged
    {
        private readonly CheckoutService _checkoutService;
        private readonly CheckoutDto _checkoutDto;
        public Company? company;
        public Action<bool>? CloseAction;
        public string ClientFullName =>
            ClientDto != null
                ? $"{ClientDto.Name} {ClientDto.LastName}"
                : "Consumidor final";
        public string ClientDocument =>
            ClientDto?.Dni?? "";
        public event PropertyChangedEventHandler? PropertyChanged;
        public RelayCommand PrintPDFCommand { get; }
        public RelayCommand PrintTicketCommand { get; }

        // Reprint the ticket (or save it as PDF if there is no ticket printer on this PC)
        private void PrintTicket()
        {
            var invoice = InvoiceDto.From(_model, company!);
            try
            {
                if (StockControl.Utils.TicketPrinter.IsConfigured)
                    StockControl.Utils.TicketPrinter.Print(invoice);
                else
                    new TicketDocument(invoice, StockControl.Utils.TicketPrinter.Config.PaperWidthMm)
                        .GeneratePdfAndShow($"Ticket_{invoice.FormattedNumber}");
            }
            catch (Exception ex)
            {
                StockControl.Utils.ErrorLog.Write("Imprimir ticket", ex);
                System.Windows.MessageBox.Show($"No se pudo imprimir el ticket:\n{ex.Message}", "Ticket",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private readonly StockControl.Models.Checkout _model;

        // ---- ARCA ----
        public string FiscalSummary => _model.FiscalSummary;
        public string? FiscalDetail => _model.FiscalStatus switch
        {
            Enums.FiscalStatus.Authorized => $"CAE {_model.Cae}, vence el {_model.CaeDueDate:dd/MM/yyyy}",
            Enums.FiscalStatus.Internal => "No se informó a ARCA (no válido como factura)",
            _ => _model.FiscalMessage
        };

        public CheckoutDetailViewModel(CheckoutService CheckoutService, CheckoutDto Checkout, StockControl.Models.Checkout model)
        {
            _model = model;
            var companyService = AppServices.CompanyService;
            company = companyService.GetCompanyInfo();
            _checkoutService = CheckoutService;
            _checkoutDto = Checkout;
            ID = Checkout.ID;
            ClientDto = Checkout.Client;
            Items = Checkout.Items;
            Date = Checkout.Date;
            Total = model.Total;
            PrintPDFCommand = new RelayCommand(_ => GenerateInvoicePDF());
            PrintTicketCommand = new RelayCommand(_ => PrintTicket());
        }
        private int _id;
        public int ID
        {
            get => _id;
            set { _id = value;}
        }
        private ClientDto? _clientDto;
        public ClientDto? ClientDto
        {
            get => _clientDto;
            set
            {
                _clientDto = value;
                OnPropertyChanged(nameof(ClientDto));
                OnPropertyChanged(nameof(ClientFullName));
                OnPropertyChanged(nameof(ClientDocument));
            }
        }
        private ObservableCollection<CheckoutItemDto> _items;
        public ObservableCollection<CheckoutItemDto> Items
        {
            get => _items;
            set { _items = value;}
        }
        private DateTime _date;
        public DateTime Date
        {
            get => _date;
            set { _date = value;}
        }
        private decimal _total;
        public decimal Total
        {
            get => _total;
            set {_total = value;}
        }
        private void GenerateInvoicePDF()
        {
            var invoice = InvoiceDto.From(_model, company!);
            var invoiceService = new InvoiceDocumentService(invoice);
            invoiceService.GeneratePdfAndShow(
                $"{invoice.VoucherTitle}_{invoice.FormattedNumber}_{invoice.client.Name}{invoice.client.LastName}"
            );
            CloseAction?.Invoke(false);
        }
    }
}
