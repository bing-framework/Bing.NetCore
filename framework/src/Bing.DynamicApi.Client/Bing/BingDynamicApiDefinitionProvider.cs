using Bing.DynamicApi.Contracts;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Bing.DynamicApi.Client;

/// <summary>
/// 获取动态 API 描述并发送配置化请求。
/// </summary>
internal sealed class BingDynamicApiDefinitionProvider
{
    /// <summary>
    /// 为具名远程服务创建 HTTP 客户端。
    /// </summary>
    private readonly IHttpClientFactory _httpClientFactory;
    /// <summary>
    /// 提供客户端序列化与远程服务选项。
    /// </summary>
    private readonly IOptions<BingDynamicApiClientOptions> _options;
    /// <summary>
    /// 在请求发送时提供租户、关联标识和语言。
    /// </summary>
    private readonly IBingDynamicApiRequestContext _requestContext;
    /// <summary>
    /// 决定可重放请求的重试条件和间隔。
    /// </summary>
    private readonly IBingDynamicApiRetryPolicy _retryPolicy;
    /// <summary>
    /// 按注册顺序执行发送前的宿主请求处理。
    /// </summary>
    private readonly IEnumerable<IBingDynamicApiRequestHandler> _requestHandlers;
    /// <summary>
    /// 在宿主内共享按远程服务与版本隔离的描述缓存。
    /// </summary>
    private readonly BingDynamicApiDefinitionCache _cache;

    /// <summary>
    /// 初始化 BingDynamicApiDefinitionProvider 类的新实例。
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂。</param>
    /// <param name="options">客户端选项。</param>
    /// <param name="requestContext">请求上下文访问器。</param>
    /// <param name="retryPolicy">重试策略。</param>
    /// <param name="requestHandlers">发送前请求处理器。</param>
    /// <param name="cache">共享描述缓存。</param>
    public BingDynamicApiDefinitionProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<BingDynamicApiClientOptions> options,
        IBingDynamicApiRequestContext requestContext,
        IBingDynamicApiRetryPolicy retryPolicy,
        IEnumerable<IBingDynamicApiRequestHandler> requestHandlers,
        BingDynamicApiDefinitionCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _requestContext = requestContext;
        _retryPolicy = retryPolicy;
        _requestHandlers = requestHandlers;
        _cache = cache;
    }

    /// <summary>
    /// 异步获取远程 API 描述。
    /// </summary>
    /// <param name="remoteName">远程服务名称。</param>
    /// <param name="remoteOptions">远程服务配置。</param>
    /// <param name="cancellationToken">取消当前等待的令牌。</param>
    /// <returns>从缓存或远程端点获取的 API 描述。</returns>
    /// <remarks>取消单个等待者不会取消共享描述请求。</remarks>
    public async Task<BingDynamicApiDefinition> GetAsync(string remoteName, BingDynamicApiRemoteOptions remoteOptions, CancellationToken cancellationToken)
    {
        return await _cache.GetAsync(
            (remoteName, remoteOptions.ApiVersion),
            () => FetchAsync(remoteName, remoteOptions, CancellationToken.None),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步下载并校验远程 API 描述。
    /// </summary>
    /// <param name="remoteName">远程服务名称。</param>
    /// <param name="remoteOptions">远程服务配置。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>通过结构版本和 API 版本校验的描述。</returns>
    private async Task<BingDynamicApiDefinition> FetchAsync(
        string remoteName,
        BingDynamicApiRemoteOptions remoteOptions,
        CancellationToken cancellationToken)
    {
        var url = CombineUrl(remoteOptions.BaseUrl, $"api/dynamic-api/{Uri.EscapeDataString(remoteOptions.ApiVersion)}/definition");
        var response = await SendAsync(remoteName, remoteOptions, () => new HttpRequestMessage(HttpMethod.Get, url), true, cancellationToken).ConfigureAwait(false);
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw await BingDynamicApiHttpErrors.CreateExceptionAsync(response, _options.Value.JsonSerializerOptions).ConfigureAwait(false);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var definition = await JsonSerializer.DeserializeAsync<BingDynamicApiDefinition>(stream, _options.Value.JsonSerializerOptions, cancellationToken).ConfigureAwait(false);
            if (definition == null)
                throw new InvalidOperationException($"远程服务 '{remoteName}' 返回了空的动态 API 定义。");
            if (definition.SchemaVersion != 1)
                throw new NotSupportedException($"远程服务 '{remoteName}' 返回了不支持的动态 API SchemaVersion '{definition.SchemaVersion}'。");
            if (!string.Equals(definition.ApiVersion, remoteOptions.ApiVersion, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"远程服务 '{remoteName}' 返回的 API 版本 '{definition.ApiVersion}' 与配置版本 '{remoteOptions.ApiVersion}' 不一致。");
            if (definition.Services == null)
                throw new InvalidOperationException($"远程服务 '{remoteName}' 返回的动态 API 定义缺少 Services。");
            return definition;
        }
    }

    /// <summary>
    /// 异步发送远程请求。
    /// </summary>
    /// <param name="remoteName">远程服务名称。</param>
    /// <param name="remoteOptions">远程服务配置。</param>
    /// <param name="requestFactory">每次尝试创建独立请求的工厂。</param>
    /// <param name="retryAllowed">是否允许按配置重放请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>最终 HTTP 响应，由调用方释放。</returns>
    /// <remarks>发送前附加配置头并执行宿主处理器；重试时释放先前响应。</remarks>
    internal async Task<HttpResponseMessage> SendAsync(
        string remoteName,
        BingDynamicApiRemoteOptions remoteOptions,
        Func<HttpRequestMessage> requestFactory,
        bool retryAllowed,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(remoteName);
        var retries = retryAllowed ? remoteOptions.MaxRetries : 0;
        for (var attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            BingDynamicApiHttpErrors.SetRequestCancellationToken(request, cancellationToken);
            AddConfiguredHeaders(request, remoteOptions);
            foreach (var handler in _requestHandlers)
                await handler.HandleAsync(remoteName, request, cancellationToken).ConfigureAwait(false);

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception) when (attempt < retries
                                                          && !cancellationToken.IsCancellationRequested
                                                          && _retryPolicy.ShouldRetry(remoteName, attempt, null, exception, remoteOptions))
            {
                await Task.Delay(GetRetryDelay(remoteName, attempt, remoteOptions), cancellationToken).ConfigureAwait(false);
                continue;
            }

            response.RequestMessage ??= request;

            if (attempt < retries && _retryPolicy.ShouldRetry(remoteName, attempt, response.StatusCode, null, remoteOptions))
            {
                response.Dispose();
                await Task.Delay(GetRetryDelay(remoteName, attempt, remoteOptions), cancellationToken).ConfigureAwait(false);
                continue;
            }

            return response;
        }
    }

    /// <summary>
    /// 附加静态请求头和当前上下文头。
    /// </summary>
    /// <param name="request">待发送请求。</param>
    /// <param name="remoteOptions">远程服务配置。</param>
    private void AddConfiguredHeaders(HttpRequestMessage request, BingDynamicApiRemoteOptions remoteOptions)
    {
        foreach (var header in remoteOptions.Headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        AddHeader(request, remoteOptions.TenantHeaderName, _requestContext.TenantId);
        AddHeader(request, remoteOptions.CorrelationIdHeaderName, _requestContext.CorrelationId);
        AddHeader(request, remoteOptions.LanguageHeaderName, _requestContext.Language);
    }

    /// <summary>
    /// 附加非空请求头。
    /// </summary>
    /// <param name="request">待发送请求。</param>
    /// <param name="headerName">头名称；为空时跳过。</param>
    /// <param name="value">头值；为空时跳过。</param>
    private static void AddHeader(HttpRequestMessage request, string headerName, string value)
    {
        if (!string.IsNullOrWhiteSpace(headerName) && !string.IsNullOrWhiteSpace(value))
            request.Headers.TryAddWithoutValidation(headerName, value);
    }

    /// <summary>
    /// 拼接基础地址和相对路径。
    /// </summary>
    /// <param name="baseUrl">基础地址。</param>
    /// <param name="relativePath">相对路径。</param>
    /// <returns>连接处包含一个斜杠的地址。</returns>
    internal static string CombineUrl(string baseUrl, string relativePath)
    {
        return $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }

    /// <summary>
    /// 获取并校验重试间隔。
    /// </summary>
    /// <param name="remoteName">远程服务名称。</param>
    /// <param name="attempt">当前尝试序号。</param>
    /// <param name="remoteOptions">远程服务配置。</param>
    /// <returns>非负的重试间隔。</returns>
    private TimeSpan GetRetryDelay(string remoteName, int attempt, BingDynamicApiRemoteOptions remoteOptions)
    {
        var delay = _retryPolicy.GetDelay(remoteName, attempt, remoteOptions);
        if (delay < TimeSpan.Zero)
            throw new InvalidOperationException($"动态 API 远程服务 '{remoteName}' 的重试策略返回了负间隔。");
        return delay;
    }
}

/// <summary>
/// 缓存并合并同一远程服务版本的描述请求。
/// </summary>
internal sealed class BingDynamicApiDefinitionCache
{
    /// <summary>
    /// 保存共享描述任务；失败条目会被移除以允许后续重新获取。
    /// </summary>
    private readonly ConcurrentDictionary<(string RemoteName, string ApiVersion), Lazy<Task<BingDynamicApiDefinition>>> _cache = new();

    /// <summary>
    /// 异步获取缓存中的 API 描述。
    /// </summary>
    /// <param name="key">远程服务名称和版本组成的缓存键。</param>
    /// <param name="factory">缓存未命中时的描述获取工厂。</param>
    /// <param name="cancellationToken">取消当前等待的令牌。</param>
    /// <returns>该缓存键对应的 API 描述。</returns>
    /// <remarks>并发等待共享同一任务；取消等待不会取消共享任务。</remarks>
    public async Task<BingDynamicApiDefinition> GetAsync(
        (string RemoteName, string ApiVersion) key,
        Func<Task<BingDynamicApiDefinition>> factory,
        CancellationToken cancellationToken)
    {
        Lazy<Task<BingDynamicApiDefinition>> newEntry = null;
        newEntry = new Lazy<Task<BingDynamicApiDefinition>>(
            () => FetchAndRemoveOnFailureAsync(key, factory, newEntry),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var entry = _cache.GetOrAdd(key, newEntry);
        return await entry.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步获取描述并移除失败缓存条目。
    /// </summary>
    /// <param name="key">当前缓存键。</param>
    /// <param name="factory">描述获取工厂。</param>
    /// <param name="entry">本次获取对应的缓存条目。</param>
    /// <returns>获取成功的 API 描述。</returns>
    private async Task<BingDynamicApiDefinition> FetchAndRemoveOnFailureAsync(
        (string RemoteName, string ApiVersion) key,
        Func<Task<BingDynamicApiDefinition>> factory,
        Lazy<Task<BingDynamicApiDefinition>> entry)
    {
        try
        {
            return await factory().ConfigureAwait(false);
        }
        catch
        {
            ((ICollection<KeyValuePair<(string RemoteName, string ApiVersion), Lazy<Task<BingDynamicApiDefinition>>>>)_cache)
                .Remove(new KeyValuePair<(string RemoteName, string ApiVersion), Lazy<Task<BingDynamicApiDefinition>>>(key, entry));
            throw;
        }
    }
}
