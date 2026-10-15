using System.Reflection;
using AspectCore.Configuration;
using global::AspectCore.DynamicProxy.Parameters;
using AspectCore.Extensions.Hosting;
using Bing.Aspects;
using Bing.DependencyInjection;
using Bing.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 验证中立参数特性的 AspectCore 注册方式。
/// </summary>
public sealed class ParameterValidationRegistrationTest
{
    /// <summary>
    /// 验证默认启用 AOP 时不会隐式执行参数校验。
    /// </summary>
    [Fact]
    public async Task EnableAop_ShouldKeepParameterValidationDisabledByDefault()
    {
        using var host = CreateHost(_ => { });
        var service = host.Services.GetRequiredService<IParameterProbe>();
        var implementation = host.Services.GetRequiredService<IImplementationParameterProbe>();

        var result = await service.AcceptAsync(null);
        var implementationResult = await implementation.AcceptAsync("");

        Assert.Equal("accepted", result);
        Assert.Equal(string.Empty, implementationResult);
    }

    /// <summary>
    /// 验证无关拦截器不会隐式启用 Bing 参数校验。
    /// </summary>
    [Fact]
    public async Task UnrelatedInterceptor_ShouldNotEnableBingParameterValidation()
    {
        using var host = CreateHost(configuration => configuration.Interceptors.AddTyped<UnrelatedInterceptor>());
        var implementation = host.Services.GetRequiredService<IImplementationParameterProbe>();

        var result = await implementation.AcceptAsync("");

        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// 验证显式启用 Bing 参数校验后会执行中立特性。
    /// </summary>
    [Fact]
    public async Task EnableBingParameterValidation_ShouldExecuteNotNull()
    {
        using var host = CreateHost(configuration => configuration.EnableBingParameterValidation());
        var service = host.Services.GetRequiredService<IParameterProbe>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.AcceptAsync(null));
    }

    /// <summary>
    /// 验证重复调用启用入口时仍会执行后续配置。
    /// </summary>
    [Fact]
    public async Task EnableAop_WhenCalledAgain_ShouldApplyLaterConfiguration()
    {
        using var host = CreateHost(
            configuration => configuration.EnableBingParameterValidation(),
            enableAopInTwoCalls: true);
        var service = host.Services.GetRequiredService<IParameterProbe>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.AcceptAsync(null));
    }

    /// <summary>
    /// 验证新旧参数校验入口组合时，每个标记只执行一次且调用方选择器仍然生效。
    /// </summary>
    [Fact]
    public async Task NewAndLegacyParameterValidation_ShouldKeepCustomSelectorAndRunOnce()
    {
        CustomParameterInterceptor.Reset();
        using var host = CreateHost(configuration =>
        {
            configuration.EnableBingParameterValidation();
            configuration.EnableParameterAspect();
        }, registerCustomSelector: true);

        var service = host.Services.GetRequiredService<IParameterProbe>();
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.AcceptAsync(null));

        Assert.Equal(1, CustomParameterInterceptor.Count);
    }

    /// <summary>
    /// 验证旧参数入口同样执行实现方法参数上的中立标记。
    /// </summary>
    [Fact]
    public async Task LegacyParameterValidation_ShouldExecuteImplementationMarker()
    {
        using var host = CreateHost(configuration => configuration.EnableParameterAspect());
        var implementation = host.Services.GetRequiredService<IImplementationParameterProbe>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => implementation.AcceptAsync(""));
    }

    /// <summary>
    /// 验证验证标记可以作用于接口实现方法，并且集合中的模型各验证一次。
    /// </summary>
    [Fact]
    public async Task ImplementationParameterAndGenericCollection_ShouldValidateOnce()
    {
        using var host = CreateHost(configuration => configuration.EnableBingParameterValidation());
        var implementation = host.Services.GetRequiredService<IImplementationParameterProbe>();
        var collection = host.Services.GetRequiredService<ICollectionParameterProbe>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => implementation.AcceptAsync(""));

        var first = new TrackingParameterModel();
        var second = new TrackingParameterModel();
        await collection.ValidateAsync(new[] { first, second });

        Assert.Equal(1, first.ValidationCount);
        Assert.Equal(1, second.ValidationCount);
    }

    /// <summary>
    /// 验证接口和实现方法上的不同标记均会执行。
    /// </summary>
    [Fact]
    public async Task InterfaceAndImplementationMarkers_ShouldValidateEachRule()
    {
        using var host = CreateHost(configuration => configuration.EnableBingParameterValidation());
        var service = host.Services.GetRequiredService<ICombinedParameterProbe>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.AcceptAsync(""));
    }

    /// <summary>
    /// 验证接口和实现方法上的不同验证标记均会执行。
    /// </summary>
    [Fact]
    public async Task InterfaceNotNullAndImplementationValid_ShouldExecuteBothRules()
    {
        using var host = CreateHost(configuration => configuration.EnableBingParameterValidation());
        var service = host.Services.GetRequiredService<IMixedValidationParameterProbe>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.AcceptAsync(null));

        var model = new TrackingParameterModel();
        await service.AcceptAsync(model);

        Assert.Equal(1, model.ValidationCount);
    }

    /// <summary>
    /// 验证接口和实现方法上的相同验证标记只执行一次。
    /// </summary>
    [Fact]
    public async Task InterfaceAndImplementationSameValidMarker_ShouldExecuteOnce()
    {
        using var host = CreateHost(configuration => configuration.EnableBingParameterValidation());
        var service = host.Services.GetRequiredService<IDuplicateValidationParameterProbe>();
        var model = new TrackingParameterModel();

        await service.AcceptAsync(model);

        Assert.Equal(1, model.ValidationCount);
    }

    /// <summary>
    /// 创建使用指定 AspectCore 配置的测试主机。
    /// </summary>
    /// <param name="configure">AspectCore 配置委托。</param>
    /// <returns>测试主机。</returns>
    private static IHost CreateHost(
        Action<global::AspectCore.Configuration.IAspectConfiguration> configure,
        bool registerCustomSelector = false,
        bool enableAopInTwoCalls = false)
    {
        return new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.AddScoped<IParameterProbe, ParameterProbe>();
                services.AddScoped<IImplementationParameterProbe, ImplementationParameterProbe>();
                services.AddScoped<ICollectionParameterProbe, CollectionParameterProbe>();
                services.AddScoped<ICombinedParameterProbe, CombinedParameterProbe>();
                services.AddScoped<IMixedValidationParameterProbe, MixedValidationParameterProbe>();
                services.AddScoped<IDuplicateValidationParameterProbe, DuplicateValidationParameterProbe>();
                if (registerCustomSelector)
                services.AddScoped<global::AspectCore.DynamicProxy.Parameters.IParameterInterceptorSelector, CustomParameterSelector>();
                if (enableAopInTwoCalls)
                {
                    services.EnableAop();
                    services.EnableAop(configure);
                }
                else
                    services.EnableAop(configure);
            })
            .Build();
    }

    /// <summary>
    /// 带有中立参数标记的测试服务。
    /// </summary>
    public interface IParameterProbe
    {
        /// <summary>
        /// 接收参数并返回固定结果。
        /// </summary>
        Task<string> AcceptAsync([Bing.Aspects.NotNull, CustomParameterMarker] string value);
    }

    /// <summary>
    /// 中立参数标记测试服务实现。
    /// </summary>
    public sealed class ParameterProbe : IParameterProbe
    {
        /// <inheritdoc />
        public Task<string> AcceptAsync(string value) => Task.FromResult("accepted");
    }

    /// <summary>
    /// 使用接口和实现方法参数标记的测试服务契约。
    /// </summary>
    public interface IImplementationParameterProbe
    {
        /// <summary>
        /// 接收非空文本。
        /// </summary>
        Task<string> AcceptAsync(string value);
    }

    /// <summary>
    /// 使用实现方法参数标记的测试服务。
    /// </summary>
    public sealed class ImplementationParameterProbe : IImplementationParameterProbe
    {
        /// <inheritdoc />
        public Task<string> AcceptAsync([Bing.Aspects.NotEmpty] string value) => Task.FromResult(value);
    }

    /// <summary>
    /// 使用泛型集合验证标记的测试服务契约。
    /// </summary>
    public interface ICollectionParameterProbe
    {
        /// <summary>
        /// 验证模型集合。
        /// </summary>
        Task ValidateAsync([Bing.Validation.Valid] IEnumerable<TrackingParameterModel> models);
    }

    /// <summary>
    /// 泛型集合验证测试服务。
    /// </summary>
    public sealed class CollectionParameterProbe : ICollectionParameterProbe
    {
        /// <inheritdoc />
        public Task ValidateAsync(IEnumerable<TrackingParameterModel> models) => Task.CompletedTask;
    }

    /// <summary>
    /// 使用接口和实现方法不同验证标记的测试服务契约。
    /// </summary>
    public interface ICombinedParameterProbe
    {
        /// <summary>
        /// 接收非空文本。
        /// </summary>
        Task<string> AcceptAsync([Bing.Aspects.NotNull] string value);
    }

    /// <summary>
    /// 使用接口和实现方法不同验证标记的测试服务。
    /// </summary>
    public sealed class CombinedParameterProbe : ICombinedParameterProbe
    {
        /// <inheritdoc />
        public Task<string> AcceptAsync([Bing.Aspects.NotEmpty] string value) => Task.FromResult(value);
    }

    /// <summary>
    /// 使用接口和实现方法不同验证标记的对象参数服务契约。
    /// </summary>
    public interface IMixedValidationParameterProbe
    {
        /// <summary>
        /// 接收经过非空和对象验证的模型。
        /// </summary>
        Task<string> AcceptAsync([Bing.Aspects.NotNull] TrackingParameterModel value);
    }

    /// <summary>
    /// 使用接口和实现方法不同验证标记的对象参数服务。
    /// </summary>
    public sealed class MixedValidationParameterProbe : IMixedValidationParameterProbe
    {
        /// <inheritdoc />
        public Task<string> AcceptAsync([Bing.Validation.Valid] TrackingParameterModel value) =>
            Task.FromResult("accepted");
    }

    /// <summary>
    /// 使用接口和实现方法相同验证标记的对象参数服务契约。
    /// </summary>
    public interface IDuplicateValidationParameterProbe
    {
        /// <summary>
        /// 接收需要验证的模型。
        /// </summary>
        Task AcceptAsync([Bing.Validation.Valid] TrackingParameterModel value);
    }

    /// <summary>
    /// 使用接口和实现方法相同验证标记的对象参数服务。
    /// </summary>
    public sealed class DuplicateValidationParameterProbe : IDuplicateValidationParameterProbe
    {
        /// <inheritdoc />
        public Task AcceptAsync([Bing.Validation.Valid] TrackingParameterModel value) => Task.CompletedTask;
    }

    /// <summary>
    /// 记录验证调用次数的测试模型。
    /// </summary>
    public sealed class TrackingParameterModel : IVerifyModel
    {
        /// <summary>
        /// 获取验证调用次数。
        /// </summary>
        public int ValidationCount { get; private set; }

        /// <inheritdoc />
        public IValidationResult Validate()
        {
            ValidationCount++;
            return null;
        }
    }

    /// <summary>
    /// 标记由调用方参数选择器提供的扩展拦截。
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class CustomParameterMarkerAttribute : Attribute
    {
    }

    /// <summary>
    /// 提供调用方参数拦截器的测试选择器。
    /// </summary>
    public sealed class CustomParameterSelector : global::AspectCore.DynamicProxy.Parameters.IParameterInterceptorSelector
    {
        /// <inheritdoc />
        public global::AspectCore.DynamicProxy.Parameters.IParameterInterceptor[] Select(ParameterInfo parameter)
        {
            return parameter.IsDefined(typeof(CustomParameterMarkerAttribute), inherit: true)
                ? new global::AspectCore.DynamicProxy.Parameters.IParameterInterceptor[] { new CustomParameterInterceptor() }
                : Array.Empty<global::AspectCore.DynamicProxy.Parameters.IParameterInterceptor>();
        }
    }

    /// <summary>
    /// 记录调用次数的自定义参数拦截器。
    /// </summary>
    public sealed class CustomParameterInterceptor : global::AspectCore.DynamicProxy.Parameters.IParameterInterceptor
    {
        /// <summary>
        /// 获取调用次数。
        /// </summary>
        public static int Count;

        /// <summary>
        /// 重置调用次数。
        /// </summary>
        public static void Reset() => Interlocked.Exchange(ref Count, 0);

        /// <inheritdoc />
        public Task Invoke(
            global::AspectCore.DynamicProxy.Parameters.ParameterAspectContext context,
            global::AspectCore.DynamicProxy.Parameters.ParameterAspectDelegate next)
        {
            Interlocked.Increment(ref Count);
            return next(context);
        }
    }

    /// <summary>
    /// 用于验证普通拦截器不会开启参数校验的测试拦截器。
    /// </summary>
    public sealed class UnrelatedInterceptor : global::AspectCore.DynamicProxy.AbstractInterceptorAttribute
    {
        /// <inheritdoc />
        public override Task Invoke(
            global::AspectCore.DynamicProxy.AspectContext context,
            global::AspectCore.DynamicProxy.AspectDelegate next)
        {
            return next(context);
        }
    }
}
