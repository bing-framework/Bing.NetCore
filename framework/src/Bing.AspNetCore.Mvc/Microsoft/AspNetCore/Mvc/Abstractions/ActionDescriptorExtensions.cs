using System;
using System.Reflection;
using Bing.AspNetCore.Mvc;
using Bing.Exceptions;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Microsoft.AspNetCore.Mvc.Abstractions;

/// <summary>
/// 操作描述符(<see cref="ActionDescriptor"/>) 扩展
/// </summary>
public static class ActionDescriptorExtensions
{
    /// <summary>
    /// 转换为控制器操作符
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    public static ControllerActionDescriptor AsControllerActionDescriptor(this ActionDescriptor actionDescriptor)
    {
        if (!actionDescriptor.IsControllerAction())
            throw new Warning($"{nameof(actionDescriptor)} should be type of {typeof(ControllerActionDescriptor).AssemblyQualifiedName}");
        return actionDescriptor as ControllerActionDescriptor;
    }

    /// <summary>
    /// 获取方法信息
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    public static MethodInfo GetMethodInfo(this ActionDescriptor actionDescriptor) => actionDescriptor.AsControllerActionDescriptor().MethodInfo;

    /// <summary>
    /// 获取返回类型
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    public static Type GetReturnType(this ActionDescriptor actionDescriptor) => actionDescriptor.GetMethodInfo().ReturnType;

    /// <summary>
    /// 是否有对象结果
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    public static bool HasObjectResult(this ActionDescriptor actionDescriptor) => ActionResultHelper.IsObjectResult(actionDescriptor.GetReturnType());

    /// <summary>
    /// 是否控制器操作符
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    public static bool IsControllerAction(this ActionDescriptor actionDescriptor) => actionDescriptor is ControllerActionDescriptor;
}