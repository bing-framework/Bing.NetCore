using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 将动态操作描述符关联到服务契约。
/// </summary>
internal sealed class BingDynamicApiActionDescriptorProvider : IActionDescriptorProvider
{
    /// <summary>
    /// 在操作属性中保存原控制器类型的唯一键。
    /// </summary>
    internal static readonly object OriginalControllerTypePropertyKey = new();

    /// <summary>
    /// 保存当前宿主的动态服务注册信息。
    /// </summary>
    private readonly BingDynamicApiRegistry _registry;

    /// <summary>
    /// 初始化 BingDynamicApiActionDescriptorProvider 类的新实例。
    /// </summary>
    /// <param name="registry">动态服务注册表。</param>
    public BingDynamicApiActionDescriptorProvider(BingDynamicApiRegistry registry) => _registry = registry;

    /// <inheritdoc />
    public int Order => int.MinValue;

    /// <inheritdoc />
    public void OnProvidersExecuting(ActionDescriptorProviderContext context)
    {
    }

    /// <inheritdoc />
    public void OnProvidersExecuted(ActionDescriptorProviderContext context)
    {
        foreach (var action in context.Results.OfType<ControllerActionDescriptor>())
        {
            var implementationType = action.ControllerTypeInfo.AsType();
            var registration = _registry.Find(implementationType);
            if (registration == null)
                continue;

            action.Properties[OriginalControllerTypePropertyKey] = implementationType;
            action.ControllerTypeInfo = registration.ServiceInterface.GetTypeInfo();
        }
    }
}
