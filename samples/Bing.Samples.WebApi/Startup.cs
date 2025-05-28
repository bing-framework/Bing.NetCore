using Bing.Samples.WebApi.Modules;

namespace Bing.Samples.WebApi;

/// <summary>
/// 启动配置
/// </summary>
public class Startup
{
    /// <summary>
    /// 初始化一个<see cref="Startup"/>类型的是
    /// </summary>
    /// <param name="configuration">配置</param>
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    /// <summary>
    /// 配置
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// 配置服务
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddBing()
            .AddModule<AppModule>();
    }

    /// <summary>
    /// 配置请求管道
    /// </summary>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
    {
        app.UseBing();
    }
}
