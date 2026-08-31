namespace Bing.Localization;

/// <summary>
/// 本地化资源存储器
/// </summary>
public interface ILocalizedStore
{
    /// <summary>
    /// 获取本地化资源值，未查找到资源则返回 null。
    /// </summary>
    /// <param name="culture">区域文化。范例：zh-CN</param>
    /// <param name="type">资源类型</param>
    /// <param name="name">资源名</param>
    /// <returns>指定区域文化、资源类型和名称对应的资源值；未找到时返回 <see langword="null"/>。</returns>
    string GetValue(string culture, string type, string name);

    /// <summary>
    /// 获取区域文化列表
    /// </summary>
    /// <returns>已加载的区域文化名称列表。</returns>
    IList<string> GetCultures();

    /// <summary>
    /// 获取资源类型列表
    /// </summary>
    /// <returns>已加载的本地化资源类型列表。</returns>
    IList<string> GetTypes();

    /// <summary>
    /// 获取本地化资源集合
    /// </summary>
    /// <param name="culture">区域文化。范例：zh-CN</param>
    /// <param name="type">资源类型</param>
    /// <returns>指定区域文化和资源类型对应的资源字典。</returns>
    IDictionary<string, string> GetResources(string culture, string type);
}
