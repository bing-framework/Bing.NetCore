using System;
using System.ComponentModel;
using System.Diagnostics;
using Bing.AspNetCore;
using Bing.Core.Modularity;
using Bing.Logging;
using Bing.Logging.Serilog;
using Bing.Tracing;
using Exceptionless;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Enrichers.Span;
using serilog = Serilog;

namespace Bing.Admin.Modules
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
            services.AddBingLogging(x => { });
            // 同时输出2种方式的日志，可能存在重复 需要陆续兼容
            
            //ExceptionlessClient.Default.Configuration.ApiKey = "vCFssLV6HPlElQ6wkQJaLvaCqvhTTsWWTOm8dzQo";
            //ExceptionlessClient.Default.Configuration.ServerUrl = "http://10.186.135.147:5100";
            //ExceptionlessClient.Default.Startup();
            services.AddExceptionless(x =>
            {
                x.ApiKey = "aUBmHcfhK8VvPLqsTYvVOeXJYY4jN5QghOY68FZe";
                x.ServerUrl = "http://10.186.135.147:5100";
            });
            services.AddLogging(loggingBuilder =>
            {
                var logFilePath = $"{AppContext.BaseDirectory}logs\\log-.log";
                var configuration = services.GetConfiguration();
                serilog.Log.Logger = new serilog.LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .Enrich.WithLogContext()
                    .Enrich.WithLogLevel()
                    .Enrich.WithSpan()
                    .WriteTo.Exceptionless(additionalOperation: (builder) =>
                    {
                        if (builder.Target.Data.TryGetValue("TraceId", out var traceId))
                        {
                            builder.Target.AddTags(traceId.ToString() ?? string.Empty);
                            Debug.WriteLine($"Exceptionless[TraceId:{traceId}]");
                        }
                        else
                        {
                            var id = (TraceIdContext.Current ??= new TraceIdContext(string.Empty)).TraceId;
                            builder.Target.AddTags(id);
                            Debug.WriteLine($"Exceptionless-Id[TraceId:{id}]");
                        }
                        
                        return builder;
                    })
                    .WriteTo.Async(o =>
                    {
                        o.File(logFilePath,
                            rollingInterval: RollingInterval.Day,
                            rollOnFileSizeLimit: true,
                            fileSizeLimitBytes: 102400,
                            retainedFileCountLimit: 10,
                            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}][{LogLevel}][{TraceId}][{SourceContext}] {Message}{NewLine}{Exception}"
                        );
                    })
                    .ReadFrom.Configuration(configuration)
                    .ConfigLogLevel(configuration)
                    .CreateLogger();
                loggingBuilder.AddSerilog();
            });
            return services;
        }

        /// <summary>
        /// 应用AspNetCore的服务业务
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public override void UseModule(IApplicationBuilder app)
        {
            app.UseExceptionless();
        }
    }
}
