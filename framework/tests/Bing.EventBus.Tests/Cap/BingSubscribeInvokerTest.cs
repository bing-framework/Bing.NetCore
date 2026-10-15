using System.Reflection;
using Bing.Events.Cap;
using DotNetCore.CAP;
using DotNetCore.CAP.Filter;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bing.EventBus.Tests.Cap;

/// <summary>
/// 通过真实 CAP 上下文和原生调用链验证显式取消兼容调用器。
/// </summary>
public sealed class BingSubscribeInvokerTest
{
    /// <summary>
    /// 接口派发转换同步或异步取消，保留原异常与业务令牌且不改变调用上下文。
    /// </summary>
    /// <param name="asynchronous">是否使用实际异步订阅方法抛出取消。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvokeAsync_WhenConsumerCancels_ShouldPreserveOriginalAndUseDerivedInterface(bool asynchronous)
    {
        using var businessCancellation = new CancellationTokenSource();
        businessCancellation.Cancel();
        using var invocationCancellation = new CancellationTokenSource();
        var original = new OperationCanceledException("订阅业务已取消。", businessCancellation.Token);
        var consumer = new NativeConsumer { Failure = original };
        using var provider = CreateProvider(consumer);
        var invoker = new TrackingInvoker(provider, CreateSerializer());
        var context = CreateContext(asynchronous ? nameof(NativeConsumer.CancelAsynchronously) :
            nameof(NativeConsumer.CancelSynchronously));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ((ISubscribeInvoker)invoker).InvokeAsync(context, invocationCancellation.Token));

        Assert.Same(original, failure.InnerException);
        Assert.Equal(businessCancellation.Token,
            Assert.IsType<OperationCanceledException>(failure.InnerException).CancellationToken);
        Assert.True(Assert.IsType<bool>(failure.Data["Bing.CapConsumerCancellation"]));
        Assert.Equal(invocationCancellation.Token, consumer.InvocationToken);
        Assert.Same(context, invoker.ObservedContext);
        Assert.Equal(1, invoker.GetInstanceCalls);
        Assert.Equal(1, consumer.Calls);
    }

    /// <summary>
    /// 正常调用保留原生返回结果、消息标识和回调名称，并执行继承的实例选择。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenConsumerSucceeds_ShouldKeepNativeResultAndInstanceOverride()
    {
        var consumer = new NativeConsumer();
        using var provider = CreateProvider(consumer);
        var invoker = new TrackingInvoker(provider, CreateSerializer());
        var context = CreateContext(nameof(NativeConsumer.Succeed));

        var result = await ((ISubscribeInvoker)invoker).InvokeAsync(context, CancellationToken.None);

        Assert.Equal("原生业务结果", result.Result);
        Assert.Equal(context.DeliverMessage.Headers[Headers.MessageId], result.MessageId);
        Assert.Equal(context.DeliverMessage.Headers[Headers.CallbackName], result.CallbackName);
        Assert.Same(context, invoker.ObservedContext);
        Assert.Equal(1, invoker.GetInstanceCalls);
        Assert.Equal(1, consumer.Calls);
    }

    /// <summary>
    /// 非取消业务异常沿原生路径传播，不转换类型或添加取消标记。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenConsumerFailsWithoutCancellation_ShouldKeepOriginalException()
    {
        var original = new ApplicationException("普通业务失败。");
        var consumer = new NativeConsumer { Failure = original };
        using var provider = CreateProvider(consumer);
        var invoker = new TrackingInvoker(provider, CreateSerializer());

        var failure = await Assert.ThrowsAsync<ApplicationException>(() =>
            ((ISubscribeInvoker)invoker).InvokeAsync(CreateContext(nameof(NativeConsumer.CancelSynchronously)),
                CancellationToken.None));

        Assert.Same(original, failure);
        Assert.False(failure.Data.Contains("Bing.CapConsumerCancellation"));
        Assert.Equal(1, invoker.GetInstanceCalls);
        Assert.Equal(1, consumer.Calls);
    }

    /// <summary>
    /// 显式适配之外的原生接口调用仍传播取消异常，不被全局修改。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenNativeInvokerIsUsed_ShouldKeepNativeCancellation()
    {
        var original = new OperationCanceledException("原生取消。");
        var consumer = new NativeConsumer { Failure = original };
        using var provider = CreateProvider(consumer);
        ISubscribeInvoker invoker = new SubscribeInvoker(provider, CreateSerializer());

        var actual = await Assert.ThrowsAsync<OperationCanceledException>(() => invoker.InvokeAsync(
            CreateContext(nameof(NativeConsumer.CancelAsynchronously)), CancellationToken.None));

        Assert.Same(original, actual);
        Assert.False(actual.Data.Contains("Bing.CapConsumerCancellation"));
        Assert.Equal(1, consumer.Calls);
    }

    /// <summary>
    /// 原生过滤器未处理取消时仍转换异常，并保留过滤器收到的原对象。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenFilterLeavesCancellationUnhandled_ShouldWrapOriginal()
    {
        var original = new OperationCanceledException("未处理的业务取消。");
        var consumer = new NativeConsumer { Failure = original };
        var filter = new CancellationFilter();
        using var provider = CreateProvider(consumer, filter);
        ISubscribeInvoker invoker = new TrackingInvoker(provider, CreateSerializer());

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.InvokeAsync(
            CreateContext(nameof(NativeConsumer.CancelAsynchronously)), CancellationToken.None));

        Assert.Same(original, filter.ObservedException);
        Assert.Same(original, failure.InnerException);
        Assert.True(Assert.IsType<bool>(failure.Data["Bing.CapConsumerCancellation"]));
        Assert.Equal(1, filter.ExecutingCalls);
        Assert.Equal(1, filter.ExceptionCalls);
        Assert.Equal(1, consumer.Calls);
    }

    /// <summary>
    /// 原生过滤器已处理取消时保留其恢复结果，不强行转换为失败。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenFilterHandlesCancellation_ShouldKeepFilterResult()
    {
        var original = new OperationCanceledException("过滤器处理的业务取消。");
        var consumer = new NativeConsumer { Failure = original };
        var filter = new CancellationFilter(handled: true);
        using var provider = CreateProvider(consumer, filter);
        ISubscribeInvoker invoker = new TrackingInvoker(provider, CreateSerializer());
        var context = CreateContext(nameof(NativeConsumer.CancelAsynchronously));

        var result = await invoker.InvokeAsync(context, CancellationToken.None);

        Assert.Equal("过滤器恢复结果", result.Result);
        Assert.Equal(context.DeliverMessage.Headers[Headers.MessageId], result.MessageId);
        Assert.Same(original, filter.ObservedException);
        Assert.False(original.Data.Contains("Bing.CapConsumerCancellation"));
        Assert.Equal(1, filter.ExecutingCalls);
        Assert.Equal(1, filter.ExceptionCalls);
        Assert.Equal(1, consumer.Calls);
    }

    /// <summary>
    /// 原生过滤器将取消改写为普通异常时保留其异常，不再次包装。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenFilterRewritesCancellation_ShouldKeepReplacementException()
    {
        var original = new OperationCanceledException("过滤器改写的业务取消。");
        var replacement = new ApplicationException("过滤器定义的普通失败。", original);
        var consumer = new NativeConsumer { Failure = original };
        var filter = new CancellationFilter(replacement: replacement);
        using var provider = CreateProvider(consumer, filter);
        ISubscribeInvoker invoker = new TrackingInvoker(provider, CreateSerializer());

        var failure = await Assert.ThrowsAsync<ApplicationException>(() => invoker.InvokeAsync(
            CreateContext(nameof(NativeConsumer.CancelAsynchronously)), CancellationToken.None));

        Assert.Same(original, filter.ObservedException);
        Assert.Same(replacement, failure);
        Assert.Same(original, failure.InnerException);
        Assert.False(failure.Data.Contains("Bing.CapConsumerCancellation"));
        Assert.Equal(1, filter.ExceptionCalls);
        Assert.Equal(1, consumer.Calls);
    }

    /// <summary>
    /// 调用令牌开始时已取消，保留原生提前检查并包装其取消且不调用业务。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenInvocationTokenIsAlreadyCancelled_ShouldKeepNativePrecheck()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var consumer = new NativeConsumer();
        using var provider = CreateProvider(consumer);
        var invoker = new TrackingInvoker(provider, CreateSerializer());

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ((ISubscribeInvoker)invoker).InvokeAsync(CreateContext(nameof(NativeConsumer.Succeed)), cancellation.Token));

        Assert.Equal(cancellation.Token,
            Assert.IsType<OperationCanceledException>(failure.InnerException).CancellationToken);
        Assert.True(Assert.IsType<bool>(failure.Data["Bing.CapConsumerCancellation"]));
        Assert.Equal(0, consumer.Calls);
        Assert.Equal(0, invoker.GetInstanceCalls);
    }

    /// <summary>
    /// 调用令牌在真实异步业务入场后取消，原样传递令牌并保留实际取消对象。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_WhenInvocationTokenCancelsDuringConsumer_ShouldKeepTokenAndOriginal()
    {
        using var cancellation = new CancellationTokenSource();
        var consumer = new NativeConsumer();
        using var provider = CreateProvider(consumer);
        var invoker = new TrackingInvoker(provider, CreateSerializer());
        var context = CreateContext(nameof(NativeConsumer.WaitForInvocationCancellation));
        var invocation = ((ISubscribeInvoker)invoker).InvokeAsync(context, cancellation.Token);
        try
        {
            await consumer.CallbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(cancellation.Token, consumer.InvocationToken);
            cancellation.Cancel();
        }
        finally
        {
            consumer.CallbackRelease.TrySetResult(true);
        }
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invocation.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Same(consumer.Failure, failure.InnerException);
        Assert.Equal(cancellation.Token,
            Assert.IsType<OperationCanceledException>(failure.InnerException).CancellationToken);
        Assert.True(Assert.IsType<bool>(failure.Data["Bing.CapConsumerCancellation"]));
        Assert.Same(context, invoker.ObservedContext);
        Assert.Equal(1, consumer.Calls);
        Assert.Equal(1, invoker.GetInstanceCalls);
    }

    /// <summary>
    /// 排空期间保留在途回调，完成后才报告真实退出并拒绝新的回调。
    /// </summary>
    [Fact]
    public async Task ConsumerRuntime_WhenDraining_ShouldWaitForInFlightAndRejectNewInvocation()
    {
        var consumer = new NativeConsumer();
        using var provider = CreateProvider(consumer);
        var invoker = new TrackingInvoker(provider, CreateSerializer());
        var subscribeInvoker = (ISubscribeInvoker)invoker;
        var runtime = (IBingCapConsumerRuntime)invoker;
        var invocation = subscribeInvoker.InvokeAsync(
            CreateContext(nameof(NativeConsumer.WaitForInvocationCancellation)), CancellationToken.None);

        await consumer.CallbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, runtime.InFlightCount);

        runtime.BeginDrain();
        runtime.BeginDrain();
        Assert.True(runtime.IsDraining);
        Assert.False(await runtime.WaitForDrainAsync(TimeSpan.FromMilliseconds(20)));
        using var waitCancellation = new CancellationTokenSource();
        waitCancellation.Cancel();
        Assert.False(await runtime.WaitForDrainAsync(Timeout.InfiniteTimeSpan, waitCancellation.Token));
        Assert.Equal(1, runtime.InFlightCount);

        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() => subscribeInvoker.InvokeAsync(
            CreateContext(nameof(NativeConsumer.Succeed)), CancellationToken.None));
        Assert.True(Assert.IsType<bool>(rejected.Data["Bing.CapConsumerDraining"]));
        Assert.Equal(1, consumer.Calls);

        consumer.CallbackRelease.TrySetResult(true);
        await invocation;
        Assert.True(await runtime.WaitForDrainAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, runtime.InFlightCount);
    }

    /// <summary>
    /// 启用取消重试时注册与调用器共享实例的排空运行时。
    /// </summary>
    [Fact]
    public void EnableCancellationRetry_ShouldExposeInvokerRuntime()
    {
        var services = new ServiceCollection();
        services.AddCapEventBus(_ => { });
        services.EnableBingCapConsumerCancellationRetry();
        using var provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<ISubscribeInvoker>(),
            provider.GetRequiredService<IBingCapConsumerRuntime>());
    }

    /// <summary>
    /// 创建包含真实订阅实例的 Microsoft DI 容器。
    /// </summary>
    /// <param name="consumer">本次原生调用链使用的订阅实例。</param>
    /// <param name="filter">交由 CAP 原生作用域解析的可选真实过滤器。</param>
    /// <returns>支持 CAP 原生作用域解析的服务容器。</returns>
    private static ServiceProvider CreateProvider(NativeConsumer consumer, ISubscribeFilter filter = null)
    {
        var services = new ServiceCollection().AddSingleton(consumer);
        if (filter != null)
            services.AddSingleton(filter);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建 CAP 8.0.1 的原生 UTF-8 序列化器。
    /// </summary>
    /// <returns>传递给原生订阅调用器的实际序列化器。</returns>
    private static ISerializer CreateSerializer() => new JsonUtf8Serializer(Microsoft.Extensions.Options.Options.Create(new CapOptions()));

    /// <summary>
    /// 用实际方法元数据、令牌参数和消息头构造原生消费上下文。
    /// </summary>
    /// <param name="methodName">需要由原生调用器执行的方法名称。</param>
    /// <returns>不使用替身调用器的真实 CAP 消费上下文。</returns>
    private static ConsumerContext CreateContext(string methodName)
    {
        var method = typeof(NativeConsumer).GetMethod(methodName);
        Assert.NotNull(method);
        var descriptor = new ConsumerExecutorDescriptor
        {
            ImplTypeInfo = typeof(NativeConsumer).GetTypeInfo(),
            ServiceTypeInfo = typeof(NativeConsumer).GetTypeInfo(),
            MethodInfo = method,
            Attribute = new CapSubscribeAttribute("bing.tests.consumer-cancellation", false),
            Parameters = method.GetParameters().Select(parameter => new ParameterDescriptor
            {
                Name = parameter.Name,
                ParameterType = parameter.ParameterType,
                IsFromCap = true
            }).ToList()
        };
        var message = new Message(new Dictionary<string, string>
        {
            [Headers.MessageId] = Guid.NewGuid().ToString("N"),
            [Headers.MessageName] = "bing.tests.consumer-cancellation",
            [Headers.CallbackName] = "bing.tests.consumer-cancellation.callback"
        }, null);
        return new ConsumerContext(descriptor, message);
    }

    /// <summary>
    /// 经 CAP ObjectMethodExecutor 实际执行的同步与异步订阅对象。
    /// </summary>
    private sealed class NativeConsumer
    {
        /// <summary>
        /// 获取或设置需要从真实订阅方法抛出的原始异常。
        /// </summary>
        internal Exception Failure { get; set; }

        /// <summary>
        /// 获取原生调用链实际传入的令牌。
        /// </summary>
        internal CancellationToken InvocationToken { get; private set; }

        /// <summary>
        /// 获取实际订阅方法调用次数。
        /// </summary>
        internal int Calls { get; private set; }

        /// <summary>
        /// 获取真实异步业务已经收到调用令牌的信号。
        /// </summary>
        internal TaskCompletionSource<bool> CallbackEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取测试 finally 保证放行的真实业务等待信号。
        /// </summary>
        internal TaskCompletionSource<bool> CallbackRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 收到调用令牌后实际挂起，在测试放行时执行令牌取消检查。
        /// </summary>
        /// <param name="token">由 CAP 原样提供且在入场后取消的调用令牌。</param>
        public async Task WaitForInvocationCancellation([FromCap] CancellationToken token)
        {
            InvocationToken = token;
            Calls++;
            CallbackEntered.TrySetResult(true);
            await CallbackRelease.Task;
            try
            {
                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException exception)
            {
                Failure = exception;
                throw;
            }
        }

        /// <summary>
        /// 记录原生参数后同步抛出本次原始业务异常。
        /// </summary>
        /// <param name="token">由 CAP 原样提供的调用令牌。</param>
        public void CancelSynchronously([FromCap] CancellationToken token)
        {
            InvocationToken = token;
            Calls++;
            throw Failure;
        }

        /// <summary>
        /// 记录原生参数并实际异步挂起后抛出本次原始业务异常。
        /// </summary>
        /// <param name="token">由 CAP 原样提供的调用令牌。</param>
        public async Task CancelAsynchronously([FromCap] CancellationToken token)
        {
            InvocationToken = token;
            Calls++;
            await Task.Yield();
            throw Failure;
        }

        /// <summary>
        /// 记录原生参数并返回正常业务结果。
        /// </summary>
        /// <param name="token">由 CAP 原样提供的调用令牌。</param>
        /// <returns>供原生结果契约断言的业务值。</returns>
        public Task<string> Succeed([FromCap] CancellationToken token)
        {
            InvocationToken = token;
            Calls++;
            return Task.FromResult("原生业务结果");
        }
    }

    /// <summary>
    /// 通过原生过滤器扩展点观察、处理或改写真实订阅取消异常。
    /// </summary>
    private sealed class CancellationFilter : SubscribeFilter
    {
        /// <summary>
        /// 是否由过滤器声明业务异常已处理。
        /// </summary>
        private readonly bool _handled;

        /// <summary>
        /// 过滤器需要传播的可选替换异常。
        /// </summary>
        private readonly Exception _replacement;

        /// <summary>
        /// 初始化一个 <see cref="CancellationFilter" /> 类型的实例。
        /// </summary>
        /// <param name="handled">是否处理异常并返回恢复结果。</param>
        /// <param name="replacement">需要替换的普通异常；为空时保留原异常。</param>
        internal CancellationFilter(bool handled = false, Exception replacement = null)
        {
            _handled = handled;
            _replacement = replacement;
        }

        /// <summary>
        /// 获取原生异常回调收到的原始异常。
        /// </summary>
        internal Exception ObservedException { get; private set; }

        /// <summary>
        /// 获取原生执行前回调次数。
        /// </summary>
        internal int ExecutingCalls { get; private set; }

        /// <summary>
        /// 获取原生异常回调次数。
        /// </summary>
        internal int ExceptionCalls { get; private set; }

        /// <inheritdoc />
        public override Task OnSubscribeExecutingAsync(ExecutingContext context)
        {
            ExecutingCalls++;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task OnSubscribeExceptionAsync(ExceptionContext context)
        {
            ExceptionCalls++;
            ObservedException = context.Exception;
            if (_replacement != null)
                context.Exception = _replacement;
            context.ExceptionHandled = _handled;
            if (_handled)
                context.Result = "过滤器恢复结果";
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 保留原生实例解析并记录继承扩展点是否真实执行的调用器。
    /// </summary>
    private sealed class TrackingInvoker : BingSubscribeInvoker
    {
        /// <summary>
        /// 初始化一个 <see cref="TrackingInvoker" /> 类型的实例。
        /// </summary>
        /// <param name="provider">原生调用器使用的服务提供程序。</param>
        /// <param name="serializer">原生调用器使用的实际序列化器。</param>
        internal TrackingInvoker(IServiceProvider provider, ISerializer serializer) : base(provider, serializer)
        {
        }

        /// <summary>
        /// 获取继承的实例选择扩展点执行次数。
        /// </summary>
        internal int GetInstanceCalls { get; private set; }

        /// <summary>
        /// 获取实例选择扩展点实际收到的原上下文。
        /// </summary>
        internal ConsumerContext ObservedContext { get; private set; }

        /// <inheritdoc />
        protected override object GetInstance(IServiceProvider provider, ConsumerContext context)
        {
            GetInstanceCalls++;
            ObservedContext = context;
            return base.GetInstance(provider, context);
        }
    }
}
