namespace Bing.Localization;

/// <summary>
/// 配置本地化资源支持的文化名称和资源缓存有效期。
/// </summary>
public class LocalizationOptions
{
    /// <summary>
    /// 初始化 <see cref="LocalizationOptions"/> 的实例及空文化名称集合。
    /// </summary>
    public LocalizationOptions() => Cultures = new List<string>();

    /// <summary>
    /// 获取或设置应用支持的文化名称列表。
    /// </summary>
    public IList<string> Cultures { get; set; }

    /// <summary>
    /// 获取或设置本地化资源缓存的过期时间，单位为秒，默认值为 <c>28800</c>。
    /// </summary>
    public int Expiration { get; set; } = 28800;
}
