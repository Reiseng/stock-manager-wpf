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

        public PrintActionViewModel(InvoiceDto Invoice)
        {
            invoice = Invoice;
            GenerateInvoicePDFCommand = new RelayCommand(_ => GenerateInvoicePDF());
            ConfirmCheckoutCommand = new RelayCommand(_=> CloseAction?.Invoke(false));
        }

        // ---- What happened with ARCA (shown in the dialog) ----
        public string Title => invoice.FiscalStatus switch
        {
            FiscalStatus.Authorized => $"{invoice.VoucherTitle} {invoice.FormattedNumber}",
            FiscalStatus.Pending => "Venta registrada, falta el CAE",
            FiscalStatus.Rejected => "Venta registrada, ARCA rechazó la factura",
            _ => "Operación registrada"
        };

        public string Detail => invoice.FiscalStatus switch
        {
            FiscalStatus.Authorized => $"CAE {invoice.Cae}, vence el {invoice.CaeDueDate:dd/MM/yyyy}." +
                                       (invoice.Production ? "" : " (Homologación: sin validez fiscal.)"),
            FiscalStatus.Pending => $"{invoice.FiscalMessage}\nSe vuelve a intentar sola. También podés enviarla desde el Historial de ventas.",
            FiscalStatus.Rejected => $"Motivo: {invoice.FiscalMessage}\nCorregí el dato y reenviala desde el Historial de ventas.",
            _ => "¿Querés generar el comprobante en PDF para imprimirlo o enviarlo?"
        };

        // Icon/color: ok (green) or warning (red)
        public bool IsWarning => invoice.FiscalStatus == FiscalStatus.Pending || invoice.FiscalStatus == FiscalStatus.Rejected;

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
