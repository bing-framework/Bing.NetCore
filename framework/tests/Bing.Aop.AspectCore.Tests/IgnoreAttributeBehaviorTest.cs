using AspectCore.DynamicProxy;
using AspectCore.Extensions.Hosting;
using Bing.Aspects;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 验证中立忽略拦截标记的 AspectCore 适配行为。
/// </summary>
public sealed class IgnoreAttributeBehaviorTest
{
    /// <summary>
    /// 验证方法上的 Ignore 标记只跳过该方法的拦截。
    /// </summary>
    [Fact]
    public async Task IgnoredMethod_ShouldSkipInterceptor()
    {
        ProbeInterceptor.Reset();
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.AddScoped<IIgnoreProbe, IgnoreProbe>();
            })
            .Build();

        var service = host.Services.GetRequiredService<IIgnoreProbe>();
        await service.IgnoredAsync();
        Assert.Equal(0, ProbeInterceptor.Count);

        await service.InterceptedAsync();
        Assert.Equal(1, ProbeInterceptor.Count);
    }

    /// <summary>
    /// 验证实现方法上的 Ignore 标记也能跳过拦截。
    /// </summary>
    [Fact]
    public async Task IgnoredImplementationMethod_ShouldSkipInterceptor()
    {
        ProbeInterceptor.Reset();
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.AddScoped<IImplementationIgnoreProbe, ImplementationIgnoreProbe>();
            })
            .Build();

        var service = host.Services.GetRequiredService<IImplementationIgnoreProbe>();
        await service.IgnoredAsync();
        Assert.Equal(0, ProbeInterceptor.Count);

        await service.InterceptedAsync();
        Assert.Equal(1, ProbeInterceptor.Count);
    }

    /// <summary>
    /// 验证类型上的 Ignore 标记会跳过该类型的拦截。
    /// </summary>
    [Fact]
    public async Task IgnoredType_ShouldSkipInterceptor()
    {
        ProbeInterceptor.Reset();
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.AddScoped<IIgnoredTypeProbe, IgnoredTypeProbe>();
            })
            .Build();

        await host.Services.GetRequiredService<IIgnoredTypeProbe>().ExecuteAsync();

        Assert.Equal(0, ProbeInterceptor.Count);
    }

    /// <summary>
    /// 验证属性上的 Ignore 标记会跳过属性访问器拦截。
    /// </summary>
    [Fact]
    public void IgnoredProperty_ShouldSkipAccessorInterceptor()
    {
        ProbeInterceptor.Reset();
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.AddScoped<IIgnorePropertyProbe, IgnorePropertyProbe>();
            })
            .Build();

        _ = host.Services.GetRequiredService<IIgnorePropertyProbe>().Value;

        Assert.Equal(0, ProbeInterceptor.Count);
    }

    /// <summary>
    /// 验证 AspectCore 原生 NonAspect 标记仍然有效。
    /// </summary>
    [Fact]
    public async Task NativeNonAspect_ShouldRemainSupported()
    {
        ProbeInterceptor.Reset();
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.AddScoped<INativeNonAspectProbe, NativeNonAspectProbe>();
            })
            .Build();

        await host.Services.GetRequiredService<INativeNonAspectProbe>().ExecuteAsync();

        Assert.Equal(0, ProbeInterceptor.Count);
    }

    /// <summary>
    /// 验证基类方法上的 Ignore 标记不会违反特性原有的非继承语义。
    /// </summary>
    [Fact]
    public async Task IgnoredBaseMethod_ShouldPreserveNonInheritedAttributeBehavior()
    {
        ProbeInterceptor.Reset();
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.AddScoped<IInheritedIgnoreProbe, InheritedIgnoreProbe>();
            })
            .Build();

        await host.Services.GetRequiredService<IInheritedIgnoreProbe>().ExecuteAsync();

        Assert.Equal(1, ProbeInterceptor.Count);
    }

    /// <summary>
    /// 带有可观测调用次数的测试拦截器。
    /// </summary>
    public sealed class ProbeInterceptor : AbstractInterceptorAttribute
    {
        /// <summary>
        /// 获取已执行次数。
        /// </summary>
        public static int Count;

        /// <inheritdoc />
        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
            Interlocked.Increment(ref Count);
            await next(context);
        }

        /// <summary>
        /// 重置调用次数。
        /// </summary>
        public static void Reset() => Interlocked.Exchange(ref Count, 0);
    }

    /// <summary>
    /// 使用接口方法标记的测试服务。
    /// </summary>
    public interface IIgnoreProbe
    {
        /// <summary>
        /// 不应执行拦截的方法。
        /// </summary>
        [Ignore]
        [ProbeInterceptor]
        Task IgnoredAsync();

        /// <summary>
        /// 应执行拦截的方法。
        /// </summary>
        [ProbeInterceptor]
        Task InterceptedAsync();
    }

    /// <summary>
    /// 接口方法标记的测试服务实现。
    /// </summary>
    public sealed class IgnoreProbe : IIgnoreProbe
    {
        /// <inheritdoc />
        public Task IgnoredAsync() => Task.CompletedTask;

        /// <inheritdoc />
        public Task InterceptedAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 使用实现方法标记的测试服务契约。
    /// </summary>
    public interface IImplementationIgnoreProbe
    {
        /// <summary>
        /// 不应执行拦截的方法。
        /// </summary>
        [ProbeInterceptor]
        Task IgnoredAsync();

        /// <summary>
        /// 应执行拦截的方法。
        /// </summary>
        [ProbeInterceptor]
        Task InterceptedAsync();
    }

    /// <summary>
    /// 实现方法标记的测试服务。
    /// </summary>
    public sealed class ImplementationIgnoreProbe : IImplementationIgnoreProbe
    {
        /// <inheritdoc />
        [Ignore]
        [ProbeInterceptor]
        public Task IgnoredAsync() => Task.CompletedTask;

        /// <inheritdoc />
        [ProbeInterceptor]
        public Task InterceptedAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 使用类型标记的测试服务契约。
    /// </summary>
    public interface IIgnoredTypeProbe
    {
        /// <summary>
        /// 执行测试调用。
        /// </summary>
        Task ExecuteAsync();
    }

    /// <summary>
    /// 带有类型忽略标记的测试服务。
    /// </summary>
    [Ignore]
    public sealed class IgnoredTypeProbe : IIgnoredTypeProbe
    {
        /// <inheritdoc />
        [ProbeInterceptor]
        public Task ExecuteAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 使用属性标记的测试服务契约。
    /// </summary>
    public interface IIgnorePropertyProbe
    {
        /// <summary>
        /// 读取被忽略的属性。
        /// </summary>
        [Ignore]
        string Value { [ProbeInterceptor] get; }
    }

    /// <summary>
    /// 属性忽略标记测试服务。
    /// </summary>
    public sealed class IgnorePropertyProbe : IIgnorePropertyProbe
    {
        /// <inheritdoc />
        public string Value => "value";
    }

    /// <summary>
    /// 使用 AspectCore 原生 NonAspect 标记的测试服务契约。
    /// </summary>
    public interface INativeNonAspectProbe
    {
        /// <summary>
        /// 执行不应被拦截的调用。
        /// </summary>
        [NonAspect]
        [ProbeInterceptor]
        Task ExecuteAsync();
    }

    /// <summary>
    /// AspectCore 原生 NonAspect 测试服务。
    /// </summary>
    public sealed class NativeNonAspectProbe : INativeNonAspectProbe
    {
        /// <inheritdoc />
        public Task ExecuteAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 使用基类忽略标记的测试服务契约。
    /// </summary>
    public interface IInheritedIgnoreProbe
    {
        /// <summary>
        /// 执行继承忽略标记的方法。
        /// </summary>
        [ProbeInterceptor]
        Task ExecuteAsync();
    }

    /// <summary>
    /// 声明忽略标记的测试基类。
    /// </summary>
    public abstract class IgnoredBaseProbe
    {
        /// <summary>
        /// 执行基类方法。
        /// </summary>
        [Ignore]
        public virtual Task ExecuteAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// 继承忽略标记方法的测试服务。
    /// </summary>
    public sealed class InheritedIgnoreProbe : IgnoredBaseProbe, IInheritedIgnoreProbe
    {
        /// <inheritdoc />
        public override Task ExecuteAsync() => Task.CompletedTask;
    }
}
