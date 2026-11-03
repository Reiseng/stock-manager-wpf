using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using StockControl.Dtos;
using StockControl.Enums;
using StockControl.Services;

namespace StockControl.Utils
{
    // Prints tickets straight to the printer chosen in Configuración > Tickets,
    // without a dialog. The ticket is drawn by QuestPDF (same layout as the
    // ticket PDF) as an image at 203 dpi, the resolution of thermal printers.
    public static class TicketPrinter
    {
        private const int ThermalDpi = 203;

        public static TicketConfig Config => ConfigService.Config.Ticket;
        public static bool IsConfigured => !string.IsNullOrWhiteSpace(Config.Printer);

        // Printers installed on this PC (local and network)
        public static List<string> InstalledPrinters()
        {
            try
            {
                using var server = new LocalPrintServer();
                return server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections })
                    .Select(q => q.FullName)
                    .OrderBy(n => n)
                    .ToList();
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Listar impresoras", ex);
                return new List<string>();
            }
        }

        public static void Print(InvoiceDto invoice)
        {
            if (!IsConfigured)
                throw new Exception("Elegí la impresora de tickets en Configuración > Tickets.");

            var width = Config.PaperWidthMm == 58 ? 58 : 80;
            var png = new TicketDocument(invoice, width)
                .GenerateImages(new ImageGenerationSettings { RasterDpi = ThermalDpi, ImageFormat = ImageFormat.Png })
                .First();

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = new MemoryStream(png);
            bitmap.EndInit();
            bitmap.Freeze();

            // WPF works in 1/96 inch units
            double ToDip(double mm) => mm / 25.4 * 96;
            var imageWidth = ToDip(TicketDocument.PrintableWidthMm(width));
            var imageHeight = imageWidth * bitmap.PixelHeight / bitmap.PixelWidth;
            var paperWidth = ToDip(width);

            var image = new System.Windows.Controls.Image
            {
                Source = bitmap,
                Width = imageWidth,
                Height = imageHeight,
                Stretch = Stretch.Fill,
                // centred on the paper; the printer's hardware margins are outside the printable width
                Margin = new Thickness(Math.Max(0, (paperWidth - imageWidth) / 2), 0, 0, 0),
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor); // crisp at 203 dpi
            var root = new Grid { Background = Brushes.White, Width = paperWidth, Height = imageHeight };
            root.Children.Add(image);
            root.Measure(new System.Windows.Size(paperWidth, imageHeight));
            root.Arrange(new Rect(0, 0, paperWidth, imageHeight));
            root.UpdateLayout();

            var queue = FindQueue(Config.Printer!)
                ?? throw new Exception($"No se encontró la impresora «{Config.Printer}». Revisá que esté encendida y conectada, o elegila de nuevo en Configuración > Tickets.");

            var dialog = new PrintDialog { PrintQueue = queue };
            var ticket = queue.UserPrintTicket ?? queue.DefaultPrintTicket;
            // Roll paper: as long as the ticket (drivers that support it cut right after the content)
            ticket.PageMediaSize = new PageMediaSize(paperWidth, imageHeight);
            ticket.PageOrientation = PageOrientation.Portrait;
            dialog.PrintTicket = ticket;
            dialog.PrintVisual(root, $"Ticket {invoice.FormattedNumber}");
        }

        // A sale-free ticket to check the printer and the paper width
        public static InvoiceDto Sample(Company company)
        {
            var items = new List<CheckoutItemDto>
            {
                new() { ProductName = "Producto de prueba", Unit = UnitType.Unit, Quantity = 2, UnitPrice = 1000m, Tax = 21m },
                new() { ProductName = "Otro producto (por kilo)", Unit = UnitType.Kg, Quantity = 0.5m, UnitPrice = 4000m, Tax = 10.5m },
            };
            var total = items.Sum(i => Math.Round(i.Total, 2));
            return new InvoiceDto("PRUEBA", DateTime.Now, InvoiceType.FinalConsumer, OperationType.Sale, company,
                new ClientDto { Name = "Consumidor", LastName = "Final" }, items, total);
        }

        private static PrintQueue? FindQueue(string name)
        {
            // The server is not disposed here: the queue returned still uses it
            var server = new LocalPrintServer();
            return server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections })
                .FirstOrDefault(q => string.Equals(q.FullName, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
