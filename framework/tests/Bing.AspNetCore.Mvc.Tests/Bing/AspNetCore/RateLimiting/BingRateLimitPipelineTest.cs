using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Bing.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Mvc.Tests.RateLimiting;

/// <summary>
/// 验证真实 HTTP 管道中的路由、身份和代理限流。
/// </summary>
public class BingRateLimitPipelineTest
{
    /// <summary>
    /// 未受信代理的转发头不能绕过匿名客户端额度。
    /// </summary>
    [Fact]
    public async Task Pipeline_UntrustedForwardedHeaders_ShouldShareClientBucket()
    {
        using var server = CreateServer(false);
        using var client = server.CreateClient();
        using var first = await SendAsync(client, "/orders/1", "192.0.2.1");
        using var second = await SendAsync(client, "/orders/2", "192.0.2.2");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        second.Headers.RetryAfter.ShouldNotBeNull();
    }

    /// <summary>
    /// 受信代理转发后的客户端地址隔离匿名额度。
    /// </summary>
    [Fact]
    public async Task Pipeline_TrustedProxy_ShouldUseForwardedClientAddress()
    {
        using var server = CreateServer(true);
        using var client = server.CreateClient();
        using var first = await SendAsync(client, "/orders/1", "192.0.2.1");
        using var second = await SendAsync(client, "/orders/2", "192.0.2.2");
        using var repeated = await SendAsync(client, "/orders/3", "192.0.2.1");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        repeated.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// 真实认证后同一用户在不同租户的额度隔离。
    /// </summary>
    [Fact]
    public async Task Pipeline_AuthenticatedTenant_ShouldIsolateAndUseRouteTemplate()
    {
        using var server = CreateServer(false);
        using var client = server.CreateClient();
        using var first = await SendAsync(client, "/orders/1", token: "tenant-one");
        using var second = await SendAsync(client, "/orders/2", token: "tenant-two");
        using var repeated = await SendAsync(client, "/orders/3", token: "tenant-one");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        repeated.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// 真实管道在未认证身份排在前面时使用后续第一个已认证身份。
    /// </summary>
    [Fact]
    public async Task Pipeline_AuthenticatedSchemeAfterUnauthenticatedScheme_ShouldUseAuthenticatedIdentity()
    {
        using var server = CreateMultiIdentityServer();
        using var client = server.CreateClient();
        using var mixed = await SendMultiIdentityAsync(client, "unauthenticated", "authenticated-trusted");
        using var trusted = await SendMultiIdentityAsync(client, second: "authenticated-trusted");

        mixed.StatusCode.ShouldBe(HttpStatusCode.OK);
        trusted.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// 真实管道不使用已认证主体中其他未认证身份的声明。
    /// </summary>
    [Fact]
    public async Task Pipeline_UnauthenticatedSchemeClaims_ShouldNotAffectAuthenticatedBucket()
    {
        using var server = CreateMultiIdentityServer();
        using var client = server.CreateClient();
        using var mixed = await SendMultiIdentityAsync(client, "authenticated-empty", "unauthenticated");
        using var anonymous = await SendMultiIdentityAsync(client);

        mixed.StatusCode.ShouldBe(HttpStatusCode.OK);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// 真实管道将两个已认证 scheme 的声明归属于第一个 scheme 并隔离不同主体。
    /// </summary>
    [Fact]
    public async Task Pipeline_TwoAuthenticatedSchemes_ShouldUseFirstSchemeAndKeepBucketsSeparate()
    {
        using var server = CreateMultiIdentityServer();
        using var client = server.CreateClient();
        using var both = await SendMultiIdentityAsync(client, "authenticated-a", "authenticated-b");
        using var firstOnly = await SendMultiIdentityAsync(client, "authenticated-a");
        using var secondOnly = await SendMultiIdentityAsync(client, second: "authenticated-b");

        both.StatusCode.ShouldBe(HttpStatusCode.OK);
        firstOnly.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        secondOnly.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// 未匹配路由在真实管道中共享有界分桶。
    /// </summary>
    [Fact]
    public async Task Pipeline_UnmatchedPaths_ShouldShareBucket()
    {
        using var server = CreateServer(false);
        using var client = server.CreateClient();
        using var first = await client.GetAsync("/unknown/1");
        using var second = await client.GetAsync("/unknown/2");

        first.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// 创建具有显式代理信任和认证顺序的测试宿主。
    /// </summary>
    private static TestServer CreateServer(bool trustProxy) => new(new WebHostBuilder()
        .ConfigureServices(services =>
        {
            services.AddRouting();
            services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
            services.AddBingLocalRateLimiting(1, 0.001, TimeSpan.FromMinutes(1), 100,
                options => options.UserIdClaimType = "sub");
        })
        .Configure(app =>
        {
            // TestServer 没有网络连接，显式设置模拟传输层对端地址。
            app.Use((context, next) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
                return next();
            });
            var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor };
            forwarded.KnownNetworks.Clear();
            forwarded.KnownProxies.Clear();
            forwarded.KnownProxies.Add(IPAddress.Parse(trustProxy ? "10.0.0.5" : "10.0.0.6"));
            app.UseForwardedHeaders(forwarded);
            app.UseRouting();
            app.UseAuthentication();
            app.UseBingRateLimiting();
            app.UseEndpoints(endpoints => endpoints.MapGet("/orders/{id}", context => context.Response.WriteAsync("ok")));
        }));

    /// <summary>
    /// 创建聚合两个认证 scheme 的测试宿主。
    /// </summary>
    /// <returns>配置了真实认证和限流中间件的测试服务器。</returns>
    private static TestServer CreateMultiIdentityServer() => new(new WebHostBuilder()
        .ConfigureServices(services =>
        {
            services.AddRouting();
            services.AddAuthentication("scheme-a")
                .AddScheme<AuthenticationSchemeOptions, MultiSchemeAuthenticationHandler>("scheme-a", _ => { })
                .AddScheme<AuthenticationSchemeOptions, MultiSchemeAuthenticationHandler>("scheme-b", _ => { });
            services.AddBingLocalRateLimiting(1, 0.001, TimeSpan.FromMinutes(1), 100,
                options => options.UserIdClaimType = "sub");
        })
        .Configure(app =>
        {
            app.Use((context, next) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
                return next();
            });
            app.UseRouting();
            app.UseAuthentication();
            app.Use(async (context, next) =>
            {
                var identities = new List<ClaimsIdentity>();
                var first = await context.AuthenticateAsync("scheme-a");
                if (first.Succeeded)
                    identities.AddRange(first.Principal.Identities);
                var second = await context.AuthenticateAsync("scheme-b");
                if (second.Succeeded)
                    identities.AddRange(second.Principal.Identities);
                context.User = new ClaimsPrincipal(identities);
                await next();
            });
            app.UseBingRateLimiting();
            app.UseEndpoints(endpoints => endpoints.MapGet("/orders/{id}", context => context.Response.WriteAsync("ok")));
        }));

    /// <summary>
    /// 发送带可选转发信息和测试凭据的请求。
    /// </summary>
    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path,
        string forwardedAddress = null, string token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (forwardedAddress != null)
            request.Headers.Add("X-Forwarded-For", forwardedAddress);
        if (token != null)
            request.Headers.Add("Authorization", "Bearer " + token);
        return await client.SendAsync(request);
    }

    /// <summary>
    /// 发送带两个测试认证 scheme 头部的请求。
    /// </summary>
    /// <param name="client">测试 HTTP 客户端。</param>
    /// <param name="first">第一个 scheme 的测试身份描述。</param>
    /// <param name="second">第二个 scheme 的测试身份描述。</param>
    /// <returns>测试服务器返回的 HTTP 响应。</returns>
    private static async Task<HttpResponseMessage> SendMultiIdentityAsync(HttpClient client,
        string first = null, string second = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/orders/1");
        if (first != null)
            request.Headers.Add("X-Test-Scheme-A", first);
        if (second != null)
            request.Headers.Add("X-Test-Scheme-B", second);
        return await client.SendAsync(request);
    }

    /// <summary>
    /// 仅接受两个固定凭据的测试认证处理器。
    /// </summary>
    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        /// <summary>
        /// 初始化一个 <see cref="TestAuthenticationHandler" /> 类型的实例。
        /// </summary>
        public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock) { }

        /// <inheritdoc />
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var token = Request.Headers.Authorization.ToString();
            var tenant = token switch { "Bearer tenant-one" => "t1", "Bearer tenant-two" => "t2", _ => null };
            if (tenant == null)
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim("sub", "u1"), new Claim("tenant_id", tenant) }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    /// <summary>
    /// 根据测试请求头生成可控的认证身份。
    /// </summary>
    private sealed class MultiSchemeAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        /// <summary>
        /// 初始化一个 <see cref="MultiSchemeAuthenticationHandler" /> 类型的实例。
        /// </summary>
        /// <param name="options">认证方案选项。</param>
        /// <param name="logger">认证日志工厂。</param>
        /// <param name="encoder">URL 编码器。</param>
        /// <param name="clock">系统时钟。</param>
        public MultiSchemeAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock) { }

        /// <inheritdoc />
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Scheme.Name == "scheme-a" ? "X-Test-Scheme-A" : "X-Test-Scheme-B";
            var descriptor = Request.Headers[header].ToString();
            var identity = CreateIdentity(descriptor);
            return Task.FromResult(identity == null
                ? AuthenticateResult.NoResult()
                : AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }

        /// <summary>
        /// 将测试身份描述转换为指定 scheme 的 ClaimsIdentity。
        /// </summary>
        /// <param name="descriptor">请求头中的身份描述。</param>
        /// <returns>测试身份；描述为空或不支持时返回 null。</returns>
        private ClaimsIdentity CreateIdentity(string descriptor)
        {
            return descriptor switch
            {
                "unauthenticated" => new ClaimsIdentity(new[]
                {
                    new Claim("sub", "untrusted-user"),
                    new Claim("tenant_id", "untrusted-tenant")
                }),
                "authenticated-empty" => new ClaimsIdentity(Array.Empty<Claim>(), Scheme.Name),
                "authenticated-trusted" => new ClaimsIdentity(new[]
                {
                    new Claim("sub", "trusted-user"),
                    new Claim("tenant_id", "trusted-tenant")
                }, Scheme.Name),
                "authenticated-a" => new ClaimsIdentity(new[] { new Claim("sub", "scheme-a-user") }, Scheme.Name),
                "authenticated-b" => new ClaimsIdentity(new[]
                {
                    new Claim("sub", "scheme-b-user"),
                    new Claim("tenant_id", "scheme-b-tenant")
                }, Scheme.Name),
                _ => null
            };
        }
    }
}
