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
    public static string GetCacheKey(string culture, string type, string name) => $"{culture}-{type}-{name}";

    /// <summary>
    /// 获取缓存过期时间间隔
    /// </summary>
    /// <param name="options">本地化配置</param>
    public static int GetExpiration(LocalizationOptions options) => options.Expiration;// 此处需要随机数
}
