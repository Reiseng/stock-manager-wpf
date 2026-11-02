using System.ComponentModel;
using System.Runtime.CompilerServices;
using StockControl.Services;
using Velopack;

namespace StockControl.ViewModels
{
    public class UpdatePromptViewModel : INotifyPropertyChanged
    {
        private readonly UpdateService _service;
        private readonly UpdateInfo _update;

        public Action? Close { get; set; }

        public UpdatePromptViewModel(UpdateService service, UpdateInfo update)
        {
            _service = service;
            _update = update;
            UpdateNowCommand = new RelayCommand(async _ => await UpdateNow(), _ => !IsBusy);
            UpdateOnExitCommand = new RelayCommand(_ => UpdateOnExit(), _ => !IsBusy);
            LaterCommand = new RelayCommand(_ => Close?.Invoke(), _ => !IsBusy);
        }

        public string NewVersionText => $"Versión {_update.TargetFullRelease.Version}";
        public string CurrentVersionText => $"Ahora tenés la {UpdateService.CurrentVersionText}.";
        public string? ReleaseNotes => string.IsNullOrWhiteSpace(_update.TargetFullRelease.NotesMarkdown)
            ? null
            : _update.TargetFullRelease.NotesMarkdown.Trim();

        public RelayCommand UpdateNowCommand { get; }
        public RelayCommand UpdateOnExitCommand { get; }
        public RelayCommand LaterCommand { get; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                _isBusy = value;
                OnPropertyChanged();
                UpdateNowCommand.RaiseCanExecuteChanged();
                UpdateOnExitCommand.RaiseCanExecuteChanged();
                LaterCommand.RaiseCanExecuteChanged();
            }
        }

        private int _progress;
        public int Progress
        {
            get => _progress;
            private set { _progress = value; OnPropertyChanged(); }
        }

        private string? _status;
        public string? Status
        {
            get => _status;
            private set { _status = value; OnPropertyChanged(); }
        }

        private async Task UpdateNow()
        {
            IsBusy = true;
            Status = "Descargando la actualización...";
            try
            {
                await _service.DownloadAsync(_update, p => System.Windows.Application.Current.Dispatcher.Invoke(() => Progress = p));
                Status = "Instalando. El programa se va a abrir de nuevo en unos segundos.";
                _service.ApplyAndRestart(_update);
            }
            catch (Exception)
            {
                Status = "No se pudo descargar (¿hay internet?). Podés seguir usando el programa y probar más tarde.";
                IsBusy = false;
            }
        }

        // Downloads in the background while the program is used; it is
        // installed when the program is closed.
        private void UpdateOnExit()
        {
            _ = _service.DownloadInBackgroundForExit(_update);
            Close?.Invoke();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
