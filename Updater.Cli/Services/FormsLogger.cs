using Updater.Core.Services;

namespace Updater.Cli.Services;

public class FormsLogger : ILogger
{
    private readonly TextBox _logTextBox;
    private readonly Action<string> _appendLogAction;

    public FormsLogger(TextBox logTextBox)
    {
        _logTextBox = logTextBox;
        _appendLogAction = AppendLog;
    }

    public void Log(LogLevel level, string message)
    {
        var prefix = level switch
        {
            LogLevel.Info => "[INFO]",
            LogLevel.Warn => "[WARN]",
            LogLevel.Error => "[ERROR]",
            _ => "[INFO]"
        };
        
        var logMessage = $"{prefix} {message}";
        
        if (_logTextBox.InvokeRequired)
        {
            _logTextBox.Invoke(_appendLogAction, logMessage);
        }
        else
        {
            AppendLog(logMessage);
        }
    }

    private void AppendLog(string message)
    {
        _logTextBox.AppendText($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}");
        _logTextBox.SelectionStart = _logTextBox.Text.Length;
        _logTextBox.ScrollToCaret();
    }

    public void LogInfo(string message) => Log(LogLevel.Info, message);
    public void LogWarn(string message) => Log(LogLevel.Warn, message);
    public void LogError(string message) => Log(LogLevel.Error, message);
}

