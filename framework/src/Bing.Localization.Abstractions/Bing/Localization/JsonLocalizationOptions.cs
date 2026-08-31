namespace Bing.Localization;

/// <summary>
/// 配置基于 JSON 文件的本地化资源加载方式。
/// </summary>
public class JsonLocalizationOptions : LocalizationOptions
{
    /// <summary>
    /// 初始化 <see cref="JsonLocalizationOptions"/> 的实例，并将资源路径设为 <c>Resources</c>。
    /// </summary>
    public JsonLocalizationOptions()
    {
        ResourcesPath = "Resources";
    }

    /// <summary>
    /// 获取或设置 JSON 本地化资源文件所在的相对路径或目录名称。
    /// </summary>
    public string ResourcesPath { get; set; }
}
