using System.Text.Json;
using Bing.Helpers;

namespace Bing.Localization.Json;

/// <summary>
/// Json本地化资源查找器
/// </summary>
public class JsonStringLocalizer : StringLocalizerBase
{
    /// <summary>
    /// Json文档选项配置
    /// </summary>
    private static readonly JsonDocumentOptions _jsonDocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// 路径解析器
    /// </summary>
    private readonly IPathResolver _pathResolver;

    /// <summary>
    /// 资源根目录路径
    /// </summary>
    private readonly string _resourcesRootPath;

    /// <summary>
    /// 资源基名称
    /// </summary>
    private readonly string _resourcesBaseName;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 资源缓存
    /// </summary>
    private readonly ConcurrentDictionary<string, IEnumerable<KeyValuePair<string, string>>> _resourcesCache;

    /// <summary>
    /// 搜索位置
    /// </summary>
    private readonly string _searchedLocation;

    /// <summary>
    /// 初始化一个<see cref="JsonStringLocalizer"/>类型的实例
    /// </summary>
    /// <param name="pathResolver">路径解析器</param>
    /// <param name="resourcesRootPath">资源根目录路径</param>
    /// <param name="resourcesBaseName">资源基名称</param>
    /// <param name="logger">日志</param>
    /// <param name="cache">缓存</param>
    public JsonStringLocalizer(IPathResolver pathResolver, string resourcesRootPath, string resourcesBaseName, ILogger logger, IMemoryCache cache)
        : base(cache, resourcesBaseName)
    {
        _pathResolver = pathResolver;
        _resourcesRootPath = resourcesRootPath;
        _resourcesBaseName = resourcesBaseName;
        _logger = logger ?? NullLogger.Instance;
        _resourcesCache = new ConcurrentDictionary<string, IEnumerable<KeyValuePair<string, string>>>();
        _searchedLocation = $"{resourcesRootPath}.{resourcesBaseName}";
    }

    /// <summary>
    /// 获取本地化字符串
    /// </summary>
    /// <param name="culture">区域文化</param>
    /// <param name="name">资源名称</param>
    protected override LocalizedString GetLocalizedString(CultureInfo culture, string name)
    {
        var value = GetValue(culture, name, _resourcesBaseName) ?? GetValue(culture, name, null);
        if (string.IsNullOrWhiteSpace(value))
            return new LocalizedString(name, string.Empty, true, null);
        return new LocalizedString(name, value, false, _searchedLocation);
    }

    /// <summary>
    /// 获取资源值
    /// </summary>
    /// <param name="culture">区域文化</param>
    /// <param name="name">资源名称</param>
    /// <param name="type">资源类型</param>
    protected override string GetValue(CultureInfo culture, string name, string type)
    {
        var resources = GetResourcesByCache(culture, type);
        if (resources == null)
            return null;
        var resource = resources.SingleOrDefault(t => t.Key == name);
        var result = resource.Value;
        _logger.SearchedLocation(name, _searchedLocation, culture);
        return result;
    }

    /// <summary>
    /// 从缓存获取资源列表
    /// </summary>
    /// <param name="culture">区域文化</param>
    /// <param name="resourcesBaseName">资源基名称</param>
    protected virtual IEnumerable<KeyValuePair<string, string>> GetResourcesByCache(CultureInfo culture, string resourcesBaseName) =>
        _resourcesCache.GetOrAdd($"{culture.Name}_{resourcesBaseName}", _ => GetResources(culture, resourcesBaseName));

    /// <summary>
    /// 获取资源列表
    /// </summary>
    /// <param name="culture">区域文化</param>
    /// <param name="resourcesBaseName">资源基名称</param>
    protected virtual IEnumerable<KeyValuePair<string, string>> GetResources(CultureInfo culture, string resourcesBaseName)
    {
        var path = _pathResolver.GetJsonResourcePath(_resourcesRootPath, resourcesBaseName, culture);
        if (File.Exists(path) == false)
        {
            if (string.IsNullOrWhiteSpace(resourcesBaseName))
                return null;
            path = _pathResolver.GetJsonResourcePath(_resourcesRootPath, resourcesBaseName.Replace('.', Path.DirectorySeparatorChar), culture);
        }

        return LoadJsonResources(path);
    }

    /// <summary>
    /// 加载Json资源
    /// </summary>
    /// <param name="filePath">文件路径</param>
    private static IDictionary<string, string> LoadJsonResources(string filePath)
    {
        if (File.Exists(filePath) == false)
            return null;
        using var reader = new StreamReader(filePath);
        using var document = JsonDocument.Parse(reader.BaseStream, _jsonDocumentOptions);
        return document.RootElement.EnumerateObject().ToDictionary(e => e.Name, e => e.Value.ToString());
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
            var resources = ToLocalizedStrings(GetResourcesByCache(culture, _resourcesBaseName));
            result.AddRange(resources);
            if (includeParentCultures == false)
                return result;
        }
        return result;
    }

    /// <summary>
    /// 转换为<see cref="LocalizedString"/>集合
    /// </summary>
    /// <param name="resources">资源集合</param>
    protected virtual IEnumerable<LocalizedString> ToLocalizedStrings(IEnumerable<KeyValuePair<string, string>> resources)
    {
        var result = new List<LocalizedString>();
        foreach (var resource in resources)
            result.Add(new LocalizedString(resource.Key, resource.Value, false, _searchedLocation));
        return result;
    }
}
