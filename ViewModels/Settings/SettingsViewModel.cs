using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StockControl.Services;
using System.Windows;
using StockControl.Enums;
using StockControl.Views.Windows;
using StockControl.Services.Database;
using StockControl.Services.Arca;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using StockControl.Utils;

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

            GenerateRequestCommand = new RelayCommand(_ => GenerateRequest());
            ImportCertificateCommand = new RelayCommand(_ => ImportCertificate());
            TestConnectionCommand = new RelayCommand(async _ => await TestConnectionAsync(), _ => !IsTesting);
            OpenArcaGuideCommand = new RelayCommand(_ => OpenUrl("https://github.com/Reiseng/stock-manager-wpf/blob/main/ARCA.md"));

            PrintTestTicketCommand = new RelayCommand(_ => PrintTestTicket(), _ => HasTicketPrinter);
            PreviewTicketCommand = new RelayCommand(_ => PreviewTicket());
            LoadLogoCommand = new RelayCommand(_ => LoadLogo());
            RemoveLogoCommand = new RelayCommand(_ => RemoveLogo(), _ => HasLogo);

            ExportDatabaseCommand = new RelayCommand(_ => ExportDatabase());
            ImportDatabaseCommand = new RelayCommand(_ => ImportDatabase());
            OpenBackupFolderCommand = new RelayCommand(_ => OpenBackupFolder());
        }

        // ---- Logo of the vouchers ----
        // Saved as PNG in the data folder (not next to the .exe: updates replace that folder)
        public RelayCommand LoadLogoCommand { get; }
        public RelayCommand RemoveLogoCommand { get; }
        public bool HasLogo => File.Exists(ConfigService.LogoPath);

        // Read into memory so the file is never locked (it can be replaced or deleted)
        public System.Windows.Media.ImageSource? LogoPreview
        {
            get
            {
                try
                {
                    if (!HasLogo) return null;
                    var image = new System.Windows.Media.Imaging.BitmapImage();
                    image.BeginInit();
                    image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    image.StreamSource = new MemoryStream(File.ReadAllBytes(ConfigService.LogoPath));
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private void LoadLogo()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Elegir el logo del negocio",
                Filter = "Imágenes (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
            };
            if (dialog.ShowDialog() != true)
                return;
            try
            {
                SaveLogoAsPng(dialog.FileName, ConfigService.LogoPath);
                LogoChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo usar esa imagen:\n{ex.Message}", "Logo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RemoveLogo()
        {
            if (MessageBox.Show("¿Quitar el logo de los comprobantes?", "Logo", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            try
            {
                File.Delete(ConfigService.LogoPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo quitar el logo:\n{ex.Message}", "Logo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            LogoChanged();
        }

        private void LogoChanged()
        {
            OnPropertyChanged(nameof(HasLogo));
            OnPropertyChanged(nameof(LogoPreview));
            RemoveLogoCommand.RaiseCanExecuteChanged();
        }

        // Any image -> PNG, at most 800x400 (keeps the PDFs light)
        private static void SaveLogoAsPng(string source, string target)
        {
            using var input = new MemoryStream(File.ReadAllBytes(source));
            var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(input,
                System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            System.Windows.Media.Imaging.BitmapSource frame = decoder.Frames[0];
            var scale = Math.Min(1.0, Math.Min(800.0 / frame.PixelWidth, 400.0 / frame.PixelHeight));
            if (scale < 1.0)
                frame = new System.Windows.Media.Imaging.TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(frame));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temp = target + ".tmp";
            using (var output = File.Create(temp))
                encoder.Save(output);
            File.Move(temp, target, overwrite: true);
        }

        // ---- Facturación electrónica (ARCA) ----
        private static ArcaConfig Arca => ConfigService.Config.Arca;

        public bool ArcaEnabled
        {
            get => Arca.Enabled;
            set { Arca.Enabled = value; ConfigService.Save(); OnPropertyChanged(); }
        }
        public bool ArcaProduction
        {
            get => Arca.Production;
            set
            {
                if (value && !Arca.Production && MessageBox.Show(
                        "En producción las facturas son REALES y se informan a ARCA.\n\n¿Pasar a producción?",
                        "Facturación electrónica", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ArcaHomologation));
                    return;
                }
                Arca.Production = value;
                ConfigService.Save();
                ConnectionResult = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ArcaHomologation));
                OnPropertyChanged(nameof(CertificateStatus));
            }
        }
        public bool ArcaHomologation
        {
            get => !Arca.Production;
            set { if (value) ArcaProduction = false; }
        }
        public string PointOfSaleText
        {
            get => Arca.PointOfSale.ToString();
            set
            {
                if (int.TryParse(value, out var n) && n > 0 && n < 100000)
                {
                    Arca.PointOfSale = n;
                    ConfigService.Save();
                }
                OnPropertyChanged();
            }
        }
        public bool InvoiceByDefault
        {
            get => Arca.InvoiceByDefault;
            set { Arca.InvoiceByDefault = value; ConfigService.Save(); OnPropertyChanged(); }
        }

        // Name of the certificate in ARCA ("alias")
        public string CertificateAlias { get; set; } = "stockmanager";
        // Only for importing a .pfx (set from the PasswordBox)
        public string? PfxPassword { get; set; }

        public string CertificateStatus
        {
            get
            {
                var store = AppServices.FiscalService.Certificates;
                var info = store.Info;
                var env = Arca.Production ? "producción" : "homologación";
                if (info != null)
                    return info.Expired
                        ? $"El certificado de {env} venció el {info.ValidUntil:dd/MM/yyyy}. Generá uno nuevo."
                        : $"Certificado de {env} cargado. Vence el {info.ValidUntil:dd/MM/yyyy}.";
                return store.HasPendingRequest
                    ? $"Pedido de {env} generado. Falta importar el certificado (.crt) que te da ARCA."
                    : $"Todavía no hay certificado de {env}.";
            }
        }

        private string? _connectionResult;
        public string? ConnectionResult
        {
            get => _connectionResult;
            set { _connectionResult = value; OnPropertyChanged(); }
        }
        private bool _isTesting;
        public bool IsTesting
        {
            get => _isTesting;
            set { _isTesting = value; OnPropertyChanged(); TestConnectionCommand.RaiseCanExecuteChanged(); }
        }

        public RelayCommand GenerateRequestCommand { get; }
        public RelayCommand ImportCertificateCommand { get; }
        public RelayCommand TestConnectionCommand { get; }
        public RelayCommand OpenArcaGuideCommand { get; }

        // Step 1: key + request (.csr) to upload to ARCA. No OpenSSL needed.
        private void GenerateRequest()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Guardar pedido de certificado",
                    FileName = $"{CertificateAlias}.csr",
                    DefaultExt = ".csr",
                    Filter = "Pedido de certificado (*.csr)|*.csr"
                };
                if (dialog.ShowDialog() != true)
                    return; // cancelled: nothing is generated
                var csr = AppServices.FiscalService.Certificates.CreateRequest(CUIT ?? "", Name ?? "", CertificateAlias);
                File.WriteAllText(dialog.FileName, csr);
                OnPropertyChanged(nameof(CertificateStatus));
                MessageBox.Show($"Pedido guardado en:\n{dialog.FileName}\n\nAhora subilo a ARCA con el alias \"{CertificateAlias}\" " +
                                "y descargá el certificado (.crt). Después usá \"Importar certificado\".",
                    "Facturación electrónica", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Facturación electrónica", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Step 2: the .crt from ARCA (pairs with the key of step 1) or an existing .pfx
        private void ImportCertificate()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Importar certificado de ARCA",
                Filter = "Certificado (*.crt;*.pem;*.cer;*.pfx;*.p12)|*.crt;*.pem;*.cer;*.pfx;*.p12|Todos los archivos (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true)
                return;
            try
            {
                var info = AppServices.FiscalService.Certificates.ImportCertificate(dialog.FileName, PfxPassword);
                OnPropertyChanged(nameof(CertificateStatus));
                MessageBox.Show($"Certificado cargado.\n{info.Subject}\nVence el {info.ValidUntil:dd/MM/yyyy}.\n\nProbá la conexión.",
                    "Facturación electrónica", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo importar: {ex.Message}", "Facturación electrónica", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task TestConnectionAsync()
        {
            IsTesting = true;
            ConnectionResult = "Probando...";
            try
            {
                ConnectionResult = await AppServices.FiscalService.TestConnectionAsync();
            }
            catch (Exception ex)
            {
                ConnectionResult = "Falló: " + ex.Message;
                StockControl.Utils.ErrorLog.Write("Probar conexión ARCA", ex);
            }
            finally
            {
                IsTesting = false;
            }
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception) { }
        }

        // ---- Tickets (per PC) ----
        private static TicketConfig TicketCfg => ConfigService.Config.Ticket;
        public List<string> Printers { get; } = TicketPrinter.InstalledPrinters();
        public RelayCommand PrintTestTicketCommand { get; }
        public RelayCommand PreviewTicketCommand { get; }

        public string? TicketPrinterName
        {
            get => TicketCfg.Printer;
            set
            {
                TicketCfg.Printer = string.IsNullOrWhiteSpace(value) ? null : value;
                ConfigService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasTicketPrinter));
                PrintTestTicketCommand.RaiseCanExecuteChanged();
            }
        }
        public bool HasTicketPrinter => !string.IsNullOrWhiteSpace(TicketCfg.Printer);
        public bool NoPrinters => Printers.Count == 0;

        public bool Paper80
        {
            get => TicketCfg.PaperWidthMm != 58;
            set { if (value) SetPaper(80); }
        }
        public bool Paper58
        {
            get => TicketCfg.PaperWidthMm == 58;
            set { if (value) SetPaper(58); }
        }
        private void SetPaper(int mm)
        {
            TicketCfg.PaperWidthMm = mm;
            ConfigService.Save();
            OnPropertyChanged(nameof(Paper80));
            OnPropertyChanged(nameof(Paper58));
        }

        public bool TicketAutoPrint
        {
            get => TicketCfg.AutoPrint;
            set { TicketCfg.AutoPrint = value; ConfigService.Save(); OnPropertyChanged(); }
        }

        private void PrintTestTicket()
        {
            try
            {
                TicketPrinter.Print(TicketPrinter.Sample(_companyService.GetCompanyInfo()));
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Ticket de prueba", ex);
                MessageBox.Show($"No se pudo imprimir:\n{ex.Message}", "Ticket de prueba", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PreviewTicket()
        {
            try
            {
                new TicketDocument(TicketPrinter.Sample(_companyService.GetCompanyInfo()), TicketCfg.PaperWidthMm)
                    .GeneratePdfAndShow("Ticket de prueba");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo generar:\n{ex.Message}", "Ticket de prueba", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
