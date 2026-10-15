using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using AspectCore.Configuration;
using Bing.AspNetCore;
using Bing.AspNetCore.Auditing;
using Bing.AspNetCore.RateLimiting;
using Bing.AspNetCore.Extensions;
using Bing.AspNetCore.Hosting;
using Bing.AspNetCore.Logs;
using Bing.AspNetCore.Mvc.ExceptionHandling;
using Bing.AspNetCore.Mvc.Filters;
using Bing.Auditing.Persistence;
using Bing.Admin.Service.Events;
using Bing.Core.Modularity;
using Bing.DependencyInjection;
using Bing.Domain.Entities.Events;
using Bing.Helpers;
using Bing.Security.Claims;
using Bing.RateLimiting;
using Bing.Tracing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// 应用程序模块
    /// </summary>
    [Description("应用程序模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class AppModule : AspNetCoreBingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Application;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            BingClaimTypes.UserId = IdentityModel.JwtClaimTypes.Subject;
            BingClaimTypes.UserName = IdentityModel.JwtClaimTypes.Name;
            services.AddControllers(o =>
                {
                    o.Filters.Add<RequestResponseLoggerActionFilter>();
                    o.Filters.Add<RequestResponseLoggerErrorFilter>();
                    o.Filters.Add<ValidationModelAttribute>();
                    o.Filters.Add<ResultHandlerAttribute>();
                    o.Filters.Add<BingExceptionFilter>();
                    o.Conventions.Add(new AuthorizeControllerModelConvention());
                    o.ModelBindingMessageProvider.UseTranslatedResources();
                })
                .AddControllersAsServices()// 解决属性注入无法在控制器中注入的问题
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
                });
            // 配置模型校验
            services.Configure<ApiBehaviorOptions>(o =>
            {
                o.SuppressModelStateInvalidFilter = true;
            });

            services.EnableAop(o =>
            {
                o.ThrowAspectException = true;
                o.NonAspectPredicates.AddNamespace("Bing.Swashbuckle");
                o.NonAspectPredicates.AddNamespace("DotNetCore.CAP");
                //o.NonAspectPredicates.Add(m => m.DeclaringType == typeof(IUnitOfWork));
            });
            services.EnableBingUnitOfWork();
            services.AddDomainEventDispatcher();
            services.AddBingTrafficGate(options => options.StartReady = false);
            services.AddBingHttpAuditing(options =>
            {
                options.ApplicationName = "Bing.Admin";
                options.UserIdClaimType = BingClaimTypes.UserId;
                options.UserNameClaimType = BingClaimTypes.UserName;
                options.TenantClaimType = BingClaimTypes.TenantId;
                options.ClientIdClaimType = BingClaimTypes.ClientId;
            });
            ConfigureDatabaseAuditing(services);
            ConfigureAuditLogQuery(services);
            ConfigureRateLimiting(services);
            //services.AddAudit();
            // 添加跟踪ID
            services.Configure<CorrelationIdOptions>(x =>
            {
                x.HttpHeaderName = "X-Correlation-Id";
                x.SetResponseHeader = true;
            });
            // 内置日志仅保留方法和状态码；安全 HTTP 审计负责请求级审计，替代旧的完整请求响应采集。
            services.AddHttpLogging(o =>
                o.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.ResponseStatusCode);
            // 旧请求响应采集默认关闭，避免在中间件恢复时读取查询参数和请求正文。
            services.AddRequestResponseLog(o =>
            {
                o.IsEnabled = false;
                o.Name = "Bing.Admin";
                o.WithRequest = false;
                //o.RequestFilter.Add("/swagger/**");
                //o.RequestFilter.Add("/swagger");
            });
            return services;
        }

        /// <summary>
        /// 应用AspNetCore的服务业务
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public override void UseModule(IApplicationBuilder app)
        {
            var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
            if (configuration.IsBingBackgroundRole())
            {
                // 后台角色复用同一宿主程序集，但不装配 HTTP 业务管道或标记 HTTP 已就绪。
                Enabled = true;
                return;
            }
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var rateLimiting = configuration.GetSection("RateLimiting");
            if (rateLimiting.GetValue<bool>("Enabled") && rateLimiting.GetSection("KnownProxies").GetChildren().Any())
                app.UseForwardedHeaders();
            app.UseCorrelationId();
            // 内置日志
            app.UseHttpLogging();
            // 自定义日志
            app.UseRequestResponseLog();
            app.UseBingExceptionHandling();
            app.UseBingTrafficGate();
            // 初始化Http上下文访问器
            Web.HttpContextAccessor = app.ApplicationServices.GetService<IHttpContextAccessor>();
            app.UseAuthentication();
            app.UseBingSerilogEnrichers();
            app.UseRouting();
            app.UseBingHttpAuditing();
            if (rateLimiting.GetValue<bool>("Enabled"))
                app.UseBingRateLimiting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
            app.ApplicationServices.MarkBingTrafficReady();
            Enabled = true;
        }

        /// <summary>
        /// 注册显式启用的集群共享限流策略。
        /// </summary>
        private static void ConfigureRateLimiting(IServiceCollection services)
        {
            var settings = services.GetConfiguration().GetSection("RateLimiting");
            if (!settings.GetValue<bool>("Enabled"))
                return;

            var capacity = settings.GetValue<int?>("Capacity");
            var refill = settings.GetValue<double?>("RefillTokensPerSecond");
            var idleSeconds = settings.GetValue<int?>("IdleTimeoutSeconds");
            var maxKeys = settings.GetValue<int?>("MaxKeys");
            if (capacity is not > 0 || refill is not > 0 || double.IsInfinity(refill.Value) ||
                idleSeconds is not > 0 || maxKeys is not > 0)
                throw new InvalidOperationException("启用 Admin 限流时必须配置有效的容量、补充速率、闲置时间和最大键数。");

            const string policy = "Bing.Admin";
            services.TryAddSingleton<ITokenBucketRateLimiter>(
                new RedisTokenBucketRateLimiter(policy, capacity.Value, refill.Value,
                    TimeSpan.FromSeconds(idleSeconds.Value), maxKeys.Value));
            services.Configure<BingRateLimitOptions>(options =>
            {
                options.PolicyName = policy;
                options.UserIdClaimType = BingClaimTypes.UserId;
                options.TenantClaimType = BingClaimTypes.TenantId;
            });
            ConfigureTrustedProxies(services, settings);
        }

        /// <summary>
        /// 按配置注册普通审计的数据库存储。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <remarks>默认保持日志存储；启用后使用独立连接，不复用业务事务。</remarks>
        private static void ConfigureDatabaseAuditing(IServiceCollection services)
        {
            var configuration = services.GetConfiguration();
            var settings = configuration.GetSection("Auditing");
            if (!settings.GetValue<bool>("DatabaseEnabled"))
                return;

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "启用 Auditing:DatabaseEnabled 时必须配置 ConnectionStrings:DefaultConnection。");

            services.AddBingDatabaseAuditing(AuditLogDatabase.MySql,
                () => new MySqlConnection(connectionString), options =>
                {
                    foreach (var property in settings.GetSection("AllowedPropertyNames").GetChildren())
                    {
                        if (!string.IsNullOrWhiteSpace(property.Value))
                            options.AllowedPropertyNames.Add(property.Value);
                    }
                    options.MaxFieldLength = settings.GetValue("MaxFieldLength", options.MaxFieldLength);
                    options.MaxPayloadBytes = settings.GetValue("MaxPayloadBytes", options.MaxPayloadBytes);
                    options.MaxDetailCount = settings.GetValue("MaxDetailCount", options.MaxDetailCount);
                });
        }

        /// <summary>
        /// 注册显式启用的审计日志查询服务。
        /// </summary>
        /// <param name="services">服务集合。</param>
        private static void ConfigureAuditLogQuery(IServiceCollection services)
        {
            var configuration = services.GetConfiguration();
            var settings = configuration.GetSection("Auditing:Query");
            if (!settings.GetValue<bool>("Enabled"))
                return;
            if (!configuration.GetValue<bool>("Auditing:DatabaseEnabled"))
                throw new InvalidOperationException(
                    "启用 Auditing:Query:Enabled 前必须启用 Auditing:DatabaseEnabled。");
            if (string.IsNullOrWhiteSpace(settings["Role"]))
                throw new InvalidOperationException(
                    "启用 Auditing:Query:Enabled 时必须配置 Auditing:Query:Role。");
            if (settings.GetValue<bool>("AllowCrossTenant") &&
                string.IsNullOrWhiteSpace(settings["CrossTenantRole"]))
                throw new InvalidOperationException(
                    "启用 Auditing:Query:AllowCrossTenant 时必须配置 Auditing:Query:CrossTenantRole。");
            services.AddBingAuditLogQuery<AdminAuditLogQueryAuthorizer>();
        }

        /// <summary>
        /// 仅允许显式配置的代理更新客户端地址。
        /// </summary>
        private static void ConfigureTrustedProxies(IServiceCollection services, IConfiguration settings)
        {
            var values = settings.GetSection("KnownProxies").GetChildren().Select(item => item.Value).ToArray();
            if (values.Length == 0)
                return;
            var proxies = new List<IPAddress>(values.Length);
            foreach (var value in values)
            {
                if (!IPAddress.TryParse(value, out var address))
                    throw new InvalidOperationException($"Admin 限流的可信代理地址无效：{value}");
                proxies.Add(address);
            }
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                options.ForwardLimit = 1;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
                foreach (var address in proxies)
                    options.KnownProxies.Add(address);
            });
        }
    }

    /// <summary>
    /// 授权控制器模型转换器
    /// </summary>
    internal class AuthorizeControllerModelConvention : IControllerModelConvention
    {
        /// <summary>
        /// 实现Apply
        /// </summary>
        public void Apply(ControllerModel controller) => controller.Filters.Add(new AuthorizeFilter("jwt"));
    }
}
