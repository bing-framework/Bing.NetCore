namespace Bing.Logging;

/// <summary>
/// 配置 Bing 日志管道启动行为和扩展注册。
/// </summary>
public class BingLoggingOptions
{
    /// <summary>
    /// 初始化 <see cref="BingLoggingOptions"/> 的实例。
    /// </summary>
    public BingLoggingOptions()
    {
        ClearProviders = false;
        Extensions = new List<IBingLoggingOptionsExtension>();
    }

    /// <summary>
    /// 获取框架内部已注册的日志选项扩展列表，默认初始化为空列表；该集合用于后续配置阶段依次执行扩展。
    /// </summary>
    internal IList<IBingLoggingOptionsExtension> Extensions { get; }

    /// <summary>
    /// 获取或设置是否清除当前已注册的日志提供程序，默认值为 <c>false</c>。
    /// </summary>
    /// <remarks>设置为 <c>true</c> 时，日志管道配置会清除在此之前注册的 <c>ILoggerProvider</c>。</remarks>
    public bool ClearProviders { get; set; }

    /// <summary>
    /// 注册日志选项扩展。
    /// </summary>
    /// <param name="extension">要追加的日志选项扩展。</param>
    /// <exception cref="ArgumentNullException"><paramref name="extension"/> 为 <c>null</c> 时抛出。</exception>
    public void RegisterExtension(IBingLoggingOptionsExtension extension)
    {
        if (extension == null)
            throw new ArgumentNullException(nameof(extension));
        Extensions.Add(extension);
    }
}
