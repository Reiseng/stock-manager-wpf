using StockControl.Dtos;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Fluent;
using StockControl.Enums;
using StockControl.Utils;
using StockControl.Services.Arca;
using System.IO;
using System.Text;
using System.Text.Json;
using QRCoder;

namespace StockControl.Services
{
    // PDF of a sale.
    // - With CAE: Argentine invoice layout (letter box, CAE, ARCA QR, transparency legend on B).
    // - Without CAE: clearly marked "no válido como factura".
    public class InvoiceDocumentService : IDocument
    {
        public InvoiceDto Invoice { get; set; }
        public InvoiceDocumentService(InvoiceDto invoice)
        {
            Invoice = invoice;
        }
        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
        public DocumentSettings GetSettings() => DocumentSettings.Default;

        private string Money(decimal value) => value.ToString("C2");

        public void Compose(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Margin(24);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);
                });
        }

        public void ComposeHeader(IContainer container)
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "assets", "logo.png");
            var seller = Invoice.seller;

            container.Border(1).BorderColor(Colors.Grey.Medium).Row(row =>
            {
                // Business
                row.RelativeItem().Padding(10).Column(column =>
                {
                    if (File.Exists(logoPath))
                        column.Item().Height(40).AlignLeft().Image(logoPath).FitHeight();
                    column.Item().Text(seller.Name ?? "").FontSize(14).Bold();
                    column.Item().Text($"CUIT: {seller.CUIT}");
                    column.Item().Text($"Condición frente al IVA: {seller.taxCondition.GetDescription()}");
                    if (!string.IsNullOrWhiteSpace(seller.Address)) column.Item().Text(seller.Address);
                    if (!string.IsNullOrWhiteSpace(seller.Phone)) column.Item().Text($"Tel.: {seller.Phone}");
                    if (!string.IsNullOrWhiteSpace(seller.Email)) column.Item().Text(seller.Email);
                });

                // Letter box (only for ARCA vouchers)
                row.ConstantItem(70).BorderLeft(1).BorderRight(1).BorderColor(Colors.Grey.Medium).Padding(6).Column(column =>
                {
                    if (Invoice.IsFiscal)
                    {
                        column.Item().AlignCenter().Text(Invoice.Letter.ToString()).FontSize(30).Bold();
                        column.Item().AlignCenter().Text($"Cód. {Invoice.VoucherType:D3}").FontSize(8);
                    }
                    else
                    {
                        column.Item().AlignCenter().Text("X").FontSize(30).Bold().FontColor(Colors.Grey.Medium);
                    }
                });

                // Voucher
                row.RelativeItem().Padding(10).Column(column =>
                {
                    column.Item().Text(Invoice.VoucherTitle).FontSize(15).Bold();
                    column.Item().Text($"N° {Invoice.FormattedNumber}").FontSize(12).SemiBold();
                    column.Item().Text($"Fecha: {Invoice.date:dd/MM/yyyy}");
                    if (!Invoice.IsFiscal)
                        column.Item().PaddingTop(4).Text("Documento no válido como factura").FontColor(Colors.Red.Darken2).SemiBold();
                    if (Invoice.IsFiscal && !Invoice.Production)
                        column.Item().PaddingTop(4).Text("HOMOLOGACIÓN: SIN VALIDEZ FISCAL").FontColor(Colors.Red.Darken2).Bold();
                });
            });
        }

        public void ComposeContent(IContainer container)
        {
            container.PaddingVertical(12).Column(column =>
            {
                column.Spacing(10);

                // Client
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(c =>
                {
                    var client = Invoice.client;
                    var finalConsumer = Invoice.DocType == 99;
                    c.Item().Text(finalConsumer ? "A CONSUMIDOR FINAL" : $"{client.Name} {client.LastName}".Trim()).SemiBold();
                    if (!finalConsumer)
                        c.Item().Text($"{(Invoice.DocType == 80 ? "CUIT" : "DNI")}: {client.Dni}");
                    c.Item().Text($"Condición frente al IVA: {client.TaxCondition.GetDescription()}");
                    if (!string.IsNullOrWhiteSpace(client.Address))
                        c.Item().Text($"Domicilio: {client.Address}");
                });

                column.Item().Element(ComposeTable);
                column.Item().AlignRight().Element(ComposeTotals);
            });
        }

        // Factura A shows prices without VAT (it's discriminated below);
        // B, C and internal vouchers show final prices.
        private bool NetPrices => Invoice.IsFiscal && Invoice.Letter == 'A';

        void ComposeTable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(1.6f);
                    if (NetPrices) columns.RelativeColumn(1);
                    columns.RelativeColumn(1.8f);
                });

                static IContainer Head(IContainer c) => c.DefaultTextStyle(x => x.SemiBold()).Background(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(4);
                static IContainer Cell(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(4);

                table.Header(header =>
                {
                    header.Cell().Element(Head).Text("Producto");
                    header.Cell().Element(Head).AlignRight().Text("Cantidad");
                    header.Cell().Element(Head).AlignRight().Text(NetPrices ? "Precio s/IVA" : "Precio");
                    if (NetPrices) header.Cell().Element(Head).AlignRight().Text("IVA");
                    header.Cell().Element(Head).AlignRight().Text(NetPrices ? "Subtotal s/IVA" : "Subtotal");
                });

                foreach (var item in Invoice.items)
                {
                    var unit = NetPrices ? item.UnitPrice : FiscalRules.Round(item.UnitPrice * (1 + item.Tax / 100));
                    var subtotal = NetPrices ? FiscalRules.Round(item.UnitPrice * item.Quantity) : FiscalRules.Round(item.Total);
                    table.Cell().Element(Cell).Text(item.ProductName);
                    table.Cell().Element(Cell).AlignRight().Text($"{item.Quantity:0.###}");
                    table.Cell().Element(Cell).AlignRight().Text(Money(unit));
                    if (NetPrices) table.Cell().Element(Cell).AlignRight().Text($"{item.Tax:0.##}%");
                    table.Cell().Element(Cell).AlignRight().Text(Money(subtotal));
                }
            });
        }

        void ComposeTotals(IContainer container)
        {
            container.Width(260).Column(column =>
            {
                var amounts = Invoice.Amounts;
                if (NetPrices && amounts != null)
                {
                    Line(column, "Importe neto gravado", Money(amounts.Net));
                    foreach (var vat in amounts.VatLines)
                        Line(column, $"IVA {vat.Rate:0.##}%", Money(vat.Vat));
                }
                column.Item().PaddingTop(4).BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text("Total").FontSize(13).Bold();
                    r.RelativeItem().AlignRight().Text(Money(Invoice.total)).FontSize(13).Bold();
                });

                // Ley 27.743: the VAT contained in the price, on its own line (Factura B)
                if (Invoice.IsFiscal && Invoice.Letter == 'B' && amounts != null)
                {
                    column.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(t =>
                    {
                        t.Item().Text("Régimen de Transparencia Fiscal al Consumidor (Ley 27.743)").FontSize(8).SemiBold();
                        t.Item().Row(r =>
                        {
                            r.RelativeItem().Text("IVA contenido").FontSize(8);
                            r.RelativeItem().AlignRight().Text(Money(amounts.Vat)).FontSize(8);
                        });
                        t.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Otros impuestos nacionales indirectos").FontSize(8);
                            r.RelativeItem().AlignRight().Text(Money(0)).FontSize(8);
                        });
                    });
                }
            });
        }

        private void Line(ColumnDescriptor column, string label, string value)
        {
            column.Item().Row(r =>
            {
                r.RelativeItem().Text(label);
                r.RelativeItem().AlignRight().Text(value);
            });
        }

        public void ComposeFooter(IContainer container)
        {
            if (!Invoice.IsFiscal)
            {
                container.AlignCenter().Text(Invoice.FiscalStatus == FiscalStatus.Pending
                    ? "Pendiente de autorización en ARCA. Documento no válido como factura."
                    : "Documento no válido como factura.").FontSize(9).FontColor(Colors.Grey.Darken1);
                return;
            }

            container.BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(8).Row(row =>
            {
                row.ConstantItem(90).Height(90).Image(QrPng());
                row.RelativeItem().PaddingLeft(12).AlignMiddle().Column(column =>
                {
                    column.Item().Text("Comprobante autorizado por ARCA").SemiBold();
                    column.Item().Text($"CAE N°: {Invoice.Cae}");
                    column.Item().Text($"Fecha de vencimiento del CAE: {Invoice.CaeDueDate:dd/MM/yyyy}");
                    if (!Invoice.Production)
                        column.Item().Text("Emitido en homologación (prueba): sin validez fiscal.").FontColor(Colors.Red.Darken2);
                });
            });
        }

        // QR required on printed ARCA vouchers (spec: arca.gob.ar/fe/qr)
        public string QrUrl()
        {
            var cuit = long.Parse(FiscalService.IssuerCuit(Invoice.seller));
            var data = new Dictionary<string, object>
            {
                ["ver"] = 1,
                ["fecha"] = Invoice.date.ToString("yyyy-MM-dd"),
                ["cuit"] = cuit,
                ["ptoVta"] = Invoice.PointOfSale,
                ["tipoCmp"] = Invoice.VoucherType,
                ["nroCmp"] = Invoice.VoucherNumber,
                ["importe"] = Invoice.total,
                ["moneda"] = "PES",
                ["ctz"] = 1,
                ["tipoDocRec"] = Invoice.DocType,
                ["nroDocRec"] = Invoice.DocNumber,
                ["tipoCodAut"] = "E",
                ["codAut"] = long.Parse(Invoice.Cae ?? "0"),
            };
            var json = JsonSerializer.Serialize(data);
            return "https://www.arca.gob.ar/fe/qr/?p=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        private byte[] QrPng()
        {
            using var generator = new QRCodeGenerator();
            using var qr = generator.CreateQrCode(QrUrl(), QRCodeGenerator.ECCLevel.M);
            return new PngByteQRCode(qr).GetGraphic(8);
        }
    }
}
