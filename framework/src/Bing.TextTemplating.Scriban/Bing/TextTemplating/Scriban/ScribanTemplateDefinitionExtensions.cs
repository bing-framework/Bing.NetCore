namespace Bing.TextTemplating.Scriban;

/// <summary>
/// Scriban模板定义扩展
/// </summary>
public static class ScribanTemplateDefinitionExtensions
{
    /// <summary>
    /// 设置Scriban引擎
    /// </summary>
    /// <param name="templateDefinition">模板定义</param>
    /// <returns>已设置 Scriban 渲染引擎的模板定义。</returns>
    public static TemplateDefinition WithScribanEngine(this TemplateDefinition templateDefinition) => templateDefinition.WithRenderEngine(ScribanTemplateRenderingEngine.EngineName);
}