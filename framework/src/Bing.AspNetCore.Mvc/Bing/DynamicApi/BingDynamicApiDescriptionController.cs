using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bing.AspNetCore.Mvc.Filters;
using Bing.DynamicApi.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 提供运行时动态 API 描述的 HTTP 端点。
/// </summary>
[ApiController]
[Route("api/dynamic-api/{version}/definition")]
[IgnoreResultHandler]
internal sealed class BingDynamicApiDescriptionController : ControllerBase
{
    /// <summary>
    /// 提供最终 MVC 操作的 API 描述。
    /// </summary>
    private readonly IApiDescriptionGroupCollectionProvider _apiDescriptionProvider;
    /// <summary>
    /// 提供当前宿主的服务契约与版本注册信息。
    /// </summary>
    private readonly BingDynamicApiRegistry _registry;

    /// <summary>
    /// 初始化 BingDynamicApiDescriptionController 类的新实例。
    /// </summary>
    /// <param name="apiDescriptionProvider">MVC API 描述提供器。</param>
    /// <param name="registry">动态服务注册表。</param>
    public BingDynamicApiDescriptionController(
        IApiDescriptionGroupCollectionProvider apiDescriptionProvider,
        BingDynamicApiRegistry registry)
    {
        _apiDescriptionProvider = apiDescriptionProvider;
        _registry = registry;
    }

    /// <summary>
    /// 获取指定版本的服务端 API 描述。
    /// </summary>
    /// <param name="version">API 版本。</param>
    /// <returns>版本对应的服务描述；不存在该版本时返回 404 结果。</returns>
    [HttpGet]
    public ActionResult<BingDynamicApiDefinition> Get([FromRoute] string version)
    {
        var registrations = _registry.GetServices(version);
        if (registrations.Count == 0)
            return NotFound();

        var registrationMap = registrations.ToDictionary(item => item.ControllerType);
        var descriptions = _apiDescriptionProvider.ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Where(description => description.ActionDescriptor is ControllerActionDescriptor)
            .Select(description => (Description: description, Action: (ControllerActionDescriptor)description.ActionDescriptor))
            .Where(item => registrationMap.ContainsKey(GetRegistrationControllerType(item.Action)))
            .Where(item => string.Equals(registrationMap[GetRegistrationControllerType(item.Action)].ApiVersion, version, StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Description.HttpMethod != null)
            .Select(item => CreateMethodDefinition(item.Description, item.Action, registrationMap[GetRegistrationControllerType(item.Action)]))
            .GroupBy(item => item.ServiceType)
            .Select(group => new BingDynamicApiServiceDefinition
            {
                ServiceName = group.First().Registration.ServiceName,
                ServiceTypeName = group.Key.FullName ?? group.Key.Name,
                Methods = group.Select(item => item.Definition).OrderBy(item => item.MethodName, StringComparer.Ordinal).ToList(),
            })
            .OrderBy(service => service.ServiceName, StringComparer.Ordinal)
            .ToList();

        return Ok(new BingDynamicApiDefinition
        {
            ApiVersion = version,
            Services = descriptions,
        });
    }

    /// <summary>
    /// 获取操作对应的原始服务实现类型。
    /// </summary>
    /// <param name="action">MVC 操作描述符。</param>
    /// <returns>记录的原控制器类型，未记录时返回描述符中的类型。</returns>
    private static Type GetRegistrationControllerType(ControllerActionDescriptor action)
    {
        return action.Properties.TryGetValue(BingDynamicApiActionDescriptorProvider.OriginalControllerTypePropertyKey, out var value)
               && value is Type controllerType
            ? controllerType
            : action.ControllerTypeInfo.AsType();
    }

    /// <summary>
    /// 生成服务方法的远程调用描述。
    /// </summary>
    /// <param name="description">最终 MVC API 描述。</param>
    /// <param name="action">MVC 操作描述符。</param>
    /// <param name="registration">动态服务注册信息。</param>
    /// <returns>服务契约类型、注册信息和方法描述。</returns>
    private static (Type ServiceType, BingDynamicApiServiceRegistration Registration, BingDynamicApiMethodDefinition Definition)
        CreateMethodDefinition(Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription description,
            ControllerActionDescriptor action, BingDynamicApiServiceRegistration registration)
    {
        var contractMethod = registration.ContractMethods[action.MethodInfo.MetadataToken];
        var parameters = contractMethod.GetParameters()
            .Select(parameter =>
            {
                if (parameter.ParameterType == typeof(CancellationToken))
                {
                    return new BingDynamicApiParameterDefinition
                    {
                        Name = parameter.Name ?? $"arg{parameter.Position}",
                        Position = parameter.Position,
                        TypeName = GetTypeName(parameter.ParameterType),
                        BindingSource = "Special",
                        IsCancellationToken = true,
                    };
                }

                var apiParameter = description.ParameterDescriptions.FirstOrDefault(item =>
                                       item.ParameterDescriptor is ControllerParameterDescriptor descriptor
                                       && descriptor.ParameterInfo.Position == parameter.Position)
                                   ?? description.ParameterDescriptions.FirstOrDefault(item =>
                                       item.ParameterDescriptor?.Name == parameter.Name || item.Name == parameter.Name);
                return new BingDynamicApiParameterDefinition
                {
                    Name = apiParameter?.Name ?? parameter.Name ?? $"arg{parameter.Position}",
                    Position = parameter.Position,
                    TypeName = GetTypeName(parameter.ParameterType),
                    BindingSource = apiParameter?.Source?.Id ?? string.Empty,
                    IsOptional = parameter.IsOptional || parameter.HasDefaultValue,
                    IsCancellationToken = false,
                    IsFile = typeof(Bing.Content.IRemoteStreamContent).IsAssignableFrom(parameter.ParameterType)
                             || typeof(Microsoft.AspNetCore.Http.IFormFile).IsAssignableFrom(parameter.ParameterType),
                };
            })
            .ToList();

        var returnType = contractMethod.ReturnType;
        var payloadType = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            ? returnType.GetGenericArguments()[0]
            : typeof(void);
        var route = action.AttributeRouteInfo?.Template ?? description.RelativePath ?? string.Empty;
        return (registration.ServiceInterface, registration, new BingDynamicApiMethodDefinition
        {
            MethodName = contractMethod.Name,
            HttpMethod = description.HttpMethod ?? string.Empty,
            RouteTemplate = route,
            ReturnTypeName = GetTypeName(payloadType),
            IsStreamResult = typeof(Bing.Content.IRemoteStreamContent).IsAssignableFrom(payloadType),
            Parameters = parameters,
        });
    }

    /// <summary>
    /// 获取描述协议中的类型名称。
    /// </summary>
    /// <param name="type">待描述的类型。</param>
    /// <returns>类型的字符串表示。</returns>
    private static string GetTypeName(Type type) => type.ToString();
}
