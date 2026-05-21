using XUnity.Common.Logging;
using System.IO;
using System.Threading;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public static class Logger
{
    private static StreamWriter _logger = null;
    private static LogLevel _minLevel = LogLevel.Debug;
    private static readonly object _lockObject = new object();

    public static void SetLogLevel(LogLevel level)
    {
        _minLevel = level;
    }

    public static LogLevel GetLogLevel()
    {
        return _minLevel;
    }

    private static void InitLogger()
    {
        lock (_lockObject)
        {
            if (_logger == null)
            {
                try
                {
                    string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    var logfile = Path.Combine(appDirectory, "AutoLLM.log");
                    _logger = new StreamWriter(logfile, true)
                    {
                        AutoFlush = true
                    };
                }
                catch (Exception ex)
                {
                    XuaLogger.AutoTranslator.Error($"[LLMT] Failed to initialize logger: {ex.Message}");
                }
            }
        }
    }

    public static void CloseLogger()
    {
        lock (_lockObject)
        {
            if (_logger != null)
            {
                try
                {
                    _logger.Close();
                    _logger.Dispose();
                }
                catch (Exception ex)
                {
                    XuaLogger.AutoTranslator.Error($"[LLMT] Failed to close logger: {ex.Message}");
                }
                finally
                {
                    _logger = null;
                }
            }
        }
    }

    public static void Debug(string source, string message)
    {
        Write(LogLevel.Debug, source, message);
    }

    public static void Info(string source, string message)
    {
        Write(LogLevel.Info, source, message);
    }

    public static void Warning(string source, string message)
    {
        Write(LogLevel.Warning, source, message);
    }

    public static void Error(string source, string message)
    {
        Write(LogLevel.Error, source, message);
    }

    public static void Error(string source, Exception ex, string message)
    {
        Write(LogLevel.Error, source, $"{message} | {ex.GetType().Name}: {ex.Message}");
    }

    private static void Write(LogLevel level, string source, string message)
    {
        if (level < _minLevel)
            return;

        InitLogger();

        string levelStr = level.ToString().ToUpperInvariant().PadRight(7);
        string formatted = $"[{DateTime.Now:HH:mm:ss}] [{levelStr}] [{source}] {message}";

        // Always log to XUnity logger
        switch (level)
        {
            case LogLevel.Debug:
                XuaLogger.AutoTranslator.Debug($"[LLMT] {formatted}");
                break;
            case LogLevel.Info:
                XuaLogger.AutoTranslator.Info($"[LLMT] {formatted}");
                break;
            case LogLevel.Warning:
                XuaLogger.AutoTranslator.Warn($"[LLMT] {formatted}");
                break;
            case LogLevel.Error:
                XuaLogger.AutoTranslator.Error($"[LLMT] {formatted}");
                break;
        }

        // Log to file
        lock (_lockObject)
        {
            if (_logger != null)
            {
                try
                {
                    _logger.WriteLine(formatted);
                }
                catch (Exception ex)
                {
                    XuaLogger.AutoTranslator.Error($"[LLMT] Failed to write to log file: {ex.Message}");
                }
            }
        }
    }
}
