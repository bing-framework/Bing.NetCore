using System.Reflection;
using Bing;
using Bing.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Microsoft.AspNetCore.Mvc.Abstractions;

/// <summary>
/// 操作描述符(<see cref="ActionDescriptor"/>) 扩展
/// </summary>
public static class ActionDescriptorExtensions
{
    /// <summary>
    /// 将<see cref="ActionDescriptor"/>转换为<see cref="ControllerActionDescriptor"/>。
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    /// <returns>转换后的<see cref="ControllerActionDescriptor"/>对象。</returns>
    /// <exception cref="BingFrameworkException">如果actionDescriptor不是ControllerActionDescriptor类型，则抛出异常。</exception>
    public static ControllerActionDescriptor AsControllerActionDescriptor(this ActionDescriptor actionDescriptor)
    {
        if (!actionDescriptor.IsControllerAction())
            throw new BingFrameworkException($"{nameof(actionDescriptor)} should be type of {typeof(ControllerActionDescriptor).AssemblyQualifiedName}");
        return actionDescriptor as ControllerActionDescriptor;
    }

    /// <summary>
    /// 获取与给定的操作描述符关联的方法信息
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    /// <returns>表示操作方法的<see cref="MethodInfo"/>对象。</returns>
    public static MethodInfo GetMethodInfo(this ActionDescriptor actionDescriptor) => actionDescriptor.AsControllerActionDescriptor().MethodInfo;

    /// <summary>
    /// 获取指定操作描述符所代表的的方法的返回类型。
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    public static Type GetReturnType(this ActionDescriptor actionDescriptor) => actionDescriptor.GetMethodInfo().ReturnType;

    /// <summary>
    /// 判断指定的操作描述符的返回类型是否为对象结果。
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    /// <returns>如果操作描述符的返回类型是对象结果（如<see cref="ObjectResult"/>或<see cref="ActionResult{TValue}"/>等），则返回true；否则返回false。</returns>
    public static bool HasObjectResult(this ActionDescriptor actionDescriptor) => ActionResultHelper.IsObjectResult(actionDescriptor.GetReturnType());

    /// <summary>
    /// 判断一个操作描述符是否为MVC的控制器操作描述符。
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    public static bool IsControllerAction(this ActionDescriptor actionDescriptor) => actionDescriptor is ControllerActionDescriptor;

    /// <summary>
    /// 判断一个操作描述符是否为Razor Page的页面操作描述符。
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    public static bool IsPageAction(this ActionDescriptor actionDescriptor) => actionDescriptor is PageActionDescriptor;

    /// <summary>
    /// 将<see cref="ActionDescriptor"/>转换为<see cref="PageActionDescriptor"/>。
    /// </summary>
    /// <param name="actionDescriptor">操作描述符</param>
    /// <returns>转换后的<see cref="PageActionDescriptor"/>对象。</returns>
    /// <exception cref="BingFrameworkException">如果actionDescriptor不是PageActionDescriptor类型，则抛出异常。</exception>
    public static PageActionDescriptor AsPageAction(this ActionDescriptor actionDescriptor)
    {
        if (!actionDescriptor.IsPageAction())
            throw new BingFrameworkException($"{nameof(actionDescriptor)} should be type of {typeof(PageActionDescriptor).AssemblyQualifiedName}");
        return actionDescriptor as PageActionDescriptor;
    }

}
