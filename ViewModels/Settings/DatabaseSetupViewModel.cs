using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Data.Sqlite;
using Npgsql;
using StockControl.Config.DataBaseConfig;
using StockControl.Data.Ef;
using StockControl.Services.Database;
using StockControl.Utils;

namespace StockControl.ViewModels.Settings
{
    // Where the data lives: a file on this PC (SQLite) or a shared server (PostgreSQL).
    // Shown the first time the program runs, and from Configuración > Base de datos.
    public class DatabaseSetupViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseSettings _settings;

        // firstRun: no data yet, the program continues after saving.
        // Otherwise the program has to restart to use the new database.
        public DatabaseSetupViewModel(bool firstRun)
        {
            FirstRun = firstRun;
            _settings = ConfigService.Config.Database.Copy();
            _password = _settings.GetPassword();
            TestCommand = new RelayCommand(async _ => await TestAsync(), _ => !IsBusy && UsePostgres);
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => !IsBusy);
            CancelCommand = new RelayCommand(_ => Close?.Invoke(false));
        }

        public bool FirstRun { get; }
        public bool NotFirstRun => !FirstRun;
        public Action<bool>? Close { get; set; }
        public RelayCommand TestCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public string SaveText => FirstRun ? "Continuar" : "Guardar y reiniciar";

        public bool UseSqlite
        {
            get => _settings.Provider == DatabaseProvider.Sqlite;
            set { if (value) SetProvider(DatabaseProvider.Sqlite); }
        }
        public bool UsePostgres
        {
            get => _settings.Provider == DatabaseProvider.PostgreSql;
            set { if (value) SetProvider(DatabaseProvider.PostgreSql); }
        }
        private void SetProvider(DatabaseProvider provider)
        {
            _settings.Provider = provider;
            Result = "";
            OnPropertyChanged(nameof(UseSqlite));
            OnPropertyChanged(nameof(UsePostgres));
            OnPropertyChanged(nameof(CanCopyLocalData));
            TestCommand.RaiseCanExecuteChanged();
        }

        public string Host
        {
            get => _settings.Host;
            set { _settings.Host = (value ?? "").Trim(); OnPropertyChanged(); }
        }
        public string PortText
        {
            get => _settings.Port.ToString();
            set { if (int.TryParse(value, out var p) && p > 0 && p < 65536) _settings.Port = p; OnPropertyChanged(); }
        }
        public string DatabaseName
        {
            get => _settings.Name;
            set { _settings.Name = (value ?? "").Trim(); OnPropertyChanged(); }
        }
        public string Username
        {
            get => _settings.Username;
            set { _settings.Username = (value ?? "").Trim(); OnPropertyChanged(); }
        }
        private string _password;
        public string Password
        {
            get => _password;
            set { _password = value ?? ""; }
        }

        // Moving a store that already used the program on this PC to the server
        public bool HasLocalData => File.Exists(ConfigService.DatabaseFullPath);
        public bool CanCopyLocalData => UsePostgres && HasLocalData;
        private bool _copyLocalData = true;
        public bool CopyLocalData
        {
            get => _copyLocalData;
            set { _copyLocalData = value; OnPropertyChanged(); }
        }

        private string _result = "";
        public string Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged(); }
        }
        private bool _resultOk;
        public bool ResultOk
        {
            get => _resultOk;
            set { _resultOk = value; OnPropertyChanged(); }
        }
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                TestCommand.RaiseCanExecuteChanged();
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        private DatabaseSettings Current()
        {
            var s = _settings.Copy();
            s.SetPassword(_password);
            return s;
        }

        private async Task TestAsync()
        {
            IsBusy = true;
            try
            {
                var version = await Task.Run(() => Connect(Current()));
                Result = $"Conexión correcta. {version}";
                ResultOk = true;
            }
            catch (Exception ex)
            {
                Result = ex.Message;
                ResultOk = false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Opens the server; if the database doesn't exist yet, offers to create it
        private static string Connect(DatabaseSettings s)
        {
            if (string.IsNullOrWhiteSpace(s.Host) || string.IsNullOrWhiteSpace(s.Name) || string.IsNullOrWhiteSpace(s.Username))
                throw new Exception("Completá servidor, base de datos y usuario.");
            try
            {
                using var connection = new NpgsqlConnection(ConfigService.PostgresConnectionString(s));
                connection.Open();
                return ServerVersion(connection);
            }
            catch (PostgresException ex) when (ex.SqlState == "3D000") // database doesn't exist
            {
                var create = Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                    $"El servidor responde, pero la base «{s.Name}» todavía no existe.\n\n¿La creo?",
                    "Base de datos", MessageBoxButton.YesNo, MessageBoxImage.Question));
                if (create != MessageBoxResult.Yes)
                    throw new Exception($"La base «{s.Name}» no existe en el servidor.");
                if (!Regex.IsMatch(s.Name, "^[A-Za-z_][A-Za-z0-9_]{0,62}$"))
                    throw new Exception("El nombre de la base solo puede tener letras, números y guión bajo (sin espacios ni acentos).");

                using (var admin = new NpgsqlConnection(ConfigService.PostgresConnectionString(s, "postgres")))
                {
                    admin.Open();
                    using var command = admin.CreateCommand();
                    command.CommandText = $"CREATE DATABASE \"{s.Name}\" ENCODING 'UTF8';";
                    command.ExecuteNonQuery();
                }
                using var connection = new NpgsqlConnection(ConfigService.PostgresConnectionString(s));
                connection.Open();
                return "Base creada. " + ServerVersion(connection);
            }
            catch (PostgresException ex) when (ex.SqlState == "28P01" || ex.SqlState == "28000")
            {
                throw new Exception("Usuario o contraseña incorrectos.");
            }
            catch (NpgsqlException ex) when (ex.InnerException is System.Net.Sockets.SocketException || ex.InnerException is TimeoutException)
            {
                throw new Exception($"No se pudo llegar al servidor {s.Host}:{s.Port}. Revisá la dirección, que el servidor esté encendido y que el firewall permita la conexión.");
            }
        }

        private static string ServerVersion(NpgsqlConnection connection)
            => $"PostgreSQL {connection.PostgreSqlVersion}";

        private async Task SaveAsync()
        {
            var settings = Current();
            IsBusy = true;
            try
            {
                if (settings.Provider == DatabaseProvider.PostgreSql)
                {
                    Result = "Conectando...";
                    ResultOk = true;
                    await Task.Run(() => Connect(settings));

                    var server = ConfigService.CreateDatabaseContext(settings);
                    Result = "Preparando la base del servidor...";
                    await Task.Run(() => new DatabaseInitializer(server).Initialize());

                    if (CanCopyLocalData && CopyLocalData)
                    {
                        if (!DataTransfer.IsEmpty(server) && MessageBox.Show(
                                "El servidor ya tiene datos (productos o ventas).\n\n" +
                                "¿Reemplazarlos por los de esta PC? Lo que hay en el servidor se pierde.\n" +
                                "Elegí «No» para usar los datos del servidor como están.",
                                "Base de datos", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        {
                            // keep the server's data
                        }
                        else
                        {
                            Result = "Copiando los datos de esta PC al servidor...";
                            var summary = await Task.Run(() =>
                            {
                                var local = new DatabaseContext(ConfigService.SqliteConnectionString(ConfigService.DatabaseFullPath));
                                new DatabaseInitializer(local).Initialize(); // up to date before copying
                                var copied = DataTransfer.CopyAll(local, server);
                                SqliteConnection.ClearAllPools();
                                return copied;
                            });
                            MessageBox.Show($"Datos copiados al servidor: {summary.Products} productos, {summary.Clients} clientes, " +
                                            $"{summary.Users} usuarios y {summary.Checkouts} operaciones.\n\n" +
                                            "La base de esta PC queda como estaba, por las dudas.",
                                "Base de datos", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }

                ConfigService.Config.Database = settings;
                ConfigService.Save();
                Close?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Configurar base de datos", ex);
                Result = ex.Message;
                ResultOk = false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
