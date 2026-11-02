using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace StockControl.Services
{
    // Updates are published as GitHub Releases (see build/publicar.ps1 and
    // .github/workflows/release.yml). The program checks them on startup.
    //
    // Rules:
    //  - Without internet (or if GitHub is slow/down) the program keeps working
    //    normally: every failure here is silent.
    //  - Running from Visual Studio (not installed) there is nothing to update.
    public class UpdateService
    {
        public const string RepositoryUrl = "https://github.com/Reiseng/stock-manager-wpf";
        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(10);

        private readonly UpdateManager? _manager;

        public UpdateService()
        {
            try
            {
                _manager = new UpdateManager(new GithubSource(RepositoryUrl, null, false));
            }
            catch (Exception)
            {
                _manager = null;
            }
        }

        public bool IsInstalled => _manager?.IsInstalled == true;

        // Version shown in the UI: the installed one, or the one of the build
        public static string CurrentVersionText
        {
            get
            {
                var version = Assembly.GetEntryAssembly()?.GetName().Version;
                return version == null ? "" : $"v{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        // Returns null when there is nothing to do or the check couldn't be made
        public async Task<UpdateInfo?> CheckAsync()
        {
            if (_manager == null || !_manager.IsInstalled)
                return null;
            try
            {
                var check = _manager.CheckForUpdatesAsync();
                var finished = await Task.WhenAny(check, Task.Delay(CheckTimeout));
                return finished == check ? await check : null;
            }
            catch (Exception)
            {
                return null; // offline, GitHub down, rate limit...
            }
        }

        public Task DownloadAsync(UpdateInfo update, Action<int> progress)
            => _manager!.DownloadUpdatesAsync(update, progress);

        // Closes the program, installs and opens the new version
        public void ApplyAndRestart(UpdateInfo update)
            => _manager!.ApplyUpdatesAndRestart(update.TargetFullRelease);

        // ---- "Install when the program is closed" ----
        private UpdateInfo? _readyForExit;

        public async Task DownloadInBackgroundForExit(UpdateInfo update)
        {
            try
            {
                await _manager!.DownloadUpdatesAsync(update);
                _readyForExit = update;
            }
            catch (Exception)
            {
                // Lost connection: it will be offered again next time
            }
        }

        // Called from App.OnExit. The updater waits for this process to end,
        // installs silently and does NOT reopen the program; the next time it
        // is opened it is already the new version.
        public void ApplyPendingOnExit()
        {
            if (_readyForExit == null || _manager == null)
                return;
            try
            {
                _manager.WaitExitThenApplyUpdates(_readyForExit.TargetFullRelease, true, false);
            }
            catch (Exception)
            {
            }
        }
    }
}
