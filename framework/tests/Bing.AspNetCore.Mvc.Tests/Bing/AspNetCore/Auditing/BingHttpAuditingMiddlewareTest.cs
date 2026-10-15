using System.Security.Claims;
using System.Threading;
using Bing.AspNetCore.Auditing;
using Bing.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Mvc.Tests.Bing.AspNetCore.Auditing;

/// <summary>
/// 验证 HTTP 审计中间件的元数据采集和异常语义。
/// </summary>
public class BingHttpAuditingMiddlewareTest
{
    /// <summary>
    /// 中间件附加清理异常使用的数据键。
    /// </summary>
    private const string CleanupErrorsKey = "Bing.HttpAuditCleanupErrors";

    /// <summary>
    /// 请求完成后保存元数据且不读取请求正文。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldSaveRequestMetadata()
    {
        var store = new RecordingStore();
        var manager = new AuditingManager(store);
        var middleware = CreateMiddleware();
        var context = CreateContext();
        context.Response.StatusCode = StatusCodes.Status201Created;

        await middleware.InvokeAsync(context, manager);

        store.Log.ShouldNotBeNull();
        store.Log.HttpMethod.ShouldBe("POST");
        store.Log.Url.ShouldBe("/orders");
        store.Log.UserId.ShouldBe("u1");
        store.Log.UserName.ShouldBe("alice");
        store.Log.HttpStatusCode.ShouldBe(201);
        store.Log.Exceptions.ShouldBeEmpty();
        manager.Current.ShouldBeNull();
    }

    /// <summary>
    /// 禁用标记存在时不创建审计作用域。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenEndpointDisablesAuditing_ShouldSkipStore()
    {
        var store = new RecordingStore();
        var manager = new AuditingManager(store);
        var middleware = CreateMiddleware();
        var context = CreateContext();
        var disableType = typeof(IAuditingManager).Assembly.GetType("Bing.Auditing.DisableAuditingAttribute");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(Activator.CreateInstance(disableType)), "disabled"));

        await middleware.InvokeAsync(context, manager);

        store.Log.ShouldBeNull();
    }

    /// <summary>
    /// 业务异常优先于审计保存异常返回。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenRequestAndStoreFail_ShouldKeepRequestException()
    {
        var store = new RecordingStore { Failure = new InvalidOperationException("store") };
        var manager = new AuditingManager(store);
        var middleware = new BingHttpAuditingMiddleware(_ =>
            Task.FromException(new InvalidOperationException("request")),
            Microsoft.Extensions.Options.Options.Create(new BingHttpAuditingOptions()));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(CreateContext(), manager));

        exception.Message.ShouldBe("request");
        store.Log.Exceptions.Count.ShouldBe(1);
        manager.Current.ShouldBeNull();
    }

    /// <summary>
    /// 审计存储失败策略可配置为阻断没有业务异常的请求。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenStoreFailsAndThrowEnabled_ShouldThrowStoreException()
    {
        var storeFailure = new InvalidOperationException("store");
        var store = new RecordingStore { Failure = storeFailure };
        var middleware = new BingHttpAuditingMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingHttpAuditingOptions
            {
                ThrowOnStoreFailure = true
            }));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(CreateContext(), new AuditingManager(store)));

        exception.ShouldBeSameAs(storeFailure);
        exception.Message.ShouldBe("store");
        var errors = exception.Data[CleanupErrorsKey].ShouldBeOfType<Exception[]>();
        errors.ShouldHaveSingleItem().ShouldBeSameAs(storeFailure);
    }

    /// <summary>
    /// 保存和异步释放同时失败时抛出首个异常并保留全部清理异常。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenStoreAndDisposeFail_ShouldThrowFirstFailureAndPreserveCleanupErrors()
    {
        var saveFailure = new InvalidOperationException("save-secret");
        var disposeFailure = new ApplicationException("dispose-secret");
        var manager = new FaultingManager(saveFailure, disposeFailure);
        var logger = new RecordingLogger();
        var middleware = new BingHttpAuditingMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingHttpAuditingOptions
            {
                ThrowOnStoreFailure = true
            }), logger);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(CreateContext(), manager));

        exception.ShouldBeSameAs(saveFailure);
        var errors = exception.Data[CleanupErrorsKey].ShouldBeOfType<Exception[]>();
        errors.Length.ShouldBe(2);
        errors[0].ShouldBeSameAs(saveFailure);
        errors[1].ShouldBeSameAs(disposeFailure);
        manager.Handle.SaveCalls.ShouldBe(1);
        manager.Handle.DisposeAsyncCalls.ShouldBe(1);
        manager.Current.ShouldBeNull();
        var message = string.Join("|", logger.Messages);
        message.ShouldNotContain("save-secret");
        message.ShouldNotContain("dispose-secret");
    }

    /// <summary>
    /// 请求异常与保存、释放异常同时出现时保留请求异常并附加清理错误。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenRequestStoreAndDisposeFail_ShouldKeepRequestExceptionAndCleanupErrors()
    {
        var requestFailure = new InvalidOperationException("request-secret");
        var saveFailure = new InvalidOperationException("save-secret");
        var disposeFailure = new ApplicationException("dispose-secret");
        var manager = new FaultingManager(saveFailure, disposeFailure);
        var logger = new RecordingLogger();
        var middleware = new BingHttpAuditingMiddleware(_ => Task.FromException(requestFailure),
            Microsoft.Extensions.Options.Options.Create(new BingHttpAuditingOptions()), logger);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(CreateContext(), manager));

        exception.ShouldBeSameAs(requestFailure);
        var errors = exception.Data[CleanupErrorsKey].ShouldBeOfType<Exception[]>();
        errors.Length.ShouldBe(2);
        errors[0].ShouldBeSameAs(saveFailure);
        errors[1].ShouldBeSameAs(disposeFailure);
        manager.Handle.SaveCalls.ShouldBe(1);
        manager.Handle.DisposeAsyncCalls.ShouldBe(1);
        manager.Current.ShouldBeNull();
        var message = string.Join("|", logger.Messages);
        message.ShouldNotContain("request-secret");
        message.ShouldNotContain("save-secret");
        message.ShouldNotContain("dispose-secret");
    }

    /// <summary>
    /// 仅异步释放失败时抛出释放异常并保留其对象身份。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenDisposeFailsWithThrowEnabled_ShouldThrowDisposeFailure()
    {
        var disposeFailure = new ApplicationException("dispose-secret");
        var manager = new FaultingManager(null, disposeFailure);
        var logger = new RecordingLogger();
        var middleware = new BingHttpAuditingMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingHttpAuditingOptions
            {
                ThrowOnStoreFailure = true
            }), logger);

        var exception = await Should.ThrowAsync<ApplicationException>(() =>
            middleware.InvokeAsync(CreateContext(), manager));

        exception.ShouldBeSameAs(disposeFailure);
        var errors = exception.Data[CleanupErrorsKey].ShouldBeOfType<Exception[]>();
        errors.ShouldHaveSingleItem().ShouldBeSameAs(disposeFailure);
        manager.Handle.SaveCalls.ShouldBe(1);
        manager.Handle.DisposeAsyncCalls.ShouldBe(1);
        manager.Current.ShouldBeNull();
        string.Join("|", logger.Messages).ShouldNotContain("dispose-secret");
    }

    /// <summary>
    /// 默认策略下保存和释放失败只告警并继续完成成功请求。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenStoreAndDisposeFailWithDefaultPolicy_ShouldForwardRequest()
    {
        var forwarded = false;
        var saveFailure = new InvalidOperationException("save-secret");
        var disposeFailure = new ApplicationException("dispose-secret");
        var manager = new FaultingManager(saveFailure, disposeFailure);
        var logger = new RecordingLogger();
        var middleware = new BingHttpAuditingMiddleware(_ =>
        {
            forwarded = true;
            return Task.CompletedTask;
        }, Microsoft.Extensions.Options.Options.Create(new BingHttpAuditingOptions()), logger);

        await middleware.InvokeAsync(CreateContext(), manager);

        forwarded.ShouldBeTrue();
        manager.Handle.SaveCalls.ShouldBe(1);
        manager.Handle.DisposeAsyncCalls.ShouldBe(1);
        manager.Current.ShouldBeNull();
        var message = string.Join("|", logger.Messages);
        message.ShouldContain("ErrorCount=2");
        message.ShouldContain(nameof(InvalidOperationException));
        message.ShouldContain(nameof(ApplicationException));
        message.ShouldNotContain("save-secret");
        message.ShouldNotContain("dispose-secret");
    }

    /// <summary>
    /// 注册入口应用配置并保持审计管理器的默认注册。
    /// </summary>
    [Fact]
    public void AddBingHttpAuditing_ShouldApplyOptions()
    {
        var services = new ServiceCollection();
        services.AddBingHttpAuditing(options =>
        {
            options.ApplicationName = "orders";
            options.MaxUrlLength = 64;
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        provider.GetRequiredService<IOptions<BingHttpAuditingOptions>>().Value.ApplicationName.ShouldBe("orders");
        scope.ServiceProvider.GetRequiredService<IAuditingManager>().ShouldBeOfType<AuditingManager>();
    }

    /// <summary>
    /// 查询参数只记录构造时确定的白名单。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldRecordOnlyAllowedQueryParameters()
    {
        var store = new RecordingStore();
        var options = new BingHttpAuditingOptions();
        options.AllowedQueryParameters.Add("id");
        var middleware = new BingHttpAuditingMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(options));
        options.AllowedQueryParameters.Add("token");
        var context = CreateContext();
        context.Request.QueryString = new QueryString("?token=secret&id=hello%20world&id=2&password=hidden");

        await middleware.InvokeAsync(context, new AuditingManager(store));

        store.Log.Url.ShouldBe("/orders?id=hello%20world&id=2");
    }

    /// <summary>
    /// 未认证身份不能提供审计用户、租户和客户端。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldIgnoreUnauthenticatedIdentityClaims()
    {
        var store = new RecordingStore();
        var context = CreateContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "untrusted"),
            new Claim("tenant_id", "untrusted"),
            new Claim("client_id", "untrusted")
        }));

        await CreateMiddleware().InvokeAsync(context, new AuditingManager(store));

        store.Log.UserId.ShouldBeNull();
        store.Log.TenantId.ShouldBeNull();
        store.Log.ClientId.ShouldBeNull();
    }

    /// <summary>
    /// 多身份请求只采用已认证身份中的配置声明。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ShouldReadConfiguredClaimsFromAuthenticatedIdentity()
    {
        var store = new RecordingStore();
        var options = new BingHttpAuditingOptions { UserIdClaimType = "sub", TenantClaimType = "tenant" };
        var context = CreateContext();
        context.User = new ClaimsPrincipal(new[]
        {
            new ClaimsIdentity(new[] { new Claim("sub", "untrusted"), new Claim("tenant", "untrusted") }),
            new ClaimsIdentity(new[] { new Claim("sub", "u1"), new Claim("tenant", "t1") }, "test")
        });
        var middleware = new BingHttpAuditingMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(options));

        await middleware.InvokeAsync(context, new AuditingManager(store));

        store.Log.UserId.ShouldBe("u1");
        store.Log.TenantId.ShouldBe("t1");
    }

    /// <summary>
    /// 首个已认证身份缺少用户时不借用后续身份的用户声明。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenFirstAuthenticatedIdentityHasNoUser_ShouldNotBorrowLaterUser()
    {
        var store = new RecordingStore();
        var context = CreateContext();
        context.User = new ClaimsPrincipal(new[]
        {
            new ClaimsIdentity(new[]
            {
                new Claim("tenant_id", "selected-tenant"), new Claim(ClaimTypes.Name, "selected-name"),
                new Claim("client_id", "selected-client")
            }, "selected"),
            CompleteIdentity("later")
        });

        await CreateMiddleware().InvokeAsync(context, new AuditingManager(store));

        store.Log.UserId.ShouldBeNull();
        store.Log.UserName.ShouldBe("selected-name");
        store.Log.TenantId.ShouldBe("selected-tenant");
        store.Log.ClientId.ShouldBe("selected-client");
    }

    /// <summary>
    /// 首个已认证身份缺少租户时不借用后续身份的租户声明。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenFirstAuthenticatedIdentityHasNoTenant_ShouldNotBorrowLaterTenant()
    {
        var store = new RecordingStore();
        var context = CreateContext();
        context.User = new ClaimsPrincipal(new[]
        {
            new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "selected-user"),
                new Claim(ClaimTypes.Name, "selected-name"), new Claim("client_id", "selected-client")
            }, "selected"),
            CompleteIdentity("later")
        });

        await CreateMiddleware().InvokeAsync(context, new AuditingManager(store));

        store.Log.UserId.ShouldBe("selected-user");
        store.Log.UserName.ShouldBe("selected-name");
        store.Log.TenantId.ShouldBeNull();
        store.Log.ClientId.ShouldBe("selected-client");
    }

    /// <summary>
    /// 用户和租户所属身份缺少名称及客户端时保持空值。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenSelectedIdentityHasNoNameOrClient_ShouldNotBorrowLaterClaims()
    {
        var store = new RecordingStore();
        var context = CreateContext();
        context.User = new ClaimsPrincipal(new[]
        {
            new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "selected-user"), new Claim("tenant_id", "selected-tenant")
            }, "selected"),
            CompleteIdentity("later")
        });

        await CreateMiddleware().InvokeAsync(context, new AuditingManager(store));

        store.Log.UserId.ShouldBe("selected-user");
        store.Log.UserName.ShouldBeNull();
        store.Log.TenantId.ShouldBe("selected-tenant");
        store.Log.ClientId.ShouldBeNull();
    }

    /// <summary>
    /// 身份顺序决定唯一可信来源，未认证身份不参与选择。
    /// </summary>
    /// <param name="reverse">是否交换两个已认证身份的顺序。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvokeAsync_WhenAuthenticatedIdentitiesAreReordered_ShouldUseFirstAuthenticatedIdentity(bool reverse)
    {
        var store = new RecordingStore();
        var context = CreateContext();
        var first = CompleteIdentity("first");
        var second = CompleteIdentity("second");
        context.User = new ClaimsPrincipal(new[]
        {
            new ClaimsIdentity(CompleteIdentity("untrusted").Claims),
            reverse ? second : first,
            reverse ? first : second
        });

        await CreateMiddleware().InvokeAsync(context, new AuditingManager(store));

        var selected = reverse ? "second" : "first";
        store.Log.UserId.ShouldBe(selected + "-user");
        store.Log.UserName.ShouldBe(selected + "-name");
        store.Log.TenantId.ShouldBe(selected + "-tenant");
        store.Log.ClientId.ShouldBe(selected + "-client");
    }

    /// <summary>
    /// 创建包含四种审计声明的已认证身份。
    /// </summary>
    /// <param name="prefix">身份及声明值的区分前缀。</param>
    /// <returns>具有用户、名称、租户和客户端声明的已认证身份。</returns>
    private static ClaimsIdentity CompleteIdentity(string prefix) => new(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, prefix + "-user"), new Claim(ClaimTypes.Name, prefix + "-name"),
        new Claim("tenant_id", prefix + "-tenant"), new Claim("client_id", prefix + "-client")
    }, prefix);

    /// <summary>
    /// 创建含用户身份的测试请求。
    /// </summary>
    /// <returns>包含默认认证身份的请求上下文。</returns>
    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/orders";
        context.Request.QueryString = new QueryString("?id=1");
        context.TraceIdentifier = "trace-1";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "u1"),
            new Claim(ClaimTypes.Name, "alice")
        }, "test"));
        return context;
    }

    /// <summary>
    /// 创建测试中间件。
    /// </summary>
    /// <returns>使用默认审计配置的中间件。</returns>
    private static BingHttpAuditingMiddleware CreateMiddleware() =>
        new(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingHttpAuditingOptions()));

    /// <summary>
    /// 保存超过预算时仍等待底层任务完成后再释放作用域。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenStoreExceedsSaveBudget_ShouldWaitForActualCompletion()
    {
        var store = new DelayedStore();
        var middleware = new BingHttpAuditingMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingHttpAuditingOptions
            {
                SaveTimeout = TimeSpan.FromMilliseconds(10)
            }));
        var invocation = middleware.InvokeAsync(new DefaultHttpContext(), new AuditingManager(store));

        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);
        invocation.IsCompleted.ShouldBeFalse();

        store.Completion.TrySetResult(true);
        await invocation;
        store.SaveCompleted.ShouldBeTrue();
    }

    /// <summary>
    /// 保存等待预算为非法负值时拒绝创建中间件。
    /// </summary>
    [Fact]
    public void Constructor_WhenSaveTimeoutIsInvalid_ShouldThrow()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BingHttpAuditingMiddleware(
            _ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingHttpAuditingOptions
            {
                SaveTimeout = TimeSpan.FromMilliseconds(-2)
            })));

        exception.ParamName.ShouldBe("options");
    }

    /// <summary>
    /// 超过保存预算且启用失败传播时，在实际保存完成后传播超时诊断。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenStoreExceedsBudgetAndThrowEnabled_ShouldPropagateTimeoutAfterCompletion()
    {
        var store = new DelayedStore();
        var middleware = new BingHttpAuditingMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingHttpAuditingOptions
            {
                SaveTimeout = TimeSpan.FromMilliseconds(10),
                ThrowOnStoreFailure = true
            }));
        var invocation = middleware.InvokeAsync(new DefaultHttpContext(), new AuditingManager(store));

        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);
        invocation.IsCompleted.ShouldBeFalse();
        store.Completion.TrySetResult(true);

        var exception = await Should.ThrowAsync<TimeoutException>(() => invocation);

        store.SaveCompleted.ShouldBeTrue();
        exception.Message.ShouldContain("等待预算");
    }

    /// <summary>
    /// 验证请求中止时将取消令牌传递给支持取消的审计存储。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenRequestIsAborted_ShouldForwardCancellationToStore()
    {
        using var cancellation = new CancellationTokenSource();
        var store = new CancellableStore();
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext
        {
            RequestAborted = cancellation.Token
        };
        var invocation = middleware.InvokeAsync(context, new AuditingManager(store));

        var token = await store.Token.Task.WaitAsync(TimeSpan.FromSeconds(2));
        token.CanBeCanceled.ShouldBeTrue();
        token.ShouldBe(cancellation.Token);
        cancellation.Cancel();

        await invocation.WaitAsync(TimeSpan.FromSeconds(2));
        store.Cancelled.ShouldBeTrue();
    }

    /// <summary>
    /// 记录审计日志的测试存储。
    /// </summary>
    private sealed class RecordingStore : IAuditingStore
    {
        /// <summary>
        /// 获取保存的审计日志。
        /// </summary>
        public AuditLogInfo Log { get; private set; }

        /// <summary>
        /// 获取或设置保存时抛出的异常。
        /// </summary>
        public Exception Failure { get; set; }

        /// <inheritdoc />
        public Task SaveAsync(AuditLogInfo auditInfo)
        {
            Log = auditInfo;
            return Failure == null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    /// <summary>
    /// 延迟完成保存任务以验证作用域释放边界。
    /// </summary>
    private sealed class DelayedStore : IAuditingStore
    {
        /// <summary>
        /// 保存开始通知。
        /// </summary>
        public TaskCompletionSource<bool> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 保存完成通知。
        /// </summary>
        public TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取保存任务是否已完成。
        /// </summary>
        public bool SaveCompleted => Completion.Task.IsCompleted;

        /// <inheritdoc />
        public async Task SaveAsync(AuditLogInfo auditInfo)
        {
            Started.TrySetResult(true);
            await Completion.Task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 记录请求取消并等待令牌触发的审计存储。
    /// </summary>
    private sealed class CancellableStore : ICancellableAuditingStore
    {
        /// <summary>
        /// 收到的取消令牌。
        /// </summary>
        public TaskCompletionSource<CancellationToken> Token { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 保存是否因取消令牌结束。
        /// </summary>
        public bool Cancelled { get; private set; }

        /// <inheritdoc />
        public Task SaveAsync(AuditLogInfo auditInfo) => Task.CompletedTask;

        /// <inheritdoc />
        public async Task SaveAsync(AuditLogInfo auditInfo, CancellationToken cancellationToken)
        {
            Token.TrySetResult(cancellationToken);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Cancelled = true;
                throw;
            }
        }
    }

    /// <summary>
    /// 提供可独立注入保存和释放异常的审计管理器。
    /// </summary>
    private sealed class FaultingManager : IAuditingManager
    {
        /// <summary>
        /// 初始化一个 <see cref="FaultingManager" /> 类型的实例。
        /// </summary>
        /// <param name="saveFailure">保存阶段异常。</param>
        /// <param name="disposeFailure">异步释放阶段异常。</param>
        public FaultingManager(Exception saveFailure, Exception disposeFailure)
        {
            var scope = new AuditLogScope(new AuditLogInfo());
            Handle = new FaultingHandle(scope, saveFailure, disposeFailure, () => Current = null);
            Current = scope;
        }

        /// <summary>
        /// 获取可观测的故障注入句柄。
        /// </summary>
        public FaultingHandle Handle { get; }

        /// <inheritdoc />
        public IAuditLogScope Current { get; private set; }

        /// <inheritdoc />
        public IAuditLogSaveHandle BeginScope() => Handle;
    }

    /// <summary>
    /// 记录保存、释放次数并分别返回预设异常的异步审计句柄。
    /// </summary>
    private sealed class FaultingHandle : IAsyncAuditLogSaveHandle
    {
        /// <summary>
        /// 释放后清除当前管理器作用域的回调。
        /// </summary>
        private readonly Action _onDisposed;

        /// <summary>
        /// 保存阶段预设异常。
        /// </summary>
        private readonly Exception _saveFailure;

        /// <summary>
        /// 异步释放阶段预设异常。
        /// </summary>
        private readonly Exception _disposeFailure;

        /// <summary>
        /// 当前作用域日志。
        /// </summary>
        private readonly IAuditLogScope _scope;

        /// <summary>
        /// 是否已执行释放回调。
        /// </summary>
        private int _disposed;

        /// <summary>
        /// 初始化一个 <see cref="FaultingHandle" /> 类型的实例。
        /// </summary>
        /// <param name="scope">当前审计日志作用域。</param>
        /// <param name="saveFailure">保存阶段异常。</param>
        /// <param name="disposeFailure">异步释放阶段异常。</param>
        /// <param name="onDisposed">释放后清理管理器作用域的回调。</param>
        public FaultingHandle(IAuditLogScope scope, Exception saveFailure, Exception disposeFailure,
            Action onDisposed)
        {
            _scope = scope;
            _saveFailure = saveFailure;
            _disposeFailure = disposeFailure;
            _onDisposed = onDisposed;
        }

        /// <summary>
        /// 获取当前审计日志作用域。
        /// </summary>
        public IAuditLogScope Scope => _scope;

        /// <summary>
        /// 获取保存调用次数。
        /// </summary>
        public int SaveCalls { get; private set; }

        /// <summary>
        /// 获取异步释放调用次数。
        /// </summary>
        public int DisposeAsyncCalls { get; private set; }

        /// <inheritdoc />
        public Task SaveAsync()
        {
            SaveCalls++;
            return _saveFailure == null ? Task.CompletedTask : Task.FromException(_saveFailure);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            MarkDisposed();
            if (_disposeFailure != null)
                throw _disposeFailure;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            DisposeAsyncCalls++;
            MarkDisposed();
            return _disposeFailure == null
                ? ValueTask.CompletedTask
                : new ValueTask(Task.FromException(_disposeFailure));
        }

        /// <summary>
        /// 仅执行一次管理器作用域清理回调。
        /// </summary>
        private void MarkDisposed()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _onDisposed();
        }
    }

    /// <summary>
    /// 记录中间件输出而不展开异常详情。
    /// </summary>
    private sealed class RecordingLogger : ILogger<BingHttpAuditingMiddleware>
    {
        /// <summary>
        /// 获取格式化后的日志消息。
        /// </summary>
        public List<string> Messages { get; } = new();

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state) => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter) => Messages.Add(formatter(state, exception) + exception);
    }
}
