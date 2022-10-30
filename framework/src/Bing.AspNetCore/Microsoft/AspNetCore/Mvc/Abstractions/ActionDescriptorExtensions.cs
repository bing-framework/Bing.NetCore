using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Bing.Extensions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Mvc.Abstractions;

/// <summary>
/// 操作描述符(<see cref="ActionDescriptor"/>) 扩展
/// </summary>
public static class ActionDescriptorExtensions
{
    /// <summary>
    /// 获取Action路径
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    public static string GetActionPath(this ActionDescriptor actionDescriptor)
    {
        var controllerAction = actionDescriptor as ControllerActionDescriptor;
        var httpMethodMetadata = controllerAction.EndpointMetadata.FirstOrDefault(x => x is HttpMethodMetadata);
        var methods = new List<string>();
        if (httpMethodMetadata != null)
            methods.AddRange(((HttpMethodMetadata)httpMethodMetadata).HttpMethods);
        if (controllerAction.AttributeRouteInfo == null)
        {
            var area = actionDescriptor.GetAreaName();
            var controller = actionDescriptor.GetControllerName();
            var action = actionDescriptor.GetActionName();
            var path = (area == null ? $"{controller}/{action}" : $"{area}/{controller}/{action}").ToLower();
            return $"{methods.ExpandAndToString()} {path}".ToLower();
        }
        var regex = new Regex("{\\w+}");
        var template = controllerAction.AttributeRouteInfo.Template.ToLower();
        template = regex.Replace(template, "{arg}");
        return $"{methods.ExpandAndToString()} {template}".ToLower();
    }

    /// <summary>
    /// 获取Area名称
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    public static string GetAreaName(this ActionDescriptor actionDescriptor)
    {
        if (actionDescriptor.RouteValues.TryGetValue("area", out var area))
            return area;
        return string.Empty;
    }

    /// <summary>
    /// 获取Controller名称
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    public static string GetControllerName(this ActionDescriptor actionDescriptor)
    {
        if (actionDescriptor.RouteValues.TryGetValue("controller", out var controller))
            return controller;
        return string.Empty;
    }

    /// <summary>
    /// 获取Action名称
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    public static string GetActionName(this ActionDescriptor actionDescriptor)
    {
        if (actionDescriptor.RouteValues.TryGetValue("action", out var action))
            return action;
        return string.Empty;
    }
}