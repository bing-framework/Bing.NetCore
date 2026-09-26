using Bing.Application.Services;
using Bing.DynamicApi.Client;
using Bing.DynamicApi.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Bing.DynamicApi.Client.Tests;

/// <summary>
/// 验证动态 API 客户端对取消请求的处理。
/// </summary>
public class BingDynamicApiCancellationTest
{
    /// <summary>
    /// 验证取消一个共享描述请求的等待者不会取消实际请求。
    /// </summary>
    [Fact]
    public async Task Should_cancel_one_definition_waiter_without_cancelling_shared_fetch()
    {
        var definitionRequestStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var definitionResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var definitionRequestCount = 0;
        CancellationToken definitionRequestToken = default;
        using var handler = new TestHandler(async (request, cancellationToken) =>
        {
            if (IsDefinitionRequest(request))
            {
                Interlocked.Increment(ref definitionRequestCount);
                definitionRequestToken = cancellationToken;
                definitionRequestStarted.TrySetResult(true);
                return await definitionResponse.Task.ConfigureAwait(false);
            }

            return JsonResponse("shared-result");
        });
        using var provider = CreateProvider(handler);
        var service = provider.GetRequiredService<IProbeAppService>();
        using var firstCallCancellation = new CancellationTokenSource();

        var firstCall = service.GetAsync(firstCallCancellation.Token);
        await definitionRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondCall = service.GetAsync(CancellationToken.None);
        firstCallCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstCall);
        Assert.False(definitionRequestToken.IsCancellationRequested);

        definitionResponse.TrySetResult(JsonResponse(CreateDefinition()));
        Assert.Equal("shared-result", await secondCall.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, definitionRequestCount);
    }

    /// <summary>
    /// 验证已取消的描述请求会清除缓存状态并允许后续重试。
    /// </summary>
    [Fact]
    public async Task Should_remove_cancelled_definition_fetch_and_allow_retry()
    {
        var definitionRequestCount = 0;
        using var handler = new TestHandler((request, _) =>
        {
            if (IsDefinitionRequest(request) && Interlocked.Increment(ref definitionRequestCount) == 1)
                return Task.FromException<HttpResponseMessage>(new OperationCanceledException("metadata fetch cancelled"));

            return Task.FromResult(IsDefinitionRequest(request)
                ? JsonResponse(CreateDefinition())
                : JsonResponse("retried"));
        });
        using var provider = CreateProvider(handler);
        var service = provider.GetRequiredService<IProbeAppService>();
        using var firstCancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(firstCancellation.Token));

        using var retryCancellation = new CancellationTokenSource();
        var result = await service.GetAsync(retryCancellation.Token);

        Assert.Equal("retried", result);
        Assert.Equal(2, definitionRequestCount);
    }

    /// <summary>
    /// 验证方法取消令牌会中止远程错误响应体的读取。
    /// </summary>
    [Fact]
    public async Task Should_cancel_remote_error_body_read_with_method_token()
    {
        var blockingStream = new BlockingReadStream();
        using var handler = new TestHandler((request, _) => Task.FromResult(IsDefinitionRequest(request)
            ? JsonResponse(CreateDefinition())
            : new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StreamContent(blockingStream)
            }));
        using var provider = CreateProvider(handler);
        var service = provider.GetRequiredService<IProbeAppService>();
        using var cancellation = new CancellationTokenSource();

        var call = service.GetAsync(cancellation.Token);
        await blockingStream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var completed = await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(5)));
        if (!ReferenceEquals(completed, call))
            blockingStream.Release();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.True(blockingStream.IsDisposed);
    }

    /// <summary>
    /// 验证方法取消令牌会中止原始字符串响应体的读取。
    /// </summary>
    [Fact]
    public async Task Should_cancel_raw_string_response_body_read_with_method_token()
    {
        var blockingStream = new BlockingReadStream();
        using var handler = new TestHandler((request, _) => Task.FromResult(IsDefinitionRequest(request)
            ? JsonResponse(CreateDefinition())
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(blockingStream)
            }));
        using var provider = CreateProvider(handler);
        var service = provider.GetRequiredService<IProbeAppService>();
        using var cancellation = new CancellationTokenSource();

        var call = service.GetAsync(cancellation.Token);
        await blockingStream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        Assert.True(blockingStream.IsDisposed);
    }

    /// <summary>
    /// 创建注册 probe 动态客户端的服务提供程序。
    /// </summary>
    /// <param name="handler">处理测试 HTTP 请求的消息处理器。</param>
    /// <returns>包含 probe 客户端的服务提供程序。</returns>
    private static ServiceProvider CreateProvider(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddBingDynamicApiClient(options => options.AddRemoteService("probe", remote =>
        {
            remote.BaseUrl = "https://service.test";
            remote.ApiVersion = "v1";
        }));
        services.AddHttpClient("probe").ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddBingDynamicApiClient<IProbeAppService>("probe");
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建 probe 服务的 API 描述测试数据。
    /// </summary>
    /// <returns>包含 probe 查询方法的 API 描述。</returns>
    private static BingDynamicApiDefinition CreateDefinition()
    {
        return new BingDynamicApiDefinition
        {
            ApiVersion = "v1",
            Services =
            {
                new BingDynamicApiServiceDefinition
                {
                    ServiceName = "probe",
                    ServiceTypeName = typeof(IProbeAppService).FullName,
                    Methods =
                    {
                        new BingDynamicApiMethodDefinition
                        {
                            MethodName = nameof(IProbeAppService.GetAsync),
                            HttpMethod = "GET",
                            RouteTemplate = "api/app/v1/probe",
                            ReturnTypeName = typeof(string).FullName,
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
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// 判断请求是否指向动态 API 描述端点。
    /// </summary>
    /// <param name="request">待检查的 HTTP 请求。</param>
    /// <returns>请求路径匹配描述端点时返回 true，否则返回 false。</returns>
    private static bool IsDefinitionRequest(HttpRequestMessage request) =>
        request.RequestUri.AbsolutePath.EndsWith("/api/dynamic-api/v1/definition", StringComparison.Ordinal);

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
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };
    }

    /// <summary>
    /// 定义取消场景测试使用的 probe 服务契约。
    /// </summary>
    public interface IProbeAppService : IAppService
    {
        /// <summary>
        /// 获取 probe 服务响应。
        /// </summary>
        /// <param name="cancellationToken">用于取消请求的令牌。</param>
        /// <returns>包含服务响应文本的异步操作。</returns>
        Task<string> GetAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// 根据委托生成测试 HTTP 响应。
    /// </summary>
    private sealed class TestHandler : HttpMessageHandler
    {
        /// <summary>
        /// 获取用于生成 HTTP 响应的委托。
        /// </summary>
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        /// <summary>
        /// 初始化 TestHandler 类的新实例。
        /// </summary>
        /// <param name="responseFactory">接收请求和取消令牌并生成响应的委托。</param>
        public TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _responseFactory(request, cancellationToken);
        }
    }

    /// <summary>
    /// 提供可暂停读取并记录取消及释放状态的测试流。
    /// </summary>
    private sealed class BlockingReadStream : Stream
    {
        /// <summary>
        /// 获取用于解除阻塞读取的任务源。
        /// </summary>
        private readonly TaskCompletionSource<int> _readResult = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取用于通知读取已开始的任务源。
        /// </summary>
        public TaskCompletionSource<bool> ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取流是否已释放。
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// 解除当前阻塞的读取操作。
        /// </summary>
        public void Release() => _readResult.TrySetResult(0);

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override void Flush() { }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return WaitForReadAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return new ValueTask<int>(WaitForReadAsync(cancellationToken));
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
                _readResult.TrySetCanceled();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 等待读取被释放或取消。
        /// </summary>
        /// <param name="cancellationToken">用于取消读取等待的令牌。</param>
        /// <returns>包含读取字节数的异步操作。</returns>
        private Task<int> WaitForReadAsync(CancellationToken cancellationToken)
        {
            ReadStarted.TrySetResult(true);
            if (cancellationToken.CanBeCanceled)
                cancellationToken.Register(() => _readResult.TrySetCanceled(cancellationToken));
            return _readResult.Task;
        }
    }
}
