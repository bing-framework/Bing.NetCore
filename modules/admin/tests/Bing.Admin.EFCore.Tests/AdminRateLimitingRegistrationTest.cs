using Bing.Admin.Modules;
using Bing.Auditing;
using Bing.Auditing.Persistence;
using Bing.AspNetCore.Auditing;
using Bing.AspNetCore.Logs;
using Bing.AspNetCore.RateLimiting;
using Bing.RateLimiting;
using Bing.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Builder;
using System.Net;
using Xunit;

namespace Bing.Admin.EFCore.Tests;

/// <summary>
/// 验证 Admin EFCore 宿主的限流注册。
/// </summary>
public sealed class AdminRateLimitingRegistrationTest
{
    /// <summary>
    /// 默认关闭时不注册限流器。
    /// </summary>
    [Fact]
    public void AddServices_WhenRateLimitingDisabled_ShouldNotRegisterLimiter()
    {
        var services = CreateServices(false);

        new AppModule().AddServices(services);

        Assert.DoesNotContain(services, item => item.ServiceType == typeof(ITokenBucketRateLimiter));
    }

    /// <summary>
    /// 启用时注册 Redis 限流器与稳定策略和声明类型。
    /// </summary>
    [Fact]
    public void AddServices_WhenRateLimitingEnabled_ShouldRegisterSharedPolicy()
    {
        var services = CreateServices(true);

        new AppModule().AddServices(services);
        using var provider = services.BuildServiceProvider();

        Assert.Equal("RedisTokenBucketRateLimiter", provider.GetRequiredService<ITokenBucketRateLimiter>().GetType().Name);
        var options = provider.GetRequiredService<IOptions<BingRateLimitOptions>>().Value;
        Assert.Equal("Bing.Admin", options.PolicyName);
        Assert.Equal(BingClaimTypes.UserId, options.UserIdClaimType);
        Assert.Equal(BingClaimTypes.TenantId, options.TenantClaimType);
    }

    /// <summary>
    /// 启用宿主服务时使用统一的 HTTP 审计声明类型。
    /// </summary>
    [Fact]
    public void AddServices_ShouldUseHostClaimTypesForHttpAuditing()
    {
        var services = CreateServices(true);

        new AppModule().AddServices(services);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BingHttpAuditingOptions>>().Value;

        Assert.Equal("Bing.Admin", options.ApplicationName);
        Assert.Equal(BingClaimTypes.UserId, options.UserIdClaimType);
        Assert.Equal(BingClaimTypes.UserName, options.UserNameClaimType);
        Assert.Equal(BingClaimTypes.TenantId, options.TenantClaimType);
        Assert.Equal(BingClaimTypes.ClientId, options.ClientIdClaimType);
    }

    /// <summary>
    /// 验证宿主仅注册安全的内置 HTTP 日志字段并关闭旧请求响应采集。
    /// </summary>
    [Fact]
    public void AddServices_ShouldUseSafeHttpLoggingAndDisableLegacyRequestResponseLog()
    {
        var services = CreateServices(true);

        new AppModule().AddServices(services);
        using var provider = services.BuildServiceProvider();

        var httpLogging = provider.GetRequiredService<IOptions<HttpLoggingOptions>>().Value;
        Assert.Equal(HttpLoggingFields.RequestMethod | HttpLoggingFields.ResponseStatusCode,
            httpLogging.LoggingFields);

        var legacyLogging = provider.GetRequiredService<IOptions<RequestResponseLoggerOptions>>().Value;
        Assert.False(legacyLogging.IsEnabled);
        Assert.False(legacyLogging.WithRequest);
    }

    /// <summary>
    /// 启用时拒绝不完整的容量配置。
    /// </summary>
    [Fact]
    public void AddServices_WhenRateLimitingEnabledWithoutCapacity_ShouldRejectConfiguration()
    {
        var services = CreateServices(true, includeCapacity: false);

        Assert.Throws<InvalidOperationException>(() => new AppModule().AddServices(services));
    }

    /// <summary>
    /// 极小补充速率在注册阶段被拒绝。
    /// </summary>
    [Fact]
    public void AddServices_WhenRefillIsTooSmall_ShouldRejectAtRegistration()
    {
        var services = CreateServices(true, refill: "1e-300");

        Assert.Throws<ArgumentOutOfRangeException>(() => new AppModule().AddServices(services));
    }

    /// <summary>
    /// 可信代理列表仅包含显式配置的地址。
    /// </summary>
    [Fact]
    public void AddServices_WithKnownProxy_ShouldTrustOnlyConfiguredAddress()
    {
        var services = CreateServices(true, proxy: "192.0.2.10");

        new AppModule().AddServices(services);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal(ForwardedHeaders.XForwardedFor, options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Empty(options.KnownNetworks);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), Assert.Single(options.KnownProxies));
    }

    /// <summary>
    /// 无效代理地址在注册阶段被拒绝。
    /// </summary>
    [Fact]
    public void AddServices_WithInvalidProxy_ShouldRejectConfiguration()
    {
        var services = CreateServices(true, proxy: "not-an-ip");

        Assert.Throws<InvalidOperationException>(() => new AppModule().AddServices(services));
    }

    /// <summary>
    /// 显式启用普通数据库审计时替换默认日志存储。
    /// </summary>
    [Fact]
    public void AddServices_WhenDatabaseAuditingEnabled_ShouldRegisterDatabaseStore()
    {
        var services = CreateServices(false, databaseAuditing: true);

        new AppModule().AddServices(services);
        using var provider = services.BuildServiceProvider();

        Assert.IsType<DbAuditingStore>(provider.GetRequiredService<IAuditingStore>());
    }

    /// <summary>
    /// 数据库审计缺少连接串时在注册阶段拒绝。
    /// </summary>
    [Fact]
    public void AddServices_WhenDatabaseAuditingConnectionIsMissing_ShouldRejectConfiguration()
    {
        var services = CreateServices(false, databaseAuditing: true, includeConnection: false);

        Assert.Throws<InvalidOperationException>(() => new AppModule().AddServices(services));
    }

    /// <summary>
    /// 创建仅用于验证限流注册的服务集合。
    /// </summary>
    private static ServiceCollection CreateServices(bool enabled, bool includeCapacity = true, string proxy = null,
        string refill = "10", bool databaseAuditing = false, bool includeConnection = true)
    {
        var values = new Dictionary<string, string>
        {
            ["RateLimiting:Enabled"] = enabled.ToString(),
            ["RateLimiting:RefillTokensPerSecond"] = refill,
            ["RateLimiting:IdleTimeoutSeconds"] = "600",
            ["RateLimiting:MaxKeys"] = "10000"
        };
        if (includeCapacity)
            values["RateLimiting:Capacity"] = "100";
        if (proxy != null)
            values["RateLimiting:KnownProxies:0"] = proxy;
        if (databaseAuditing)
            values["Auditing:DatabaseEnabled"] = "true";
        if (includeConnection)
            values["ConnectionStrings:DefaultConnection"] = "server=localhost;database=test;user=test;password=test;";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        return services;
    }
}
