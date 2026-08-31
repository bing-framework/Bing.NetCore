using System.Reflection;
using Bing.Extensions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bing.AspNetCore.Mvc.UI.RazorPages;

/// <summary>
/// 路由分析器
/// </summary>
public class RouteAnalyzer : IRouteAnalyzer
{
    /// <summary>
    /// 操作描述集合提供程序
    /// </summary>
    private readonly IActionDescriptorCollectionProvider _actionDescriptorCollectionProvider;

    /// <summary>
    /// 初始化一个<see cref="RouteAnalyzer"/>类型的实例
    /// </summary>
    /// <param name="actionDescriptorCollectionProvider">操作描述集合提供程序</param>
    public RouteAnalyzer(IActionDescriptorCollectionProvider actionDescriptorCollectionProvider)
    {
        _actionDescriptorCollectionProvider = actionDescriptorCollectionProvider;
    }

    /// <summary>
    /// 获取所有路由信息
    /// </summary>
    /// <returns>应用中 MVC 和 Razor Page 操作描述对应的路由、调用入口及视图信息集合。</returns>
    public IEnumerable<RouteInformation> GetAllRouteInformations()
    {
        var list = new List<RouteInformation>();

        var actionDescriptors = this._actionDescriptorCollectionProvider.ActionDescriptors.Items;
        foreach (var actionDescriptor in actionDescriptors)
        {
            var info = new RouteInformation();

            if (actionDescriptor.RouteValues.ContainsKey("area"))
            {
                info.AreaName = actionDescriptor.RouteValues["area"];
            }

            // Razor页面路径以及调用
            if (actionDescriptor is PageActionDescriptor pageActionDescriptor)
            {
                info.Path = pageActionDescriptor.ViewEnginePath;
                info.Invocation = pageActionDescriptor.RelativePath;
            }

            // 路由属性路径
            if (actionDescriptor.AttributeRouteInfo != null)
            {
                info.Path = $"/{actionDescriptor.AttributeRouteInfo.Template}";
            }

            // Controller/Action 的路径以及调用
            if (actionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
            {
                if (info.Path.IsEmpty())
                {
                    info.Path = $"/{controllerActionDescriptor.ControllerName}/{controllerActionDescriptor.ActionName}";
                }
                SetHtmlInfo(info, controllerActionDescriptor);
                info.ControllerName = controllerActionDescriptor.ControllerName;
                info.ActionName = controllerActionDescriptor.ActionName;
                info.Invocation = $"{controllerActionDescriptor.ControllerName}Controller.{controllerActionDescriptor.ActionName}";
            }

            info.Invocation += $"({actionDescriptor.DisplayName})";

            list.Add(info);
        }

        return list;
    }

    /// <summary>
    /// 设置Html信息
    /// </summary>
    /// <param name="routeInformation">路由信息</param>
    /// <param name="controllerActionDescriptor">控制器</param>
    /// <remarks>根据控制器或操作上的 <see cref="RazorHtmlAttribute"/> 填充视图文件、模板和局部视图信息。</remarks>
    private void SetHtmlInfo(RouteInformation routeInformation,
        ControllerActionDescriptor controllerActionDescriptor)
    {
        var htmlAttribute = controllerActionDescriptor.ControllerTypeInfo.GetCustomAttribute<RazorHtmlAttribute>() ??
                            controllerActionDescriptor.MethodInfo.GetCustomAttribute<RazorHtmlAttribute>();
        if (htmlAttribute == null)
            return;
        routeInformation.FilePath = htmlAttribute.Path;
        routeInformation.TemplatePath = htmlAttribute.Template;
        routeInformation.IsPartialView = htmlAttribute.IsPartialView;
        routeInformation.ViewName = htmlAttribute.ViewName;
    }
}