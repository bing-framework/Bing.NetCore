namespace Bing.Localization;

/// <summary>
/// 本地化配置
/// </summary>
public class LocalizationOptions
{
    /// <summary>
    /// 初始化一个<see cref="LocalizationOptions"/>类型的实例
    /// </summary>
    public LocalizationOptions() => Cultures = new List<string>();

    /// <summary>
    /// 语言文化
    /// </summary>
    public IList<string> Cultures { get; set; }
}
