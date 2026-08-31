namespace Bing.TextTemplating;

/// <summary>
/// 定义根据模板名称和上下文模型生成文本内容的渲染引擎契约。
/// </summary>
public interface ITemplateRenderingEngine
{
    /// <summary>
    /// 获取渲染引擎的唯一名称。
    /// </summary>
    string Name { get; }

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
