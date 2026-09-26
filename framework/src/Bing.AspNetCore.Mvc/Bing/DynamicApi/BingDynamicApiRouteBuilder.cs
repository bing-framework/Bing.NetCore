using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 提供动态 API 的默认名称、动词和路由计算规则。
/// </summary>
public static class BingDynamicApiRouteBuilder
{
    /// <summary>
    /// 根据服务契约名称生成默认 kebab-case 路由名。
    /// </summary>
    /// <param name="serviceName">服务契约名称。</param>
    /// <returns>移除接口前缀和服务后缀后的路由名称。</returns>
    public static string NormalizeServiceName(string serviceName)
    {
        var name = serviceName.Length > 1 && serviceName[0] == 'I' && char.IsUpper(serviceName[1])
            ? serviceName[1..]
            : serviceName;
        if (name.EndsWith("AppService", StringComparison.Ordinal))
            name = name[..^"AppService".Length];
        else if (name.EndsWith("Service", StringComparison.Ordinal))
            name = name[..^"Service".Length];
        return ToKebabCase(name);
    }

    /// <summary>
    /// 根据方法名前缀推断 HTTP 动词。
    /// </summary>
    /// <param name="method">服务契约方法。</param>
    /// <returns>推断的 HTTP 动词；未匹配前缀时返回 POST。</returns>
    public static string GetHttpMethod(MethodInfo method)
    {
        var name = RemoveAsyncSuffix(method.Name);
        if (name.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
            return "GET";
        if (name.StartsWith("Put", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
            return "PUT";
        if (name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Remove", StringComparison.OrdinalIgnoreCase))
            return "DELETE";
        if (name.StartsWith("Patch", StringComparison.OrdinalIgnoreCase))
            return "PATCH";
        if (name.StartsWith("Create", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Add", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Insert", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Post", StringComparison.OrdinalIgnoreCase))
            return "POST";
        return "POST";
    }

    /// <summary>
    /// 根据常见 CRUD 方法名生成服务路由下的方法相对模板。
    /// </summary>
    /// <param name="method">服务契约方法。</param>
    /// <returns>相对于服务路由的模板；集合操作可返回空字符串。</returns>
    public static string BuildMethodRoute(MethodInfo method)
    {
        var name = RemoveAsyncSuffix(method.Name);
        var parameters = method.GetParameters();
        var idParameter = parameters.FirstOrDefault(parameter => string.Equals(parameter.Name, "id", StringComparison.OrdinalIgnoreCase))
                          ?? parameters.FirstOrDefault(parameter => parameter.Name?.EndsWith("Id", StringComparison.OrdinalIgnoreCase) == true);
        var idRoute = idParameter == null ? "{id}" : $"{{{idParameter.Name}}}";

        foreach (var prefix in new[] { "GetList", "GetAll", "Create", "Add", "Insert", "Post" })
        {
            if (name.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;
        }

        foreach (var prefix in new[] { "GetById", "Update", "Delete", "Remove", "Put", "Patch", "Get" })
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var remainder = name[prefix.Length..];
            if (remainder.Length == 0 && idParameter != null && prefix != "Get")
                return idRoute;
            if (prefix == "Get" && remainder.Length == 0)
                return idParameter == null ? string.Empty : idRoute;
            if (prefix == "GetById")
                return idRoute;
            if (prefix is "Update" or "Delete" or "Remove" or "Put" or "Patch")
                return idParameter == null ? ToKebabCase(remainder) : idRoute;
            if (remainder.Length > 0)
                return idParameter == null ? ToKebabCase(remainder) : $"{idRoute}/{ToKebabCase(remainder)}";
            return string.Empty;
        }

        return ToKebabCase(name);
    }

    /// <summary>
    /// 将 PascalCase 或 camelCase 名称转换为 kebab-case。
    /// </summary>
    /// <param name="value">待转换的名称。</param>
    /// <returns>以连字符分隔的小写名称。</returns>
    public static string ToKebabCase(string value)
    {
        var kebab = Regex.Replace(value, "([a-z0-9])([A-Z])", "$1-$2");
        kebab = Regex.Replace(kebab, "([A-Z])([A-Z][a-z])", "$1-$2");
        return kebab.ToLowerInvariant();
    }

    /// <summary>
    /// 去除方法名末尾的异步后缀。
    /// </summary>
    /// <param name="methodName">方法名称。</param>
    /// <returns>去除 Async 后缀的名称；不存在后缀时返回原名称。</returns>
    public static string RemoveAsyncSuffix(string methodName) =>
        methodName.EndsWith("Async", StringComparison.Ordinal) ? methodName[..^"Async".Length] : methodName;
}
