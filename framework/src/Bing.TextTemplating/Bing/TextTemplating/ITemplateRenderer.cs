namespace Bing.TextTemplating;

/// <summary>
/// 定义按模板名称生成文本内容的统一渲染契约。
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// 异步渲染指定模板并返回生成的文本内容。
    /// </summary>
    /// <param name="templateName">要渲染的模板名称。</param>
    /// <param name="model">传递给模板的模型，可为空。</param>
    /// <param name="cultureName">渲染使用的区域性名称，可为空。</param>
    /// <param name="globalContext">传递给模板的全局上下文，可为空。</param>
    /// <returns>表示异步渲染操作的任务，结果为生成的文本内容。</returns>
    Task<string> RenderAsync(string templateName, object model = null, string cultureName = null, Dictionary<string, object> globalContext = null);
}
