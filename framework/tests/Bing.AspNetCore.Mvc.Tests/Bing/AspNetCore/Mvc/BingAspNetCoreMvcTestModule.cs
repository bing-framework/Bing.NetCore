using Bing.Core.Modularity;
using Bing.Helpers;
using Bing.Http.Clients;
using Bing.Http;
using Bing.Serialization.SystemTextJson;
using Microsoft.AspNetCore.Builder;
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
        services.AddControllers();
        RegisterHttpContextAccessor(services);
        services.Configure( ( Microsoft.AspNetCore.Mvc.JsonOptions options ) => {
            options.JsonSerializerOptions.Converters.Add( new DateTimeJsonConverter() );
            options.JsonSerializerOptions.Converters.Add( new NullableDateTimeJsonConverter() );
        } );
        services.AddTransient<IHttpClient>(t =>
        {
            var client = new HttpClientService();
            client.SetHttpClient(t.GetService<IHost>().GetTestClient());
            return client;
        });
        //services.AddAuthentication(options =>
        //{
        //    options.DefaultChallengeScheme = "Bearer";
        //    options.DefaultForbidScheme = "Cookie";
        //}).AddCookie("Cookie").AddJwtBearer("Bearer", _ => { });

        //services.AddAuthorization(options =>
        //{
        //    options.AddPolicy("MyClaimTestPolicy", policy =>
        //    {
        //        policy.RequireClaim("MyCustomClaimType", "42");
        //    });
        //});

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

    /// <inheritdoc />
    public override void UseModule(IApplicationBuilder app)
    {
        //app.UseCorrelationId();
        app.UseRouting();
        //app.UseAuthentication();
        //app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
