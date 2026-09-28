using Bing.AspNetCore;
using Bing.Core.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 新模块入口在 ASP.NET Core 管道中的生命周期测试。
/// </summary>
public sealed class WebModuleLifecycleTest
{
    /// <summary>
    /// 验证 Web 与非 Web 入口使用相同的依赖顺序。
    /// </summary>
    [Fact]
    public void AddBingApplication_WebAndNonWebUseBing_ShouldKeepTheSameDependencyOrder()
    {
        var nonWebTrace = BuildNonWebApplication();
        var webServices = CreateServices<OrderedRootModule>();
        using var webProvider = webServices.BuildServiceProvider();
        var webApp = new ApplicationBuilder(webProvider);

        webApp.UseBing();

        webProvider.GetRequiredService<OrderTrace>().Items.ShouldBe(nonWebTrace.Items);
    }

    /// <summary>
    /// 验证 Web 模块和服务提供程序模块各初始化一次。
    /// </summary>
    [Fact]
    public async Task UseBing_ShouldDispatchWebAndProviderModules_AndAvoidDuplicateMiddleware()
    {
        var services = CreateServices<WebDispatchRootModule>();
        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);

        app.UseBing();
        app.UseBing();
        app.Run(context => context.Response.WriteAsync("done"));
        var request = app.Build();

        var context = new DefaultHttpContext { RequestServices = provider };
        await request(context);

        var state = provider.GetRequiredService<DispatchState>();
        state.ProviderUseCount.ShouldBe(1);
        state.WebUseCount.ShouldBe(1);
        state.MiddlewareRequestCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证异步 Web 初始化收到实际应用程序构建器。
    /// </summary>
    [Fact]
    public async Task UseBingAsync_ShouldGiveAsyncWebModuleTheActualApplicationBuilder()
    {
        var services = CreateServices<AsyncWebModule>();
        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);

        await app.UseBingAsync();
        app.Run(context => context.Response.WriteAsync("done"));
        var request = app.Build();
        var context = new DefaultHttpContext { RequestServices = provider };

        await request(context);

        var state = provider.GetRequiredService<AsyncWebState>();
        state.HostContext.ShouldBeSameAs(app);
        state.MiddlewareRequestCount.ShouldBe(1);
    }

    /// <summary>
    /// Web 异步入口保留模块取消异常及阶段诊断。
    /// </summary>
    [Fact]
    public async Task UseBingAsync_CancellationPreservesModuleCause()
    {
        using var cancellation = new CancellationTokenSource();
        var state = new WebCancelState(cancellation);
        var services = new ServiceCollection().AddSingleton(state);
        services.AddBingApplication<WebCancelModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildBingServiceProvider();
        var app = new ApplicationBuilder(provider);

        var initialization = app.UseBingAsync(cancellation.Token);
        OperationCanceledException error;
        try
        {
            await initialization;
            throw new InvalidOperationException("Web 初始化应以取消结束。");
        }
        catch (OperationCanceledException canceled) { error = canceled; }

        error.ShouldBeSameAs(state.Original);
        error.CancellationToken.ShouldBe(cancellation.Token);
        error.Data["Origin"].ShouldBe("web-module");
        error.Data["Bing.ModulePhase"].ShouldBe("PreInitialize");
        error.Data["Bing.ModuleType"].ShouldBe(typeof(WebCancelModule).FullName);
        initialization.IsCanceled.ShouldBeTrue();
        initialization.IsFaulted.ShouldBeFalse();
    }

    /// <summary>
    /// Web 三阶段共享宿主上下文，主阶段仍即时装配管道。
    /// </summary>
    [Fact]
    public async Task UseBing_WebPhases_ShouldShareApplicationBuilderAndComposePipeline()
    {
        var services = CreateServices<PhasedWebModule>();
        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);

        app.UseBing();
        app.Run(context => context.Response.WriteAsync("done"));
        var request = app.Build();
        await request(new DefaultHttpContext { RequestServices = provider });

        var state = provider.GetRequiredService<PhasedWebState>();
        state.HostContexts.ShouldBe(new object[] { app, app });
        state.Events.ShouldBe(new[] { "pre", "main", "post", "request" });
    }

    /// <summary>
    /// 验证 Web 初始化重入会失败并保留失败状态。
    /// </summary>
    [Fact]
    public async Task UseBingAsync_WebReentry_ShouldFailAndLeaveTheApplicationFailed()
    {
        var services = CreateServices<ReentrantAsyncModule>();
        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => app.UseBingAsync());

        exception.Message.ShouldContain("重入");
    }

    /// <summary>
    /// 验证并发 Web 初始化会拒绝第二个上下文。
    /// </summary>
    [Fact]
    public async Task UseBingAsync_ConcurrentWebInitialization_ShouldRejectTheSecondApplicationContext()
    {
        var services = CreateServices<ConcurrentAsyncModule>();
        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);
        var state = provider.GetRequiredService<ConcurrentState>();

        var first = app.UseBingAsync();
        await state.Entered.Task;

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => app.UseBingAsync());
        exception.Message.ShouldContain("Initializing");

        state.Release.TrySetResult(true);
        await first;
    }

    /// <summary>
    /// 验证宿主停止时执行异步模块关闭钩子。
    /// </summary>
    [Fact]
    public async Task HostStop_ShouldRunAsyncModuleShutdown()
    {
        var host = new HostBuilder()
            .ConfigureServices(services => services.AddBingApplication<HostShutdownModule>(o =>
            {
                o.AutoRegisterServices = false;
            }))
            .ConfigureWebHostDefaults(webHost => webHost
                .UseTestServer()
                .Configure(app => app.UseBingAsync().GetAwaiter().GetResult()))
            .Build();

        try
        {
            await host.StartAsync();
            var state = host.Services.GetRequiredService<ShutdownState>();
            await host.StopAsync();

            state.AsyncShutdownCount.ShouldBe(1);
        }
        finally
        {
            host.Dispose();
        }
    }

    /// <summary>
    /// 验证 Web 新异步三阶段共享宿主上下文，并按阶段顺序装配管道。
    /// </summary>
    [Fact]
    public async Task UseBingAsync_NewAsyncOverrides_ShouldShareApplicationBuilderAndComposePipeline()
    {
        var services = CreateServices<AsyncOverrideWebModule>();
        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);

        await app.UseBingAsync();
        app.Run(context => context.Response.WriteAsync("done"));
        var request = app.Build();

        await request(new DefaultHttpContext { RequestServices = provider });

        var state = provider.GetRequiredService<AsyncOverrideWebState>();
        state.HostContexts.ShouldBe(new object[] { app, app, app });
        state.Events.ShouldBe(new[] { "pre", "main", "post", "request" });
    }

    /// <summary>
    /// 验证 Generic Host 停止会等待新异步关闭重写完成。
    /// </summary>
    [Fact]
    public async Task HostStop_ShouldAwaitAsyncShutdownOverride()
    {
        var host = new HostBuilder()
            .ConfigureServices(services => services.AddBingApplication<AsyncOverrideHostShutdownModule>(o =>
            {
                o.AutoRegisterServices = false;
            }))
            .ConfigureWebHostDefaults(webHost => webHost
                .UseTestServer()
                .Configure(app => app.UseBingAsync().GetAwaiter().GetResult()))
            .Build();

        try
        {
            await host.StartAsync();
            var state = host.Services.GetRequiredService<AsyncOverrideShutdownState>();

            var stopping = host.StopAsync();
            await state.Entered.Task;

            state.Completed.ShouldBeFalse();
            stopping.IsCompleted.ShouldBeFalse();

            state.Release.TrySetResult(true);
            await stopping;

            state.Completed.ShouldBeTrue();
            state.ShutdownCount.ShouldBe(1);
        }
        finally
        {
            host.Dispose();
        }
    }

    /// <summary>
    /// 构建并初始化非 Web 模块应用。
    /// </summary>
    /// <returns>非 Web 应用记录的执行顺序。</returns>
    private static OrderTrace BuildNonWebApplication()
    {
        var services = CreateServices<OrderedRootModule>();
        using var provider = services.BuildServiceProvider();
        provider.UseBing();
        return provider.GetRequiredService<OrderTrace>();
    }

    /// <summary>
    /// 创建指定启动模块的测试服务集合。
    /// </summary>
    /// <typeparam name="TModule">启动模块类型。</typeparam>
    /// <returns>已完成模块注册的服务集合。</returns>
    private static IServiceCollection CreateServices<TModule>() where TModule : BingModule
    {
        var services = new ServiceCollection();
        services.AddBingApplication<TModule>(options => options.AutoRegisterServices = false);
        return services;
    }

    /// <summary>
    /// 保存 Web 模块取消诊断。
    /// </summary>
    private sealed class WebCancelState
    {
        /// <summary>
        /// 获取模块初始化取消令牌源。
        /// </summary>
        public CancellationTokenSource Source { get; }
        /// <summary>
        /// 获取或设置模块产生的原始异常。
        /// </summary>
        public OperationCanceledException Original { get; set; }
        /// <summary>
        /// 初始化 Web 模块取消状态。
        /// </summary>
        /// <param name="source">用于触发模块初始化取消的令牌源。</param>
        public WebCancelState(CancellationTokenSource source) => Source = source;
    }

    /// <summary>
    /// 初始化时取消的 Web 测试模块。
    /// </summary>
    public sealed class WebCancelModule : BingModule
    {
        /// <inheritdoc />
        public override Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            var state = context.ServiceProvider.GetRequiredService<WebCancelState>();
            state.Source.Cancel();
            state.Original = new OperationCanceledException("web initialization canceled", state.Source.Token);
            state.Original.Data["Origin"] = "web-module";
            return Task.FromException(state.Original);
        }
    }

    /// <summary>
    /// 记录模块执行顺序的测试状态。
    /// </summary>
    private sealed class OrderTrace
    {
        /// <summary>
        /// 获取执行顺序记录。
        /// </summary>
        public List<string> Items { get; } = new();
    }

    /// <summary>
    /// 验证 Web 根模块与依赖模块的初始化顺序。
    /// </summary>
    [DependsOnModule(typeof(OrderedDependencyModule))]
    public sealed class OrderedRootModule : BingModule
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddSingleton<OrderTrace>();
            return services;
        }

        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider) =>
            provider.GetRequiredService<OrderTrace>().Items.Add(nameof(OrderedRootModule));
    }

    /// <summary>
    /// 验证 Web 根模块依赖的普通模块。
    /// </summary>
    public sealed class OrderedDependencyModule : BingModule
    {
        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider) =>
            provider.GetRequiredService<OrderTrace>().Items.Add(nameof(OrderedDependencyModule));
    }

    /// <summary>
    /// 记录 Web 和提供程序模块调用次数。
    /// </summary>
    private sealed class DispatchState
    {
        /// <summary>
        /// 获取或设置提供程序模块调用次数。
        /// </summary>
        public int ProviderUseCount { get; set; }
        /// <summary>
        /// 获取或设置 Web 模块调用次数。
        /// </summary>
        public int WebUseCount { get; set; }
        /// <summary>
        /// 获取或设置中间件请求次数。
        /// </summary>
        public int MiddlewareRequestCount { get; set; }
    }

    /// <summary>
    /// 注册 Web 中间件并记录 Web 初始化次数的模块。
    /// </summary>
    [DependsOnModule(typeof(ProviderDispatchModule))]
    public sealed class WebDispatchRootModule : AspNetCoreBingModule
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddSingleton<DispatchState>();
            return services;
        }

        /// <inheritdoc />
        public override void UseModule(IApplicationBuilder app)
        {
            var state = app.ApplicationServices.GetRequiredService<DispatchState>();
            state.WebUseCount++;
            app.Use(next => async context =>
            {
                state.MiddlewareRequestCount++;
                await next(context);
            });
        }
    }

    /// <summary>
    /// 记录普通提供程序模块初始化次数的模块。
    /// </summary>
    public sealed class ProviderDispatchModule : BingModule
    {
        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider) =>
            provider.GetRequiredService<DispatchState>().ProviderUseCount++;
    }

    /// <summary>
    /// 保存异步 Web 模块收到的宿主上下文和请求次数。
    /// </summary>
    private sealed class AsyncWebState
    {
        /// <summary>
        /// 获取或设置宿主上下文。
        /// </summary>
        public object HostContext { get; set; }
        /// <summary>
        /// 获取或设置中间件请求次数。
        /// </summary>
        public int MiddlewareRequestCount { get; set; }
    }

    /// <summary>
    /// 记录 Web 初始化阶段和请求调用。
    /// </summary>
    private sealed class PhasedWebState
    {
        /// <summary>
        /// 获取生命周期阶段事件。
        /// </summary>
        public List<string> Events { get; } = new();
        /// <summary>
        /// 获取前置与后置阶段收到的宿主上下文。
        /// </summary>
        public List<object> HostContexts { get; } = new();
    }

    /// <summary>
    /// 记录新异步 Web 三阶段和请求调用。
    /// </summary>
    private sealed class AsyncOverrideWebState
    {
        /// <summary>
        /// 获取生命周期阶段事件。
        /// </summary>
        public List<string> Events { get; } = new();
        /// <summary>
        /// 获取各阶段收到的宿主上下文。
        /// </summary>
        public List<object> HostContexts { get; } = new();
    }

    /// <summary>
    /// 验证 Web 生命周期钩子与旧管道分派兼容的模块。
    /// </summary>
    public sealed class PhasedWebModule : AspNetCoreBingModule
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddSingleton<PhasedWebState>();
            return services;
        }

        /// <inheritdoc />
        public override void OnPreApplicationInitialization(BingModuleInitializationContext context)
        {
            var state = context.ServiceProvider.GetRequiredService<PhasedWebState>();
            state.HostContexts.Add(context.HostContext);
            state.Events.Add("pre");
        }

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            base.OnApplicationInitialization(context);

        /// <inheritdoc />
        public override void UseModule(IApplicationBuilder app)
        {
            var state = app.ApplicationServices.GetRequiredService<PhasedWebState>();
            state.Events.Add("main");
            app.Use(next => async context =>
            {
                state.Events.Add("request");
                await next(context);
            });
        }

        /// <inheritdoc />
        public override void OnPostApplicationInitialization(BingModuleInitializationContext context)
        {
            var state = context.ServiceProvider.GetRequiredService<PhasedWebState>();
            state.HostContexts.Add(context.HostContext);
            state.Events.Add("post");
        }
    }

    /// <summary>
    /// 通过新异步重写装配 Web 管道的测试模块。
    /// </summary>
    public sealed class AsyncOverrideWebModule : AspNetCoreBingModule
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddSingleton<AsyncOverrideWebState>();
            return services;
        }

        /// <inheritdoc />
        public override Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            var state = context.ServiceProvider.GetRequiredService<AsyncOverrideWebState>();
            state.HostContexts.Add(context.HostContext);
            state.Events.Add("pre");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task OnApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            var state = context.ServiceProvider.GetRequiredService<AsyncOverrideWebState>();
            state.HostContexts.Add(context.HostContext);
            state.Events.Add("main");
            var app = Assert.IsAssignableFrom<IApplicationBuilder>(context.HostContext);
            app.Use(next => async httpContext =>
            {
                state.Events.Add("request");
                await next(httpContext);
            });
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task OnPostApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            var state = context.ServiceProvider.GetRequiredService<AsyncOverrideWebState>();
            state.HostContexts.Add(context.HostContext);
            state.Events.Add("post");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 注册异步 Web 中间件的模块。
    /// </summary>
    public sealed class AsyncWebModule : BingModule, IBingAsyncModuleInitializer
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddSingleton<AsyncWebState>();
            return services;
        }

        /// <inheritdoc />
        public async Task InitializeAsync(BingModuleInitializationContext context, CancellationToken cancellationToken)
        {
            var state = context.ServiceProvider.GetRequiredService<AsyncWebState>();
            state.HostContext = context.HostContext;
            Assert.IsAssignableFrom<IApplicationBuilder>(context.HostContext);
            var app = (IApplicationBuilder)context.HostContext;
            app.Use(next => async httpContext =>
            {
                state.MiddlewareRequestCount++;
                await next(httpContext);
            });
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// 在初始化期间再次调用管理器的测试模块。
    /// </summary>
    public sealed class ReentrantAsyncModule : BingModule, IBingAsyncModuleInitializer
    {
        /// <inheritdoc />
        public Task InitializeAsync(BingModuleInitializationContext context, CancellationToken cancellationToken) =>
            context.ServiceProvider.GetRequiredService<IBingModuleManager>().InitializeAsync(context);
    }

    /// <summary>
    /// 控制并发初始化测试时机的状态。
    /// </summary>
    private sealed class ConcurrentState
    {
        /// <summary>
        /// 获取初始化已进入的通知源。
        /// </summary>
        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 获取释放初始化的通知源。
        /// </summary>
        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// 阻塞初始化以验证并发重入的测试模块。
    /// </summary>
    public sealed class ConcurrentAsyncModule : BingModule, IBingAsyncModuleInitializer
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddSingleton<ConcurrentState>();
            return services;
        }

        /// <inheritdoc />
        public async Task InitializeAsync(BingModuleInitializationContext context, CancellationToken cancellationToken)
        {
            var state = context.ServiceProvider.GetRequiredService<ConcurrentState>();
            state.Entered.TrySetResult(true);
            await state.Release.Task.WaitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 记录宿主停止期间异步关闭次数。
    /// </summary>
    private sealed class ShutdownState
    {
        /// <summary>
        /// 获取或设置异步关闭次数。
        /// </summary>
        public int AsyncShutdownCount { get; set; }
    }

    /// <summary>
    /// 控制异步关闭重写测试时机的状态。
    /// </summary>
    private sealed class AsyncOverrideShutdownState
    {
        /// <summary>
        /// 获取关闭钩子已进入的通知源。
        /// </summary>
        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取释放关闭钩子的通知源。
        /// </summary>
        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取或设置关闭钩子完成标记。
        /// </summary>
        public bool Completed { get; set; }

        /// <summary>
        /// 获取或设置关闭钩子调用次数。
        /// </summary>
        public int ShutdownCount { get; set; }
    }

    /// <summary>
    /// 在宿主停止期间记录异步关闭的模块。
    /// </summary>
    public sealed class HostShutdownModule : BingModule, IBingAsyncModuleShutdown
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddSingleton<ShutdownState>();
            return services;
        }

        /// <inheritdoc />
        public Task ShutdownAsync(BingModuleShutdownContext context, CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ShutdownState>().AsyncShutdownCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 通过新异步关闭重写等待宿主停止的测试模块。
    /// </summary>
    public sealed class AsyncOverrideHostShutdownModule : BingModule
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddSingleton<AsyncOverrideShutdownState>();
            return services;
        }

        /// <inheritdoc />
        public override async Task OnApplicationShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            var state = context.ServiceProvider.GetRequiredService<AsyncOverrideShutdownState>();
            state.ShutdownCount++;
            state.Entered.TrySetResult(true);
            await state.Release.Task.WaitAsync(cancellationToken);
            state.Completed = true;
        }
    }
}
