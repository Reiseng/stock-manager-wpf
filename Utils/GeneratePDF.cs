
using System.Diagnostics;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace StockControl.Utils
{
    public static class GeneratePDF
    {
        public static void GeneratePdfAndShow(
            this IDocument document,
            string fileName
        )
        {
            var safeFileName = string.Concat(
                fileName.Split(Path.GetInvalidFileNameChars())
            );

            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Facturas"
            );

            Directory.CreateDirectory(basePath);

            var filePath = Path.Combine(basePath, $"{safeFileName}.pdf");

            document.GeneratePdf(filePath);
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
    }
}