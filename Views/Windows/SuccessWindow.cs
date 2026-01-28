using System.Windows;

namespace StockControl.Views.Windows
{
    public partial class SuccessWindow : Window
    {
        public SuccessWindow()
        {
            InitializeComponent();

            Loaded += async (_, _) =>
            {
                await Task.Delay(600);
                Close();
            };
        }
    }
}