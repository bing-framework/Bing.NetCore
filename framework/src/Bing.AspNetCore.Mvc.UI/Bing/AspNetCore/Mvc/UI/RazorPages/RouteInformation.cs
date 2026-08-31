namespace Bing.AspNetCore.Mvc.UI.RazorPages;

/// <summary>
/// 表示 MVC 或 Razor 页面路由及其源文件关联信息。
/// </summary>
public class RouteInformation
{
    /// <summary>
    /// 获取或设置 ASP.NET Core 区域名称。
    /// </summary>
    public string AreaName { get; set; }

    /// <summary>
    /// 获取或设置 MVC 控制器名称。
    /// </summary>
    public string ControllerName { get; set; }

    /// <summary>
    /// 获取或设置 MVC 操作方法名称。
    /// </summary>
    public string ActionName { get; set; }

    /// <summary>
    /// 获取或设置 Razor 页面路由路径。
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 获取或设置用于调用该路由的目标方法描述。
    /// </summary>
    public string Invocation { get; set; }

    /// <summary>
    /// 获取或设置路由对应的源文件路径。
    /// </summary>
    public string FilePath { get; set; }

    /// <summary>
    /// 获取或设置路由使用的模板路径。
    /// </summary>
    public string TemplatePath { get; set; }

    /// <summary>
    /// 获取或设置路由对应的视图名称。
    /// </summary>
    public string ViewName { get; set; }

    /// <summary>
    /// 获取或设置该路由是否对应部分视图，默认值为 <c>false</c>。
    /// </summary>
    public bool IsPartialView { get; set; } = false;
}