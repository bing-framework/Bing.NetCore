namespace Bing.Logging;

/// <summary>
/// 日志操作(<see cref="ILog{TCategoryName}"/>) 扩展
/// </summary>
public static class ILogExtensions
{
    /// <summary>
    /// 添加消息
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="message">消息</param>
    /// <param name="args">日志消息参数</param>
    public static ILog Append(this ILog log, string message, params object[] args)
    {
        if (log is null)
            throw new ArgumentNullException(nameof(log));
        log.Message(message, args);
        return log;
    }

    /// <summary>
    /// 添加消息，当条件为true时
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="message">消息</param>
    /// <param name="condition">条件，值为true时，则添加消息</param>
    /// <param name="args">日志消息参数</param>
    public static ILog AppendIf(this ILog log, string message, bool condition, params object[] args)
    {
        if (log is null)
            throw new ArgumentNullException(nameof(log));
        if (condition)
            log.Message(message, args);
        return log;
    }

    /// <summary>
    /// 添加消息并换行
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="message">消息</param>
    /// <param name="args">日志消息参数</param>
    public static ILog AppendLine(this ILog log, string message, params object[] args)
    {
        if (log is null)
            throw new ArgumentNullException(nameof(log));
        log.Message(message, args);
        log.Message(Environment.NewLine);
        return log;
    }

    /// <summary>
    /// 添加消息并换行，当条件为true时
    /// </summary>
    /// <param name="log">日志操作</param>
    /// <param name="message">消息</param>
    /// <param name="condition">条件，值为true时，则添加消息</param>
    /// <param name="args">日志消息参数</param>
    /// <returns></returns>
    public static ILog AppendLineIf(this ILog log, string message, bool condition, params object[] args)
    {
        if (log is null)
            throw new ArgumentNullException(nameof(log));
        if (condition)
        {
            log.Message(message, args);
            log.Message(Environment.NewLine);
        }
        return log;
    }

    /// <summary>
    /// 消息换行
    /// </summary>
    /// <param name="log">日志操作</param>
    public static ILog Line(this ILog log)
    {
        if (log is null)
            throw new ArgumentNullException(nameof(log));
        log.Message(Environment.NewLine);
        return log;
    }

    /// <summary>
    /// 设置扩展属性
    /// </summary>
    /// <param name="log">日志</param>
    /// <param name="propertyName">属性名</param>
    /// <param name="propertyValue">属性值</param>
    public static ILog ExtraProperty(this ILog log, string propertyName, object propertyValue) =>
        log.Set(x => x.Context.SetExtraProperty(propertyName, propertyValue));

    /// <summary>
    /// 设置扩展属性，当条件为true时
    /// </summary>
    /// <param name="log">日志</param>
    /// <param name="propertyName">属性名</param>
    /// <param name="propertyValue">属性值</param>
    /// <param name="condition">条件，值为true时，则添加扩展属性</param>
    public static ILog ExtraPropertyIf(this ILog log, string propertyName, object propertyValue, bool condition) =>
        !condition ? log : log.Set(x => x.Context.SetExtraProperty(propertyName, propertyValue));

    /// <summary>
    /// 设置标签列表
    /// </summary>
    /// <param name="log">日志</param>
    /// <param name="tags">标签列表</param>
    public static ILog Tags(this ILog log, params string[] tags) => log.Set(x => x.Context.SetTags(tags));

    /// <summary>
    /// 设置标签列表，当条件为true时
    /// </summary>
    /// <param name="log">日志</param>
    /// <param name="condition">条件，值为true时，则添加标签列表</param>
    /// <param name="tags">标签列表</param>
    public static ILog TagsIf(this ILog log, bool condition, params string[] tags) =>
        !condition ? log : log.Set(x => x.Context.SetTags(tags));

    /// <summary>
    /// 设置标签
    /// </summary>
    /// <param name="log">日志</param>
    /// <param name="tag">标签</param>
    public static ILog Tag(this ILog log, string tag) => log.Set(x => x.Context.SetTags(tag));

    /// <summary>
    /// 设置标签，当条件为true时
    /// </summary>
    /// <param name="log">日志</param>
    /// <param name="tag">标签</param>
    /// <param name="condition">条件，值为true时，则添加标签</param>
    public static ILog TagIf(this ILog log, string tag, bool condition) =>
        !condition ? log : log.Set(x => x.Context.SetTags(tag));
}
