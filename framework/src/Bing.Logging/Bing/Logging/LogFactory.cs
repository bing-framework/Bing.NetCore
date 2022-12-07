namespace Bing.Logging;

/// <summary>
/// 日志操作工厂
/// </summary>
public class LogFactory : ILogFactory
{
    /// <summary>
    /// 日志记录器工厂
    /// </summary>
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// 日志上下文访问器
    /// </summary>
    private readonly ILogContextAccessor _logContextAccessor;

    /// <summary>
    /// 初始化一个<see cref="LogFactory"/>类型的实例
    /// </summary>
    /// <param name="factory">日志记录器工厂</param>
    /// <param name="logContextAccessor">日志上下文访问器</param>
    /// <exception cref="ArgumentNullException"></exception>
    public LogFactory(ILoggerFactory factory, ILogContextAccessor logContextAccessor)
    {
        _loggerFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logContextAccessor = logContextAccessor;
    }

    /// <summary>
    /// 创建日志操作
    /// </summary>
    /// <param name="categoryName">日志类别</param>
    public ILog CreateLog(string categoryName)
    {
        var logger = _loggerFactory.CreateLogger(categoryName);
        var wrapper = new LoggerWrapper(logger);
        return new Log(wrapper, _logContextAccessor);
    }

    /// <summary>
    /// 创建日志操作
    /// </summary>
    /// <param name="type">日志类别类型</param>
    public ILog CreateLog(Type type)
    {
        var logger = _loggerFactory.CreateLogger(type);
        var wrapper = new LoggerWrapper(logger);
        return new Log(wrapper, _logContextAccessor);
    }
}
