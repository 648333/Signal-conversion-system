namespace DH.Core.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}

public interface ILogService
{
    void Debug(string message);
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? ex = null);
    void Fatal(string message, Exception? ex = null);
}
