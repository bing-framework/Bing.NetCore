namespace Bing.TextTemplating;

/// <summary>
/// 按模板定义和区域性获取模板内容。
/// </summary>
public interface ITemplateContentProvider
{
    /// <summary>
    /// 按模板名称获取模板内容。
    /// </summary>
    /// <param name="templateName">已注册模板名称。</param>
    /// <param name="cultureName">可选的区域性名称。</param>
    /// <param name="tryDefaults">未命中内容时是否继续默认回退流程。</param>
    /// <param name="useCurrentCultureIfCultureNameIsNull">区域性名称为空时是否使用当前 UI 区域性。</param>
    /// <returns>可用的模板内容；未找到时返回 <c>null</c>。</returns>
    Task<string> GetContentOrNullAsync(string templateName, string cultureName = null, bool tryDefaults = true, bool useCurrentCultureIfCultureNameIsNull = true);

    /// <summary>
    /// 按模板定义获取模板内容。
    /// </summary>
    /// <param name="templateDefinition">已注册的模板定义。</param>
    /// <param name="cultureName">可选的区域性名称。</param>
    /// <param name="tryDefaults">未命中内容时是否继续默认回退流程。</param>
    /// <param name="useCurrentCultureIfCultureNameIsNull">区域性名称为空时是否使用当前 UI 区域性。</param>
    /// <returns>可用的模板内容；未找到时返回 <c>null</c>。</returns>
    Task<string> GetContentOrNullAsync(TemplateDefinition templateDefinition, string cultureName = null, bool tryDefaults = true, bool useCurrentCultureIfCultureNameIsNull = true);
}
