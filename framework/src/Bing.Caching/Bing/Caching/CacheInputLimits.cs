namespace Bing.Caching;

/// <summary>
/// 缓存逻辑输入的可选长度限制。
/// </summary>
/// <remarks>
/// 所有值均按 .NET <see cref="string.Length"/>（UTF-16 code unit）计算；值为 0 表示不限制。
/// 默认实例不改变现有缓存调用方的行为。限制只在对应缓存实例的入口生效。
/// </remarks>
public sealed class CacheInputLimits
{
    /// <summary>
    /// 初始化一个 <see cref="CacheInputLimits"/> 类型的实例。
    /// </summary>
    /// <param name="maxLogicalKeyLength">逻辑键最大长度，0 表示不限制。</param>
    /// <param name="maxPrefixLength">前缀最大长度，0 表示不限制。</param>
    /// <param name="maxPatternLength">模式最大长度，0 表示不限制。</param>
    public CacheInputLimits(int maxLogicalKeyLength = 0, int maxPrefixLength = 0,
        int maxPatternLength = 0)
    {
        Validate(maxLogicalKeyLength, nameof(maxLogicalKeyLength));
        Validate(maxPrefixLength, nameof(maxPrefixLength));
        Validate(maxPatternLength, nameof(maxPatternLength));
        MaxLogicalKeyLength = maxLogicalKeyLength;
        MaxPrefixLength = maxPrefixLength;
        MaxPatternLength = maxPatternLength;
    }

    /// <summary>
    /// 获取不限制逻辑键、前缀和模式长度的配置。
    /// </summary>
    public static CacheInputLimits Unlimited { get; } = new();

    /// <summary>
    /// 获取逻辑键最大长度。
    /// </summary>
    /// <remarks>
    /// 0 表示不限制。
    /// </remarks>
    public int MaxLogicalKeyLength { get; }

    /// <summary>
    /// 获取前缀最大长度。
    /// </summary>
    /// <remarks>
    /// 0 表示不限制。
    /// </remarks>
    public int MaxPrefixLength { get; }

    /// <summary>
    /// 获取模式最大长度。
    /// </summary>
    /// <remarks>
    /// 0 表示不限制。
    /// </remarks>
    public int MaxPatternLength { get; }

    /// <summary>
    /// 校验长度限制不能为负数。
    /// </summary>
    /// <param name="value">待校验的最大长度。</param>
    /// <param name="parameterName">发生错误时使用的参数名称。</param>
    private static void Validate(int value, string parameterName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(parameterName, "缓存输入长度限制不能为负数。");
    }
}
