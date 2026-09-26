using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Bing.AspNetCore.Mvc.DynamicApi;

/// <summary>
/// 配置动态服务的 MVC 路由、绑定与端点元数据。
/// </summary>
internal sealed class BingDynamicApiApplicationModelConvention : IApplicationModelConvention
{
    /// <summary>
    /// 提供当前宿主的动态服务注册信息。
    /// </summary>
    private readonly BingDynamicApiRegistry _registry;

    /// <summary>
    /// 初始化 BingDynamicApiApplicationModelConvention 类的新实例。
    /// </summary>
    /// <param name="registry">动态服务注册表。</param>
    public BingDynamicApiApplicationModelConvention(BingDynamicApiRegistry registry) => _registry = registry;

    /// <inheritdoc />
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            var registration = _registry.Find(controller.ControllerType);
            if (registration == null)
                continue;

            ConfigureController(controller, registration);
        }

        ValidateRouteConflicts(application);
    }

    /// <summary>
    /// 将应用服务契约配置为 MVC 控制器模型。
    /// </summary>
    /// <param name="controller">控制器模型。</param>
    /// <param name="registration">动态服务注册信息。</param>
    private void ConfigureController(ControllerModel controller, BingDynamicApiServiceRegistration registration)
    {
        foreach (var action in controller.Actions.Where(action => !registration.ActionMethods.ContainsKey(action.ActionMethod.MetadataToken)).ToArray())
            controller.Actions.Remove(action);
        controller.Selectors.Clear();
        controller.ApiExplorer.IsVisible = true;

        var serviceRoute = registration.GetServiceRoute();
        var controllerSelector = new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(serviceRoute)),
        };
        controller.Selectors.Add(controllerSelector);

        foreach (var implementationAction in controller.Actions.ToArray())
        {
            var implementationMethod = implementationAction.ActionMethod;
            var contractMethod = registration.ActionMethods[implementationMethod.MetadataToken];
            var methodRoute = GetEffectiveMethodRoute(
                implementationMethod,
                contractMethod,
                registration.MethodRouteFactory(contractMethod));
            var action = CreateContractActionModel(implementationAction, registration.ServiceInterface, contractMethod);
            controller.Actions.Remove(implementationAction);
            controller.Actions.Add(action);
            ConfigureParameters(action, contractMethod, methodRoute);
            ConfigureActionSelectors(action, controllerSelector, registration.ServiceInterface, implementationMethod, contractMethod, methodRoute);
        }
    }

    /// <summary>
    /// 选择方法的有效路由模板。
    /// </summary>
    /// <param name="implementationMethod">实现方法。</param>
    /// <param name="contractMethod">契约方法。</param>
    /// <param name="defaultRoute">默认方法路由。</param>
    /// <returns>显式特性指定的模板或默认路由。</returns>
    private static string GetEffectiveMethodRoute(MethodInfo implementationMethod, MethodInfo contractMethod, string defaultRoute)
    {
        var httpRoute = GetMethodAttributes<HttpMethodAttribute>(implementationMethod, contractMethod)
            .Select(attribute => attribute.Template)
            .FirstOrDefault(template => template != null);
        if (httpRoute != null)
            return httpRoute;

        return GetMethodAttributes<RouteAttribute>(implementationMethod, contractMethod)
                   .Select(attribute => attribute.Template)
                   .FirstOrDefault(template => template != null)
               ?? defaultRoute;
    }

    /// <summary>
    /// 基于服务契约构建操作模型。
    /// </summary>
    /// <param name="source">实现方法的操作模型。</param>
    /// <param name="serviceContract">服务契约类型。</param>
    /// <param name="contractMethod">契约方法。</param>
    /// <returns>合并实现元数据与契约参数后的操作模型。</returns>
    private static ActionModel CreateContractActionModel(ActionModel source, Type serviceContract, MethodInfo contractMethod)
    {
        var attributes = source.Attributes
            .Concat(contractMethod.GetCustomAttributes(true).Cast<object>())
            .ToArray();
        var action = new ActionModel(contractMethod, attributes)
        {
            Controller = source.Controller,
            ApiExplorer = new ApiExplorerModel(source.ApiExplorer),
            RouteParameterTransformer = source.RouteParameterTransformer,
        };

        foreach (var routeValue in source.RouteValues)
            action.RouteValues[routeValue.Key] = routeValue.Value;
        foreach (var property in source.Properties)
            action.Properties[property.Key] = property.Value;
        foreach (var filter in source.Filters)
            action.Filters.Add(filter);

        var implementationParameters = source.Parameters.ToDictionary(parameter => parameter.ParameterInfo.Position);
        foreach (var parameter in contractMethod.GetParameters())
        {
            if (!implementationParameters.TryGetValue(parameter.Position, out var implementationParameter))
                throw new InvalidOperationException($"动态 API 契约方法 {contractMethod.DeclaringType?.FullName}.{contractMethod.Name} 与实现签名不一致。");

            var parameterAttributes = implementationParameter.Attributes
                .Concat(parameter.GetCustomAttributes(true).Cast<object>())
                .Distinct()
                .ToArray();
            var parameterModel = new ParameterModel(parameter, parameterAttributes)
            {
                Action = action,
                ParameterName = parameter.Name,
            };
            parameterModel.BindingInfo = MergeBindingInfo(
                parameterModel.BindingInfo,
                BindingInfo.GetBindingInfo(parameter.GetCustomAttributes(true).Cast<object>()));
            if (!string.Equals(parameter.Name, implementationParameter.ParameterInfo.Name, StringComparison.Ordinal))
            {
                parameterModel.BindingInfo ??= new BindingInfo();
                parameterModel.BindingInfo.BinderModelName ??= parameter.Name;
            }
            action.Parameters.Add(parameterModel);
        }

        return action;
    }

    /// <summary>
    /// 合并参数绑定配置。
    /// </summary>
    /// <param name="implementation">实现侧配置，可为 null。</param>
    /// <param name="contract">契约侧配置，可为 null。</param>
    /// <returns>优先采用契约配置的绑定信息；两侧均为空时返回 null。</returns>
    private static BindingInfo MergeBindingInfo(BindingInfo implementation, BindingInfo contract)
    {
        if (implementation == null)
            return contract;
        if (contract == null)
            return implementation;

        implementation.BindingSource = contract.BindingSource ?? implementation.BindingSource;
        implementation.BinderModelName = contract.BinderModelName ?? implementation.BinderModelName;
        implementation.BinderType = contract.BinderType ?? implementation.BinderType;
        implementation.PropertyFilterProvider = contract.PropertyFilterProvider ?? implementation.PropertyFilterProvider;
        implementation.RequestPredicate = contract.RequestPredicate ?? implementation.RequestPredicate;
        implementation.EmptyBodyBehavior = contract.EmptyBodyBehavior;
        return implementation;
    }

    /// <summary>
    /// 配置并校验操作参数的绑定来源。
    /// </summary>
    /// <param name="action">操作模型。</param>
    /// <param name="contractMethod">契约方法。</param>
    /// <param name="routeTemplate">方法路由模板。</param>
    private static void ConfigureParameters(ActionModel action, MethodInfo contractMethod, string routeTemplate)
    {
        var routeParameterNames = Regex.Matches(routeTemplate, "\\{([^}:?=]+)")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var contractParameters = contractMethod.GetParameters().ToDictionary(parameter => parameter.Position);
        var parameterNames = action.Parameters
            .SelectMany(parameter =>
            {
                contractParameters.TryGetValue(parameter.ParameterInfo.Position, out var contractParameter);
                return new[]
                {
                    parameter.ParameterInfo.Name ?? string.Empty,
                    parameter.BindingInfo?.BinderModelName ?? string.Empty,
                    contractParameter?.Name ?? string.Empty,
                };
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unmatchedRouteParameters = routeParameterNames.Except(parameterNames, StringComparer.OrdinalIgnoreCase).ToArray();
        if (unmatchedRouteParameters.Length > 0)
        {
            throw new InvalidOperationException(
                $"动态 API 方法 {contractMethod.DeclaringType?.FullName}.{contractMethod.Name} 的路由参数 " +
                $"{string.Join(", ", unmatchedRouteParameters)} 没有对应的服务方法参数。");
        }

        foreach (var parameter in action.Parameters)
        {
            var parameterInfo = contractMethod.GetParameters()
                .FirstOrDefault(item => item.Position == parameter.ParameterInfo.Position);
            if (parameterInfo == null || parameterInfo.ParameterType == typeof(CancellationToken))
                continue;
            var type = parameterInfo.ParameterType;
            var routeParameter = routeParameterNames.Contains(parameterInfo.Name ?? string.Empty)
                                 || routeParameterNames.Contains(parameter.ParameterInfo.Name ?? string.Empty)
                                 || routeParameterNames.Contains(parameter.BindingInfo?.BinderModelName ?? string.Empty);
            if (routeParameter && !IsSimple(type) && type != typeof(object))
            {
                throw new InvalidOperationException(
                    $"动态 API 方法 {contractMethod.DeclaringType?.FullName}.{contractMethod.Name} 的路径参数 " +
                    $"{parameterInfo.Name} 必须是简单类型。");
            }
            if (routeParameter && parameter.BindingInfo?.BindingSource != null
                                && parameter.BindingInfo.BindingSource != BindingSource.Path)
            {
                throw new InvalidOperationException(
                    $"动态 API 方法 {contractMethod.DeclaringType?.FullName}.{contractMethod.Name} 的路径参数 " +
                    $"{parameterInfo.Name} 必须使用路径绑定来源。");
            }
            if (parameter.BindingInfo?.BindingSource != null)
            {
                if (routeParameter)
                    parameter.BindingInfo.BinderType = typeof(BingDynamicApiRouteValueModelBinder);
                continue;
            }

            var source = routeParameter
                ? BindingSource.Path
                : IsFile(type)
                    ? BindingSource.FormFile
                    : IsSimple(type)
                        ? BindingSource.Query
                        : BindingSource.Body;
            parameter.BindingInfo ??= new BindingInfo();
            parameter.BindingInfo.BindingSource = source;
            if (routeParameter)
                parameter.BindingInfo.BinderType = typeof(BingDynamicApiRouteValueModelBinder);
        }

        ValidateUniqueBindingNames(action, contractMethod);
    }

    /// <summary>
    /// 校验同一绑定来源中的参数名称唯一性。
    /// </summary>
    /// <param name="action">操作模型。</param>
    /// <param name="contractMethod">契约方法。</param>
    private static void ValidateUniqueBindingNames(ActionModel action, MethodInfo contractMethod)
    {
        var duplicates = action.Parameters
            .Where(parameter => parameter.BindingInfo?.BindingSource != null)
            .Select(parameter => new
            {
                Source = GetBindingSourceGroup(parameter.BindingInfo.BindingSource),
                Name = parameter.BindingInfo.BinderModelName
                       ?? parameter.ParameterName
                       ?? parameter.ParameterInfo.Name
                       ?? $"arg{parameter.ParameterInfo.Position}",
            })
            .Where(parameter => parameter.Source != null)
            .GroupBy(parameter => parameter.Source, StringComparer.Ordinal)
            .SelectMany(source => source.GroupBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
                .Where(name => name.Count() > 1)
                .Select(name => new { Source = source.Key, Name = name.Key }))
            .FirstOrDefault();

        if (duplicates != null)
        {
            throw new InvalidOperationException(
                $"动态 API 方法 {contractMethod.DeclaringType?.FullName}.{contractMethod.Name} 的绑定来源 {duplicates.Source} " +
                $"存在重复参数名称 '{duplicates.Name}'，无法唯一绑定。请为每个参数配置不同的绑定名称或来源。");
        }
    }

    /// <summary>
    /// 获取用于名称冲突校验的绑定分组。
    /// </summary>
    /// <param name="source">绑定来源。</param>
    /// <returns>来源分组；请求体、服务或特殊参数返回 null。</returns>
    private static string GetBindingSourceGroup(BindingSource source)
    {
        if (source == BindingSource.Body || source == BindingSource.Services || source == BindingSource.Special)
            return null;
        if (source == BindingSource.Form || source == BindingSource.FormFile)
            return "Form";
        return source.Id;
    }

    /// <summary>
    /// 配置操作的路由、HTTP 动词和授权元数据。
    /// </summary>
    /// <param name="action">操作模型。</param>
    /// <param name="controllerSelector">控制器选择器。</param>
    /// <param name="serviceContract">服务契约类型。</param>
    /// <param name="implementationMethod">实现方法。</param>
    /// <param name="contractMethod">契约方法。</param>
    /// <param name="defaultRoute">默认方法路由。</param>
    private static void ConfigureActionSelectors(
        ActionModel action,
        SelectorModel controllerSelector,
        Type serviceContract,
        MethodInfo implementationMethod,
        MethodInfo contractMethod,
        string defaultRoute)
    {
        var httpAttributes = GetMethodAttributes<HttpMethodAttribute>(implementationMethod, contractMethod).ToArray();
        var routeAttributes = GetMethodAttributes<RouteAttribute>(implementationMethod, contractMethod).ToArray();
        var authorizationMetadata = serviceContract.GetCustomAttributes(true)
            .Concat(GetMethodAttributes<Attribute>(implementationMethod, contractMethod))
            .Where(attribute => attribute is IAuthorizeData || attribute is IAllowAnonymous)
            .ToArray();
        var fallbackRoute = routeAttributes.Select(attribute => attribute.Template).FirstOrDefault(template => template != null) ?? defaultRoute;

        action.Selectors.Clear();
        if (httpAttributes.Length == 0)
        {
            AddSelector(action, controllerSelector, fallbackRoute, BingDynamicApiRouteBuilder.GetHttpMethod(contractMethod), authorizationMetadata);
            return;
        }

        foreach (var attribute in httpAttributes)
        {
            var template = attribute.Template ?? fallbackRoute;
            foreach (var method in attribute.HttpMethods.DefaultIfEmpty(BingDynamicApiRouteBuilder.GetHttpMethod(contractMethod)))
                AddSelector(action, controllerSelector, template, method, authorizationMetadata);
        }
    }

    /// <summary>
    /// 获取契约和实现方法上的指定特性。
    /// </summary>
    /// <typeparam name="TAttribute">待获取的特性类型。</typeparam>
    /// <param name="implementationMethod">实现方法。</param>
    /// <param name="contractMethod">契约方法。</param>
    /// <returns>按契约侧、实现侧顺序排列的特性。</returns>
    private static IEnumerable<TAttribute> GetMethodAttributes<TAttribute>(MethodInfo implementationMethod, MethodInfo contractMethod)
        where TAttribute : Attribute
    {
        return contractMethod.GetCustomAttributes<TAttribute>(true)
            .Concat(implementationMethod.GetCustomAttributes<TAttribute>(true));
    }

    /// <summary>
    /// 添加动态操作的端点选择器。
    /// </summary>
    /// <param name="action">操作模型。</param>
    /// <param name="controllerSelector">控制器选择器。</param>
    /// <param name="methodTemplate">方法路由模板。</param>
    /// <param name="httpMethod">HTTP 动词。</param>
    /// <param name="endpointMetadata">需透传的端点元数据。</param>
    private static void AddSelector(
        ActionModel action,
        SelectorModel controllerSelector,
        string methodTemplate,
        string httpMethod,
        IEnumerable<object> endpointMetadata)
    {
        var actionSelector = new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(methodTemplate)),
        };
        actionSelector.EndpointMetadata.Add(new Bing.AspNetCore.Mvc.Filters.IgnoreResultHandlerAttribute());
        actionSelector.EndpointMetadata.Add(new ApiControllerAttribute());
        foreach (var metadata in endpointMetadata)
            actionSelector.EndpointMetadata.Add(metadata);
        actionSelector.ActionConstraints.Add(new HttpMethodActionConstraint(new[] { httpMethod }));
        action.Selectors.Add(actionSelector);
    }

    /// <summary>
    /// 校验涉及动态 API 的路由冲突。
    /// </summary>
    /// <param name="application">MVC 应用模型。</param>
    private void ValidateRouteConflicts(ApplicationModel application)
    {
        var endpoints = application.Controllers
            .SelectMany(controller =>
            {
                var controllerSelectors = controller.Selectors.Count == 0
                    ? new[] { new SelectorModel() }
                    : controller.Selectors.ToArray();
                return controller.Actions.SelectMany(action => action.Selectors
                    .SelectMany(selector => controllerSelectors.Select(controllerSelector => new
                    {
                        Controller = controller.ControllerType,
                        Action = action.ActionMethod,
                        Route = GetEffectiveRoute(controllerSelector, selector),
                        Methods = selector.ActionConstraints.OfType<HttpMethodActionConstraint>()
                            .SelectMany(constraint => constraint.HttpMethods)
                            .Select(method => method.ToUpperInvariant())
                            .ToHashSet(StringComparer.OrdinalIgnoreCase),
                    })));
            })
            .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Route))
            .ToArray();

        for (var leftIndex = 0; leftIndex < endpoints.Length; leftIndex++)
        {
            var left = endpoints[leftIndex];
            for (var rightIndex = leftIndex + 1; rightIndex < endpoints.Length; rightIndex++)
            {
                var right = endpoints[rightIndex];
                if (!RoutesMatch(left.Route!, right.Route!)
                    && !IsDefinitionRouteCollision(left.Controller, left.Route!, right.Controller, right.Route!))
                    continue;
                if (left.Methods.Count > 0 && right.Methods.Count > 0 && !left.Methods.Overlaps(right.Methods))
                    continue;
                if (!_registry.IsDynamicEndpoint(left.Controller) && !_registry.IsDynamicEndpoint(right.Controller))
                    continue;

                throw new InvalidOperationException(
                    $"动态 API 路由冲突：{left.Methods.ExpandToString()} {left.Route} ({left.Controller.Name}.{left.Action.Name}) 与 " +
                    $"{right.Methods.ExpandToString()} {right.Route} ({right.Controller.Name}.{right.Action.Name}) 重复。请调整动态 API 路由或显式 Controller 路由。");
            }
        }
    }

    /// <summary>
    /// 比较路由模板是否具有相同结构。
    /// </summary>
    /// <param name="left">第一个路由。</param>
    /// <param name="right">第二个路由。</param>
    /// <returns>忽略参数名称和大小写后相同时返回 true，否则返回 false。</returns>
    private static bool RoutesMatch(string left, string right)
    {
        static string Normalize(string route) => Regex.Replace(route.Trim('/'), "\\{[^}]+\\}", "{}").ToLowerInvariant();
        return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断路由是否与 API 描述端点冲突。
    /// </summary>
    /// <param name="leftController">第一个控制器类型。</param>
    /// <param name="leftRoute">第一个路由。</param>
    /// <param name="rightController">第二个控制器类型。</param>
    /// <param name="rightRoute">第二个路由。</param>
    /// <returns>与描述端点匹配时返回 true，否则返回 false。</returns>
    private static bool IsDefinitionRouteCollision(Type leftController, string leftRoute, Type rightController, string rightRoute)
    {
        const string descriptionRoute = "api/dynamic-api/{version}/definition";
        var leftIsDescription = leftController == typeof(BingDynamicApiDescriptionController);
        var rightIsDescription = rightController == typeof(BingDynamicApiDescriptionController);
        if (!leftIsDescription && !rightIsDescription)
            return false;

        var otherRoute = leftIsDescription ? rightRoute : leftRoute;
        var expected = descriptionRoute.Split('/');
        var candidate = otherRoute.Trim('/').Split('/');
        if (candidate.Length != expected.Length)
            return false;

        for (var index = 0; index < expected.Length; index++)
        {
            if (expected[index].StartsWith("{", StringComparison.Ordinal))
                continue;
            if (!string.Equals(expected[index], candidate[index], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 合并控制器与操作路由。
    /// </summary>
    /// <param name="controllerSelector">控制器选择器，可为 null。</param>
    /// <param name="actionSelector">操作选择器。</param>
    /// <returns>合并后的模板；没有特性路由时返回 null。</returns>
    private static string GetEffectiveRoute(SelectorModel controllerSelector, SelectorModel actionSelector)
    {
        var route = AttributeRouteModel.CombineAttributeRouteModel(
            controllerSelector?.AttributeRouteModel,
            actionSelector.AttributeRouteModel);
        return route?.Template;
    }

    /// <summary>
    /// 判断参数是否为上传文件。
    /// </summary>
    /// <param name="type">参数类型。</param>
    /// <returns>为远程流或表单文件时返回 true，否则返回 false。</returns>
    private static bool IsFile(Type type) => typeof(Bing.Content.IRemoteStreamContent).IsAssignableFrom(type)
                                               || typeof(Microsoft.AspNetCore.Http.IFormFile).IsAssignableFrom(type);

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
}

/// <summary>
/// 提供动态 API 诊断文本的集合扩展。
/// </summary>
internal static class DynamicApiCollectionExtensions
{
    /// <summary>
    /// 拼接集合中的诊断文本。
    /// </summary>
    /// <param name="values">待拼接的文本。</param>
    /// <returns>以逗号分隔的文本。</returns>
    public static string ExpandToString(this IEnumerable<string> values) => string.Join(", ", values);
}
