using System.Reflection;
using Bing.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// MVC方法查找器
/// </summary>
public class MvcMethodInfoFinder : IMethodInfoFinder
{
    /// <inheritdoc />
    public MethodInfo[] Find(Type type, Func<MethodInfo, bool> predicate) => FindAll(type).Where(predicate).ToArray();

    /// <inheritdoc />
    public MethodInfo[] FindAll(Type type)
    {
        var types = new List<Type>();
        while (IsController(type))
        {
            types.AddIfNotContains(type);
            type = type?.BaseType;
            if (type?.Name == "Controller" || type?.Name == "ControllerBase")
                break;
        }

        return types
            .SelectMany(m => m.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .ToArray();
    }

    /// <summary>
    /// 是否控制器
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>类型符合 MVC 控制器判定条件时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private static bool IsController(Type type) =>
        type != null &&
        type.IsClass &&
        type.IsPublic &&
        !type.ContainsGenericParameters &&
        !type.IsDefined(typeof(NonControllerAttribute)) &&
        (type.Name.EndsWith("Controller") || type.Name.EndsWith("ControllerBase") || type.IsDefined(typeof(ControllerAttribute)));
}