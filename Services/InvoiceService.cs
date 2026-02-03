using StockControl.Dtos;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Fluent;
using StockControl.Enums;

namespace StockControl.Services
{
    public class InvoiceDocumentService : IDocument
    {
        public InvoiceDto Invoice { get; set; }
        public InvoiceDocumentService(InvoiceDto invoice)
        {
            Invoice = invoice;
        }
        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
        public DocumentSettings GetSettings() => DocumentSettings.Default;
        public void Compose(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);

                    page.Header().Height(100).Background(Colors.Grey.Lighten1).Element(ComposeHeader);
                    page.Content().Background(Colors.Grey.Lighten3).Element(ComposeContent);
                    page.Footer().Height(50).Background(Colors.Grey.Lighten1).Element(ComposeFooter);
                });
        }
        public void ComposeHeader(IContainer container)
        {
            container.PaddingVertical(10).PaddingHorizontal(20).Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text($"Factura N°: {Invoice.invoiceNumber}").FontSize(16).Bold();
                    column.Item().Text($"Fecha: {Invoice.date:dd/MM/yyyy}").FontSize(12);
                });
                row.ConstantItem(100).Height(50).Placeholder();
            });
        }
        public void ComposeContent(IContainer container)
        {
             container.PaddingVertical(40).Column(column => 
                {
                    column.Spacing(5);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Component(new CompanyComponent ("De", Invoice.seller));
                        row.ConstantItem(50);
                        row.RelativeItem().Component(new ClientComponent("Para", Invoice.client));
                    });

                    column.Item().Element(ComposeTable);

                    var totalPrice = Invoice.items.Sum(x => x.UnitPrice * x.Quantity)*(1+(Invoice.tax/100));
                    column.Item().AlignRight().Text($"Total + IVA: {totalPrice}$").FontSize(14);
                });
        }
        public void ComposeFooter(IContainer container)
        {
            container.PaddingVertical(10).PaddingHorizontal(20).Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text($"Total: ${Invoice.total:F2}").FontSize(14).Bold();
                });
            });
        }
        void ComposeTable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                
                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("#");
                    header.Cell().Element(CellStyle).Text("Producto");
                    header.Cell().Element(CellStyle).AlignRight().Text("Precio Unit.");
                    header.Cell().Element(CellStyle).AlignRight().Text("Cant.");
                    header.Cell().Element(CellStyle).AlignRight().Text("Total");
                    
                    static IContainer CellStyle(IContainer container)
                    {
                        return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                    }
                });
                
                foreach (var item in Invoice.items)
                {
                    table.Cell().Element(CellStyle).Text((Invoice.items.IndexOf(item) + 1).ToString());
                    table.Cell().Element(CellStyle).Text(item.ProductName);
                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.UnitPrice}$");
                    table.Cell().Element(CellStyle).AlignRight().Text(item.Quantity.ToString());
                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.UnitPrice * item.Quantity}$");
                    
                    static IContainer CellStyle(IContainer container)
                    {
                        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                    }
                }
            });
        }
    }
}