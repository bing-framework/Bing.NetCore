using System.Net;
using System.Security.Cryptography;
using System.Text;
using Bing.Admin.Modules;
using Bing.AspNetCore.RateLimiting;
using Bing.RateLimiting;
using CSRedis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>
/// 验证 Admin 模块注册后的 HTTP 限流链路。
/// </summary>
public sealed class AdminRateLimitingHttpPipelineTest
{
    /// <summary>
    /// 串行化会切换全局 Redis 客户端的集成用例。
    /// </summary>
    private static readonly SemaphoreSlim RedisGate = new(1, 1);

    /// <summary>
    /// Redis 可用时通过 Admin 模块管道共享请求额度。
    /// </summary>
    [AdminRedisIntegrationFact]
    public async Task AppModule_WhenRedisIsAvailable_ShouldLimitThroughHttpPipeline()
    {
        await RedisGate.WaitAsync();
        try
        {
            var connection = GetRedisConnection();
            RedisHelper.Initialization(new CSRedisClient(connection));
            var redisKey = GetPolicyKey();
            await RedisHelper.DelAsync(redisKey);
            var services = CreateServices();
            new AppModule().AddServices(services);
            using var provider = services.BuildServiceProvider();
            var pipeline = BuildPipeline(provider);

            var first = await InvokeAsync(pipeline, provider, "192.0.2.41");
            var second = await InvokeAsync(pipeline, provider, "192.0.2.41");

            Assert.Equal(StatusCodes.Status200OK, first.StatusCode);
            Assert.Equal(StatusCodes.Status429TooManyRequests, second.StatusCode);
            Assert.True(second.Headers.ContainsKey("Retry-After"));
        }
        finally
        {
            try
            {
                await RedisHelper.DelAsync(GetPolicyKey());
            }
            catch
            {
                // 初始化或连接失败时没有可清理的测试键，不能遮蔽原始测试错误。
            }
            RedisGate.Release();
        }
    }

    /// <summary>
    /// 真实 Redis 连接不可用时 Admin 管道返回服务不可用。
    /// </summary>
    [AdminRedisIntegrationFact]
    public async Task AppModule_WhenRedisIsUnavailable_ShouldReturnUnavailable()
    {
        await RedisGate.WaitAsync();
        try
        {
            RedisHelper.Initialization(new CSRedisClient(
                "127.0.0.1:63999,connectTimeout=100,syncTimeout=100,asyncTimeout=100"));
            var services = CreateServices();
            new AppModule().AddServices(services);
            using var provider = services.BuildServiceProvider();
            var pipeline = BuildPipeline(provider);

            var response = await InvokeAsync(pipeline, provider, "192.0.2.43");

            Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            RedisHelper.Initialization(new CSRedisClient(GetRedisConnection()));
            RedisGate.Release();
        }
    }

    /// <summary>
    /// 限流存储不可用时 Admin 管道默认拒绝请求。
    /// </summary>
    [Fact]
    public async Task AppModule_WhenLimiterFails_ShouldReturnUnavailable()
    {
        var services = CreateServices();
        services.AddSingleton<ITokenBucketRateLimiter, FailingLimiter>();
        new AppModule().AddServices(services);
        using var provider = services.BuildServiceProvider();
        var pipeline = BuildPipeline(provider);

        var response = await InvokeAsync(pipeline, provider, "192.0.2.42");

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
    }

    /// <summary>
    /// 创建启用 Admin Redis 限流的最小服务集合。
    /// </summary>
    private static ServiceCollection CreateServices()
    {
        var values = new Dictionary<string, string>
        {
            ["RateLimiting:Enabled"] = "true",
            ["RateLimiting:Capacity"] = "1",
            ["RateLimiting:RefillTokensPerSecond"] = "0.001",
            ["RateLimiting:IdleTimeoutSeconds"] = "60",
            ["RateLimiting:MaxKeys"] = "100"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        return services;
    }

    /// <summary>
    /// 构建只包含 Admin 限流中间件的请求管道。
    /// </summary>
    private static RequestDelegate BuildPipeline(IServiceProvider provider)
    {
        var builder = new ApplicationBuilder(provider);
        builder.UseBingRateLimiting();
        builder.Run(_ => Task.CompletedTask);
        return builder.Build();
    }

    /// <summary>
    /// 执行一个带固定客户端地址的测试请求。
    /// </summary>
    private static async Task<HttpResponse> InvokeAsync(RequestDelegate pipeline, IServiceProvider provider, string address)
    {
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        await pipeline(context);
        return context.Response;
    }

    /// <summary>
    /// 读取 Redis 集成测试连接串。
    /// </summary>
    private static string GetRedisConnection() =>
        Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection") ?? "127.0.0.1:6379";

    /// <summary>
    /// 计算 Admin 固定限流策略的 Redis 键。
    /// </summary>
    private static string GetPolicyKey()
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes("Bing.Admin"));
        return "bing:rate:" + Convert.ToHexString(digest);
    }

    /// <summary>
    /// 模拟 Redis 限流器不可用。
    /// </summary>
    private sealed class FailingLimiter : ITokenBucketRateLimiter
    {
        /// <inheritdoc />
        public RateLimitResult TryAcquire(string key, int permits = 1) =>
            throw new InvalidOperationException("Redis unavailable.");

        /// <inheritdoc />
        public Task<RateLimitResult> TryAcquireAsync(string key, int permits = 1,
            CancellationToken cancellationToken = default) =>
            Task.FromException<RateLimitResult>(new InvalidOperationException("Redis unavailable."));
    }

    /// <summary>
    /// 受环境变量控制的 Redis 集成测试特性。
    /// </summary>
    private sealed class AdminRedisIntegrationFactAttribute : FactAttribute
    {
        /// <summary>
        /// 初始化一个 <see cref="AdminRedisIntegrationFactAttribute" /> 类型的实例。
        /// </summary>
        public AdminRedisIntegrationFactAttribute()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("RUN_ADMIN_REDIS_HTTP_TESTS"), "true",
                    StringComparison.OrdinalIgnoreCase))
                Skip = "Admin Redis HTTP 集成测试已跳过。设置 RUN_ADMIN_REDIS_HTTP_TESTS=true 以启用。";
        }
    }
}
