using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Jobs;
using Bing.Admin.Modules;
using Bing.AspNetCore.Hosting;
using Bing.Core.Modularity;
using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using Hangfire.MemoryStorage;
using Hangfire.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bing.Admin.FreeSQL.Tests;

/// <summary>
/// 验证 FreeSQL Admin 的 Hangfire 任务宿主注册边界。
/// </summary>
public sealed class AdminHangfireRegistrationTest
{
    /// <summary>
    /// Task 角色应注册 Hangfire 运行时和托管服务。
    /// </summary>
    [Fact]
    public void AddServices_WhenTaskRole_ShouldRegisterHangfireRuntime()
    {
        var services = CreateServices("task");

        new HangfireModule().AddServices(services);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(BingHangfireRuntime));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IBingHangfireRuntime));
    }

    /// <summary>
    /// Web 角色应保留 Hangfire 配置但不启动 Server。
    /// </summary>
    [Fact]
    public void AddServices_WhenWebRole_ShouldNotRegisterHostedServer()
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
    /// FreeSQL 宿主的排空过滤器应拒绝新任务并等待在途任务完成。
    /// </summary>
    [Fact]
    public async Task DrainFilter_WhenDraining_ShouldRejectNewJobsAndWaitForInFlight()
    {
        var runtime = new BingHangfireRuntime();
        var filter = new BingHangfireDrainFilter(runtime);
        var storage = new MemoryStorage();
        using var connection = storage.GetConnection();
        var job = Job.FromExpression(() => HangfireTestJob.NoOp());
        var backgroundJob = new BackgroundJob("freesql-hangfire-test", job, DateTime.UtcNow);
        var activeContext = new PerformContext(storage, connection, backgroundJob, new NeverCanceledJobToken());

        filter.OnPerforming(new PerformingContext(activeContext));
        Assert.Equal(1, runtime.InFlightCount);

        runtime.BeginDrain();
        var rejected = new PerformingContext(new PerformContext(storage, connection, backgroundJob,
            new NeverCanceledJobToken()));
        filter.OnPerforming(rejected);
        Assert.True(rejected.Canceled);

        filter.OnPerformed(new PerformedContext(activeContext, null, false, null));
        Assert.True(await runtime.WaitForDrainAsync(TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    /// Consumer 角色通过 FreeSQL 启动配置时不应加入 Hangfire 模块。
    /// </summary>
    [Fact]
    public void Startup_WhenConsumerRole_ShouldNotRegisterHangfireModule()
    {
        var configuration = CreateConfiguration("consumer", includeAdminDependencies: true);
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        new Bing.Admin.Startup(configuration).ConfigureServices(services);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(BingModule) &&
            descriptor.ImplementationInstance is HangfireModule);
    }

    /// <summary>
    /// Task 角色通过 FreeSQL 启动配置时应加入 Hangfire 模块。
    /// </summary>
    [Fact]
    public void Startup_WhenTaskRole_ShouldRegisterHangfireModule()
    {
        var configuration = CreateConfiguration("task", includeAdminDependencies: true);
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        new Bing.Admin.Startup(configuration).ConfigureServices(services);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(BingModule) &&
            descriptor.ImplementationInstance is HangfireModule);
    }

    /// <summary>
    /// 创建 Hangfire 模块注册测试服务集合。
    /// </summary>
    /// <param name="role">宿主角色。</param>
    /// <returns>包含角色配置的服务集合。</returns>
    private static ServiceCollection CreateServices(string role)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateConfiguration(role));
        return services;
    }

    /// <summary>
    /// 创建启动配置测试数据。
    /// </summary>
    /// <param name="role">宿主角色。</param>
    /// <param name="includeAdminDependencies">是否包含模块注册所需的连接配置。</param>
    /// <returns>内存配置。</returns>
    private static IConfiguration CreateConfiguration(string role, bool includeAdminDependencies = false)
    {
        var values = new Dictionary<string, string>
        {
            ["BING_HOST_ROLE"] = role
        };
        if (includeAdminDependencies)
        {
            values["BING_CAP_CONSUMER_ENABLED"] = "true";
            values["CAP:RabbitMQ:HostName"] = "localhost";
            values["CAP:RabbitMQ:UserName"] = "test";
            values["CAP:RabbitMQ:Password"] = "test";
            values["ConnectionStrings:DefaultConnection"] =
                "Server=localhost;Database=bing_admin_freesql_hangfire_test;User ID=test;Password=test";
            values["ConnectionStrings:RedisConnection"] = "127.0.0.1:6379";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
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

}
