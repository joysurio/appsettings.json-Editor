using System.Diagnostics;
using AppSettingsEditor.Models;

namespace AppSettingsEditor.Services;

internal sealed class OpenExeWatchdogService : IDisposable
{
    private readonly ResidentOpenExeConfigService _configService = new();
    private readonly object _sync = new();
    private System.Threading.Timer? _timer;
    private bool _isRunning;
    private bool _isChecking;

    // Same default used by ResidentOpenExeControl.
    public static string DefaultConfigPath =>
        Path.Combine(@"C:\La Fabrica de Software", "ResidentOpenExe", "Config.xml");

    public bool IsRunning
    {
        get
        {
            lock (_sync) return _isRunning;
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_isRunning)
                return;

            _timer = new System.Threading.Timer(Tick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
            _isRunning = true;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            _isRunning = false;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Tick(object? state)
    {
        lock (_sync)
        {
            if (!_isRunning || _isChecking)
                return;

            _isChecking = true;
        }

        try
        {
            EnsureConfiguredProcessesRunning(DefaultConfigPath);
        }
        catch
        {
            // Watchdog must not crash the editor.
        }
        finally
        {
            lock (_sync)
            {
                _isChecking = false;
            }
        }
    }

    private void EnsureConfiguredProcessesRunning(string configPath)
    {
        if (!File.Exists(configPath))
            return;

        var cfg = _configService.Load(configPath);
        if (cfg.RunOnceThenExit)
            return; // configuration explicitly disables watchdog loop

        EnsureOneRunning(cfg.ExecutableDirectory, cfg.ExecutableFileName, cfg.OpenAsAdministrator);

        if (!string.IsNullOrWhiteSpace(cfg.Executable2Directory) &&
            !string.IsNullOrWhiteSpace(cfg.Executable2FileName))
        {
            EnsureOneRunning(cfg.Executable2Directory!, cfg.Executable2FileName!, cfg.OpenAsAdministrator);
        }

        // Update the timer period from config when possible.
        var sec = Math.Clamp(cfg.CheckIntervalSeconds, 1, 3600);
        lock (_sync)
        {
            _timer?.Change(TimeSpan.FromSeconds(sec), TimeSpan.FromSeconds(sec));
        }
    }

    private static void EnsureOneRunning(string executableDirectory, string executableFileName, bool runAsAdmin)
    {
        if (string.IsNullOrWhiteSpace(executableDirectory) || string.IsNullOrWhiteSpace(executableFileName))
            return;

        var fullPath = Path.Combine(executableDirectory.Trim(), executableFileName.Trim());
        if (!File.Exists(fullPath))
            return;

        if (IsAlreadyRunning(fullPath))
            return;

        var start = new ProcessStartInfo
        {
            FileName = fullPath,
            WorkingDirectory = Path.GetDirectoryName(fullPath) ?? executableDirectory,
            UseShellExecute = true
        };
        if (runAsAdmin)
            start.Verb = "runas";

        Process.Start(start);
    }

    private static bool IsAlreadyRunning(string fullPath)
    {
        var processName = Path.GetFileNameWithoutExtension(fullPath);
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0)
            return false;

        // Prefer exact path match. If we cannot inspect a process path due to permissions,
        // treat same-name as running to avoid duplicate launches.
        foreach (var p in processes)
        {
            try
            {
                var pPath = p.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(pPath) &&
                    string.Equals(Path.GetFullPath(pPath), Path.GetFullPath(fullPath), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                return true;
            }
            finally
            {
                p.Dispose();
            }
        }

        return false;
    }

    public void Dispose()
    {
        Stop();
    }
}

