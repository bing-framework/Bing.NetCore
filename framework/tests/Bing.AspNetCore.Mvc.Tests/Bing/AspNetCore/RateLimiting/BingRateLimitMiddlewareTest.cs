using System.Net;
using System.Security.Claims;
using Bing.AspNetCore.RateLimiting;
using Bing.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Mvc.Tests.RateLimiting;

/// <summary>
/// 验证 HTTP 令牌桶分组与拒绝响应。
/// </summary>
public class BingRateLimitMiddlewareTest
{
    /// <summary>
    /// 小于最小限流键长度的配置由真实中间件构造拒绝。
    /// </summary>
    /// <param name="maxKeyLength">待验证的限流键最大长度。</param>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(31)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaxKeyLengthIsBelowMinimum_ShouldThrowArgumentOutOfRangeException(int maxKeyLength)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BingRateLimitMiddleware(_ => Task.CompletedTask,
                Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions
                {
                    MaxKeyLength = maxKeyLength
                })));

        exception.ParamName.ShouldBe("options");
        exception.Message.ShouldContain("至少为 32 个字符");
    }

    /// <summary>
    /// 达到最小长度的限流键配置由真实中间件构造接受。
    /// </summary>
    /// <param name="maxKeyLength">待验证的限流键最大长度。</param>
    [Theory]
    [InlineData(32)]
    [InlineData(71)]
    [InlineData(256)]
    public void Constructor_WhenMaxKeyLengthMeetsMinimum_ShouldSucceed(int maxKeyLength)
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions
            {
                MaxKeyLength = maxKeyLength
            }));

        middleware.ShouldNotBeNull();
    }

    /// <summary>
    /// 超额请求返回 429 且不执行后续处理器。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_Rejected_ShouldReturnRetryAfter()
    {
        var forwarded = false;
        var middleware = new BingRateLimitMiddleware(_ =>
        {
            forwarded = true;
            return Task.CompletedTask;
        }, Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions { PolicyName = "api" }));
        var context = new DefaultHttpContext();
        context.Request.Path = "/stock";
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "/stock"));
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        context.Request.Headers["X-Forwarded-For"] = "1.2.3.4";
        var limiter = new FakeLimiter(new RateLimitResult(false, TimeSpan.FromSeconds(2.1)));

        await middleware.InvokeAsync(context, limiter);

        forwarded.ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(429);
        context.Response.Headers["Retry-After"].ToString().ShouldBe("3");
        limiter.LastKey.ShouldBe("3:api6:/stock12:ip:127.0.0.1");
    }

    /// <summary>
    /// 已认证用户按可信身份键分组并继续请求。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_ShouldUseSubjectId()
    {
        var forwarded = false;
        var middleware = new BingRateLimitMiddleware(_ =>
        {
            forwarded = true;
            return Task.CompletedTask;
        }, Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions()));
        var context = new DefaultHttpContext();
        context.Request.Path = "/stock";
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "/stock"));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "u1") }, "test"));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));

        await middleware.InvokeAsync(context, limiter);

        forwarded.ShouldBeTrue();
        limiter.LastKey.ShouldBe("7:default6:/stock7:user:u1");
    }

    /// <summary>
    /// 默认键包含租户声明，避免不同租户共享用户分桶。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_TenantClaim_ShouldIsolateTenantBucket()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions()));
        var context = new DefaultHttpContext();
        context.Request.Path = "/stock";
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "/stock"));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "u1"),
            new Claim("tenant_id", "t1")
        }, "test"));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));

        await middleware.InvokeAsync(context, limiter);

        limiter.LastKey.ShouldBe("7:default6:/stock21:tenant:2:t1|7:user:u1");
    }

    /// <summary>
    /// 默认 Claims 的租户和用户分隔符不会合并不同分桶。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_TenantAndUserSeparators_ShouldKeepBucketsSeparate()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions()));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));

        await middleware.InvokeAsync(AuthenticatedContext("a|user:b", "c"), limiter);
        var firstKey = limiter.LastKey;
        await middleware.InvokeAsync(AuthenticatedContext("a", "b|user:c"), limiter);

        limiter.LastKey.ShouldNotBe(firstKey);
    }

    /// <summary>
    /// 默认 Claims 中相同的租户和用户始终使用相同分桶。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_SameTenantAndUser_ShouldReuseBucket()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions()));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));

        await middleware.InvokeAsync(AuthenticatedContext("tenant-a", "user-a"), limiter);
        var firstKey = limiter.LastKey;
        await middleware.InvokeAsync(AuthenticatedContext("tenant-a", "user-a"), limiter);

        limiter.LastKey.ShouldBe(firstKey);
    }

    /// <summary>
    /// 摘要长度路径仍能隔离不同的默认 Claims 分桶。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_LongTenantAndUserClaims_ShouldKeepDigestBucketsSeparate()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions { MaxKeyLength = 32 }));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));
        const string prefix = "tenant-prefix-0123456789-";
        const string firstTenant = prefix + "a|user:b";
        const string firstUser = "c";
        const string secondTenant = prefix + "a";
        const string secondUser = "b|user:c";

        $"tenant:{firstTenant}|user:{firstUser}"
            .ShouldBe($"tenant:{secondTenant}|user:{secondUser}");

        await middleware.InvokeAsync(AuthenticatedContext(firstTenant, firstUser), limiter);
        var firstKey = limiter.LastKey;
        await middleware.InvokeAsync(AuthenticatedContext(secondTenant, secondUser), limiter);
        var secondKey = limiter.LastKey;

        firstKey.ShouldStartWith("sha256:");
        secondKey.ShouldStartWith("sha256:");
        firstKey.Length.ShouldBe(32);
        secondKey.Length.ShouldBe(32);
        secondKey.ShouldNotBe(firstKey);
    }

    /// <summary>
    /// 稳定路由选择器覆盖原始请求路径。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_RouteKeySelector_ShouldUseStableRoute()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions
            {
                RouteKeySelector = _ => "orders/{id}"
            }));
        var context = new DefaultHttpContext();
        context.Request.Path = "/orders/123456";
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));

        await middleware.InvokeAsync(context, limiter);

        limiter.LastKey.ShouldBe("7:default11:orders/{id}10:ip:unknown");
    }

    /// <summary>
    /// 匹配路由时默认使用路由模板而不是参数值。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_RouteEndpoint_ShouldUseRoutePattern()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions()));
        var context = new DefaultHttpContext();
        context.Request.Path = "/orders/123456";
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
            RoutePatternFactory.Parse("orders/{id}"), 0, new EndpointMetadataCollection(), "orders"));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));

        await middleware.InvokeAsync(context, limiter);

        limiter.LastKey.ShouldBe("7:default11:orders/{id}10:ip:unknown");
    }

    /// <summary>
    /// 路由和自定义身份中的分隔符不会合并不同分桶。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_SeparatorInCustomKey_ShouldKeepBucketsSeparate()
    {
        var options = new BingRateLimitOptions
        {
            PolicyName = "api",
            KeySelector = context => context.Items["subject"]?.ToString(),
            RouteKeySelector = context => context.GetEndpoint()?.DisplayName
        };
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(options));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));
        var first = new DefaultHttpContext();
        first.Request.Path = "/stock:tenant";
        first.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "/stock:tenant"));
        first.Items["subject"] = "user";
        await middleware.InvokeAsync(first, limiter);
        var firstKey = limiter.LastKey;

        var second = new DefaultHttpContext();
        second.Request.Path = "/stock";
        second.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "/stock"));
        second.Items["subject"] = "tenant:user";
        await middleware.InvokeAsync(second, limiter);

        limiter.LastKey.ShouldNotBe(firstKey);
    }

    /// <summary>
    /// 限流存储故障时拒绝请求并保留取消异常。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_LimiterFailure_ShouldReturnUnavailable()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions()));
        var context = new DefaultHttpContext();
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero)) { Failure = new InvalidOperationException("redis unavailable") };

        await middleware.InvokeAsync(context, limiter);

        context.Response.StatusCode.ShouldBe(503);
    }

    /// <summary>
    /// 限流存储故障在显式降级时继续执行请求。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_LimiterFailureWithFailOpen_ShouldForwardRequest()
    {
        var forwarded = false;
        var middleware = new BingRateLimitMiddleware(_ =>
        {
            forwarded = true;
            return Task.CompletedTask;
        }, Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions
        {
            FailOpenOnInfrastructureError = true
        }));
        var context = new DefaultHttpContext();
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero))
        {
            Failure = new InvalidOperationException("redis unavailable")
        };

        await middleware.InvokeAsync(context, limiter);

        forwarded.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(200);
    }

    /// <summary>
    /// 超长限流键使用固定摘要，避免将高基数原文写入共享存储。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_LongKey_ShouldUseBoundedHashKey()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions
            {
                MaxKeyLength = 32,
                KeySelector = _ => new string('x', 512)
            }));
        var context = new DefaultHttpContext();
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));

        await middleware.InvokeAsync(context, limiter);

        limiter.LastKey.ShouldStartWith("sha256:");
        limiter.LastKey.Length.ShouldBe(32);
    }

    /// <summary>
    /// 重复注册保持单个本地限流器实例。
    /// </summary>
    [Fact]
    public void AddBingLocalRateLimiting_ShouldBeIdempotent()
    {
        var services = new ServiceCollection();
        services.AddBingLocalRateLimiting(2, 1, TimeSpan.FromMinutes(1), 20);
        services.AddBingLocalRateLimiting(2, 1, TimeSpan.FromMinutes(1), 20);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITokenBucketRateLimiter>().ShouldBeOfType<LocalTokenBucketRateLimiter>();
        services.Count(x => x.ServiceType == typeof(ITokenBucketRateLimiter)).ShouldBe(1);
    }

    /// <summary>
    /// 任意未匹配路径使用同一分桶，避免随机路径扩张键空间。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_UnmatchedPaths_ShouldShareBucket()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions()));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));
        var context = new DefaultHttpContext();
        context.Request.Path = "/missing/one";
        await middleware.InvokeAsync(context, limiter);
        var first = limiter.LastKey;
        context.Request.Path = "/missing/two";
        await middleware.InvokeAsync(context, limiter);

        limiter.LastKey.ShouldBe(first);
        first.ShouldBe("7:default9:unmatched10:ip:unknown");
    }

    /// <summary>
    /// 未认证身份的租户声明不改变匿名分桶。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_UnauthenticatedClaims_ShouldUseClientAddress()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions()));
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "untrusted"), new Claim("tenant_id", "untrusted")
        }));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));

        await middleware.InvokeAsync(context, limiter);

        limiter.LastKey.ShouldBe("7:default9:unmatched12:ip:127.0.0.1");
    }

    /// <summary>
    /// 认证身份排在未认证身份之后时使用第一个已认证身份。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AuthenticatedIdentityAfterUnauthenticated_ShouldUseFirstAuthenticatedIdentity()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions { UserIdClaimType = "sub" }));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));
        var authenticated = new ClaimsIdentity(new[]
        {
            new Claim("sub", "trusted-user"),
            new Claim("tenant_id", "trusted-tenant")
        }, "trusted");

        await middleware.InvokeAsync(ClaimsContext(authenticated), limiter);
        var expectedKey = limiter.LastKey;

        await middleware.InvokeAsync(ClaimsContext(
            new ClaimsIdentity(new[]
            {
                new Claim("sub", "untrusted-user"),
                new Claim("tenant_id", "untrusted-tenant")
            }),
            authenticated), limiter);

        limiter.LastKey.ShouldBe(expectedKey);
    }

    /// <summary>
    /// 未认证身份中的声明不参与已认证请求分桶。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_UnauthenticatedClaimsOnAnotherIdentity_ShouldNotBeUsed()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions { UserIdClaimType = "sub" }));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));
        var authenticatedWithoutClaims = new ClaimsIdentity(Array.Empty<Claim>(), "trusted");
        var unauthenticatedWithClaims = new ClaimsIdentity(new[]
        {
            new Claim("sub", "untrusted-user"),
            new Claim("tenant_id", "untrusted-tenant")
        });

        await middleware.InvokeAsync(ClaimsContext(authenticatedWithoutClaims, unauthenticatedWithClaims), limiter);
        var mixedIdentityKey = limiter.LastKey;

        await middleware.InvokeAsync(ClaimsContext(authenticatedWithoutClaims), limiter);

        limiter.LastKey.ShouldBe(mixedIdentityKey);
    }

    /// <summary>
    /// 两个已认证身份的声明必须归属于第一个已认证身份且保持分桶隔离。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_MultipleAuthenticatedIdentities_ShouldKeepClaimsInFirstIdentity()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions { UserIdClaimType = "sub" }));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));
        var firstIdentity = new ClaimsIdentity(new[] { new Claim("sub", "scheme-a-user") }, "scheme-a");
        var secondIdentity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "scheme-b-user"),
            new Claim("tenant_id", "scheme-b-tenant")
        }, "scheme-b");

        await middleware.InvokeAsync(ClaimsContext(firstIdentity, secondIdentity), limiter);
        var firstBucket = limiter.LastKey;
        await middleware.InvokeAsync(ClaimsContext(firstIdentity), limiter);
        firstBucket.ShouldBe(limiter.LastKey);

        await middleware.InvokeAsync(ClaimsContext(secondIdentity, firstIdentity), limiter);
        limiter.LastKey.ShouldNotBe(firstBucket);
    }

    /// <summary>
    /// 宿主可选择已经认证的用户标识声明。
    /// </summary>
    [Fact]
    public async Task InvokeAsync_CustomUserIdClaim_ShouldUseAuthenticatedSubject()
    {
        var middleware = new BingRateLimitMiddleware(_ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new BingRateLimitOptions { UserIdClaimType = "sub" }));
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "u1") }, "test"));
        var limiter = new FakeLimiter(new RateLimitResult(true, TimeSpan.Zero));

        await middleware.InvokeAsync(context, limiter);

        limiter.LastKey.ShouldBe("7:default9:unmatched7:user:u1");
    }

    /// <summary>
    /// 创建带租户和用户 Claims 的标准请求上下文。
    /// </summary>
    /// <param name="tenantId">租户声明值。</param>
    /// <param name="userId">用户标识声明值。</param>
    /// <returns>已认证且绑定固定路由的 HTTP 上下文。</returns>
    private static DefaultHttpContext AuthenticatedContext(string tenantId, string userId)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/stock";
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(), "/stock"));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("tenant_id", tenantId)
        }, "test"));
        return context;
    }

    /// <summary>
    /// 创建包含指定身份顺序的限流请求上下文。
    /// </summary>
    /// <param name="identities">按主体顺序排列的身份集合。</param>
    /// <returns>绑定固定客户端地址的请求上下文。</returns>
    private static DefaultHttpContext ClaimsContext(params ClaimsIdentity[] identities)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identities)
        };
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        return context;
    }

    /// <summary>
    /// 保存最近一次请求的限流键。
    /// </summary>
    private sealed class FakeLimiter : ITokenBucketRateLimiter
    {
        /// <summary>
        /// 预设返回结果。
        /// </summary>
        private readonly RateLimitResult _result;

        /// <summary>
        /// 初始化一个 <see cref="FakeLimiter" /> 类型的实例。
        /// </summary>
        /// <param name="result">预设返回结果。</param>
        public FakeLimiter(RateLimitResult result) => _result = result;

        /// <summary>
        /// 最近一次请求的限流键。
        /// </summary>
        public string LastKey { get; private set; }

        /// <summary>
        /// 请求时抛出的异常。
        /// </summary>
        public Exception Failure { get; set; }

        /// <inheritdoc />
        public RateLimitResult TryAcquire(string key, int permits = 1)
        {
            LastKey = key;
            return _result;
        }

        /// <inheritdoc />
        public Task<RateLimitResult> TryAcquireAsync(string key, int permits = 1, CancellationToken cancellationToken = default) =>
            Failure == null ? Task.FromResult(TryAcquire(key, permits)) : Task.FromException<RateLimitResult>(Failure);
    }
}
