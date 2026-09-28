using Bing.Core.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Bing.Tests.Modularity;

/// <summary>
/// 验证配置、宿主环境和生命周期上下文便捷访问契约。
/// </summary>
public sealed class ModuleContextAccessTest
{
    /// <summary>
    /// 直接配置优先于宿主上下文，并保留非 IConfigurationRoot 实现。
    /// </summary>
    [Fact]
    public void GetConfiguration_UsesLastDirectInstanceAndPreservesInterfaceType()
    {
        var root = new ConfigurationBuilder().Build();
        IConfiguration section = root.GetSection("nested");
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(root);
        services.AddSingleton<IConfiguration>(section);
        services.AddSingleton(new HostBuilderContext(new Dictionary<object, object>())
        {
            Configuration = new ConfigurationBuilder().Build()
        });

        services.GetConfiguration().ShouldBeSameAs(section);
        services.GetConfigurationOrNull().ShouldBeSameAs(section);
    }

    /// <summary>
    /// 缺少直接配置注册时从已注册的宿主上下文读取配置。
    /// </summary>
    [Fact]
    public void GetConfiguration_FallsBackToHostBuilderContextInstance()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build().GetSection("fallback");
        var services = new ServiceCollection();
        services.AddSingleton(new HostBuilderContext(new Dictionary<object, object>())
        {
            Configuration = configuration
        });

        services.GetConfiguration().ShouldBeSameAs(configuration);
        services.GetConfigurationOrNull().ShouldBeSameAs(configuration);
    }

    /// <summary>
    /// 工厂及类型注册在注册期不执行且不回退到旧注册。
    /// </summary>
    [Fact]
    public void GetConfiguration_InvalidLastRegistration_ShouldNotRunFactoryOrUsePreviousInstance()
    {
        var first = new ConfigurationBuilder().Build();
        var factoryCalls = 0;
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(first);
        services.AddSingleton(new HostBuilderContext(new Dictionary<object, object>())
        {
            Configuration = first
        });
        services.AddSingleton<IConfiguration>(_ =>
        {
            factoryCalls++;
            return new ConfigurationBuilder().Build();
        });

        services.GetConfigurationOrNull().ShouldBeNull();
        var error = Should.Throw<Bing.BingFrameworkException>(() => services.GetConfiguration());
        error.Message.ShouldContain("运行期通过 DI 获取");
        factoryCalls.ShouldBe(0);

        var typedServices = new ServiceCollection();
        typedServices.AddSingleton<IConfiguration, ConfigurationRoot>();
        typedServices.GetConfigurationOrNull().ShouldBeNull();
        Should.Throw<Bing.BingFrameworkException>(() => typedServices.GetConfiguration());
    }

    /// <summary>
    /// 配置替换后读取新实例，缺失配置保持必需与可选入口的差异。
    /// </summary>
    [Fact]
    public void ReplaceConfiguration_UsesReplacementAndMissingConfigurationIsExplicit()
    {
        var services = new ServiceCollection();
        var first = new ConfigurationBuilder().Build();
        var replacement = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(first);

        services.ReplaceConfiguration(replacement);
        services.GetConfiguration().ShouldBeSameAs(replacement);

        var missing = new ServiceCollection();
        missing.GetConfigurationOrNull().ShouldBeNull();
        Should.Throw<Bing.BingFrameworkException>(() => missing.GetConfiguration());
    }

    /// <summary>
    /// 通用宿主环境只读取最后一个显式单例实例，不构造环境或运行工厂。
    /// </summary>
    [Fact]
    public void GetHostEnvironment_UsesLastInstanceAndDoesNotRunFactory()
    {
        var first = CreateEnvironment("First");
        var last = CreateEnvironment("Last");
        var calls = 0;
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(first);
        services.AddSingleton<IHostEnvironment>(_ =>
        {
            calls++;
            return last;
        });

        services.GetHostEnvironmentOrNull().ShouldBeNull();
        Should.Throw<Bing.BingFrameworkException>(() => services.GetHostEnvironment());
        calls.ShouldBe(0);

        var instanceServices = new ServiceCollection().AddSingleton<IHostEnvironment>(last);
        instanceServices.GetHostEnvironment().ShouldBeSameAs(last);
        instanceServices.GetHostEnvironmentOrNull().ShouldBeSameAs(last);
        var duplicateInstances = new ServiceCollection()
            .AddSingleton<IHostEnvironment>(first)
            .AddSingleton<IHostEnvironment>(last);
        duplicateInstances.GetHostEnvironment().ShouldBeSameAs(last);
        var typedServices = new ServiceCollection().AddSingleton<IHostEnvironment, TestHostEnvironment>();
        typedServices.GetHostEnvironmentOrNull().ShouldBeNull();
        Should.Throw<Bing.BingFrameworkException>(() => typedServices.GetHostEnvironment());

        new ServiceCollection().GetHostEnvironmentOrNull().ShouldBeNull();
    }

    /// <summary>
    /// 通用环境未直接注册时读取最后一个宿主构建上下文的环境实例。
    /// </summary>
    [Fact]
    public void GetHostEnvironment_FallsBackToLastHostBuilderContext()
    {
        var first = new HostBuilderContext(new Dictionary<object, object>())
        {
            HostingEnvironment = CreateEnvironment("First")
        };
        var last = new HostBuilderContext(new Dictionary<object, object>())
        {
            HostingEnvironment = CreateEnvironment("Last")
        };
        var services = new ServiceCollection();
        services.AddSingleton(first);
        services.AddSingleton(last);

        services.GetHostEnvironment().ShouldBeSameAs(last.HostingEnvironment);
        services.GetHostEnvironmentOrNull().ShouldBeSameAs(last.HostingEnvironment);
    }

    /// <summary>
    /// 直接环境实例优先于宿主上下文；无效最后直接注册阻止回退。
    /// </summary>
    [Fact]
    public void GetHostEnvironment_DirectRegistrationPrecedenceAndInvalidRegistration()
    {
        var fallback = CreateEnvironment("Fallback");
        var context = new HostBuilderContext(new Dictionary<object, object>())
        {
            HostingEnvironment = fallback
        };
        var direct = CreateEnvironment("Direct");
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton<IHostEnvironment>(direct);

        services.GetHostEnvironment().ShouldBeSameAs(direct);
        services.GetHostEnvironmentOrNull().ShouldBeSameAs(direct);

        services.AddSingleton<IHostEnvironment>(_ => CreateEnvironment("Factory"));
        services.GetHostEnvironmentOrNull().ShouldBeNull();
        Should.Throw<Bing.BingFrameworkException>(() => services.GetHostEnvironment())
            .Message.ShouldContain("运行期通过 DI 获取");

        var typedServices = new ServiceCollection();
        typedServices.AddSingleton(context);
        typedServices.AddSingleton<IHostEnvironment, TestHostEnvironment>();
        typedServices.GetHostEnvironmentOrNull().ShouldBeNull();
        Should.Throw<Bing.BingFrameworkException>(() => typedServices.GetHostEnvironment());

        var scopedServices = new ServiceCollection();
        scopedServices.AddSingleton(context);
        scopedServices.AddScoped<IHostEnvironment>(_ => fallback);
        scopedServices.GetHostEnvironmentOrNull().ShouldBeNull();
        Should.Throw<Bing.BingFrameworkException>(() => scopedServices.GetHostEnvironment());
    }

    /// <summary>
    /// 环境与宿主上下文工厂均不在注册期执行，缺失环境保持为空或明确报错。
    /// </summary>
    [Fact]
    public void GetHostEnvironment_FallbackDoesNotExecuteFactoriesAndMissingEnvironmentThrows()
    {
        var calls = 0;
        var services = new ServiceCollection();
        services.AddSingleton<HostBuilderContext>(_ =>
        {
            calls++;
            return new HostBuilderContext(new Dictionary<object, object>())
            {
                HostingEnvironment = CreateEnvironment("Factory")
            };
        });

        services.GetHostEnvironmentOrNull().ShouldBeNull();
        Should.Throw<Bing.BingFrameworkException>(() => services.GetHostEnvironment());
        calls.ShouldBe(0);

        var noEnvironmentContext = new ServiceCollection().AddSingleton(
            new HostBuilderContext(new Dictionary<object, object>()));
        noEnvironmentContext.GetHostEnvironmentOrNull().ShouldBeNull();
        Should.Throw<Bing.BingFrameworkException>(() => noEnvironmentContext.GetHostEnvironment());
    }

    /// <summary>
    /// 初始化及关闭上下文均通过各自当前作用域解析配置和环境，不缓存结果。
    /// </summary>
    [Fact]
    public void ModuleContexts_ResolveScopedServicesFromCurrentProvider()
    {
        var configurationA = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string> { ["name"] = "A" }).Build();
        var configurationB = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string> { ["name"] = "B" }).Build();
        var environmentA = CreateEnvironment("A");
        var environmentB = CreateEnvironment("B");
        var providerA = new ServiceCollection()
            .AddScoped<IConfiguration>(_ => configurationA)
            .AddScoped<IHostEnvironment>(_ => environmentA)
            .BuildServiceProvider();
        var providerB = new ServiceCollection()
            .AddScoped<IConfiguration>(_ => configurationB)
            .AddScoped<IHostEnvironment>(_ => environmentB)
            .BuildServiceProvider();
        using (providerA)
        using (providerB)
        using (var scopeA = providerA.CreateScope())
        using (var scopeB = providerB.CreateScope())
        {
            var initializationA = new BingModuleInitializationContext(scopeA.ServiceProvider);
            var initializationB = new BingModuleInitializationContext(scopeB.ServiceProvider);
            initializationA.GetConfiguration().ShouldBeSameAs(configurationA);
            initializationA.GetHostEnvironment().ShouldBeSameAs(environmentA);
            configurationA["name"] = "A-reloaded";
            initializationA.GetConfiguration()["name"].ShouldBe("A-reloaded");
            initializationB.GetConfiguration()["name"].ShouldBe("B");
            initializationB.GetHostEnvironment().ShouldBeSameAs(environmentB);
            initializationA.GetConfigurationOrNull().ShouldBeSameAs(configurationA);

            var shutdownA = new BingModuleShutdownContext(scopeA.ServiceProvider);
            var shutdownB = new BingModuleShutdownContext(scopeB.ServiceProvider);
            shutdownA.GetConfiguration().ShouldBeSameAs(configurationA);
            shutdownA.GetHostEnvironment().ShouldBeSameAs(environmentA);
            shutdownB.GetConfiguration().ShouldBeSameAs(configurationB);
            shutdownB.GetHostEnvironment().ShouldBeSameAs(environmentB);
            shutdownB.GetHostEnvironmentOrNull().ShouldBeSameAs(environmentB);
        }
    }

    /// <summary>
    /// 上下文的可选访问返回 null，必需访问会给出服务类型诊断。
    /// </summary>
    [Fact]
    public void ModuleContexts_MissingServices_ReturnNullOrThrow()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var initialization = new BingModuleInitializationContext(provider);
        initialization.GetConfigurationOrNull().ShouldBeNull();
        initialization.GetHostEnvironmentOrNull().ShouldBeNull();
        Should.Throw<Bing.BingFrameworkException>(() => initialization.GetConfiguration())
            .Message.ShouldContain(typeof(IConfiguration).AssemblyQualifiedName);
        Should.Throw<Bing.BingFrameworkException>(() => initialization.GetHostEnvironment())
            .Message.ShouldContain(typeof(IHostEnvironment).AssemblyQualifiedName);

        var shutdown = new BingModuleShutdownContext(provider);
        shutdown.GetConfigurationOrNull().ShouldBeNull();
        shutdown.GetHostEnvironmentOrNull().ShouldBeNull();
        Should.Throw<Bing.BingFrameworkException>(() => shutdown.GetConfiguration());
        Should.Throw<Bing.BingFrameworkException>(() => shutdown.GetHostEnvironment());
    }

    /// <summary>
    /// 实际生命周期的三个初始化阶段和关闭阶段均使用当前宿主的配置与环境。
    /// </summary>
    [Fact]
    public void ModuleLifecycle_UsesCurrentContextAcrossAllStages()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string> { ["module"] = "Current" }).Build());
        services.AddSingleton<IHostEnvironment>(CreateEnvironment("Current"));
        services.AddSingleton<ContextPhaseRecorder>();
        services.AddBingApplication<ContextPhaseModule>();

        using var provider = services.BuildServiceProvider();
        provider.UseBing();
        provider.ShutdownBing();

        provider.GetRequiredService<ContextPhaseRecorder>().Stages.ShouldBe(new[]
        {
            "Pre:Current:Current", "Main:Current:Current", "Post:Current:Current",
            "Shutdown:Current:Current"
        });
    }

    /// <summary>
    /// 记录生命周期上下文读取结果。
    /// </summary>
    public sealed class ContextPhaseRecorder
    {
        /// <summary>
        /// 获取阶段记录。
        /// </summary>
        public List<string> Stages { get; } = new();

        /// <summary>
        /// 保存本轮初始化使用的短期服务作用域。
        /// </summary>
        public IServiceProvider InitializationProvider { get; set; }
    }

    /// <summary>
    /// 通过真实生命周期回调读取上下文的测试模块。
    /// </summary>
    public sealed class ContextPhaseModule : BingModule
    {
        /// <inheritdoc />
        public override void OnPreApplicationInitialization(BingModuleInitializationContext context)
        {
            var recorder = context.ServiceProvider.GetRequiredService<ContextPhaseRecorder>();
            recorder.InitializationProvider = context.ServiceProvider;
            recorder.Stages.Add("Pre:" + context.GetConfiguration()["module"] + ":" +
                context.GetHostEnvironment().EnvironmentName);
        }

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context)
        {
            var recorder = context.ServiceProvider.GetRequiredService<ContextPhaseRecorder>();
            context.ServiceProvider.ShouldBeSameAs(recorder.InitializationProvider);
            recorder.Stages.Add("Main:" + context.GetConfiguration()["module"] + ":" +
                context.GetHostEnvironment().EnvironmentName);
        }

        /// <inheritdoc />
        public override void OnPostApplicationInitialization(BingModuleInitializationContext context)
        {
            var recorder = context.ServiceProvider.GetRequiredService<ContextPhaseRecorder>();
            context.ServiceProvider.ShouldBeSameAs(recorder.InitializationProvider);
            recorder.Stages.Add("Post:" + context.GetConfiguration()["module"] + ":" +
                context.GetHostEnvironment().EnvironmentName);
            recorder.InitializationProvider = null;
        }

        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context)
        {
            var recorder = context.ServiceProvider.GetRequiredService<ContextPhaseRecorder>();
            recorder.Stages.Add("Shutdown:" + context.GetConfiguration()["module"] + ":" +
                context.GetHostEnvironment().EnvironmentName);
        }
    }

    /// <summary>
    /// 创建用于验证环境隔离的宿主环境。
    /// </summary>
    /// <param name="name">宿主环境名称。</param>
    /// <returns>使用指定名称的宿主环境。</returns>
    private static IHostEnvironment CreateEnvironment(string name) => new TestHostEnvironment
    {
        ApplicationName = name,
        EnvironmentName = name,
        ContentRootPath = ".",
        ContentRootFileProvider = new NullFileProvider()
    };

    /// <summary>
    /// 提供测试用宿主环境数据。
    /// </summary>
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        /// <inheritdoc />
        public string ApplicationName { get; set; }
        /// <inheritdoc />
        public string EnvironmentName { get; set; }
        /// <inheritdoc />
        public string ContentRootPath { get; set; }
        /// <inheritdoc />
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
