using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Modules;
using Bing.Admin.Jobs;
using Bing.Core.Modularity;
using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.MemoryStorage;
using Hangfire.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bing.Admin.EFCore.Tests;

/// <summary>
/// 验证 Admin Hangfire 在不同宿主角色下的注册边界。
/// </summary>
public sealed class AdminHangfireRegistrationTest
{
    /// <summary>
    /// Task 角色应在服务注册阶段加入 Hangfire Server。
    /// </summary>
    [Fact]
    public void AddServices_WhenTaskRole_ShouldRegisterHostedServer()
    {
        var services = CreateServices("task");

        new HangfireModule().AddServices(services);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(BingHangfireRuntime));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IBingHangfireRuntime));
    }

    /// <summary>
    /// Web 角色不应在服务注册阶段加入 Hangfire Server。
    /// </summary>
    [Fact]
    public void AddServices_WhenWebRole_ShouldNotRegisterHostedServerEarly()
    {
        var services = CreateServices("web");

        new HangfireModule().AddServices(services);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IBingHangfireRuntime));
    }

    /// <summary>
    /// Consumer 角色不应注册 Hangfire Server。
    /// </summary>
    [Fact]
    public void AddServices_WhenConsumerRole_ShouldNotRegisterHostedServer()
    {
        var services = CreateServices("consumer");

        new HangfireModule().AddServices(services);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IBingHangfireRuntime));
    }

    /// <summary>
    /// 排空期间应阻止新任务，并等待已经进入执行区的任务完成。
    /// </summary>
    [Fact]
    public async Task HangfireDrainFilter_WhenDraining_ShouldKeepInFlightAndRejectNewJobs()
    {
        var runtime = new BingHangfireRuntime();
        var filter = new BingHangfireDrainFilter(runtime);
        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();
        var backgroundJob = new BackgroundJob("test-job", Job.FromExpression(() => HangfireTestJob.NoOp()),
            DateTime.UtcNow);
        var activeContext = new PerformContext(storage, connection, backgroundJob,
            new NeverCanceledJobToken());
        var active = new PerformingContext(activeContext);

        filter.OnPerforming(active);
        Assert.False(active.Canceled);
        Assert.Equal(1, runtime.InFlightCount);

        runtime.BeginDrain();
        Assert.True(runtime.IsDraining);
        Assert.False(await runtime.WaitForDrainAsync(TimeSpan.Zero));

        var rejected = new PerformingContext(new PerformContext(storage, connection, backgroundJob,
            new NeverCanceledJobToken()));
        filter.OnPerforming(rejected);
        Assert.True(rejected.Canceled);
        Assert.Equal(1, runtime.InFlightCount);

        filter.OnPerformed(new PerformedContext(activeContext, null, false, null));
        Assert.True(await runtime.WaitForDrainAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal(0, runtime.InFlightCount);
    }

    /// <summary>
    /// 排空期间应在任务写入 Hangfire 存储前拒绝新任务。
    /// </summary>
    [Fact]
    public void HangfireDrainFilter_WhenCreatingDuringDrain_ShouldCancelCreation()
    {
        var runtime = new BingHangfireRuntime();
        var filter = new BingHangfireDrainFilter(runtime);
        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();
        var job = Job.FromExpression(() => HangfireTestJob.NoOp());

        var accepted = new CreatingContext(new CreateContext(storage, connection, job, null));
        filter.OnCreating(accepted);
        Assert.False(accepted.Canceled);

        runtime.BeginDrain();

        var rejected = new CreatingContext(new CreateContext(storage, connection, job, null));
        filter.OnCreating(rejected);
        Assert.True(rejected.Canceled);
    }

    /// <summary>
    /// 宿主停止时应等待已进入执行区的任务完成。
    /// </summary>
    [Fact]
    public async Task HangfireDrainHostedService_WhenStopping_ShouldWaitForInFlightJob()
    {
        var runtime = new BingHangfireRuntime();
        var filter = new BingHangfireDrainFilter(runtime);
        var lifetime = new TestHostApplicationLifetime();
        using var hostedService = new Bing.Admin.Jobs.BingHangfireDrainHostedService(runtime, filter, lifetime);
        await hostedService.StartAsync(CancellationToken.None);

        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();
        var backgroundJob = new BackgroundJob("test-job", Job.FromExpression(() => HangfireTestJob.NoOp()),
            DateTime.UtcNow);
        var performContext = new PerformContext(storage, connection, backgroundJob,
            new NeverCanceledJobToken());
        var performing = new PerformingContext(performContext);
        filter.OnPerforming(performing);

        var stopTask = hostedService.StopAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.False(stopTask.IsCompleted);

        filter.OnPerformed(new PerformedContext(performContext, null, false, null));
        await stopTask;
        Assert.Equal(0, runtime.InFlightCount);
    }

    /// <summary>
    /// 宿主停止等待被取消时应保留在途任务并报告未完成排空。
    /// </summary>
    [Fact]
    public async Task HangfireDrainHostedService_WhenStopWaitIsCanceled_ShouldKeepInFlightJob()
    {
        var runtime = new BingHangfireRuntime();
        var filter = new BingHangfireDrainFilter(runtime);
        var lifetime = new TestHostApplicationLifetime();
        using var hostedService = new Bing.Admin.Jobs.BingHangfireDrainHostedService(runtime, filter, lifetime);

        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();
        var backgroundJob = new BackgroundJob("test-job", Job.FromExpression(() => HangfireTestJob.NoOp()),
            DateTime.UtcNow);
        var performContext = new PerformContext(storage, connection, backgroundJob,
            new NeverCanceledJobToken());
        filter.OnPerforming(new PerformingContext(performContext));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            hostedService.StopAsync(cancellation.Token));

        Assert.True(exception.Data.Contains("Bing.Admin.HangfireDrainIncomplete"));
        Assert.Equal(1, runtime.InFlightCount);
        filter.OnPerformed(new PerformedContext(performContext, null, false, null));
        Assert.True(await runtime.WaitForDrainAsync(TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    /// 宿主进入停止阶段时应开始排空但保留在途任务。
    /// </summary>
    [Fact]
    public async Task HangfireDrainHostedService_WhenApplicationStopping_ShouldBeginDrain()
    {
        var runtime = new BingHangfireRuntime();
        var filter = new BingHangfireDrainFilter(runtime);
        var lifetime = new TestHostApplicationLifetime();
        using var hostedService = new Bing.Admin.Jobs.BingHangfireDrainHostedService(runtime, filter, lifetime);
        await hostedService.StartAsync(CancellationToken.None);

        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();
        var backgroundJob = new BackgroundJob("test-job", Job.FromExpression(() => HangfireTestJob.NoOp()),
            DateTime.UtcNow);
        var performContext = new PerformContext(storage, connection, backgroundJob,
            new NeverCanceledJobToken());
        filter.OnPerforming(new PerformingContext(performContext));

        lifetime.StopApplication();

        Assert.True(runtime.IsDraining);
        Assert.Equal(1, runtime.InFlightCount);
        Assert.False(await runtime.WaitForDrainAsync(TimeSpan.Zero));

        filter.OnPerformed(new PerformedContext(performContext, null, false, null));
        Assert.True(await runtime.WaitForDrainAsync(TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    /// Consumer 角色不应把 Hangfire 模块加入 Admin 模块目录。
    /// </summary>
    [Fact]
    public void Startup_WhenConsumerRole_ShouldNotRegisterHangfireModule()
    {
        var configuration = CreateConfiguration("consumer", includeAdminDependencies: true);
        var services = new ServiceCollection();
        services.AddSingleton(configuration);

        new Bing.Admin.Startup(configuration).ConfigureServices(services);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(BingModule) &&
            descriptor.ImplementationInstance is HangfireModule);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IBingModuleManager>();
    }

    /// <summary>
    /// Task 角色通过管道装配时也不应重复添加 Server 中间件。
    /// </summary>
    [Fact]
    public void UseModule_WhenTaskRole_ShouldNotAddServerMiddleware()
    {
        var services = CreateServices("task");
        new HangfireModule().AddServices(services);

        using var provider = services.BuildServiceProvider();
        var builder = new RecordingApplicationBuilder(provider);

        new HangfireModule().UseModule(builder);

        Assert.Equal(0, builder.ComponentCount);
    }

    /// <summary>
    /// Dashboard 未配置角色时应拒绝访问。
    /// </summary>
    [Fact]
    public void DashboardAuthorization_WhenRoleIsMissing_ShouldDeny()
    {
        var filter = new AdminHangfireDashboardAuthorizationFilter(null);
        var principal = new ClaimsPrincipal(new ClaimsIdentity("jwt"));

        Assert.False(filter.IsAuthorized(principal));
    }

    /// <summary>
    /// Dashboard 未认证时应拒绝访问。
    /// </summary>
    [Fact]
    public void DashboardAuthorization_WhenUserIsAnonymous_ShouldDeny()
    {
        var filter = new AdminHangfireDashboardAuthorizationFilter("admin");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, "admin")
        }));

        Assert.False(filter.IsAuthorized(principal));
    }

    /// <summary>
    /// Dashboard 用户具备配置角色时应允许访问。
    /// </summary>
    [Fact]
    public void DashboardAuthorization_WhenRoleMatches_ShouldAllow()
    {
        var filter = new AdminHangfireDashboardAuthorizationFilter("admin");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("role_code", "operator,admin")
        }, "jwt"));

        Assert.True(filter.IsAuthorized(principal));
    }

    /// <summary>
    /// 创建 Hangfire 模块测试服务集合。
    /// </summary>
    private static ServiceCollection CreateServices(string role)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateConfiguration(role));
        return services;
    }

    /// <summary>
    /// 创建测试配置。
    /// </summary>
    /// <param name="role">宿主角色。</param>
    /// <param name="includeAdminDependencies">是否包含 Admin 模块注册所需的最小连接配置。</param>
    private static IConfiguration CreateConfiguration(string role, bool includeAdminDependencies = false)
    {
        var values = new Dictionary<string, string>
        {
            ["BING_HOST_ROLE"] = role
        };
        if (includeAdminDependencies)
        {
            values["Caching:Namespace"] = "bing.admin.tests";
            values["BING_CAP_CONSUMER_ENABLED"] = "true";
            values["CAP:RabbitMQ:HostName"] = "localhost";
            values["CAP:RabbitMQ:UserName"] = "test";
            values["CAP:RabbitMQ:Password"] = "test";
            values["ConnectionStrings:DefaultConnection"] =
                "Server=localhost;Database=bing_admin_hangfire_test;User ID=test;Password=test";
            values["ConnectionStrings:RedisConnection"] = "127.0.0.1:6379";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    /// <summary>
    /// 记录管道装配次数的测试构建器。
    /// </summary>
    private sealed class RecordingApplicationBuilder : IApplicationBuilder
    {
        /// <summary>
        /// 测试管道的终端委托。
        /// </summary>
        private readonly RequestDelegate _terminal = _ => Task.CompletedTask;

        /// <summary>
        /// 当前已装配的请求管道。
        /// </summary>
        private RequestDelegate _pipeline;

        /// <summary>
        /// 初始化一个 <see cref="RecordingApplicationBuilder" /> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">应用服务提供程序。</param>
        public RecordingApplicationBuilder(IServiceProvider serviceProvider)
        {
            ApplicationServices = serviceProvider;
            _pipeline = _terminal;
        }

        /// <summary>
        /// 已记录的中间件数量。
        /// </summary>
        public int ComponentCount { get; private set; }

        /// <inheritdoc />
        public IServiceProvider ApplicationServices { get; set; }

        /// <inheritdoc />
        public IFeatureCollection ServerFeatures { get; } = new FeatureCollection();

        /// <inheritdoc />
        public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

        /// <inheritdoc />
        public RequestDelegate Build() => _pipeline;

        /// <inheritdoc />
        public IApplicationBuilder New() => new RecordingApplicationBuilder(ApplicationServices);

        /// <inheritdoc />
        public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware)
        {
            ComponentCount++;
            _pipeline = middleware(_pipeline);
            return this;
        }
    }

    /// <summary>
    /// 提供无副作用的 Hangfire 测试任务。
    /// </summary>
    private static class HangfireTestJob
    {
        /// <summary>
        /// 执行不产生副作用的测试任务。
        /// </summary>
        public static void NoOp()
        {
        }
    }

    /// <summary>
    /// 提供永不取消的 Hangfire 测试令牌。
    /// </summary>
    private sealed class NeverCanceledJobToken : IJobCancellationToken
    {
        /// <inheritdoc />
        public CancellationToken ShutdownToken => CancellationToken.None;

        /// <inheritdoc />
        public void ThrowIfCancellationRequested()
        {
        }
    }

    /// <summary>
    /// 控制 Hangfire 宿主测试停止信号的生命周期实现。
    /// </summary>
    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        /// <summary>
        /// 控制测试宿主停止信号的令牌源。
        /// </summary>
        private readonly CancellationTokenSource _stopping = new();

        /// <inheritdoc />
        public CancellationToken ApplicationStarted => CancellationToken.None;

        /// <inheritdoc />
        public CancellationToken ApplicationStopping => _stopping.Token;

        /// <inheritdoc />
        public CancellationToken ApplicationStopped => CancellationToken.None;

        /// <inheritdoc />
        public void StopApplication() => _stopping.Cancel();
    }
}
