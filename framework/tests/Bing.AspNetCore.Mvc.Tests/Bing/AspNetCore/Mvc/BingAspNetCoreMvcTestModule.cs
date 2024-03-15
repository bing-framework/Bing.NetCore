using System.Net.Http;
using Bing.AspNetCore.Mvc.ExceptionHandling;
using Bing.Core.Modularity;
using Bing.Helpers;
using Bing.Http.Clients;
using Bing.Http;
using Bing.Serialization.SystemTextJson;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 测试模块
/// </summary>
[DependsOnModule(typeof(AspNetCoreModule))]
public class BingAspNetCoreMvcTestModule : AspNetCoreBingModule
{
    /// <inheritdoc />
    public override IServiceCollection AddServices(IServiceCollection services)
    {
        services.AddControllers(o =>
            {
                o.Filters.Add<BingExceptionFilter>();
                o.AddBing(services);
            })
            // Use DI to create controllers
            .AddControllersAsServices()
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
            });
        RegisterHttpContextAccessor(services);
        services.Configure((Microsoft.AspNetCore.Mvc.JsonOptions options) =>
        {
            options.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());
            options.JsonSerializerOptions.Converters.Add(new NullableDateTimeJsonConverter());
        });

        services.AddAuthentication(options =>
        {
            options.DefaultChallengeScheme = "Bearer";
            options.DefaultForbidScheme = "Cookie";
        }).AddCookie("Cookie").AddJwtBearer("Bearer", _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("MyClaimTestPolicy", policy =>
            {
                policy.RequireClaim("MyCustomClaimType", "42");
            });
        });

        RegisterDefaultHttpClient(services);
        RegisterBingHttpClient(services);

        return services;
    }

    /// <summary>
    /// 注册Http上下文访问器
    /// </summary>
    private void RegisterHttpContextAccessor(IServiceCollection services)
    {
        var httpContextAccessor = new HttpContextAccessor();
        services.TryAddSingleton<IHttpContextAccessor>(httpContextAccessor);
        Web.HttpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// 注册默认的Http客户端
    /// </summary>
    private void RegisterDefaultHttpClient(IServiceCollection services)
    {
        services.TryAddSingleton<HttpClient>(sp => ((TestServer)sp.GetRequiredService<IServer>()).CreateClient());
    }

    /// <summary>
    /// 注册Bing框架封装的Http客户端
    /// </summary>
    private void RegisterBingHttpClient(IServiceCollection services)
    {
        services.AddTransient<IHttpClient>(t =>
        {
            var client = new HttpClientService();
            client.SetHttpClient(t.GetService<IHost>().GetTestClient());
            return client;
        });
    }

    /// <inheritdoc />
    public override void UseModule(IApplicationBuilder app)
    {
        //app.UseCorrelationId();
        app.UseBingExceptionHandling();
        app.UseRouting();
        app.UseAuthentication(); // 认证中间件
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
