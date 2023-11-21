using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Bing.Localization.Json;

/// <summary>
/// Json本地化日志扩展
/// </summary>
internal static class JsonStringLocalizerLoggerExtensions
{
    /// <summary>
    /// 搜索位置操作
    /// </summary>
    private static readonly Action<ILogger, string, string, CultureInfo, Exception> _searchedLocation;

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static JsonStringLocalizerLoggerExtensions()
    {
        _searchedLocation = LoggerMessage.Define<string, string, CultureInfo>(
            LogLevel.Debug,
            1,
            $"{nameof(JsonStringLocalizer)} searched for '{{Key}}' in '{{LocationSearched}}' with culture '{{Culture}}'.");
    }

    /// <summary>
    /// 输出搜索位置日志
    /// </summary>
    /// <param name="logger">日志</param>
    /// <param name="key">资源名称</param>
    /// <param name="searchedLocation">搜索位置</param>
    /// <param name="culture">区域文化</param>
    public static void SearchedLocation(this ILogger logger, string key, string searchedLocation, CultureInfo culture) =>
        _searchedLocation(logger, key, searchedLocation, culture, null);
}
