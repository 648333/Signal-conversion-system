using System.IO;
using System.Text;

namespace DH.Core.Logging;

/// <summary>
/// 日志服务实现：文件+控制台双输出，按日期滚动
/// </summary>
public sealed class LogService : ILogService
{
    private readonly string _logDir;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private string _currentDate = string.Empty;

    public LogService(string logDir)
    {
        _logDir = logDir;
        if (!Directory.Exists(_logDir))
            Directory.CreateDirectory(_logDir);
    }

    private StreamWriter GetWriter()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_writer == null || _currentDate != today)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _currentDate = today;
            var path = Path.Combine(_logDir, $"DH-RTDAS_{today}.log");
            _writer = new StreamWriter(path, true, Encoding.UTF8) { AutoFlush = true };
        }
        return _writer;
    }

    public void Debug(string message) => Write(LogLevel.Debug, message);
    public void Info(string message) => Write(LogLevel.Info, message);
    public void Warning(string message) => Write(LogLevel.Warning, message);

    public void Error(string message, Exception? ex = null)
    {
        Write(LogLevel.Error, ex != null ? $"{message}\n{ex}" : message);
    }

    public void Fatal(string message, Exception? ex = null)
    {
        Write(LogLevel.Fatal, ex != null ? $"{message}\n{ex}" : message);
    }

    private void Write(LogLevel level, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var line = $"[{timestamp}] [{level,-7}] {message}";

        lock (_lock)
        {
            GetWriter().WriteLine(line);
#if DEBUG
            System.Diagnostics.Debug.WriteLine(line);
#endif
        }
    }

    public void Shutdown()
    {
        lock (_lock)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }
}
