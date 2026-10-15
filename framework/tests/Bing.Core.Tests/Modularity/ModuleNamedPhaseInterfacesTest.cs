using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tests.Modularity;

/// <summary>
/// 验证命名阶段接口的分派与资源清理。
/// </summary>
[Collection("Module static compatibility")]
public class ModuleNamedPhaseInterfacesTest
{
    /// <summary>
    /// 显式接口按依赖顺序执行全部全局阶段。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitInterfaces_RunOnceInDependencyOrder(bool asynchronous)
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBingApplication<SecondModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildServiceProvider();

        if (asynchronous) await provider.UseBingAsync();
        else provider.UseBing();
        await provider.ShutdownBingAsync();

        events.ShouldBe(new[]
        {
            "config-pre-first", "config-pre-second", "config-post-first", "config-post-second",
            "init-pre-first", "init-pre-second", "init-first", "init-second",
            "init-post-first", "init-post-second"
        });
    }

    /// <summary>
    /// 新旧接口同时存在时，新接口调用基类仍只执行一次。
    /// </summary>
    [Fact]
    public void NewInterfacesCallingBase_RunOnceAndTakePriority()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBingApplication<BridgeModule>(options => options.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider();

        provider.UseBing();
        provider.ShutdownBing();

        events.ShouldBe(new[] { "new-pre-config", "new-post-config", "new-pre-init", "new-post-init" });
    }

    /// <summary>
    /// 同名隐藏方法的隐式接口实现仍能进入对应阶段。
    /// </summary>
    [Fact]
    public void ImplicitInterfaces_RunAllNamedPhases()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBingApplication<ImplicitModule>(options => options.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider();

        provider.UseBing();
        provider.ShutdownBing();

        events.ShouldBe(new[] { "implicit-pre-config", "implicit-post-config", "implicit-pre-init", "implicit-post-init" });
    }

    /// <summary>
    /// 虚方法重写优先于同阶段的接口实现。
    /// </summary>
    [Fact]
    public void Overrides_ReplaceNamedInterfaces()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBingApplication<OverrideModule>(options => options.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider();

        provider.UseBing();
        provider.ShutdownBing();

        events.ShouldBe(new[] { "override-pre-config", "override-post-config", "override-pre-init", "override-post-init" });
    }

    /// <summary>
    /// 显式前置初始化失败后关闭已开始模块并保留原异常。
    /// </summary>
    [Fact]
    public void ExplicitPreInitializationFailure_ShutsDownStartedModule()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBingApplication<FailingPreModule>(options => options.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider();

        var error = Should.Throw<InvalidOperationException>(() => provider.UseBing());

        error.Message.ShouldBe("named-pre-failure");
        error.Data["Bing.ModulePhase"].ShouldBe("PreInitialize");
        events.ShouldBe(new[] { "pre-failed", "shutdown" });
    }

    /// <summary>
    /// 旧注册入口遇到新配置接口时提示迁移。
    /// </summary>
    [Fact]
    public void LegacyRegistration_RejectsNamedConfigurationInterfaces()
    {
        var services = new ServiceCollection();
        var builder = services.AddBing();

        var error = Should.Throw<InvalidOperationException>(() => builder.AddModule<BridgeModule>());

        error.Message.ShouldContain("AddBingApplication");
    }

    /// <summary>
    /// 旧注册入口仍执行命名的前后初始化接口。
    /// </summary>
    [Fact]
    public void LegacyRegistration_RunsNamedInitializationInterfaces()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBing().AddModule<LegacyInitializationModule>();
        using var provider = services.BuildServiceProvider();

        provider.UseBing();
        provider.ShutdownBing();

        events.ShouldBe(new[] { "legacy-pre-init", "legacy-post-init" });
    }

    /// <summary>
    /// 提供阶段事件记录。
    /// </summary>
    public abstract class OrderedPhaseModule : BingModule, IPreConfigureServices, IPostConfigureServices,
        IOnPreApplicationInitialization, IOnPostApplicationInitialization
    {
        /// <summary>
        /// 获取阶段事件名称。
        /// </summary>
        protected abstract string Name { get; }

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("init-" + Name);

        /// <inheritdoc />
        void IPreConfigureServices.PreConfigureServices(IServiceCollection services) =>
            Record(services, "config-pre-" + Name);

        /// <inheritdoc />
        void IPostConfigureServices.PostConfigureServices(IServiceCollection services) =>
            Record(services, "config-post-" + Name);

        /// <inheritdoc />
        void IOnPreApplicationInitialization.OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("init-pre-" + Name);

        /// <inheritdoc />
        void IOnPostApplicationInitialization.OnPostApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("init-post-" + Name);
    }

    /// <summary>
    /// 首个模块。
    /// </summary>
    public class FirstModule : OrderedPhaseModule
    {
        /// <inheritdoc />
        protected override string Name => "first";

        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Business;
    }

    /// <summary>
    /// 依赖首个模块的第二个模块。
    /// </summary>
    [DependsOnModule(typeof(FirstModule))]
    public class SecondModule : OrderedPhaseModule
    {
        /// <inheritdoc />
        protected override string Name => "second";

        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Core;
    }

    /// <summary>
    /// 同时实现新旧阶段接口的模块。
    /// </summary>
    public class BridgeModule : BingModule, IPreConfigureServices, IPostConfigureServices,
        IOnPreApplicationInitialization, IOnPostApplicationInitialization,
        IBingPreConfigureServices, IBingPostConfigureServices
    {
        /// <inheritdoc />
        void IPreConfigureServices.PreConfigureServices(IServiceCollection services)
        {
            Record(services, "new-pre-config");
            base.PreConfigureServices(services);
        }

        /// <inheritdoc />
        void IPostConfigureServices.PostConfigureServices(IServiceCollection services)
        {
            Record(services, "new-post-config");
            base.PostConfigureServices(services);
        }

        /// <inheritdoc />
        void IOnPreApplicationInitialization.OnPreApplicationInitialization(BingModuleInitializationContext context)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("new-pre-init");
            base.OnPreApplicationInitialization(context);
        }

        /// <inheritdoc />
        void IOnPostApplicationInitialization.OnPostApplicationInitialization(BingModuleInitializationContext context)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("new-post-init");
            base.OnPostApplicationInitialization(context);
        }

        /// <inheritdoc />
        void IBingPreConfigureServices.PreConfigureServices(IServiceCollection services) => Record(services, "old-pre-config");

        /// <inheritdoc />
        void IBingPostConfigureServices.PostConfigureServices(IServiceCollection services) => Record(services, "old-post-config");
    }

    /// <summary>
    /// 使用虚方法覆盖接口的模块。
    /// </summary>
    public class OverrideModule : BingModule, IPreConfigureServices, IPostConfigureServices,
        IOnPreApplicationInitialization, IOnPostApplicationInitialization
    {
        /// <inheritdoc />
        public override void PreConfigureServices(IServiceCollection services) => Record(services, "override-pre-config");

        /// <inheritdoc />
        public override void PostConfigureServices(IServiceCollection services) => Record(services, "override-post-config");

        /// <inheritdoc />
        public override void OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("override-pre-init");

        /// <inheritdoc />
        public override void OnPostApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("override-post-init");

        /// <inheritdoc />
        void IPreConfigureServices.PreConfigureServices(IServiceCollection services) => Record(services, "interface-pre-config");

        /// <inheritdoc />
        void IPostConfigureServices.PostConfigureServices(IServiceCollection services) => Record(services, "interface-post-config");

        /// <inheritdoc />
        void IOnPreApplicationInitialization.OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("interface-pre-init");

        /// <inheritdoc />
        void IOnPostApplicationInitialization.OnPostApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("interface-post-init");
    }

    /// <summary>
    /// 使用同名隐藏方法隐式实现阶段接口的模块。
    /// </summary>
    public class ImplicitModule : BingModule, IPreConfigureServices, IPostConfigureServices,
        IOnPreApplicationInitialization, IOnPostApplicationInitialization
    {
        /// <inheritdoc />
        public new void PreConfigureServices(IServiceCollection services) => Record(services, "implicit-pre-config");

        /// <inheritdoc />
        public new void PostConfigureServices(IServiceCollection services) => Record(services, "implicit-post-config");

        /// <inheritdoc />
        public new void OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("implicit-pre-init");

        /// <inheritdoc />
        public new void OnPostApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("implicit-post-init");
    }

    /// <summary>
    /// 前置初始化失败的模块。
    /// </summary>
    public class FailingPreModule : BingModule, IOnPreApplicationInitialization, IBingModuleShutdown
    {
        /// <inheritdoc />
        void IOnPreApplicationInitialization.OnPreApplicationInitialization(BingModuleInitializationContext context)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("pre-failed");
            throw new InvalidOperationException("named-pre-failure");
        }

        /// <inheritdoc />
        void IBingModuleShutdown.Shutdown(BingModuleShutdownContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("shutdown");
    }

    /// <summary>
    /// 通过旧入口注册的命名初始化模块。
    /// </summary>
    public class LegacyInitializationModule : BingModule, IOnPreApplicationInitialization,
        IOnPostApplicationInitialization
    {
        /// <inheritdoc />
        void IOnPreApplicationInitialization.OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("legacy-pre-init");

        /// <inheritdoc />
        void IOnPostApplicationInitialization.OnPostApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("legacy-post-init");
    }

    /// <summary>
    /// 在注册阶段记录服务配置事件。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    /// <param name="value">事件名称。</param>
    private static void Record(IServiceCollection services, string value) =>
        ((List<string>)services.First(x => x.ServiceType == typeof(List<string>)).ImplementationInstance).Add(value);
}
