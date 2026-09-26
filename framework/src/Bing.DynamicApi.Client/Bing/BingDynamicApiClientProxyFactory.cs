using Bing.Application.Services;
using Bing.Content;
using Bing.DynamicApi.Contracts;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;

namespace Bing.DynamicApi.Client;

/// <summary>
/// 创建并执行应用服务的动态 HTTP 代理。
/// </summary>
internal sealed class BingDynamicApiClientProxyFactory
{
    /// <summary>
    /// 提供远程连接与序列化选项。
    /// </summary>
    private readonly IOptions<BingDynamicApiClientOptions> _options;
    /// <summary>
    /// 获取服务描述并发送配置化 HTTP 请求。
    /// </summary>
    private readonly BingDynamicApiDefinitionProvider _definitionProvider;
    /// <summary>
    /// 将路径、查询及表单参数转换为字符串。
    /// </summary>
    private readonly IBingDynamicApiParameterConverter _parameterConverter;
    /// <summary>
    /// 序列化请求体并反序列化 JSON 响应。
    /// </summary>
    private readonly IBingDynamicApiJsonSerializer _serializer;

    /// <summary>
    /// 初始化 BingDynamicApiClientProxyFactory 类的新实例。
    /// </summary>
    /// <param name="options">客户端选项。</param>
    /// <param name="definitionProvider">远程 API 描述与请求提供器。</param>
    /// <param name="parameterConverter">参数转换器。</param>
    /// <param name="serializer">JSON 序列化器。</param>
    public BingDynamicApiClientProxyFactory(
        IOptions<BingDynamicApiClientOptions> options,
        BingDynamicApiDefinitionProvider definitionProvider,
        IBingDynamicApiParameterConverter parameterConverter,
        IBingDynamicApiJsonSerializer serializer)
    {
        _options = options;
        _definitionProvider = definitionProvider;
        _parameterConverter = parameterConverter;
        _serializer = serializer;
    }

    /// <summary>
    /// 创建应用服务接口的远程代理。
    /// </summary>
    /// <param name="serviceType">继承 IAppService 的接口类型。</param>
    /// <param name="remoteName">远程服务名称。</param>
    /// <returns>已初始化的服务接口代理。</returns>
    public object Create(Type serviceType, string remoteName)
    {
        if (!serviceType.IsInterface || !typeof(IAppService).IsAssignableFrom(serviceType))
            throw new ArgumentException($"动态 API 客户端类型 '{serviceType.FullName}' 必须是继承 {nameof(IAppService)} 的接口。", nameof(serviceType));

        var createMethod = typeof(DispatchProxy)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(x => x.Name == "Create" && x.IsGenericMethodDefinition && x.GetGenericArguments().Length == 2);
        var proxy = createMethod.MakeGenericMethod(serviceType, typeof(BingDynamicApiDispatchProxy)).Invoke(null, null);
        ((BingDynamicApiDispatchProxy)proxy).Initialize(this, serviceType, remoteName);
        return proxy;
    }

    /// <summary>
    /// 分派服务接口的异步调用。
    /// </summary>
    /// <param name="serviceType">服务接口类型。</param>
    /// <param name="remoteName">远程服务名称。</param>
    /// <param name="targetMethod">调用的契约方法。</param>
    /// <param name="args">方法参数值。</param>
    /// <returns>与契约返回类型一致的异步任务。</returns>
    internal object Invoke(Type serviceType, string remoteName, MethodInfo targetMethod, object[] args)
    {
        var returnType = targetMethod.ReturnType;
        if (returnType == typeof(Task))
            return InvokeVoidAsync(serviceType, remoteName, targetMethod, args);
        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
            throw new NotSupportedException($"动态 API 客户端方法 '{targetMethod.Name}' 必须返回 Task 或 Task<T>；实际返回 '{returnType.FullName}'。");

        var resultType = returnType.GetGenericArguments()[0];
        return typeof(BingDynamicApiClientProxyFactory)
            .GetMethod(nameof(InvokeResultAsync), BindingFlags.Instance | BindingFlags.NonPublic)
            .MakeGenericMethod(resultType)
            .Invoke(this, new object[] { serviceType, remoteName, targetMethod, args });
    }

    /// <summary>
    /// 异步执行无返回值的远程调用。
    /// </summary>
    /// <param name="serviceType">服务接口类型。</param>
    /// <param name="remoteName">远程服务名称。</param>
    /// <param name="targetMethod">调用的契约方法。</param>
    /// <param name="args">方法参数值。</param>
    private async Task InvokeVoidAsync(Type serviceType, string remoteName, MethodInfo targetMethod, object[] args)
    {
        await ExecuteAsync(serviceType, remoteName, targetMethod, args, typeof(void)).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步执行带返回值的远程调用。
    /// </summary>
    /// <typeparam name="TResult">契约返回结果类型。</typeparam>
    /// <param name="serviceType">服务接口类型。</param>
    /// <param name="remoteName">远程服务名称。</param>
    /// <param name="targetMethod">调用的契约方法。</param>
    /// <param name="args">方法参数值。</param>
    /// <returns>远程结果；没有结果时返回类型默认值。</returns>
    private async Task<TResult> InvokeResultAsync<TResult>(Type serviceType, string remoteName, MethodInfo targetMethod, object[] args)
    {
        var result = await ExecuteAsync(serviceType, remoteName, targetMethod, args, typeof(TResult)).ConfigureAwait(false);
        return result == null ? default : (TResult)result;
    }

    /// <summary>
    /// 异步构造并执行远程 HTTP 调用。
    /// </summary>
    /// <param name="serviceType">服务接口类型。</param>
    /// <param name="remoteName">远程服务名称。</param>
    /// <param name="targetMethod">调用的契约方法。</param>
    /// <param name="args">方法参数值，可为 null。</param>
    /// <param name="resultType">目标结果类型。</param>
    /// <returns>远程结果；无返回值或空引用响应时返回 null。</returns>
    /// <remarks>流结果交由调用方释放；普通响应在读取结果后释放。</remarks>
    private async Task<object> ExecuteAsync(Type serviceType, string remoteName, MethodInfo targetMethod, object[] args, Type resultType)
    {
        args ??= Array.Empty<object>();
        var remoteOptions = _options.Value.GetRemoteService(remoteName);
        var cancellationToken = GetCancellationToken(targetMethod, args);
        var definition = await _definitionProvider.GetAsync(remoteName, remoteOptions, cancellationToken).ConfigureAwait(false);

        var service = definition.Services.SingleOrDefault(x => string.Equals(x.ServiceTypeName, serviceType.FullName, StringComparison.Ordinal));
        if (service == null)
            throw new InvalidOperationException($"动态 API 定义中找不到服务接口 '{serviceType.FullName}'（远程服务 '{remoteName}'，版本 '{remoteOptions.ApiVersion}'）。");

        var method = FindMethod(service, targetMethod);
        var parameters = targetMethod.GetParameters();
        var values = method.Parameters.ToDictionary(x => x.Position, x => x.Position >= 0 && x.Position < args.Length ? args[x.Position] : null);
        var route = BuildRoute(method.RouteTemplate, method.Parameters, values, parameters, remoteOptions);
        var query = BuildQuery(method, values, parameters, remoteOptions);
        var url = BingDynamicApiDefinitionProvider.CombineUrl(remoteOptions.BaseUrl, route) + query;
        var requestContent = BuildContent(method, values, parameters, remoteOptions);

        if (method.IsStreamResult)
        {
            if (!typeof(IRemoteStreamContent).IsAssignableFrom(resultType))
                throw new NotSupportedException($"远程方法 '{method.MethodName}' 声明返回流，但接口返回类型 '{resultType.FullName}' 不是 {nameof(IRemoteStreamContent)}。");

            var response = await _definitionProvider.SendAsync(
                remoteName,
                remoteOptions,
                () => CreateRequest(method, url, requestContent),
                false,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                using (response)
                    throw await BingDynamicApiHttpErrors.CreateExceptionAsync(response, _options.Value.JsonSerializerOptions).ConfigureAwait(false);
            }

            try
            {
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var contentDisposition = response.Content.Headers.ContentDisposition;
                var fileName = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
                fileName = fileName?.Trim('"');
                var ownedStream = new ResponseOwnedStream(stream, response);
                return new RemoteStreamContent(
                    ownedStream,
                    fileName,
                    response.Content.Headers.ContentType?.ToString(),
                    response.Content.Headers.ContentLength);
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }

        using (requestContent)
        using (var response = await _definitionProvider.SendAsync(
                   remoteName,
                   remoteOptions,
                   () => CreateRequest(method, url, requestContent),
                   requestContent == null && string.Equals(method.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase),
                   cancellationToken).ConfigureAwait(false))
        {
            if (!response.IsSuccessStatusCode)
                throw await BingDynamicApiHttpErrors.CreateExceptionAsync(response, _options.Value.JsonSerializerOptions).ConfigureAwait(false);
            if (resultType == typeof(void))
                return null;
            if (response.Content == null)
                return resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (resultType == typeof(string) && IsTextMediaType(mediaType) && !IsJsonMediaType(mediaType))
                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength == 0)
                return resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
            return await _serializer.DeserializeAsync(response.Content, resultType, _options.Value.JsonSerializerOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 判断媒体类型是否表示 JSON。
    /// </summary>
    /// <param name="mediaType">媒体类型，可为 null。</param>
    /// <returns>匹配 JSON 媒体类型时返回 true，否则返回 false。</returns>
    private static bool IsJsonMediaType(string mediaType)
    {
        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
               || string.Equals(mediaType, "text/json", StringComparison.OrdinalIgnoreCase)
               || (mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>
    /// 判断媒体类型是否表示文本。
    /// </summary>
    /// <param name="mediaType">媒体类型，可为 null。</param>
    /// <returns>以 text/ 开头时返回 true，否则返回 false。</returns>
    private static bool IsTextMediaType(string mediaType)
    {
        return mediaType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// 提取方法参数中的取消令牌。
    /// </summary>
    /// <param name="method">契约方法。</param>
    /// <param name="args">方法参数值。</param>
    /// <returns>首个取消令牌参数的值；未提供时返回不可取消的令牌。</returns>
    private static CancellationToken GetCancellationToken(MethodInfo method, object[] args)
    {
        var parameters = method.GetParameters();
        for (var index = 0; index < parameters.Length && index < args.Length; index++)
        {
            if (parameters[index].ParameterType == typeof(CancellationToken))
                return args[index] is CancellationToken token ? token : CancellationToken.None;
        }
        return CancellationToken.None;
    }

    /// <summary>
    /// 按方法签名查找远程操作描述。
    /// </summary>
    /// <param name="service">远程服务描述。</param>
    /// <param name="targetMethod">契约方法。</param>
    /// <returns>唯一匹配的方法描述。</returns>
    private static BingDynamicApiMethodDefinition FindMethod(BingDynamicApiServiceDefinition service, MethodInfo targetMethod)
    {
        var reflectedParameters = targetMethod.GetParameters();
        var matches = service.Methods.Where(candidate =>
            string.Equals(candidate.MethodName, targetMethod.Name, StringComparison.Ordinal) &&
            ParametersMatch(candidate.Parameters, reflectedParameters)).ToArray();

        if (matches.Length == 0)
            throw new InvalidOperationException($"动态 API 定义中找不到方法 '{targetMethod.DeclaringType?.FullName}.{targetMethod.Name}' 的匹配签名。");
        if (matches.Length > 1)
            throw new InvalidOperationException($"动态 API 定义中的方法 '{targetMethod.Name}' 存在多个匹配签名，无法唯一选择。");
        return matches[0];
    }

    /// <summary>
    /// 比较远程参数描述与契约签名。
    /// </summary>
    /// <param name="definitions">远程参数描述。</param>
    /// <param name="parameters">契约参数。</param>
    /// <returns>位置、类型及取消令牌声明匹配时返回 true，否则返回 false。</returns>
    private static bool ParametersMatch(IReadOnlyCollection<BingDynamicApiParameterDefinition> definitions, ParameterInfo[] parameters)
    {
        var definedPositions = new HashSet<int>();
        foreach (var definition in definitions)
        {
            if (definition.Position < 0 || definition.Position >= parameters.Length)
                return false;
            if (!definedPositions.Add(definition.Position))
                return false;
            var type = parameters[definition.Position].ParameterType;
            if (definition.IsCancellationToken && type != typeof(CancellationToken))
                return false;
            if (!TypeNameMatches(definition.TypeName, type))
                return false;
        }

        var requiredPositions = parameters
            .Select((parameter, position) => (parameter, position))
            .Where(x => x.parameter.ParameterType != typeof(CancellationToken))
            .Select(x => x.position);
        return requiredPositions.All(definedPositions.Contains) &&
               definitions.All(x => parameters[x.Position].ParameterType != typeof(CancellationToken) || x.IsCancellationToken);
    }

    /// <summary>
    /// 比较描述中的类型名称与契约类型。
    /// </summary>
    /// <param name="declaredTypeName">远程类型名称，可为空。</param>
    /// <param name="type">契约参数类型。</param>
    /// <returns>名称匹配或未声明名称时返回 true，否则返回 false。</returns>
    private static bool TypeNameMatches(string declaredTypeName, Type type)
    {
        if (string.IsNullOrWhiteSpace(declaredTypeName))
            return true;

        if (string.Equals(declaredTypeName, type.FullName, StringComparison.Ordinal)
            || string.Equals(declaredTypeName, type.Name, StringComparison.Ordinal)
            || string.Equals(declaredTypeName, type.ToString(), StringComparison.Ordinal))
            return true;

        var name = declaredTypeName.Split(',')[0].Trim();
        return string.Equals(name, type.FullName, StringComparison.Ordinal) ||
               string.Equals(name, type.Name, StringComparison.Ordinal) ||
               string.Equals(name, type.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 将参数值绑定到远程路由模板。
    /// </summary>
    /// <param name="routeTemplate">远程路由模板。</param>
    /// <param name="definitions">参数描述。</param>
    /// <param name="values">按位置索引的参数值。</param>
    /// <param name="reflectedParameters">契约参数。</param>
    /// <param name="remoteOptions">远程服务配置。</param>
    /// <returns>经过转义且不含首尾斜杠的相对路径。</returns>
    private string BuildRoute(
        string routeTemplate,
        IReadOnlyCollection<BingDynamicApiParameterDefinition> definitions,
        IReadOnlyDictionary<int, object> values,
        ParameterInfo[] reflectedParameters,
        BingDynamicApiRemoteOptions remoteOptions)
    {
        var route = (routeTemplate ?? string.Empty).Trim();
        if (route.StartsWith("~/", StringComparison.Ordinal))
            route = route.Substring(2);
        route = route.TrimStart('/');
        var tokenPattern = new Regex("\\{(?<name>[^{}:?=]+)(?:[:=][^{}?]+)?(?<optional>\\?)?\\}", RegexOptions.Compiled);
        route = tokenPattern.Replace(route, match =>
        {
            var name = match.Groups["name"].Value;
            var definition = definitions.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (definition == null || !values.TryGetValue(definition.Position, out var value) || value == null)
            {
                if (match.Groups["optional"].Success || definition?.IsOptional == true)
                    return string.Empty;
                throw new InvalidOperationException($"动态 API 路由参数 '{name}' 没有对应的非空方法参数。");
            }

            var type = definition.Position < reflectedParameters.Length ? reflectedParameters[definition.Position].ParameterType : value.GetType();
            var converted = _parameterConverter.ConvertToString(value, type, definition);
            if (converted == null)
                throw new InvalidOperationException($"动态 API 路由参数 '{name}' 转换后为空。");
            return Uri.EscapeDataString(converted);
        });
        route = Regex.Replace(route, "/{2,}", "/").Trim('/');
        if (string.IsNullOrWhiteSpace(route))
            throw new InvalidOperationException($"动态 API 方法路由不能为空（远程服务 '{remoteOptions.BaseUrl}'）。");
        return route;
    }

    /// <summary>
    /// 构建远程调用的查询字符串。
    /// </summary>
    /// <param name="method">远程方法描述。</param>
    /// <param name="values">按位置索引的参数值。</param>
    /// <param name="reflectedParameters">契约参数。</param>
    /// <param name="remoteOptions">远程服务配置。</param>
    /// <returns>以问号开头的查询字符串；没有查询参数时返回空字符串。</returns>
    private string BuildQuery(
        BingDynamicApiMethodDefinition method,
        IReadOnlyDictionary<int, object> values,
        ParameterInfo[] reflectedParameters,
        BingDynamicApiRemoteOptions remoteOptions)
    {
        var query = new List<string>();
        var routeTokens = Regex.Matches(method.RouteTemplate ?? string.Empty, "\\{(?<name>[^{}:?=]+)")
            .Select(x => x.Groups["name"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in method.Parameters.OrderBy(x => x.Position))
        {
            if (definition.IsCancellationToken || !values.TryGetValue(definition.Position, out var value) || value == null)
                continue;
            if (routeTokens.Contains(definition.Name) || IsBinding(definition.BindingSource, "Path", "Route"))
                continue;

            if (IsBinding(definition.BindingSource, "Query"))
            {
                var type = definition.Position < reflectedParameters.Length ? reflectedParameters[definition.Position].ParameterType : value.GetType();
                if (value is IEnumerable sequence && value is not string)
                {
                    foreach (var item in sequence)
                    {
                        var convertedItem = _parameterConverter.ConvertToString(item, item?.GetType() ?? type, definition);
                        if (convertedItem != null)
                            query.Add($"{Uri.EscapeDataString(definition.Name)}={Uri.EscapeDataString(convertedItem)}");
                    }
                }
                else
                {
                    var converted = _parameterConverter.ConvertToString(value, type, definition);
                    if (converted != null)
                        query.Add($"{Uri.EscapeDataString(definition.Name)}={Uri.EscapeDataString(converted)}");
                }
            }
            else if (!IsBinding(definition.BindingSource, "Body", "Form", "FormFile", "File", "Path", "Route"))
            {
                throw new NotSupportedException($"动态 API 参数 '{definition.Name}' 使用了不支持的绑定来源 '{definition.BindingSource}'。");
            }
        }

        return query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
    }

    /// <summary>
    /// 构建远程调用的请求体。
    /// </summary>
    /// <param name="method">远程方法描述。</param>
    /// <param name="values">按位置索引的参数值。</param>
    /// <param name="reflectedParameters">契约参数。</param>
    /// <param name="remoteOptions">远程服务配置。</param>
    /// <returns>JSON 或 multipart 内容；无需请求体时返回 null。</returns>
    private HttpContent BuildContent(
        BingDynamicApiMethodDefinition method,
        IReadOnlyDictionary<int, object> values,
        ParameterInfo[] reflectedParameters,
        BingDynamicApiRemoteOptions remoteOptions)
    {
        var bodyParameters = method.Parameters.Where(x => !x.IsCancellationToken && IsBinding(x.BindingSource, "Body")).ToArray();
        var formParameters = method.Parameters.Where(x => !x.IsCancellationToken && (x.IsFile || IsBinding(x.BindingSource, "Form", "FormFile", "File"))).ToArray();

        if (bodyParameters.Length > 1)
            throw new NotSupportedException($"动态 API 方法 '{method.MethodName}' 声明了多个 JSON 请求体参数；HTTP 客户端无法推断其聚合结构。");
        if (bodyParameters.Length > 0 && formParameters.Length > 0)
            throw new NotSupportedException($"动态 API 方法 '{method.MethodName}' 同时声明 JSON 请求体和表单参数，当前元数据不足以构造唯一请求体。");

        if (formParameters.Length > 0)
        {
            var multipart = new MultipartFormDataContent();
            try
            {
                foreach (var definition in formParameters)
                {
                    if (!values.TryGetValue(definition.Position, out var value) || value == null)
                    {
                        if (!definition.IsOptional)
                            throw new InvalidOperationException($"必填表单参数 '{definition.Name}' 没有值。");
                        continue;
                    }

                    if (definition.IsFile)
                    {
                        if (value is not IRemoteStreamContent remoteStream)
                            throw new NotSupportedException($"文件参数 '{definition.Name}' 必须实现 {nameof(IRemoteStreamContent)}。");
                        var streamContent = new StreamContent(remoteStream.GetStream());
                        if (MediaTypeHeaderValue.TryParse(remoteStream.ContentType, out var contentType))
                            streamContent.Headers.ContentType = contentType;
                        multipart.Add(streamContent, definition.Name, remoteStream.FileName ?? definition.Name);
                    }
                    else
                    {
                        var type = definition.Position < reflectedParameters.Length ? reflectedParameters[definition.Position].ParameterType : value.GetType();
                        if (value is IEnumerable sequence && value is not string)
                        {
                            foreach (var item in sequence)
                                multipart.Add(new StringContent(_parameterConverter.ConvertToString(item, item?.GetType() ?? type, definition) ?? string.Empty), definition.Name);
                        }
                        else
                        {
                            multipart.Add(new StringContent(_parameterConverter.ConvertToString(value, type, definition) ?? string.Empty), definition.Name);
                        }
                    }
                }
                return multipart;
            }
            catch
            {
                multipart.Dispose();
                throw;
            }
        }

        if (bodyParameters.Length == 0)
            return null;

        var bodyParameter = bodyParameters[0];
        if (!values.TryGetValue(bodyParameter.Position, out var bodyValue) || bodyValue == null)
        {
            if (!bodyParameter.IsOptional)
                throw new InvalidOperationException($"必填请求体参数 '{bodyParameter.Name}' 没有值。");
            return null;
        }
        var bodyType = bodyParameter.Position < reflectedParameters.Length ? reflectedParameters[bodyParameter.Position].ParameterType : bodyValue.GetType();
        return _serializer.Serialize(bodyValue, bodyType, _options.Value.JsonSerializerOptions);
    }

    /// <summary>
    /// 创建远程调用的 HTTP 请求。
    /// </summary>
    /// <param name="method">远程方法描述。</param>
    /// <param name="url">完整请求地址。</param>
    /// <param name="content">请求体，可为 null。</param>
    /// <returns>包含目标地址、动词和请求体的请求消息。</returns>
    private static HttpRequestMessage CreateRequest(BingDynamicApiMethodDefinition method, string url, HttpContent content)
    {
        var request = new HttpRequestMessage(new HttpMethod(method.HttpMethod), url);
        if (content != null)
            request.Content = content;
        return request;
    }

    /// <summary>
    /// 判断参数绑定来源是否匹配指定名称。
    /// </summary>
    /// <param name="bindingSource">绑定来源名称。</param>
    /// <param name="names">允许的来源名称。</param>
    /// <returns>不区分大小写匹配任一名称时返回 true，否则返回 false。</returns>
    private static bool IsBinding(string bindingSource, params string[] names)
    {
        return names.Any(name => string.Equals(bindingSource, name, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// 将共享服务接口调用转发为动态 HTTP 请求。
/// </summary>
public class BingDynamicApiDispatchProxy : DispatchProxy
{
    /// <summary>
    /// 保存初始化后用于执行远程调用的工厂。
    /// </summary>
    private BingDynamicApiClientProxyFactory _factory;
    /// <summary>
    /// 保存当前代理的共享服务契约类型。
    /// </summary>
    private Type _serviceType;
    /// <summary>
    /// 保存当前代理关联的远程服务名称。
    /// </summary>
    private string _remoteName;

    /// <summary>
    /// 初始化远程调用代理的配置。
    /// </summary>
    /// <param name="factory">远程调用工厂。</param>
    /// <param name="serviceType">服务契约类型。</param>
    /// <param name="remoteName">远程服务名称。</param>
    internal void Initialize(BingDynamicApiClientProxyFactory factory, Type serviceType, string remoteName)
    {
        _factory = factory;
        _serviceType = serviceType;
        _remoteName = remoteName;
    }

    /// <inheritdoc />
    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        if (_factory == null)
            throw new InvalidOperationException("动态 API 客户端代理尚未初始化。");
        return _factory.Invoke(_serviceType, _remoteName, targetMethod, args);
    }
}

/// <summary>
/// 在流释放时一并释放所属 HTTP 响应。
/// </summary>
internal sealed class ResponseOwnedStream : Stream
{
    /// <summary>
    /// 保存下载响应的底层内容流。
    /// </summary>
    private readonly Stream _inner;
    /// <summary>
    /// 保存随包装流一并释放的 HTTP 响应。
    /// </summary>
    private readonly HttpResponseMessage _response;

    /// <summary>
    /// 初始化 ResponseOwnedStream 类的新实例。
    /// </summary>
    /// <param name="inner">下载响应的内容流。</param>
    /// <param name="response">内容流所属的 HTTP 响应。</param>
    public ResponseOwnedStream(Stream inner, HttpResponseMessage response)
    {
        _inner = inner;
        _response = response;
    }

    /// <inheritdoc />
    public override bool CanRead => _inner.CanRead;
    /// <inheritdoc />
    public override bool CanSeek => _inner.CanSeek;
    /// <inheritdoc />
    public override bool CanWrite => _inner.CanWrite;
    /// <inheritdoc />
    public override long Length => _inner.Length;
    /// <inheritdoc />
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    /// <inheritdoc />
    public override void Flush() => _inner.Flush();
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    /// <inheritdoc />
    public override void SetLength(long value) => _inner.SetLength(value);
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    /// <inheritdoc />
    /// <remarks>同步释放底层内容流及其所属响应。</remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _response.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    /// <remarks>异步释放底层内容流后释放所属响应。</remarks>
    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        _response.Dispose();
        GC.SuppressFinalize(this);
    }
}
