using System.Globalization;
using Bing.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.Localization.Store;

/// <summary>
/// 数据存储本地化资源查找器
/// </summary>
public class StoreStringLocalizer : StringLocalizerBase
{
    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 本地化资源存储器
    /// </summary>
    private readonly ILocalizedStore _store;

    /// <summary>
    /// 初始化一个<see cref="StoreStringLocalizer"/>类型的实例
    /// </summary>
    /// <param name="logger">日志</param>
    /// <param name="cache">内存缓存</param>
    /// <param name="store">本地化资源存储器</param>
    /// <param name="type">资源类型</param>
    public StoreStringLocalizer(ILogger logger, IMemoryCache cache, ILocalizedStore store, string type) : base(cache, type)
    {
        _logger = logger ?? NullLogger.Instance;
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// 获取本地化字符串结果
    /// </summary>
    /// <param name="name">资源名称</param>
    /// <param name="arguments">参数列表</param>
    protected override LocalizedString GetResult(string name, params object[] arguments)
    {
        var culture = Culture.GetCurrentUICulture();
        var result = GetLocalizedStringByCache(culture, name);
        return FormatResult(result, name, arguments);
    }

    /// <summary>
    /// 获取资源值
    /// </summary>
    /// <param name="culture">区域文化</param>
    /// <param name="name">资源名称</param>
    /// <param name="type">资源类型</param>
    protected override string GetValue(CultureInfo culture, string name, string type)
    {
        var result = _store.GetValue(culture.Name, type, name);
        _logger.Searched(name, type, culture);
        return result;
    }

    /// <summary>
    /// 获取全部本地化字符串
    /// </summary>
    /// <param name="includeParentCultures">是否包含父区域</param>
    public override IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var result = new List<LocalizedString>();
        var cultures = Culture.GetCurrentUICultures();
        foreach (var culture in cultures)
        {
            result.AddRange(GetAllStrings(culture.Name));
            if (!includeParentCultures)
                break;
        }

        return result;
    }

    /// <summary>
    /// 获取全部本地化字符串
    /// </summary>
    /// <param name="culture">区域文化</param>
    protected virtual IEnumerable<LocalizedString> GetAllStrings(string culture)
    {
        var result = new List<LocalizedString>();
        var types = _store.GetTypes();
        if (types == null || types.Count == 0)
            return GetAllStrings(culture, null);
        result.AddRange(types.SelectMany(type => GetAllStrings(culture, type)));
        return result;
    }

    /// <summary>
    /// 获取全部本地化字符串
    /// </summary>
    /// <param name="culture">区域文化</param>
    /// <param name="type">资源类型</param>
    protected virtual IEnumerable<LocalizedString> GetAllStrings(string culture, string type)
    {
        var resources = _store.GetResources(culture, type);
        return resources == null ? new List<LocalizedString>() : ToLocalizedStrings(resources);
    }

    /// <summary>
    /// 转换为 <see cref="LocalizedString"/> 集合
    /// </summary>
    /// <param name="resources">资源键值对集合</param>
    protected virtual IEnumerable<LocalizedString> ToLocalizedStrings(IEnumerable<KeyValuePair<string, string>> resources)
    {
        var result = new List<LocalizedString>();
        foreach (var resource in resources)
            result.Add(new LocalizedString(resource.Key, resource.Value, false, null));
        return result;
    }
}
