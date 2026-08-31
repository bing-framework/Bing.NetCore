namespace Bing.Logging;

/// <summary>
/// 使用 <see cref="ILoggerFactory"/> 创建日志操作对象的默认工厂。
/// </summary>
public class LogFactory : ILogFactory
{
    /// <summary>
    /// 用于按类别创建底层日志记录器的工厂。
    /// </summary>
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// 用于关联当前日志上下文的访问器。
    /// </summary>
    private readonly ILogContextAccessor _logContextAccessor;

    /// <summary>
    /// 使用日志记录器工厂和日志上下文访问器初始化 <see cref="LogFactory"/> 的实例。
    /// </summary>
    /// <param name="factory">按类别创建底层日志记录器的工厂。</param>
    /// <param name="logContextAccessor">关联当前日志上下文的访问器。</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> 为 <c>null</c> 时抛出。</exception>
    public LogFactory(ILoggerFactory factory, ILogContextAccessor logContextAccessor)
    {
        _loggerFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logContextAccessor = logContextAccessor;
    }

    /// <inheritdoc />
    public ILog CreateLog(string categoryName)
    {
        var logger = _loggerFactory.CreateLogger(categoryName);
        var wrapper = new LoggerWrapper(logger);
        return new Log(wrapper, _logContextAccessor);
    }

    /// <inheritdoc />
    public ILog CreateLog(Type type)
    {
        var logger = _loggerFactory.CreateLogger(type);
        var wrapper = new LoggerWrapper(logger);
        return new Log(wrapper, _logContextAccessor);
    }
}
