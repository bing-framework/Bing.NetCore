using System.Collections.Generic;
using Bing.Admin.Modules;
using Exceptionless;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit;

namespace Bing.Admin.Tests;

/// <summary>
/// 验证日志模块的 Exceptionless 配置分支。
/// </summary>
public sealed class AdminLogConfigurationTest
{
    /// <summary>
    /// 串行化全局 Serilog 状态的测试访问。
    /// </summary>
    private static readonly object SyncRoot = new();

    /// <summary>
    /// 验证未配置 API Key 时不注册远程日志客户端。
    /// </summary>
    [Fact]
    public void AddServices_WithoutExceptionlessApiKey_ShouldKeepRemoteSinkDisabled()
    {
        lock (SyncRoot)
        {
            try
            {
                var services = CreateServices(new Dictionary<string, string>
                {
                    ["Exceptionless:ApiKey"] = string.Empty,
                    ["Exceptionless:ServerUrl"] = "https://exceptionless.test"
                });

                new LogModule().AddServices(services);

                Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ExceptionlessClient));
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }

    /// <summary>
    /// 验证配置 API Key 后注册远程日志客户端并保留服务端点。
    /// </summary>
    [Fact]
    public void AddServices_WithExceptionlessApiKey_ShouldRegisterConfiguredRemoteClient()
    {
        lock (SyncRoot)
        {
            try
            {
                const string apiKey = "test-exceptionless-key";
                const string serverUrl = "https://exceptionless.test";
                var services = CreateServices(new Dictionary<string, string>
                {
                    ["Exceptionless:ApiKey"] = apiKey,
                    ["Exceptionless:ServerUrl"] = serverUrl
                });

                new LogModule().AddServices(services);

                Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ExceptionlessClient));
                using var provider = services.BuildServiceProvider();
                var client = provider.GetRequiredService<ExceptionlessClient>();
                Assert.Equal(apiKey, client.Configuration.ApiKey);
                Assert.Equal(serverUrl, client.Configuration.ServerUrl);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }

    /// <summary>
    /// 创建带配置实例的服务集合。
    /// </summary>
    /// <param name="values">配置值。</param>
    /// <returns>服务集合。</returns>
    private static ServiceCollection CreateServices(Dictionary<string, string> values)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        return services;
    }
}
