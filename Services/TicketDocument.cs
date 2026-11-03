using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StockControl.Dtos;
using StockControl.Enums;
using StockControl.Services.Arca;
using StockControl.Utils;

namespace StockControl.Services
{
    // Narrow ticket for thermal printers (58 or 80 mm roll).
    // Same data as the A4 voucher: with CAE it is a valid electronic invoice in
    // ticket format (letter, CAE, due date and ARCA QR); without CAE it says
    // "no válido como factura".
    // One continuous page as tall as the content: no blank paper at the end.
    public class TicketDocument : IDocument
    {
        public InvoiceDto Invoice { get; }
        public int PaperWidthMm { get; }

        public TicketDocument(InvoiceDto invoice, int paperWidthMm = 80)
        {
            Invoice = invoice;
            PaperWidthMm = paperWidthMm == 58 ? 58 : 80;
        }

        // What the printer can actually print on each roll (the rest is hardware margin)
        public static float PrintableWidthMm(int paperWidthMm) => paperWidthMm == 58 ? 48f : 72f;

        private bool Narrow => PaperWidthMm == 58;
        private float BaseSize => Narrow ? 7.5f : 8.5f;
        private bool NetPrices => Invoice.IsFiscal && Invoice.Letter == 'A';

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
        public DocumentSettings GetSettings() => DocumentSettings.Default;

        private static string Money(decimal value) => value.ToString("C2");

        public void Compose(IDocumentContainer container)
        {
            var width = PrintableWidthMm(PaperWidthMm);
            container.Page(page =>
            {
                page.ContinuousSize(width, Unit.Millimetre);
                page.PageColor(Colors.White); // thermal paper; also avoids transparent images
                page.Margin(1.5f, Unit.Millimetre);
                page.MarginBottom(6, Unit.Millimetre); // room before the cutter
                page.DefaultTextStyle(x => x.FontSize(BaseSize).FontColor(Colors.Black).LineHeight(1.15f));
                page.Content().Column(column =>
                {
                    column.Spacing(2);
                    Header(column);
                    Separator(column);
                    Voucher(column);
                    Separator(column);
                    Items(column);
                    Separator(column);
                    Totals(column);
                    Fiscal(column);
                    column.Item().PaddingTop(6).AlignCenter().Text("¡Gracias por su compra!").Italic();
                });
            });
        }

        private void Header(ColumnDescriptor column)
        {
            var seller = Invoice.seller;
            var logo = ConfigService.LogoPath;
            if (File.Exists(logo))
                column.Item().AlignCenter().Height(Narrow ? 12 : 15, Unit.Millimetre).Image(logo).FitHeight();
            column.Item().AlignCenter().Text(seller.Name ?? "").FontSize(BaseSize + 3).Bold();
            column.Item().AlignCenter().Text($"CUIT {seller.CUIT}");
            column.Item().AlignCenter().Text(seller.taxCondition.GetDescription());
            if (!string.IsNullOrWhiteSpace(seller.Address))
                column.Item().AlignCenter().Text(seller.Address);
            if (!string.IsNullOrWhiteSpace(seller.Phone))
                column.Item().AlignCenter().Text($"Tel. {seller.Phone}");
        }

        private void Voucher(ColumnDescriptor column)
        {
            if (Invoice.IsFiscal)
            {
                column.Item().AlignCenter().Text($"{Invoice.VoucherTitle.ToUpper()}  (Cód. {Invoice.VoucherType:D3})").FontSize(BaseSize + 2).Bold();
                column.Item().AlignCenter().Text($"N° {Invoice.FormattedNumber}").Bold();
            }
            else
            {
                var title = Invoice.operationType switch
                {
                    OperationType.Budget => "PRESUPUESTO",
                    OperationType.CreditNote => "NOTA DE CRÉDITO",
                    OperationType.DebitNote => "NOTA DE DÉBITO",
                    _ => "TICKET",
                };
                column.Item().AlignCenter().Text($"{title}  N° {Invoice.invoiceNumber}").FontSize(BaseSize + 2).Bold();
                column.Item().AlignCenter().Text("NO VÁLIDO COMO FACTURA").Bold();
            }
            column.Item().AlignCenter().Text($"{Invoice.date:dd/MM/yyyy HH:mm}");
            if (Invoice.IsFiscal && !Invoice.Production)
                column.Item().AlignCenter().Text("HOMOLOGACIÓN: SIN VALIDEZ FISCAL").Bold();

            // Customer: always on ARCA vouchers; on internal ones only if it isn't "Consumidor Final"
            var client = Invoice.client;
            var finalConsumer = Invoice.DocType == 99;
            if (Invoice.IsFiscal || !finalConsumer)
            {
                column.Item().PaddingTop(3).Text(finalConsumer ? "A CONSUMIDOR FINAL" : $"Cliente: {client.Name} {client.LastName}".Trim());
                if (!finalConsumer)
                    column.Item().Text($"{(Invoice.DocType == 80 ? "CUIT" : "DNI")}: {client.Dni}");
                if (Invoice.IsFiscal)
                    column.Item().Text($"IVA: {client.TaxCondition.GetDescription()}");
            }
        }

        private void Items(ColumnDescriptor column)
        {
            foreach (var item in Invoice.items)
            {
                var unit = NetPrices ? item.UnitPrice : FiscalRules.Round(item.UnitPrice * (1 + item.Tax / 100));
                var subtotal = NetPrices ? FiscalRules.Round(item.UnitPrice * item.Quantity) : FiscalRules.Round(item.Total);
                var qty = item.Quantity.ToString("0.###") + (item.Unit switch
                {
                    UnitType.Kg => " kg",
                    UnitType.Liter => " l",
                    _ => "",
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(item.ProductName ?? "").SemiBold();
                    if (NetPrices) // Factura A: rate of each line (prices are without VAT)
                        row.AutoItem().PaddingLeft(4).AlignRight().Text($"IVA {item.Tax:0.##}%").FontSize(BaseSize - 1);
                });
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"  {qty} x {Money(unit)}");
                    row.AutoItem().AlignRight().Text(Money(subtotal));
                });
            }
            var units = Invoice.items.Where(i => i.Unit != UnitType.Kg && i.Unit != UnitType.Liter).Sum(i => i.Quantity);
            column.Item().PaddingTop(2).Text($"Artículos: {Invoice.items.Count}" + (units > 0 ? $"  ·  Unidades: {units:0.###}" : ""))
                .FontSize(BaseSize - 0.5f);
        }

        private void Totals(ColumnDescriptor column)
        {
            var amounts = Invoice.Amounts;
            if (NetPrices && amounts != null)
            {
                Line(column, "Neto gravado", Money(amounts.Net));
                foreach (var vat in amounts.VatLines)
                    Line(column, $"IVA {vat.Rate:0.##}%", Money(vat.Vat));
            }
            column.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem().Text("TOTAL").FontSize(BaseSize + 4).Bold();
                row.AutoItem().AlignRight().Text(Money(Invoice.total)).FontSize(BaseSize + 4).Bold();
            });

            // Ley 27.743 (Factura B): VAT contained in the price
            if (Invoice.IsFiscal && Invoice.Letter == 'B' && amounts != null)
            {
                column.Item().PaddingTop(3).Text("Régimen de Transparencia Fiscal al Consumidor (Ley 27.743)").FontSize(BaseSize - 1);
                Line(column, "IVA contenido", Money(amounts.Vat), BaseSize - 1);
                Line(column, "Otros imp. nacionales indirectos", Money(0), BaseSize - 1);
            }
        }

        private void Fiscal(ColumnDescriptor column)
        {
            if (!Invoice.IsFiscal)
            {
                if (Invoice.FiscalStatus == FiscalStatus.Pending)
                    column.Item().PaddingTop(4).AlignCenter().Text("Pendiente de autorización en ARCA").FontSize(BaseSize - 0.5f);
                return;
            }
            Separator(column);
            column.Item().AlignCenter().Text($"CAE: {Invoice.Cae}").Bold();
            column.Item().AlignCenter().Text($"Vto. CAE: {Invoice.CaeDueDate:dd/MM/yyyy}");
            column.Item().PaddingTop(3).AlignCenter().Width(Narrow ? 26 : 32, Unit.Millimetre)
                .Image(InvoiceDocumentService.QrPngFor(Invoice, 6));
            column.Item().AlignCenter().Text("Comprobante autorizado por ARCA").FontSize(BaseSize - 1);
        }

        private static void Line(ColumnDescriptor column, string label, string value, float? size = null)
        {
            column.Item().Row(row =>
            {
                var l = row.RelativeItem().Text(label);
                var v = row.AutoItem().AlignRight().Text(value);
                if (size is float s) { l.FontSize(s); v.FontSize(s); }
            });
        }

        private static void Separator(ColumnDescriptor column)
            => column.Item().PaddingVertical(2).LineHorizontal(0.6f).LineDashPattern(new[] { 2f, 2f }).LineColor(Colors.Black);
    }
}
