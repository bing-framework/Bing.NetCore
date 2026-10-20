using Exceptionless.Logging;
using Serilog.Debugging;

namespace Serilog.Sinks.Exceptionless;

/// <summary>
/// 安全日志记录器
/// </summary>
public class SelfLogLogger : IExceptionlessLog
{
    /// <inheritdoc />
    public void Error(string message, string source = null, Exception exception = null) => SelfLog.WriteLine("Error: {0}, source: {1}, Exception: {2}", message, source, exception);

    /// <inheritdoc />
    public void Info(string message, string source = null) => SelfLog.WriteLine("Info: {0}, source: {1}", message, source);

    /// <inheritdoc />
    public void Debug(string message, string source = null) => SelfLog.WriteLine("Debug: {0}, source: {1}", message, source);

    /// <inheritdoc />
    public void Warn(string message, string source = null) => SelfLog.WriteLine("Warn: {0}, source: {1}", message, source);

    /// <inheritdoc />
    public void Trace(string message, string source = null) => SelfLog.WriteLine("Trace: {0}, source: {1}", message, source);

    /// <inheritdoc />
    public void Flush() { }

    /// <inheritdoc />
    public LogLevel MinimumLogLevel { get; set; }
}
