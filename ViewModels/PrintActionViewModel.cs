using System.ComponentModel;
using QuestPDF.Fluent;
using StockControl.Dtos;
using StockControl.Services;

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
        public void GenerateInvoicePDF()
        {
            var invoiceService = new InvoiceDocumentService(invoice);
            //invoiceService.GeneratePdf($"{Checkout.Client?.Name ?? "invoice"}_{Checkout.ID}");
            invoiceService.GeneratePdfAndShow();
            CloseAction?.Invoke(false);
        }
    }
}