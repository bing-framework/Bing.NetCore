using System.Threading.Tasks;

namespace Bing.TextTemplating;

/// <summary>
/// 模板内容提供程序
/// </summary>
public interface ITemplateContentProvider
{
    /// <summary>
    /// 获取模板内容
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="cultureName">区域性名称</param>
    /// <param name="tryDefaults">尝试默认值</param>
    /// <param name="useCurrentCultureIfCultureNameIsNull">如果当前区域性名称为空，则默认使用当前区域</param>
    Task<string> GetContentOrNullAsync(string templateName, string cultureName = null, bool tryDefaults = true, bool useCurrentCultureIfCultureNameIsNull = true);

    /// <summary>
    /// 获取模板内容
    /// </summary>
    /// <param name="templateDefinition">模板定义</param>
    /// <param name="cultureName">区域性名称</param>
    /// <param name="tryDefaults">尝试默认值</param>
    /// <param name="useCurrentCultureIfCultureNameIsNull">如果当前区域性名称为空，则默认使用当前区域</param>
    Task<string> GetContentOrNullAsync(TemplateDefinition templateDefinition, string cultureName = null, bool tryDefaults = true, bool useCurrentCultureIfCultureNameIsNull = true);
}