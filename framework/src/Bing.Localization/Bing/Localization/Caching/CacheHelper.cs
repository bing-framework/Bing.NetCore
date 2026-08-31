namespace Bing.Localization.Caching;

/// <summary>
/// 缓存键辅助操作
/// </summary>
internal static class CacheHelper
{
    /// <summary>
    /// 获取缓存键
    /// </summary>
    /// <param name="culture">区域文化</param>
    /// <param name="type">资源类型</param>
    /// <param name="name">资源名称</param>
    /// <returns>由区域、资源类型和资源名称组成的本地化缓存键。</returns>
    public static string GetCacheKey(string culture, string type, string name) => $"{culture}-{type}-{name}";

    /// <summary>
    /// 获取缓存过期时间间隔
    /// </summary>
    /// <param name="options">本地化配置</param>
    /// <returns>本地化资源缓存的过期时间，单位为秒；具体值由 <see cref="LocalizationOptions.Expiration"/> 配置。</returns>
    public static int GetExpiration(LocalizationOptions options) => options.Expiration;
}
