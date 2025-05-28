namespace Bing.Localization.Store;

/// <summary>
/// 数据存储本地化日志扩展
/// </summary>
internal static class StoreStringLocalizerLoggerExtensions
{
    /// <summary>
    /// 搜索操作
    /// </summary>
    private static readonly Action<ILogger, string, string, CultureInfo, Exception> _searched;

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static StoreStringLocalizerLoggerExtensions()
    {
        _searched = LoggerMessage.Define<string, string, CultureInfo>(
            LogLevel.Debug,
            1,
            $"{nameof(StoreStringLocalizer)} 查找名为 '{{Key}}'的本地化资源, 资源类型为 '{{Type}}',区域文化为 '{{Culture}}'.");
    }

    /// <summary>
    /// 输出搜索日志
    /// </summary>
    /// <param name="logger">日志</param>
    /// <param name="key">资源名称</param>
    /// <param name="type">资源类型</param>
    /// <param name="culture">区域文化</param>
    public static void Searched(this ILogger logger, string key, string type, CultureInfo culture) =>
        _searched(logger, key, type, culture, null);
}
