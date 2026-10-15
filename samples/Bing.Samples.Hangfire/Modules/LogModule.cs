using System.ComponentModel;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.Logging;
using Bing.Logging.Serilog;
using Bing.Tracing;
using Exceptionless;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Enrichers.Span;
using serilog = Serilog;

namespace Bing.Samples.Hangfire.Modules
{
    /// <summary>
    /// 日志模块
    /// </summary>
    [Description("日志模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class LogModule : AspNetCoreBingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Framework;

        /// <summary>
        /// 模块启动顺序。模块启动的顺序先按级别启动，同一级别内部再按此顺序启动，
        /// 级别默认为0，表示无依赖，需要在同级别有依赖顺序的时候，再重写为>0的顺序值
        /// </summary>
        public override int Order => 0;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            //services.AddNLog();
            var configuration = services.GetConfiguration();
            var exceptionlessApiKey = configuration["Exceptionless:ApiKey"];
            var exceptionlessServerUrl = configuration["Exceptionless:ServerUrl"];
            if (!string.IsNullOrWhiteSpace(exceptionlessApiKey))
            {
                services.AddExceptionless(o =>
                {
                    o.ApiKey = exceptionlessApiKey;
                    o.ServerUrl = exceptionlessServerUrl;
                });
            }
            services.AddBingLogging(x => { });
            services.AddLogging(loggingBuilder =>
            {
                var loggerConfiguration = new serilog.LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .Enrich.WithLogContext()
                    .Enrich.WithLogLevel()
                    .Enrich.WithSpan();
                if (!string.IsNullOrWhiteSpace(exceptionlessApiKey))
                {
                    loggerConfiguration = loggerConfiguration.WriteTo.Exceptionless(
                        exceptionlessApiKey, exceptionlessServerUrl, additionalOperation: builder =>
                        {
                            if (builder.Target.Data.TryGetValue("TraceId", out var traceId))
                                builder.Target.AddTags(traceId.ToString() ?? string.Empty);
                            builder.Target.AddTags((TraceIdContext.Current ??= new TraceIdContext(string.Empty)).TraceId);
                            return builder;
                        });
                }
                serilog.Log.Logger = loggerConfiguration
                    .ReadFrom.Configuration(configuration)
                    .ConfigLogLevel(configuration)
                    .CreateLogger();
                loggingBuilder.AddSerilog();
            });
            return services;
        }
    }
}
