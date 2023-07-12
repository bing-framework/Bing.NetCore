namespace Bing.Options;

/// <summary>
/// 配置项扩展
/// </summary>
public interface IBingOptions
{
    /// <summary>
    /// 添加配置项扩展
    /// </summary>
    /// <param name="extension">配置项扩展</param>
    void AddExtension(IBingOptionsExtension extension);
}
