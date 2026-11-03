using System.ComponentModel;
using StockControl.Dtos;
using StockControl.Enums;
using StockControl.Services;
using StockControl.Utils;

namespace StockControl.ViewModels
{
    public class PrintActionViewModel : INotifyPropertyChanged
    {
        public Action<bool>? CloseAction;
        InvoiceDto invoice {get;}
        public event PropertyChangedEventHandler? PropertyChanged;
        public RelayCommand GenerateInvoicePDFCommand { get; }
        public RelayCommand ConfirmCheckoutCommand { get; }
        public RelayCommand PrintTicketCommand { get; }

        // Set when the automatic ticket couldn't be printed
        public string? PrintError { get; init; }

        public PrintActionViewModel(InvoiceDto Invoice)
        {
            invoice = Invoice;
            GenerateInvoicePDFCommand = new RelayCommand(_ => GenerateInvoicePDF());
            ConfirmCheckoutCommand = new RelayCommand(_=> CloseAction?.Invoke(false));
            PrintTicketCommand = new RelayCommand(_ => PrintTicket());
        }

        // With a ticket printer the main button is "Imprimir ticket"; without one, "Generar PDF"
        public bool HasTicketPrinter => TicketPrinter.IsConfigured;
        public bool PdfIsDefault => !HasTicketPrinter;

        public void PrintTicket()
        {
            try
            {
                TicketPrinter.Print(invoice);
                CloseAction?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Imprimir ticket", ex);
                System.Windows.MessageBox.Show($"No se pudo imprimir el ticket:\n{ex.Message}", "Ticket",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        // ---- What happened with ARCA (shown in the dialog) ----
        public string Title => invoice.FiscalStatus switch
        {
            FiscalStatus.Authorized => $"{invoice.VoucherTitle} {invoice.FormattedNumber}",
            FiscalStatus.Pending => "Venta registrada, falta el CAE",
            FiscalStatus.Rejected => "Venta registrada, ARCA rechazó la factura",
            _ => "Operación registrada"
        };

        public string Detail => (PrintError != null ? $"No se pudo imprimir el ticket: {PrintError}\n\n" : "") + DetailText;

        private string DetailText => invoice.FiscalStatus switch
        {
            FiscalStatus.Authorized => $"CAE {invoice.Cae}, vence el {invoice.CaeDueDate:dd/MM/yyyy}." +
                                       (invoice.Production ? "" : " (Homologación: sin validez fiscal.)"),
            FiscalStatus.Pending => $"{invoice.FiscalMessage}\nSe vuelve a intentar sola. También podés enviarla desde el Historial de ventas.",
            FiscalStatus.Rejected => $"Motivo: {invoice.FiscalMessage}\nCorregí el dato y reenviala desde el Historial de ventas.",
            _ => "¿Imprimís el ticket o generás el comprobante en PDF?"
        };

        // Icon/color: ok (green) or warning (red)
        public bool IsWarning => PrintError != null || invoice.FiscalStatus == FiscalStatus.Pending || invoice.FiscalStatus == FiscalStatus.Rejected;

        public void GenerateInvoicePDF()
        {
            var invoiceService = new InvoiceDocumentService(invoice);
            invoiceService.GeneratePdfAndShow(
                $"{invoice.VoucherTitle}_{invoice.FormattedNumber}_{DateTime.Now:yyyyMMdd}"
            );
            CloseAction?.Invoke(false);
        }
    }
}
