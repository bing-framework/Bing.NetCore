using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bing.Application.Services;
using Bing.DynamicApi.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 保存动态服务的契约、路由和操作映射。
/// </summary>
internal sealed class BingDynamicApiServiceRegistration
{
    /// <summary>
    /// 获取服务实现类型。
    /// </summary>
    public Type ControllerType { get; }
    /// <summary>
    /// 获取服务契约类型。
    /// </summary>
    public Type ServiceInterface { get; }
    /// <summary>
    /// 获取服务路由名称。
    /// </summary>
    public string ServiceName { get; }
    /// <summary>
    /// 获取路由前缀。
    /// </summary>
    public string RoutePrefix { get; }
    /// <summary>
    /// 获取 API 版本。
    /// </summary>
    public string ApiVersion { get; }
    /// <summary>
    /// 获取是否在路由中包含版本。
    /// </summary>
    public bool IncludeVersionInRoute { get; }
    /// <summary>
    /// 获取方法路由计算函数。
    /// </summary>
    public Func<MethodInfo, string> MethodRouteFactory { get; }
    /// <summary>
    /// 获取实现方法标记到契约方法的映射。
    /// </summary>
    public IReadOnlyDictionary<int, MethodInfo> ActionMethods { get; }
    /// <summary>
    /// 获取契约方法标记到契约方法的映射。
    /// </summary>
    public IReadOnlyDictionary<int, MethodInfo> ContractMethods { get; }

    /// <summary>
    /// 初始化 BingDynamicApiServiceRegistration 类的新实例。
    /// </summary>
    /// <param name="controllerType">服务实现类型。</param>
    /// <param name="serviceInterface">服务契约类型。</param>
    /// <param name="options">服务发现与路由选项。</param>
    public BingDynamicApiServiceRegistration(Type controllerType, Type serviceInterface, BingDynamicApiOptions options)
    {
        ControllerType = controllerType;
        ServiceInterface = serviceInterface;
        ServiceName = options.ServiceNameFactory?.Invoke(serviceInterface) ?? BingDynamicApiRouteBuilder.NormalizeServiceName(serviceInterface.Name);
        RoutePrefix = NormalizePrefix(options.RoutePrefix);
        ApiVersion = NormalizeVersion(options.ApiVersion);
        IncludeVersionInRoute = options.IncludeVersionInRoute;
        MethodRouteFactory = options.MethodRouteFactory ?? BingDynamicApiRouteBuilder.BuildMethodRoute;
        ActionMethods = GetActionMethods(controllerType, serviceInterface);
        ContractMethods = ActionMethods.Values.ToDictionary(method => method.MetadataToken);
    }

    /// <summary>
    /// 构建服务路由。
    /// </summary>
    /// <returns>包含前缀、可选版本和服务名称的路由。</returns>
    public string GetServiceRoute()
    {
        var version = IncludeVersionInRoute ? $"/{ApiVersion}" : string.Empty;
        return $"{RoutePrefix}{version}/{ServiceName}";
    }

    /// <summary>
    /// 筛选并校验可暴露的服务操作。
    /// </summary>
    /// <param name="controllerType">服务实现类型。</param>
    /// <param name="serviceInterface">服务契约类型。</param>
    /// <returns>实现方法标记到契约方法的映射。</returns>
    private static IReadOnlyDictionary<int, MethodInfo> GetActionMethods(Type controllerType, Type serviceInterface)
    {
        var map = controllerType.GetInterfaceMap(serviceInterface);
        var methods = new Dictionary<int, MethodInfo>();
        for (var index = 0; index < map.InterfaceMethods.Length; index++)
        {
            var contractMethod = map.InterfaceMethods[index];
            var targetMethod = map.TargetMethods[index];
            if (contractMethod.IsSpecialName)
                continue;
            if (contractMethod.GetCustomAttribute<DisableBingDynamicApiAttribute>(true) != null
                || targetMethod.GetCustomAttribute<DisableBingDynamicApiAttribute>(true) != null)
                continue;
            if (!targetMethod.IsPublic || targetMethod.IsStatic)
                throw new InvalidOperationException(
                    $"动态 API 方法 {controllerType.FullName}.{targetMethod.Name} 必须是公开的实例方法。");
            if (contractMethod.IsGenericMethodDefinition || contractMethod.ContainsGenericParameters)
                throw new InvalidOperationException(
                    $"动态 API 方法 {serviceInterface.FullName}.{contractMethod.Name} 不支持泛型方法。");
            if (!IsSupportedReturnType(targetMethod.ReturnType))
            {
                throw new InvalidOperationException(
                    $"动态 API 方法 {controllerType.FullName}.{targetMethod.Name} 必须返回 Task、Task<T> 或远程流内容。");
            }
            EnsureSingleBodyParameter(targetMethod, contractMethod);
            methods[targetMethod.MetadataToken] = contractMethod;
        }
        return methods;
    }

    /// <summary>
    /// 判断返回类型是否受动态 API 支持。
    /// </summary>
    /// <param name="returnType">方法返回类型。</param>
    /// <returns>为 Task 或 Task&lt;T&gt; 时返回 true，否则返回 false。</returns>
    private static bool IsSupportedReturnType(Type returnType)
    {
        if (returnType == typeof(Task))
            return true;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            return true;
        return false;
    }

    /// <summary>
    /// 校验操作最多包含一个 JSON 请求体参数。
    /// </summary>
    /// <param name="implementationMethod">实现方法。</param>
    /// <param name="contractMethod">契约方法。</param>
    private static void EnsureSingleBodyParameter(MethodInfo implementationMethod, MethodInfo contractMethod)
    {
        var implementationParameters = implementationMethod.GetParameters()
            .ToDictionary(parameter => parameter.Position);
        var bodyParameters = contractMethod.GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
            .Where(parameter =>
            {
                var implementationParameter = implementationParameters[parameter.Position];
                var bindingSources = parameter.GetCustomAttributes(true)
                    .Concat(implementationParameter.GetCustomAttributes(true))
                    .OfType<IBindingSourceMetadata>()
                    .Select(attribute => attribute.BindingSource)
                    .Where(source => source != null)
                    .ToArray();
                return bindingSources.Length == 0
                    ? !IsSimple(parameter.ParameterType) && !IsFile(parameter.ParameterType)
                    : bindingSources.Contains(BindingSource.Body);
            })
            .ToArray();
        if (bodyParameters.Length > 1)
        {
            throw new InvalidOperationException(
                $"动态 API 方法 {contractMethod.DeclaringType?.FullName}.{contractMethod.Name} 包含多个请求体参数；每个 Action 最多支持一个 JSON 请求体参数。");
        }
    }

    /// <summary>
    /// 判断参数是否可按简单值绑定。
    /// </summary>
    /// <param name="type">参数类型。</param>
    /// <returns>支持简单值绑定时返回 true，否则返回 false。</returns>
    private static bool IsSimple(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
               || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid)
               || type == typeof(TimeSpan) || type == typeof(Uri);
    }

    /// <summary>
    /// 判断参数是否为上传文件。
    /// </summary>
    /// <param name="type">参数类型。</param>
    /// <returns>为远程流或表单文件时返回 true，否则返回 false。</returns>
    private static bool IsFile(Type type) => typeof(Bing.Content.IRemoteStreamContent).IsAssignableFrom(type)
                                               || typeof(Microsoft.AspNetCore.Http.IFormFile).IsAssignableFrom(type);

    /// <summary>
    /// 规范化路由前缀。
    /// </summary>
    /// <param name="value">待规范化的前缀。</param>
    /// <returns>去除首尾空白和斜杠的非空前缀。</returns>
    internal static string NormalizePrefix(string value)
    {
        var prefix = (value ?? string.Empty).Trim().Trim('/');
        if (prefix.Length == 0)
            throw new ArgumentException("动态 API 路由前缀不能为空。", nameof(value));
        return prefix;
    }

    /// <summary>
    /// 规范化 API 版本路由段。
    /// </summary>
    /// <param name="value">待规范化的版本。</param>
    /// <returns>非空的单个版本路由段。</returns>
    internal static string NormalizeVersion(string value)
    {
        var version = (value ?? string.Empty).Trim().Trim('/');
        if (version.Length == 0 || version.Contains('/') || version.Contains('{') || version.Contains('}'))
            throw new ArgumentException("动态 API 版本必须是非空的单个路由段。", nameof(value));
        return version;
    }
}

/// <summary>
/// 管理当前宿主的动态服务注册信息。
/// </summary>
internal sealed class BingDynamicApiRegistry
{
    /// <summary>
    /// 保存已发现且通过校验的动态服务。
    /// </summary>
    private readonly List<BingDynamicApiServiceRegistration> _services = new();

    /// <summary>
    /// 获取已注册的动态服务。
    /// </summary>
    public IReadOnlyList<BingDynamicApiServiceRegistration> Services => _services;

    /// <summary>
    /// 发现并注册程序集中的动态服务。
    /// </summary>
    /// <param name="assembly">应用服务程序集。</param>
    /// <param name="options">发现与路由选项。</param>
    public void AddAssembly(Assembly assembly, BingDynamicApiOptions options)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        foreach (var implementationType in assembly.GetExportedTypes()
                     .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
                     .Where(type => typeof(IAppService).IsAssignableFrom(type))
                     .Where(type => !IsMvcController(type))
                     .Where(type => type.GetCustomAttribute<DisableBingDynamicApiAttribute>(true) == null)
                     .Where(type => options.TypePredicate?.Invoke(type) != false))
        {
            var interfaces = implementationType.GetInterfaces()
                .Where(type => type != typeof(IAppService) && typeof(IAppService).IsAssignableFrom(type))
                .Where(type => type.GetCustomAttribute<DisableBingDynamicApiAttribute>(true) == null)
                .Where(type => type.GetMethods().Any(method => !method.IsSpecialName))
                .Where(type => !type.IsGenericTypeDefinition)
                .Where(type => !implementationType.GetInterfaces().Any(other => other != type && type.IsAssignableFrom(other)))
                .ToArray();

            if (interfaces.Length > 1)
            {
                throw new InvalidOperationException(
                    $"动态 API 服务 {implementationType.FullName} 实现了多个顶层 IAppService 契约；请使用 TypePredicate 选择单一服务类型。");
            }
            if (interfaces.Length == 0)
                continue;

            var registration = new BingDynamicApiServiceRegistration(implementationType, interfaces[0], options);
            var existing = _services.FirstOrDefault(service => service.ControllerType == implementationType);
            if (existing != null)
            {
                if (string.Equals(existing.ApiVersion, registration.ApiVersion, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.ServiceName, registration.ServiceName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.RoutePrefix, registration.RoutePrefix, StringComparison.OrdinalIgnoreCase)
                    && existing.IncludeVersionInRoute == registration.IncludeVersionInRoute
                    && existing.MethodRouteFactory.Equals(registration.MethodRouteFactory))
                    continue;

                throw new InvalidOperationException(
                    $"动态 API 服务 {implementationType.FullName} 已按版本 {existing.ApiVersion} 注册，不能在同一 MVC 控制器类型上重复配置版本或路由。请为不同版本使用独立服务类型或独立宿主。");
            }
            if (registration.ActionMethods.Count == 0)
                continue;
            _services.Add(registration);
        }
    }

    /// <summary>
    /// 判断类型是否已声明为 MVC 控制器。
    /// </summary>
    /// <param name="type">待检查的类型。</param>
    /// <returns>符合 MVC 控制器约定时返回 true，否则返回 false。</returns>
    private static bool IsMvcController(Type type) =>
        typeof(ControllerBase).IsAssignableFrom(type)
        || type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
        || type.GetCustomAttribute<ControllerAttribute>(true) != null
        || type.GetCustomAttribute<ApiControllerAttribute>(true) != null;

    /// <summary>
    /// 判断类型是否为已注册的动态控制器。
    /// </summary>
    /// <param name="controllerType">控制器类型。</param>
    /// <returns>已注册时返回 true，否则返回 false。</returns>
    public bool IsDynamicController(Type controllerType) => _services.Any(service => service.ControllerType == controllerType);

    /// <summary>
    /// 判断控制器是否提供动态 API 端点。
    /// </summary>
    /// <param name="controllerType">控制器类型。</param>
    /// <returns>为动态服务或描述控制器时返回 true，否则返回 false。</returns>
    public bool IsDynamicEndpoint(Type controllerType) =>
        controllerType == typeof(BingDynamicApiDescriptionController) || IsDynamicController(controllerType);

    /// <summary>
    /// 查找控制器的动态服务注册。
    /// </summary>
    /// <param name="controllerType">控制器类型。</param>
    /// <returns>匹配的注册；未注册时返回 null。</returns>
    public BingDynamicApiServiceRegistration Find(Type controllerType) =>
        _services.FirstOrDefault(service => service.ControllerType == controllerType);

    /// <summary>
    /// 获取指定版本的动态服务。
    /// </summary>
    /// <param name="apiVersion">API 版本，不区分大小写。</param>
    /// <returns>该版本的服务注册列表。</returns>
    public IReadOnlyList<BingDynamicApiServiceRegistration> GetServices(string apiVersion) =>
        _services.Where(service => string.Equals(service.ApiVersion, apiVersion, StringComparison.OrdinalIgnoreCase)).ToArray();
}

/// <summary>
/// 将动态服务和描述控制器加入 MVC 控制器发现结果。
/// </summary>
internal sealed class BingDynamicApiControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    /// <summary>
    /// 提供当前宿主的动态服务注册信息。
    /// </summary>
    private readonly BingDynamicApiRegistry _registry;

    /// <summary>
    /// 初始化 BingDynamicApiControllerFeatureProvider 类的新实例。
    /// </summary>
    /// <param name="registry">动态服务注册表。</param>
    public BingDynamicApiControllerFeatureProvider(BingDynamicApiRegistry registry) => _registry = registry;

    /// <inheritdoc />
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var registration in _registry.Services)
        {
            var typeInfo = registration.ControllerType.GetTypeInfo();
            if (!feature.Controllers.Contains(typeInfo))
                feature.Controllers.Add(typeInfo);
        }

        var descriptionController = typeof(BingDynamicApiDescriptionController).GetTypeInfo();
        if (!feature.Controllers.Contains(descriptionController))
            feature.Controllers.Add(descriptionController);
    }
}
