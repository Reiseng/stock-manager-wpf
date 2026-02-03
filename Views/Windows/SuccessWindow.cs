using System.Windows;

namespace StockControl.Views.Windows
{
    public partial class SuccessWindow : Window
    {
        public SuccessWindow()
        {
            InitializeComponent();
            this.Message = "Operación realizada con éxito.";
            Loaded += async (_, _) =>
            {
                await Task.Delay(600);
                Close();
            };
        }

        public string Message { get; set; }
    }
}