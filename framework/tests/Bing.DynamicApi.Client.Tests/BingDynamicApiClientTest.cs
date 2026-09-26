using Bing.Application.Services;
using Bing.Content;
using Bing.DynamicApi.Client;
using Bing.DynamicApi.Contracts;
using Bing.Http.Clients;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Xunit;

namespace Bing.DynamicApi.Client.Tests;

/// <summary>
/// 验证动态 API 客户端的请求构造与响应处理。
/// </summary>
public class BingDynamicApiClientTest
{
    /// <summary>
    /// 验证元数据缓存、路由参数、请求体和上下文请求头。
    /// </summary>
    [Fact]
    public async Task Should_cache_definition_and_dispatch_route_query_body_and_context_headers()
    {
        var handler = new StubHandler((request, _) =>
        {
            if (IsDefinitionRequest(request))
                return Task.FromResult(JsonResponse(CreateDefinition()));

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://service.test/root/api/app/v1/catalog/7b110758-6815-4c8c-9730-2098686ad98a?term=sea%20%26%20sun", request.RequestUri.AbsoluteUri);
            Assert.Equal("tenant-42", request.Headers.GetValues("X-Tenant-Id").Single());
            Assert.Equal("correlation-9", request.Headers.GetValues("X-Correlation-Id").Single());
            Assert.Equal("zh-CN", request.Headers.GetValues("Accept-Language").Single());
            return ReadBodyAndReturnAsync(request);
        });
        using var provider = CreateProvider(handler);
        var service = provider.GetRequiredService<ICatalogAppService>();
        var input = new CatalogInput { Name = "Bing", Count = 3 };

        var first = await service.FindAsync(Guid.Parse("7b110758-6815-4c8c-9730-2098686ad98a"), "sea & sun", input, CancellationToken.None);
        var second = await service.FindAsync(Guid.Parse("7b110758-6815-4c8c-9730-2098686ad98a"), "sea & sun", input, CancellationToken.None);

        Assert.Equal("Bing", first.Name);
        Assert.Equal(3, first.Count);
        Assert.Equal(first.Name, second.Name);
        Assert.Equal(1, handler.DefinitionRequestCount);
        Assert.Equal(2, handler.ApiRequestCount);
    }

    /// <summary>
    /// 验证远程错误响应会转换为包含状态码和错误详情的异常。
    /// </summary>
    [Fact]
    public async Task Should_parse_remote_error_and_status_code()
    {
        var handler = new StubHandler((request, _) =>
        {
            if (IsDefinitionRequest(request))
                return Task.FromResult(JsonResponse(CreateDefinition()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("{\"error\":{\"code\":\"Catalog:Conflict\",\"message\":\"Already exists\",\"details\":\"duplicate name\"}}")
            });
        });
        using var provider = CreateProvider(handler);
        var service = provider.GetRequiredService<ICatalogAppService>();

        var exception = await Assert.ThrowsAsync<BingRemoteCallException>(() => service.FindAsync(
            Guid.Parse("7b110758-6815-4c8c-9730-2098686ad98a"), "term", new CatalogInput(), CancellationToken.None));

        Assert.Equal((int)HttpStatusCode.Conflict, exception.HttpStatusCode);
        Assert.Equal("Catalog:Conflict", exception.Code);
        Assert.Equal("Already exists", exception.Message);
        Assert.Equal("duplicate name", exception.Details);
    }

    /// <summary>
    /// 验证方法取消令牌会传递到底层 HTTP 请求。
    /// </summary>
    [Fact]
    public async Task Should_propagate_method_cancellation_to_http_send()
    {
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            if (IsDefinitionRequest(request))
                return JsonResponse(CreateDefinition());
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var provider = CreateProvider(handler);
        var service = provider.GetRequiredService<ICatalogAppService>();
        using var cancellation = new CancellationTokenSource();

        var call = service.SaveAsync(new CatalogInput { Name = "Bing" }, cancellation.Token);
        await handler.ApiRequestObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    /// <summary>
    /// 验证方法取消令牌可以取消首次 API 描述请求。
    /// </summary>
    [Fact]
    public async Task Should_cancel_initial_definition_request()
    {
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            if (IsDefinitionRequest(request))
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(new CatalogResult { Name = "unexpected", Count = 0 });
        });
        using var provider = CreateProvider(handler);
        var service = provider.GetRequiredService<ICatalogAppService>();
        using var cancellation = new CancellationTokenSource();

        var call = service.GetAsync(Guid.Parse("7b110758-6815-4c8c-9730-2098686ad98a"), cancellation.Token);
        await handler.DefinitionRequestObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    /// <summary>
    /// 验证客户端使用宿主提供的序列化器、请求处理器和参数转换器。
    /// </summary>
    [Fact]
    public async Task Should_use_custom_serializer_and_request_handler()
    {
        var serializer = new TestJsonSerializer();
        var requestHandler = new TestRequestHandler();
        var parameterConverter = new TestParameterConverter();
        var handler = new StubHandler((request, _) =>
        {
            if (IsDefinitionRequest(request))
                return Task.FromResult(JsonResponse(CreateDefinition()));

            Assert.Equal("application/vnd.bing-test+json", request.Content.Headers.ContentType.MediaType);
            Assert.Equal("?term=special-value", request.RequestUri.Query);
            Assert.Equal("Bearer test-token", request.Headers.Authorization.ToString());
            return Task.FromResult(JsonResponse(new CatalogResult { Name = "custom", Count = 4 }));
        });
        using var provider = CreateProvider(
            handler,
            serializer: serializer,
            requestHandler: requestHandler,
            parameterConverter: parameterConverter);
        var service = provider.GetRequiredService<ICatalogAppService>();

        var result = await service.FindAsync(
            Guid.Parse("7b110758-6815-4c8c-9730-2098686ad98a"), "term", new CatalogInput { Name = "input" }, CancellationToken.None);

        Assert.Equal("custom", result.Name);
        Assert.Equal(4, result.Count);
        Assert.Equal(1, serializer.DeserializeCount);
        Assert.True(requestHandler.CallCount >= 2);
        Assert.True(parameterConverter.CallCount >= 2);
    }

    /// <summary>
    /// 验证 JSON 字符串响应经过宿主提供的序列化器处理。
    /// </summary>
    [Fact]
    public async Task Should_use_custom_serializer_for_json_string_response()
    {
        var serializer = new TestJsonSerializer();
        var handler = new StubHandler((request, _) =>
        {
            if (IsDefinitionRequest(request))
                return Task.FromResult(JsonResponse(CreateDefinition()));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize("json-string"),
                    Encoding.UTF8,
                    "application/vnd.bing-test+json")
            });
        });
        using var provider = CreateProvider(handler, serializer: serializer);
        var service = provider.GetRequiredService<ICatalogAppService>();

        var result = await service.GetByIdAsync(
            Guid.Parse("7b110758-6815-4c8c-9730-2098686ad98a"),
            CancellationToken.None);

        Assert.Equal("json-string", result);
        Assert.Equal(1, serializer.DeserializeCount);
    }

    /// <summary>
    /// 验证 multipart 文件上传及下载流的元数据解析。
    /// </summary>
    [Fact]
    public async Task Should_send_multipart_file_and_return_download_stream_metadata()
    {
        var handler = new StubHandler(async (request, _) =>
        {
            if (IsDefinitionRequest(request))
                return JsonResponse(CreateDefinition());
            if (request.RequestUri.AbsolutePath.EndsWith("/upload", StringComparison.Ordinal))
            {
                Assert.Equal("multipart/form-data", request.Content.Headers.ContentType.MediaType);
                var multipart = await request.Content.ReadAsStringAsync();
                Assert.Contains("filename=notes.txt", multipart, StringComparison.Ordinal);
                Assert.Contains("file-data", multipart, StringComparison.Ordinal);
                return JsonResponse("uploaded");
            }

            var content = new ByteArrayContent(Encoding.UTF8.GetBytes("download-data"));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "report.txt" };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var provider = CreateProvider(handler);
        var service = provider.GetRequiredService<ICatalogAppService>();

        using var upload = new RemoteStreamContent(new MemoryStream(Encoding.UTF8.GetBytes("file-data")), "notes.txt", "text/plain");
        var uploaded = await service.UploadAsync(upload, CancellationToken.None);
        using var download = await service.DownloadAsync(CancellationToken.None);
        using var downloadStream = download.GetStream();
        using var reader = new StreamReader(downloadStream);
        var downloaded = await reader.ReadToEndAsync();

        Assert.Equal("uploaded", uploaded);
        Assert.Equal("download-data", downloaded);
        Assert.Equal("report.txt", download.FileName);
        Assert.Equal("text/plain", download.ContentType);
    }

    /// <summary>
    /// 验证配置重试后幂等 GET 请求会在可重试失败后重发。
    /// </summary>
    [Fact]
    public async Task Should_retry_idempotent_get_only_when_configured()
    {
        var apiCallCount = 0;
        var handler = new StubHandler((request, _) =>
        {
            if (IsDefinitionRequest(request))
                return Task.FromResult(JsonResponse(CreateDefinition()));
            return Task.FromResult(++apiCallCount == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : JsonResponse(new CatalogResult { Name = "retried", Count = 1 }));
        });
        using var provider = CreateProvider(handler, maxRetries: 1);
        var service = provider.GetRequiredService<ICatalogAppService>();

        var result = await service.GetAsync(Guid.Parse("7b110758-6815-4c8c-9730-2098686ad98a"), CancellationToken.None);

        Assert.Equal("retried", result.Name);
        Assert.Equal(2, handler.ApiRequestCount);
    }

    /// <summary>
    /// 验证对象标识参数会转换为路由值。
    /// </summary>
    [Fact]
    public async Task Should_convert_object_identifier_to_route_value()
    {
        var id = Guid.Parse("7b110758-6815-4c8c-9730-2098686ad98a");
        var handler = new StubHandler((request, _) =>
        {
            if (IsDefinitionRequest(request))
                return Task.FromResult(JsonResponse(CreateDefinition()));

            Assert.Equal($"/root/api/app/v1/catalog/object/{id:D}", request.RequestUri.AbsolutePath);
            return Task.FromResult(JsonResponse(id.ToString("D")));
        });
        using var provider = CreateProvider(handler);
        var service = provider.GetRequiredService<ICatalogAppService>();

        var result = await service.GetByIdAsync(id, CancellationToken.None);

        Assert.Equal(id.ToString("D"), result);
    }

    /// <summary>
    /// 验证未配置时客户端不会自动重试请求。
    /// </summary>
    [Fact]
    public async Task Should_keep_retries_disabled_by_default()
    {
        var handler = new StubHandler((request, _) => Task.FromResult(IsDefinitionRequest(request)
            ? JsonResponse(CreateDefinition())
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var provider = CreateProvider(handler);
        var service = provider.GetRequiredService<ICatalogAppService>();

        var exception = await Assert.ThrowsAsync<BingRemoteCallException>(() => service.GetAsync(
            Guid.Parse("7b110758-6815-4c8c-9730-2098686ad98a"), CancellationToken.None));

        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, exception.HttpStatusCode);
        Assert.Equal(1, handler.ApiRequestCount);
    }

    /// <summary>
    /// 验证客户端使用宿主提供的重试策略。
    /// </summary>
    [Fact]
    public async Task Should_use_host_supplied_retry_policy()
    {
        var policy = new TestRetryPolicy();
        var apiCallCount = 0;
        var handler = new StubHandler((request, _) => Task.FromResult(IsDefinitionRequest(request)
            ? JsonResponse(CreateDefinition())
            : ++apiCallCount == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : JsonResponse(new CatalogResult { Name = "custom-policy", Count = 2 })));
        using var provider = CreateProvider(handler, maxRetries: 1, retryPolicy: policy);
        var service = provider.GetRequiredService<ICatalogAppService>();

        var result = await service.GetAsync(Guid.Parse("7b110758-6815-4c8c-9730-2098686ad98a"), CancellationToken.None);

        Assert.Equal("custom-policy", result.Name);
        Assert.Equal(2, handler.ApiRequestCount);
        Assert.Equal(2, policy.ShouldRetryCount);
        Assert.Equal(1, policy.GetDelayCount);
    }

    /// <summary>
    /// 验证配置的 API 版本同时用于元数据和方法路由。
    /// </summary>
    [Fact]
    public async Task Should_select_configured_api_version_for_metadata_and_method_routes()
    {
        var handler = new StubHandler((request, _) =>
        {
            if (IsDefinitionRequest(request))
            {
                Assert.EndsWith("/api/dynamic-api/v2/definition", request.RequestUri.AbsolutePath, StringComparison.Ordinal);
                return Task.FromResult(JsonResponse(CreateDefinition("v2")));
            }

            Assert.StartsWith("/root/api/app/v2/catalog/", request.RequestUri.AbsolutePath, StringComparison.Ordinal);
            return Task.FromResult(JsonResponse(new CatalogResult { Name = "v2", Count = 2 }));
        });
        using var provider = CreateProvider(handler, apiVersion: "v2");
        var service = provider.GetRequiredService<ICatalogAppService>();

        var result = await service.GetAsync(Guid.Parse("7b110758-6815-4c8c-9730-2098686ad98a"), CancellationToken.None);

        Assert.Equal("v2", result.Name);
    }

    /// <summary>
    /// 验证不支持的同步方法签名会在调用时抛出异常。
    /// </summary>
    [Fact]
    public void Should_reject_unsupported_synchronous_method_shape()
    {
        using var provider = CreateProvider(new StubHandler((request, _) => Task.FromResult(JsonResponse(CreateDefinition()))));
        var service = provider.GetRequiredService<ICatalogAppService>();

        var exception = Assert.Throws<NotSupportedException>(() => service.UnsupportedSyncMethod());

        Assert.Contains("必须返回 Task 或 Task<T>", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 创建注册动态 API 客户端及测试扩展点的服务提供程序。
    /// </summary>
    /// <param name="handler">处理客户端 HTTP 请求的消息处理器。</param>
    /// <param name="maxRetries">远程服务允许的最大重试次数。</param>
    /// <param name="apiVersion">客户端请求的 API 版本。</param>
    /// <param name="serializer">可选的 JSON 序列化器。</param>
    /// <param name="requestHandler">可选的请求处理器。</param>
    /// <param name="parameterConverter">可选的参数转换器。</param>
    /// <param name="retryPolicy">可选的重试策略。</param>
    /// <returns>包含动态客户端服务的服务提供程序。</returns>
    private static ServiceProvider CreateProvider(
        StubHandler handler,
        int maxRetries = 0,
        string apiVersion = "v1",
        IBingDynamicApiJsonSerializer serializer = null,
        IBingDynamicApiRequestHandler requestHandler = null,
        IBingDynamicApiParameterConverter parameterConverter = null,
        IBingDynamicApiRetryPolicy retryPolicy = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBingDynamicApiRequestContext>(new TestRequestContext());
        if (serializer != null)
            services.AddSingleton(serializer);
        if (requestHandler != null)
            services.AddSingleton(requestHandler);
        if (parameterConverter != null)
            services.AddSingleton(parameterConverter);
        if (retryPolicy != null)
            services.AddSingleton(retryPolicy);
        services.AddBingDynamicApiClient(options => options.AddRemoteService("catalog", remote =>
        {
            remote.BaseUrl = "https://service.test/root";
            remote.ApiVersion = apiVersion;
            remote.MaxRetries = maxRetries;
        }));
        services.AddHttpClient("catalog").ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddBingDynamicApiClient<ICatalogAppService>("catalog");
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建目录服务的 API 描述测试数据。
    /// </summary>
    /// <param name="apiVersion">写入描述的 API 版本。</param>
    /// <returns>包含目录服务方法和参数绑定信息的 API 描述。</returns>
    private static BingDynamicApiDefinition CreateDefinition(string apiVersion = "v1")
    {
        var serviceRoute = $"api/app/{apiVersion}/catalog";
        return new BingDynamicApiDefinition
        {
            ApiVersion = apiVersion,
            Services =
            {
                new BingDynamicApiServiceDefinition
                {
                    ServiceName = "catalog",
                    ServiceTypeName = typeof(ICatalogAppService).FullName,
                    Methods =
                    {
                        new BingDynamicApiMethodDefinition
                        {
                            MethodName = nameof(ICatalogAppService.FindAsync),
                            HttpMethod = "POST",
                            RouteTemplate = $"{serviceRoute}/{{id}}",
                            ReturnTypeName = typeof(CatalogResult).FullName,
                            Parameters =
                            {
                                Parameter<Guid>(0, "id", "Path"),
                                Parameter<string>(1, "term", "Query"),
                                Parameter<CatalogInput>(2, "input", "Body"),
                                new BingDynamicApiParameterDefinition
                                {
                                    Name = "cancellationToken",
                                    Position = 3,
                                    TypeName = typeof(CancellationToken).FullName,
                                    BindingSource = "Special",
                                    IsCancellationToken = true
                                }
                            }
                        },
                        new BingDynamicApiMethodDefinition
                        {
                            MethodName = nameof(ICatalogAppService.SaveAsync),
                            HttpMethod = "POST",
                            RouteTemplate = $"{serviceRoute}/save",
                            ReturnTypeName = typeof(Task).FullName,
                            Parameters =
                            {
                                Parameter<CatalogInput>(0, "input", "Body"),
                                new BingDynamicApiParameterDefinition
                                {
                                    Name = "cancellationToken",
                                    Position = 1,
                                    TypeName = typeof(CancellationToken).FullName,
                                    BindingSource = "Special",
                                    IsCancellationToken = true
                                }
                            }
                        },
                        new BingDynamicApiMethodDefinition
                        {
                            MethodName = nameof(ICatalogAppService.UploadAsync),
                            HttpMethod = "POST",
                            RouteTemplate = $"{serviceRoute}/upload",
                            ReturnTypeName = typeof(string).FullName,
                            Parameters =
                            {
                                new BingDynamicApiParameterDefinition
                                {
                                    Name = "file",
                                    Position = 0,
                                    TypeName = typeof(IRemoteStreamContent).FullName,
                                    BindingSource = "FormFile",
                                    IsFile = true
                                },
                                new BingDynamicApiParameterDefinition
                                {
                                    Name = "cancellationToken",
                                    Position = 1,
                                    TypeName = typeof(CancellationToken).FullName,
                                    BindingSource = "Special",
                                    IsCancellationToken = true
                                }
                            }
                        },
                        new BingDynamicApiMethodDefinition
                        {
                            MethodName = nameof(ICatalogAppService.DownloadAsync),
                            HttpMethod = "GET",
                            RouteTemplate = $"{serviceRoute}/download",
                            ReturnTypeName = typeof(IRemoteStreamContent).FullName,
                            IsStreamResult = true,
                            Parameters =
                            {
                                new BingDynamicApiParameterDefinition
                                {
                                    Name = "cancellationToken",
                                    Position = 0,
                                    TypeName = typeof(CancellationToken).FullName,
                                    BindingSource = "Special",
                                    IsCancellationToken = true
                                }
                            }
                        },
                        new BingDynamicApiMethodDefinition
                        {
                            MethodName = nameof(ICatalogAppService.GetByIdAsync),
                            HttpMethod = "GET",
                            RouteTemplate = $"{serviceRoute}/object/{{id}}",
                            ReturnTypeName = typeof(string).FullName,
                            Parameters =
                            {
                                Parameter<object>(0, "id", "Path"),
                                new BingDynamicApiParameterDefinition
                                {
                                    Name = "cancellationToken",
                                    Position = 1,
                                    TypeName = typeof(CancellationToken).FullName,
                                    BindingSource = "Special",
                                    IsCancellationToken = true
                                }
                            }
                        },
                        new BingDynamicApiMethodDefinition
                        {
                            MethodName = nameof(ICatalogAppService.GetAsync),
                            HttpMethod = "GET",
                            RouteTemplate = $"{serviceRoute}/{{id}}",
                            ReturnTypeName = typeof(CatalogResult).FullName,
                            Parameters =
                            {
                                Parameter<Guid>(0, "id", "Path"),
                                new BingDynamicApiParameterDefinition
                                {
                                    Name = "cancellationToken",
                                    Position = 1,
                                    TypeName = typeof(CancellationToken).FullName,
                                    BindingSource = "Special",
                                    IsCancellationToken = true
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// 创建指定位置和绑定来源的参数描述。
    /// </summary>
    /// <typeparam name="T">参数的 CLR 类型。</typeparam>
    /// <param name="position">参数在方法签名中的位置。</param>
    /// <param name="name">参数名称。</param>
    /// <param name="bindingSource">参数的绑定来源名称。</param>
    /// <returns>对应的 API 参数描述。</returns>
    private static BingDynamicApiParameterDefinition Parameter<T>(int position, string name, string bindingSource)
    {
        return new BingDynamicApiParameterDefinition
        {
            Name = name,
            Position = position,
            TypeName = typeof(T).FullName,
            BindingSource = bindingSource
        };
    }

    /// <summary>
    /// 判断请求是否指向动态 API 描述端点。
    /// </summary>
    /// <param name="request">待检查的 HTTP 请求。</param>
    /// <returns>路径匹配描述端点时返回 true，否则返回 false。</returns>
    private static bool IsDefinitionRequest(HttpRequestMessage request) =>
        request.RequestUri.AbsolutePath.StartsWith("/root/api/dynamic-api/", StringComparison.Ordinal)
        && request.RequestUri.AbsolutePath.EndsWith("/definition", StringComparison.Ordinal);

    /// <summary>
    /// 创建包含 JSON 数据的成功响应。
    /// </summary>
    /// <typeparam name="T">响应数据的类型。</typeparam>
    /// <param name="value">待序列化的响应数据。</param>
    /// <returns>包含 JSON 内容的 HTTP 成功响应。</returns>
    private static HttpResponseMessage JsonResponse<T>(T value)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), System.Text.Encoding.UTF8, "application/json")
        };
    }

    /// <summary>
    /// 验证请求体内容并生成目录查询响应。
    /// </summary>
    /// <param name="request">包含待检查 JSON 请求体的 HTTP 请求。</param>
    /// <returns>包含目录查询结果的异步操作。</returns>
    private static async Task<HttpResponseMessage> ReadBodyAndReturnAsync(HttpRequestMessage request)
    {
        var body = await request.Content.ReadAsStringAsync();
        Assert.Contains("\"name\":\"Bing\"", body, StringComparison.Ordinal);
        Assert.Contains("\"count\":3", body, StringComparison.Ordinal);
        return JsonResponse(new CatalogResult { Name = "Bing", Count = 3 });
    }

    /// <summary>
    /// 定义动态客户端测试使用的目录服务契约。
    /// </summary>
    public interface ICatalogAppService : IAppService
    {
        /// <summary>
        /// 按标识和查询词查找目录数据。
        /// </summary>
        /// <param name="id">目录对象标识。</param>
        /// <param name="term">查询词。</param>
        /// <param name="input">查询请求体。</param>
        /// <param name="cancellationToken">用于取消请求的令牌。</param>
        /// <returns>包含目录查询结果的异步操作。</returns>
        Task<CatalogResult> FindAsync(Guid id, string term, CatalogInput input, CancellationToken cancellationToken);

        /// <summary>
        /// 保存目录数据。
        /// </summary>
        /// <param name="input">待保存的目录数据。</param>
        /// <param name="cancellationToken">用于取消请求的令牌。</param>
        Task SaveAsync(CatalogInput input, CancellationToken cancellationToken);

        /// <summary>
        /// 上传目录相关文件。
        /// </summary>
        /// <param name="file">待上传的远程文件内容。</param>
        /// <param name="cancellationToken">用于取消请求的令牌。</param>
        /// <returns>包含上传结果文本的异步操作。</returns>
        Task<string> UploadAsync(IRemoteStreamContent file, CancellationToken cancellationToken);

        /// <summary>
        /// 下载目录相关文件。
        /// </summary>
        /// <param name="cancellationToken">用于取消请求的令牌。</param>
        /// <returns>包含远程文件内容的异步操作。</returns>
        Task<IRemoteStreamContent> DownloadAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 按对象标识获取目录数据。
        /// </summary>
        /// <param name="id">目录对象标识。</param>
        /// <param name="cancellationToken">用于取消请求的令牌。</param>
        /// <returns>包含对象标识文本的异步操作。</returns>
        Task<string> GetByIdAsync(object id, CancellationToken cancellationToken);

        /// <summary>
        /// 按标识获取目录数据。
        /// </summary>
        /// <param name="id">目录对象标识。</param>
        /// <param name="cancellationToken">用于取消请求的令牌。</param>
        /// <returns>包含目录查询结果的异步操作。</returns>
        Task<CatalogResult> GetAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// 定义用于验证不支持签名的同步方法。
        /// </summary>
        /// <returns>同步测试结果。</returns>
        int UnsupportedSyncMethod();
    }

    /// <summary>
    /// 表示目录查询或保存请求的数据。
    /// </summary>
    public class CatalogInput
    {
        /// <summary>
        /// 获取或设置目录项名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置目录项数量。
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// 表示目录服务返回的数据。
    /// </summary>
    public class CatalogResult
    {
        /// <summary>
        /// 获取或设置目录项名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置目录项数量。
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// 提供动态客户端测试使用的请求上下文。
    /// </summary>
    private sealed class TestRequestContext : IBingDynamicApiRequestContext
    {
        /// <inheritdoc />
        public string TenantId => "tenant-42";

        /// <inheritdoc />
        public string CorrelationId => "correlation-9";

        /// <inheritdoc />
        public string Language => "zh-CN";
    }

    /// <summary>
    /// 根据测试逻辑生成动态客户端 HTTP 响应。
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        /// <summary>
        /// 获取用于生成 HTTP 响应的委托。
        /// </summary>
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        /// <summary>
        /// 初始化 StubHandler 类的新实例。
        /// </summary>
        /// <param name="responseFactory">接收请求和取消令牌并生成响应的委托。</param>
        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        /// <summary>
        /// 获取动态 API 描述请求的累计次数。
        /// </summary>
        public int DefinitionRequestCount { get; private set; }

        /// <summary>
        /// 获取动态 API 业务请求的累计次数。
        /// </summary>
        public int ApiRequestCount { get; private set; }

        /// <summary>
        /// 获取用于通知描述请求已到达的任务源。
        /// </summary>
        public TaskCompletionSource<bool> DefinitionRequestObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取用于通知业务请求已到达的任务源。
        /// </summary>
        public TaskCompletionSource<bool> ApiRequestObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (IsDefinitionRequest(request))
            {
                DefinitionRequestCount++;
                DefinitionRequestObserved.TrySetResult(true);
            }
            else
            {
                ApiRequestCount++;
                ApiRequestObserved.TrySetResult(true);
            }
            return _responseFactory(request, cancellationToken);
        }
    }

    /// <summary>
    /// 为动态客户端请求添加测试认证信息。
    /// </summary>
    private sealed class TestRequestHandler : IBingDynamicApiRequestHandler
    {
        /// <summary>
        /// 获取请求处理器被调用的次数。
        /// </summary>
        public int CallCount { get; private set; }

        /// <inheritdoc />
        public ValueTask HandleAsync(string remoteName, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 提供可观测反序列化次数的测试 JSON 序列化器。
    /// </summary>
    private sealed class TestJsonSerializer : IBingDynamicApiJsonSerializer
    {
        /// <summary>
        /// 获取反序列化操作的累计次数。
        /// </summary>
        public int DeserializeCount { get; private set; }

        /// <inheritdoc />
        public HttpContent Serialize(object value, Type valueType, JsonSerializerOptions options)
        {
            return new StringContent(JsonSerializer.Serialize(value, valueType, options), Encoding.UTF8, "application/vnd.bing-test+json");
        }

        /// <inheritdoc />
        public async Task<object> DeserializeAsync(HttpContent content, Type returnType, JsonSerializerOptions options, CancellationToken cancellationToken)
        {
            DeserializeCount++;
            await using var stream = await content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync(stream, returnType, options, cancellationToken);
        }
    }

    /// <summary>
    /// 为目录查询参数提供可观测的测试转换。
    /// </summary>
    private sealed class TestParameterConverter : IBingDynamicApiParameterConverter
    {
        /// <summary>
        /// 获取参数转换操作的累计次数。
        /// </summary>
        public int CallCount { get; private set; }

        /// <inheritdoc />
        public string ConvertToString(object value, Type valueType, BingDynamicApiParameterDefinition parameter)
        {
            CallCount++;
            return parameter.Name == "term"
                ? "special-value"
                : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// 提供仅对服务不可用响应重试的测试策略。
    /// </summary>
    private sealed class TestRetryPolicy : IBingDynamicApiRetryPolicy
    {
        /// <summary>
        /// 获取重试判定操作的累计次数。
        /// </summary>
        public int ShouldRetryCount { get; private set; }

        /// <summary>
        /// 获取重试延迟计算操作的累计次数。
        /// </summary>
        public int GetDelayCount { get; private set; }

        /// <inheritdoc />
        public bool ShouldRetry(
            string remoteName,
            int attempt,
            HttpStatusCode? statusCode,
            HttpRequestException exception,
            BingDynamicApiRemoteOptions remoteOptions)
        {
            ShouldRetryCount++;
            return statusCode == HttpStatusCode.ServiceUnavailable;
        }

        /// <inheritdoc />
        public TimeSpan GetDelay(string remoteName, int attempt, BingDynamicApiRemoteOptions remoteOptions)
        {
            GetDelayCount++;
            return TimeSpan.Zero;
        }
    }
}
