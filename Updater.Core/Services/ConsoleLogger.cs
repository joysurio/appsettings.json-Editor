namespace Updater.Core.Services;

public class ConsoleLogger : ILogger
{
    public void Log(LogLevel level, string message)
    {
        var prefix = level switch
        {
            LogLevel.Info => "[INFO]",
            LogLevel.Warn => "[WARN]",
            LogLevel.Error => "[ERROR]",
            _ => "[INFO]"
        };
        Console.WriteLine($"{prefix} {message}");
    }

    public void LogInfo(string message) => Log(LogLevel.Info, message);
    public void LogWarn(string message) => Log(LogLevel.Warn, message);
    public void LogError(string message) => Log(LogLevel.Error, message);
}

