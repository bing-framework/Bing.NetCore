using Bing.Helpers;
using Bing.Localization.Caching;

namespace Bing.Localization.Store;

/// <summary>
/// 本地化资源管理器
/// </summary>
public class LocalizedManager : ILocalizedManager
{
    /// <summary>
    /// 本地化资源存储器
    /// </summary>
    private readonly ILocalizedStore _store;

    /// <summary>
    /// 缓存
    /// </summary>
    private readonly IMemoryCache _cache;

    /// <summary>
    /// 初始化一个<see cref="LocalizedManager"/>类型的实例
    /// </summary>
    /// <param name="store">本地化资源存储器</param>
    /// <param name="cache">缓存</param>
    public LocalizedManager(ILocalizedStore store, IMemoryCache cache)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// 加载全部本地化资源
    /// </summary>
    public virtual void LoadAllResources()
    {
        var cultures = GetCultures();
        foreach (var culture in cultures)
            LoadAllResources(culture);
    }

    /// <summary>
    /// 获取区域文化列表
    /// </summary>
    protected virtual List<string> GetCultures()
    {
        var result = new List<string> { Culture.GetCurrentUICultureName() };
        var cultures = _store.GetCultures();
        if (cultures is { Count: > 0 })
            result.AddRange(cultures);
        return result.Distinct().ToList();
    }

    /// <summary>
    /// 加载全部本地化资源
    /// </summary>
    /// <param name="culture">区域文化。范例：zh-CN</param>
    public void LoadAllResources(string culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return;
        var types = _store.GetTypes();
        if (types == null || types.Count == 0)
        {
            LoadAllResources(culture, null);
            return;
        }
        foreach (var type in types)
            LoadAllResources(culture, type);
    }

    /// <summary>
    /// 加载全部本地化资源
    /// </summary>
    /// <param name="culture">区域文化。范例：zh-CN</param>
    /// <param name="type">资源类型</param>
    public virtual void LoadAllResources(string culture, string type)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return;
        var resources = _store.GetResources(culture, type);
        if (resources == null)
            return;
        foreach (var resource in resources)
            LoadResourceCache(culture, type, resource.Key, resource.Value);
    }

    /// <summary>
    /// 加载资源缓存
    /// </summary>
    /// <param name="culture">区域文化</param>
    /// <param name="type">资源类型</param>
    /// <param name="name">资源名称</param>
    /// <param name="value">资源值</param>
    protected virtual void LoadResourceCache(string culture, string type, string name, string value)
    {
        var cacheKey = CacheKeyHelper.GetCacheKey(culture, type, name);
        var localizedString = new LocalizedString(name, value, false, null);
        _cache.Set(cacheKey, localizedString);
    }

    /// <summary>
    /// 加载指定类型的本地化资源
    /// </summary>
    /// <param name="type">资源类型</param>
    public void LoadResourcesByType(string type)
    {
        var cultures = GetCultures();
        foreach (var culture in cultures) 
            LoadAllResources(culture, type);
    }
}
