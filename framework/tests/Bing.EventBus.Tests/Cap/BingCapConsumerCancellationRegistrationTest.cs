using Bing.Events.Cap;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bing.EventBus.Tests.Cap;

/// <summary>
/// 验证消费取消重试适配的显式注册与自定义执行器边界。
/// </summary>
public sealed class BingCapConsumerCancellationRegistrationTest
{
    /// <summary>
    /// 验证首次与重复注册保持同一个单例描述符。
    /// </summary>
    [Fact]
    public void Enable_WhenCalledRepeatedly_ShouldKeepSingleDescriptor()
    {
        var services = new ServiceCollection();
        Assert.Same(services, services.EnableBingCapConsumerCancellationRetry());
        var descriptor = GetInvokerDescriptor(services);
        Assert.Equal(typeof(ISubscribeInvoker), descriptor.ServiceType);
        Assert.Equal(typeof(BingSubscribeInvoker), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        services.EnableBingCapConsumerCancellationRetry();
        Assert.Same(descriptor, GetInvokerDescriptor(services));
        Assert.Single(services, item => item.ServiceType == typeof(IBingCapConsumerRuntime));
    }

    /// <summary>
    /// 验证原生执行器被显式替换且重复调用不再次替换。
    /// </summary>
    [Fact]
    public void Enable_WhenNativeInvokerIsRegistered_ShouldReplaceItOnce()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISubscribeInvoker, SubscribeInvoker>();
        var original = GetInvokerDescriptor(services);
        services.EnableBingCapConsumerCancellationRetry();
        var replacement = GetInvokerDescriptor(services);
        Assert.NotSame(original, replacement);
        Assert.Equal(typeof(BingSubscribeInvoker), replacement.ImplementationType);
        services.EnableBingCapConsumerCancellationRetry();
        Assert.Same(replacement, GetInvokerDescriptor(services));
    }

    /// <summary>
    /// 验证仅处理实际生效的最后一个执行器注册。
    /// </summary>
    [Fact]
    public void Enable_WhenMultipleInvokersExist_ShouldReplaceEffectiveLastOnly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISubscribeInvoker, CustomInvoker>();
        var previous = GetInvokerDescriptor(services);
        services.AddSingleton<ISubscribeInvoker, SubscribeInvoker>();
        services.EnableBingCapConsumerCancellationRetry();
        Assert.Same(previous, services[0]);
        Assert.Equal(typeof(BingSubscribeInvoker), services[1].ImplementationType);
        Assert.Single(services, item => item.ServiceType == typeof(IBingCapConsumerRuntime));
        Assert.Single(services, item => item.ServiceType.FullName ==
            "Bing.Events.Cap.BingCapConsumerHostDrain");
    }

    /// <summary>
    /// 验证未知自定义执行器不会被覆盖。
    /// </summary>
    [Fact]
    public void Enable_WhenCustomInvokerExists_ShouldRejectWithoutChangingIt()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISubscribeInvoker, CustomInvoker>();
        var original = GetInvokerDescriptor(services);
        Assert.Throws<InvalidOperationException>(() => services.EnableBingCapConsumerCancellationRetry());
        Assert.Same(original, GetInvokerDescriptor(services));
    }

    /// <summary>
    /// 验证未知工厂不会为探测类型而执行或被覆盖。
    /// </summary>
    [Fact]
    public void Enable_WhenFactoryExists_ShouldRejectWithoutInvokingIt()
    {
        var services = new ServiceCollection();
        var calls = 0;
        services.AddSingleton<ISubscribeInvoker>(_ => { calls++; return new CustomInvoker(); });
        var original = GetInvokerDescriptor(services);
        Assert.Throws<InvalidOperationException>(() => services.EnableBingCapConsumerCancellationRetry());
        Assert.Equal(0, calls);
        Assert.Same(original, GetInvokerDescriptor(services));
    }

    /// <summary>
    /// 验证空服务集合参数被直接拒绝。
    /// </summary>
    [Fact]
    public void Enable_WhenServicesAreNull_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(() => Bing.Events.Cap.Extensions.EnableBingCapConsumerCancellationRetry(null));
    }

    /// <summary>
    /// 已适配派生类注册保持原描述符及生命周期，重复启用不替换。
    /// </summary>
    [Fact]
    public void Enable_WhenAdaptedDerivedInvokerExists_ShouldKeepOriginalDescriptor()
    {
        var services = new ServiceCollection();
        services.AddScoped<ISubscribeInvoker, AdaptedInvoker>();
        var original = GetInvokerDescriptor(services);

        Assert.Same(services, services.EnableBingCapConsumerCancellationRetry());
        services.EnableBingCapConsumerCancellationRetry();

        Assert.Same(original, GetInvokerDescriptor(services));
        Assert.Equal(typeof(AdaptedInvoker), original.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, original.Lifetime);
    }

    /// <summary>
    /// 验证作用域调用器不会被根容器捕获，排空运行时跟随作用域创建。
    /// </summary>
    [Fact]
    public void Enable_WhenAdaptedInvokerIsScoped_ShouldRegisterScopedRuntime()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISerializer>(new JsonUtf8Serializer(
            Microsoft.Extensions.Options.Options.Create(new DotNetCore.CAP.CapOptions())));
        services.AddScoped<ISubscribeInvoker, AdaptedInvoker>();
        services.EnableBingCapConsumerCancellationRetry();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        var invoker = scope.ServiceProvider.GetRequiredService<ISubscribeInvoker>();
        var runtime = scope.ServiceProvider.GetRequiredService<IBingCapConsumerRuntime>();
        Assert.Same(invoker, runtime);
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IBingCapConsumerRuntime>());
    }

    /// <summary>
    /// 验证 Scoped 调用器不会让单例 CAP 宿主从根容器解析排空运行时。
    /// </summary>
    [Fact]
    public void Enable_WhenScopedInvokerIsUsedByCapHost_ShouldBuildWithValidateScopes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCapEventBus(options => options.UseDispatchingPerGroup = false);
        services.AddScoped<ISubscribeInvoker, BingSubscribeInvoker>();
        services.EnableBingCapConsumerCancellationRetry();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        var hosted = provider.GetServices<IHostedService>();

        Assert.Contains(hosted, item => item.GetType().Name == "CapHostedLifecycleService");
        Assert.DoesNotContain(services, item => item.ServiceType.FullName ==
            "Bing.Events.Cap.BingCapConsumerHostDrain");
    }

    /// <summary>
    /// 验证非法宿主排空时间在解析 CAP 宿主时直接拒绝。
    /// </summary>
    [Fact]
    public void Enable_WhenDrainTimeoutIsInvalid_ShouldRejectHostResolution()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCapEventBus(options => options.UseDispatchingPerGroup = false);
        services.EnableBingCapConsumerCancellationRetry(options =>
        {
            options.DrainTimeout = TimeSpan.FromMilliseconds(-2);
        });

        using var provider = services.BuildServiceProvider();
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.GetServices<IHostedService>().ToArray());
    }

    /// <summary>
    /// 已适配执行器实例注册保持同一实例与原描述符，重复启用不替换。
    /// </summary>
    [Fact]
    public void Enable_WhenAdaptedInvokerInstanceExists_ShouldKeepInstanceAndDescriptor()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var serializer = new JsonUtf8Serializer(Microsoft.Extensions.Options.Options.Create(new DotNetCore.CAP.CapOptions()));
        var instance = new AdaptedInvoker(provider, serializer);
        var services = new ServiceCollection();
        services.AddSingleton<ISubscribeInvoker>(instance);
        var original = GetInvokerDescriptor(services);

        Assert.Same(services, services.EnableBingCapConsumerCancellationRetry());
        services.EnableBingCapConsumerCancellationRetry();

        Assert.Same(original, GetInvokerDescriptor(services));
        Assert.Same(instance, original.ImplementationInstance);
    }

    /// <summary>
    /// 取得当前服务集合中的有效 CAP 调用器描述符。
    /// </summary>
    private static ServiceDescriptor GetInvokerDescriptor(IServiceCollection services) =>
        Assert.Single(services, item => item.ServiceType == typeof(ISubscribeInvoker));

    /// <summary>
    /// 表示调用方已选择取消兼容基类的自定义执行器。
    /// </summary>
    private sealed class AdaptedInvoker : BingSubscribeInvoker
    {
        /// <summary>
        /// 初始化一个 <see cref="AdaptedInvoker" /> 类型的实例。
        /// </summary>
        /// <param name="provider">原生调用器使用的服务提供程序。</param>
        /// <param name="serializer">原生调用器使用的实际序列化器。</param>
        public AdaptedInvoker(IServiceProvider provider, ISerializer serializer) : base(provider, serializer)
        {
        }
    }

    /// <summary>
    /// 提供仅用于注册边界的调用方自定义执行器。
    /// </summary>
    private sealed class CustomInvoker : ISubscribeInvoker
    {
        /// <inheritdoc />
        public Task<ConsumerExecutedResult> InvokeAsync(ConsumerContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
