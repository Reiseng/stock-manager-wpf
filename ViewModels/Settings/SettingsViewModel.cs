using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StockControl.Services;
using System.Windows;
using StockControl.Enums;
using StockControl.Views.Windows;
using StockControl.Services.Database;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace StockControl.ViewModels.Settings
{
    public class SettingsViewModel : INotifyPropertyChanged
    {

        private readonly CompanyService _companyService;
        private readonly Company? _company;

        public Action? ShowSuccessAction { get; set; }
        public Array TaxConditions { get; }

        public RelayCommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public Action? CloseAction { get; set; }
        private bool _isSuccess;
        public bool IsSuccess
        {
            get => _isSuccess;
            set { _isSuccess = value; OnPropertyChanged(); }
        }

        public SettingsViewModel(CompanyService companyService)
        {
            _companyService = companyService;
            _company = companyService.GetCompanyInfo();
            Name = _company?.Name ?? "";
            CUIT = _company?.CUIT ?? "";
            SelectedTaxCondition = _company?.taxCondition ?? TaxCondition.FinalConsumer;
            Phone = _company?.Phone ?? "";
            Email = _company?.Email ?? "";
            Address = _company?.Address ?? "";
            TaxConditions = Enum.GetValues(typeof(TaxCondition));
            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke());

            ExportDatabaseCommand = new RelayCommand(_ => ExportDatabase());
            ImportDatabaseCommand = new RelayCommand(_ => ImportDatabase());
            OpenBackupFolderCommand = new RelayCommand(_ => OpenBackupFolder());
        }

        // ---- Apariencia ----
        public IReadOnlyList<ThemeOption> Themes => ThemeService.Themes;
        public ThemeOption SelectedTheme
        {
            get => ThemeService.Current;
            set
            {
                if (value == null || value == ThemeService.Current)
                    return;
                ThemeService.Apply(value.Id);
                OnPropertyChanged();
            }
        }

        // ---- Copias de seguridad ----
        public RelayCommand ExportDatabaseCommand { get; }
        public RelayCommand ImportDatabaseCommand { get; }
        public RelayCommand OpenBackupFolderCommand { get; }

        public string DatabasePath => AppServices.BackupService.DatabasePath;
        public string SchemaVersionText => $"Versión de la base de datos: {DatabaseInitializer.LatestVersion}";
        public string LastBackupText
        {
            get
            {
                var last = AppServices.BackupService.LastAutomaticBackup();
                return last.HasValue
                    ? $"Última copia automática: {last.Value:dd/MM/yyyy HH:mm} (se hace una por día, se guardan las últimas 30)"
                    : "Todavía no hay copias automáticas";
            }
        }

        private void ExportDatabase()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Exportar copia de la base de datos",
                FileName = BackupService.SuggestedExportFileName(),
                DefaultExt = ".db",
                Filter = "Copia de Stock Manager (*.db)|*.db"
            };
            if (dialog.ShowDialog() != true)
                return;
            try
            {
                AppServices.BackupService.ExportTo(dialog.FileName);
                MessageBox.Show($"Copia guardada en:\n{dialog.FileName}", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo exportar: {ex.Message}", "Exportar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportDatabase()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Importar copia de la base de datos",
                Filter = "Copia de Stock Manager (*.db)|*.db|Todos los archivos (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true)
                return;

            if (MessageBox.Show(
                    "Se van a REEMPLAZAR todos los datos actuales (productos, clientes, ventas, usuarios) " +
                    "por los de la copia seleccionada.\n\n" +
                    "Antes se guarda automáticamente una copia de los datos actuales en la carpeta de copias.\n\n" +
                    "El programa se va a reiniciar. ¿Continuar?",
                    "Importar copia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                AppServices.BackupService.ImportFrom(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo importar: {ex.Message}\n\nNo se modificó ningún dato.",
                    "Importar copia", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Users and data changed completely: restart from scratch
            AppServices.UserService.ForgetSavedSession();
            MessageBox.Show("Copia importada correctamente. El programa se va a reiniciar.",
                "Importar copia", MessageBoxButton.OK, MessageBoxImage.Information);
            var exe = Environment.ProcessPath;
            if (exe != null)
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            Application.Current.Shutdown();
        }

        private void OpenBackupFolder()
        {
            Directory.CreateDirectory(AppServices.BackupService.BackupFolder);
            Process.Start(new ProcessStartInfo(AppServices.BackupService.BackupFolder) { UseShellExecute = true });
        }

        private string? _name;
        public string? Name
        {
            get => _name;
            set { _name = value; 
                    OnPropertyChanged();
                }
        }

        private string? _cuit;
        public string? CUIT
        {
            get => _cuit;
            set { _cuit = value; OnPropertyChanged();}
        }

        private TaxCondition _selectedTaxCondition;
        public TaxCondition SelectedTaxCondition
        {
            get => _selectedTaxCondition;
            set { _selectedTaxCondition = value; OnPropertyChanged(); }
        }

        private string _phone;
        public string Phone
        {
            get => _phone;
            set { _phone = value; OnPropertyChanged(); }
        }

        private string _email;
        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        private string _address;
        public string Address
        {
            get => _address;
            set { _address = value; OnPropertyChanged(); }
        }



private void Save()
{
    try{
            _company.CUIT = CUIT;
            _company.Name = Name;
            _company.taxCondition = SelectedTaxCondition;
            _company.Phone = Phone;
            _company.Email = Email;
            _company.Address = Address;
            _companyService.SetCompanyInfo(_company);

            ShowSuccessAction = () =>
            {
                new SuccessWindow
                {
                    Owner = Application.Current.MainWindow,
                    Message = "La información de la empresa se ha guardado correctamente."
                }.Show();
            };
            ShowSuccessAction?.Invoke();
                    Task.Delay(1200).ContinueWith(_ =>
                        Application.Current.Dispatcher.Invoke(() =>
                            CloseAction?.Invoke()));
        }
    catch(Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
}

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Name)
                   && !string.IsNullOrWhiteSpace(CUIT);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
