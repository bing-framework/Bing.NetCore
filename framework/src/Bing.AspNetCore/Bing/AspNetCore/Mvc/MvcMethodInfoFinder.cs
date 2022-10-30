using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bing.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// MVC方法查找器
/// </summary>
public class MvcMethodInfoFinder : IMethodInfoFinder
{
    /// <summary>
    /// 查找指定条件的项
    /// </summary>
    /// <param name="type">要查找的类型</param>
    /// <param name="predicate">筛选条件</param>
    public MethodInfo[] Find(Type type, Func<MethodInfo, bool> predicate) => FindAll(type).Where(predicate).ToArray();

    /// <summary>
    /// 查找所有项
    /// </summary>
    /// <param name="type">要查找的类型</param>
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
    private static bool IsController(Type type) =>
        type != null &&
        type.IsClass &&
        type.IsPublic &&
        !type.ContainsGenericParameters &&
        !type.IsDefined(typeof(NonControllerAttribute)) &&
        (type.Name.EndsWith("Controller") || type.Name.EndsWith("ControllerBase") || type.IsDefined(typeof(ControllerAttribute)));
}