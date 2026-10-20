using Bing.Helpers;

namespace Bing.Logging.Tests;

/// <summary>
/// 日志上下文访问器
/// </summary>
public class LogContextAccessor : ILogContextAccessor
{
    /// <summary>
    /// 在线程命名数据槽中保存日志上下文的键，值为 <c>Bing.Logging.LogContext</c>。
    /// </summary>
    public const string LogContextKey = "Bing.Logging.LogContext";

    /// <inheritdoc />
    public LogContext Context
    {
        get
        {
            var slot = System.Threading.Thread.GetNamedDataSlot(LogContextKey);
            return Conv.To<LogContext>(System.Threading.Thread.GetData(slot));

        }
        set
        {
            var slot = System.Threading.Thread.GetNamedDataSlot(LogContextKey);
            System.Threading.Thread.SetData(slot, value);
        }
    }
}
