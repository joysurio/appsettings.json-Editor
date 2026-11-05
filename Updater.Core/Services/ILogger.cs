namespace Updater.Core.Services;

public enum LogLevel
{
    Info,
    Warn,
    Error
}

public interface ILogger
{
    void Log(LogLevel level, string message);
    void LogInfo(string message);
    void LogWarn(string message);
    void LogError(string message);
}

