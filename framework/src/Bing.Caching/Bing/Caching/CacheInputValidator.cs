namespace Bing.Caching;

/// <summary>
/// 统一执行缓存逻辑输入边界校验。
/// </summary>
internal static class CacheInputValidator
{
    /// <summary>
    /// 校验缓存逻辑键的格式和长度。
    /// </summary>
    /// <param name="configuration">包含输入限制的缓存实例配置。</param>
    /// <param name="key">待校验的逻辑键。</param>
    internal static void ValidateKey(CacheConfiguration configuration, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("缓存键不能为空。", nameof(key));
        ValidateWellFormedUtf16(key, nameof(key), "缓存键");
        ValidateLength(configuration.InputLimits.MaxLogicalKeyLength, key, nameof(key), "缓存键");
    }

    /// <summary>
    /// 校验缓存前缀的格式和长度。
    /// </summary>
    /// <param name="configuration">包含输入限制的缓存实例配置。</param>
    /// <param name="prefix">待校验的前缀；为 <see langword="null"/> 时不执行检查。</param>
    internal static void ValidatePrefix(CacheConfiguration configuration, string prefix)
    {
        if (prefix == null)
            return;
        ValidateWellFormedUtf16(prefix, nameof(prefix), "缓存前缀");
        ValidateLength(configuration.InputLimits.MaxPrefixLength, prefix, nameof(prefix), "缓存前缀");
    }

    /// <summary>
    /// 校验缓存模式的格式和长度。
    /// </summary>
    /// <param name="configuration">包含输入限制的缓存实例配置。</param>
    /// <param name="pattern">待校验的模式；为 <see langword="null"/> 时不执行检查。</param>
    internal static void ValidatePattern(CacheConfiguration configuration, string pattern)
    {
        if (pattern == null)
            return;
        ValidateWellFormedUtf16(pattern, nameof(pattern), "缓存模式");
        ValidateLength(configuration.InputLimits.MaxPatternLength, pattern, nameof(pattern), "缓存模式");
    }

    /// <summary>
    /// 检查输入字符串是否超过配置的长度上限。
    /// </summary>
    /// <param name="limit">允许的最大 UTF-16 code unit 数；0 表示不限制。</param>
    /// <param name="value">待检查的输入字符串。</param>
    /// <param name="parameterName">发生超限时使用的参数名称。</param>
    /// <param name="label">异常消息中显示的输入名称。</param>
    private static void ValidateLength(int limit, string value, string parameterName, string label)
    {
        if (limit > 0 && value.Length > limit)
            throw new ArgumentOutOfRangeException(parameterName,
                $"{label}长度超过缓存实例限制 {limit}。实际长度：{value.Length}。");
    }

    /// <summary>
    /// 校验字符串是否包含格式正确的 UTF-16 代理项。
    /// </summary>
    /// <param name="value">待校验的字符串。</param>
    /// <param name="parameterName">发生错误时使用的参数名称。</param>
    /// <param name="label">异常消息中显示的输入名称。</param>
    internal static void ValidateWellFormedUtf16(string value, string parameterName, string label)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    throw new ArgumentException(
                        $"{label}包含未配对的 UTF-16 代理项（位置 {index}）。", parameterName);
                index++;
                continue;
            }

            if (char.IsLowSurrogate(character))
                throw new ArgumentException(
                    $"{label}包含未配对的 UTF-16 代理项（位置 {index}）。", parameterName);
        }
    }
}
