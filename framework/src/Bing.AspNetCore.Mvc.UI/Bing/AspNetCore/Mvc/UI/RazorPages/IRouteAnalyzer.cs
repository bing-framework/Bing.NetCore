namespace Bing.AspNetCore.Mvc.UI.RazorPages;

/// <summary>
/// 路由分析器
/// </summary>
public interface IRouteAnalyzer
{
    /// <summary>
    /// 获取所有路由信息
    /// </summary>
    /// <returns>应用程序中发现的全部路由信息。</returns>
    IEnumerable<RouteInformation> GetAllRouteInformations();
}