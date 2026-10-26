namespace Bing.Caching.FusionCache.Internal;

/// <summary>
/// 编码缓存身份、数据键和标签，避免分隔符及原生内部标记冲突。
/// </summary>
/// <remarks>
/// 数据键与标签分别添加固定前缀并做 URI 转义，确保任意逻辑键不会与标签元数据或其他实例的名称冲突。
/// </remarks>
/// <param name="options">提供键、标签和标签数量上限的冻结配置。</param>
internal sealed class FusionCacheKeyNormalizer(BingFusionCacheOptions options)
{
    /// <summary>
    /// 生成同时包含缓存命名空间和实例名称的原生键前缀。
    /// </summary>
    /// <param name="cacheNamespace">应用及环境命名空间。</param>
    /// <param name="name">缓存实例名称。</param>
    /// <returns>以固定版本标识开头、可安全用于分布式缓存的键前缀。</returns>
    internal static string Prefix(string cacheNamespace, string name) =>
        $"bing:fc:v1:{Uri.EscapeDataString(cacheNamespace)}:{Uri.EscapeDataString(name)}:";

    /// <summary>
    /// 校验并编码单个逻辑键。
    /// </summary>
    /// <param name="key">调用方提供的逻辑键。</param>
    /// <returns>带数据前缀的原生缓存键。</returns>
    /// <exception cref="ArgumentException"><paramref name="key" /> 为空或包含无效 UTF-16 字符时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key" /> 超过配置的最大长度时抛出。</exception>
    internal string Key(string key)
    {
        Validate(key, options.MaxKeyLength, nameof(key));
        return "d:" + Uri.EscapeDataString(key);
    }

    /// <summary>
    /// 校验并编码单个标签。
    /// </summary>
    /// <param name="tag">调用方提供的标签。</param>
    /// <returns>带标签前缀的原生标签值。</returns>
    /// <exception cref="ArgumentException"><paramref name="tag" /> 为空或包含无效 UTF-16 字符时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tag" /> 超过配置的最大长度时抛出。</exception>
    internal string Tag(string tag)
    {
        Validate(tag, options.MaxTagLength, nameof(tag));
        return "t:" + Uri.EscapeDataString(tag);
    }

    /// <summary>
    /// 校验并编码一组标签，同时限制单条目的标签数量。
    /// </summary>
    /// <param name="tags">调用方提供的标签集合。</param>
    /// <returns>编码后的标签数组；输入为 <see langword="null" /> 时返回 <see langword="null" />。</returns>
    /// <exception cref="ArgumentException">任一标签为空或包含无效 UTF-16 字符时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">标签数量超过上限或单个标签超过长度上限时抛出。</exception>
    internal string[]? Tags(IEnumerable<string>? tags)
    {
        if (tags == null) return null;
        var result = new List<string>();
        foreach (var tag in tags)
        {
            if (result.Count >= options.MaxTagsPerEntry)
                throw new ArgumentOutOfRangeException(nameof(tags), "缓存条目的标签数量超过限制。");
            result.Add(Tag(tag));
        }
        return result.ToArray();
    }

    /// <summary>
    /// 校验缓存身份、键或标签非空、UTF-16 合法且不超过长度上限。
    /// </summary>
    /// <param name="value">待校验的输入值。</param>
    /// <param name="limit">允许的最大 UTF-16 长度。</param>
    /// <param name="parameterName">调用参数名称，用于异常定位。</param>
    /// <exception cref="ArgumentException"><paramref name="value" /> 为空或包含无效 UTF-16 字符时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value" /> 超过 <paramref name="limit" /> 时抛出。</exception>
    internal static void Validate(string value, int limit, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("缓存身份、键或标签不能为空。", parameterName);
        CacheInputValidator.ValidateWellFormedUtf16(value, parameterName, "缓存输入");
        if (value.Length > limit)
            throw new ArgumentOutOfRangeException(parameterName, $"缓存输入长度超过限制 {limit}。");
    }
}
