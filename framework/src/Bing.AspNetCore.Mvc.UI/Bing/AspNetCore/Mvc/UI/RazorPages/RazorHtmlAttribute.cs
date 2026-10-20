namespace Bing.AspNetCore.Mvc.UI.RazorPages;

/// <summary>
/// Razor生成Html静态文件
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class RazorHtmlAttribute : Attribute
{
    /// <summary>
    /// 获取或设置生成文件路径。
    /// </summary>
    /// <remarks>该路径相对于根路径，例如 <c>/Typings/app/app.component.html</c>。</remarks>
    public string Path { get; set; }

    /// <summary>
    /// 获取或设置生成路径模板。
    /// </summary>
    /// <remarks>例如 <c>Typings/app/{area}/{controller}/{controller}-{action}.component.html</c>。</remarks>
    public string Template { get; set; }

    /// <summary>
    /// 获取或设置视图名称。
    /// </summary>
    /// <remarks>例如 <c>/Home/Index</c>。</remarks>
    public string ViewName { get; set; }

    /// <summary>
    /// 获取或设置是否使用部分视图。
    /// </summary>
    /// <remarks>默认值为 <see langword="false"/>。</remarks>
    public bool IsPartialView { get; set; } = false;
}
