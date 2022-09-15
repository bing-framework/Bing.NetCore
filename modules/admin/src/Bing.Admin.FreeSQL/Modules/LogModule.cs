using System.ComponentModel;
using System.Threading;
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
            //services.AddNLog();
            // 同时输出2种方式的日志，可能存在重复 需要陆续兼容
            Logs.Exceptionless.Extensions.AddExceptionless(services, o =>
            {
                o.ApiKey = "N8HOaOLndl0hF7ZhOfiwJ9HmOi6kPwjKEKWLCMzE";
                o.ServerUrl = "http://10.186.132.40:5100";
            });
            //ExceptionlessClient.Default.Configuration.ApiKey= "ez9jumyxVxjTxqSm0oUQhCML3OGCkDfMGyW1hfmn";
            //ExceptionlessClient.Default.Configuration.ServerUrl = "http://10.186.132.40:5100";
            //ExceptionlessClient.Default.Startup();
            services.AddSingleton<ILogContextAccessor, LogContextAccessor>();
            services.AddLogging(loggingBuilder =>
            {
                var configuration = services.GetConfiguration();
                serilog.Log.Logger = new serilog.LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .Enrich.WithLogContext()
                    .Enrich.WithLogLevel()
                    .Enrich.WithSpan()
                    .WriteTo.Exceptionless(additionalOperation: (builder) =>
                    {
                        if (builder.Target.Data.TryGetValue("TraceId", out var traceId))
                            builder.Target.AddTags(traceId.ToString() ?? string.Empty);
                        builder.Target.AddTags((TraceIdContext.Current ??= new TraceIdContext(string.Empty)).TraceId);
                        return builder;
                    })
                    .ReadFrom.Configuration(configuration)
                    .ConfigLogLevel(configuration)
                    .CreateLogger();
                loggingBuilder.AddSerilog();
            });
            return services;
        }
    }

    /// <summary>
    /// 日志上下文访问器
    /// </summary>
    public class LogContextAccessor : ILogContextAccessor
    {
        /// <summary>
        /// 当前日志上下文
        /// </summary>
        private readonly AsyncLocal<LogContext> _currentLogContext;

        /// <summary>
        /// 初始化一个<see cref="LogContextAccessor"/>类型的实例
        /// </summary>
        public LogContextAccessor()
        {
            _currentLogContext = new AsyncLocal<LogContext>();
        }

        /// <summary>
        /// 日志上下文
        /// </summary>
        public LogContext Context
        {
            get
            {
                return _currentLogContext.Value ??= new LogContext();
            }
            set
            {
                _currentLogContext.Value = value;
            }
        }
    }
}
