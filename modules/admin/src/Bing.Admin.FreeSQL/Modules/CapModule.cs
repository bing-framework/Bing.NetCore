using System;
using System.ComponentModel;
using System.Globalization;
using Bing.Admin.Data;
using Bing.Admin.Data.Inventory;
using Bing.Admin.EventHandlers.Abstractions;
using Bing.Admin.EventHandlers.Abstractions.Systems;
using Bing.Admin.EventHandlers.Implements;
using Bing.Admin.EventHandlers.Implements.Systems;
using Bing.Admin.Service.Events;
using Bing.AspNetCore;
using Bing.AspNetCore.Hosting;
using Bing.Core.Modularity;
using Bing.Data.Events.Ordering;
using Bing.Events.Cap;
using Bing.Events.Ordering;
using Bing.FreeSQL;
using Bing.FreeSQL.Events;
using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// CAP模块
    /// </summary>
    [Description("CAP模块")]
    [DependsOnModule(typeof(AspNetCoreModule))]
    public class CapModule : AspNetCoreBingModule
    {
        /// <summary>
        /// CAP Dashboard 授权策略名称。
        /// </summary>
        private const string DashboardAuthorizationPolicy = "Bing.Admin.CapDashboard";

        /// <summary>
        /// 稳定的 CAP 消费组名称。
        /// </summary>
        private const string StableConsumerGroup = "bing-admin";

        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Application;

        /// <inheritdoc />
        public override int Order => 1;

        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            var configuration = services.GetConfiguration();
            var rabbit = ReadRabbitConfiguration(configuration);
            var role = configuration.GetBingHostRole();
            var consumerRole = role == BingHostRole.Consumer;
            var consumerEnabled = ReadCapConsumerEnabled(configuration, role);
            if (consumerRole && !consumerEnabled)
                throw new InvalidOperationException("BING_HOST_ROLE=consumer 必须启用 BING_CAP_CONSUMER_ENABLED。");
            if (consumerRole)
                ValidateConsumerGroup(configuration);
            if (configuration.GetValue<bool>("Inventory:Enabled"))
                services.AddAdminInventory();
            services.AddSingleton<IConsumerServiceSelector, CapConsumerServiceSelector>();
            LoadEvent(services);
            var config = services.GetConfiguration();
            var connection = config.GetConnectionString("DefaultConnection");
            // 添加事件总线服务
            services.AddCapEventBus(o =>
            {
                //o.UseEntityFramework<AdminUnitOfWork>();
                o.UseMySql(connection);
                if (configuration.GetValue<bool>("CAP:Dashboard:Enabled") && role == BingHostRole.Web)
                {
                    services.AddAuthorization(options => options.AddPolicy(DashboardAuthorizationPolicy,
                        policy => policy.RequireAuthenticatedUser()
                            .RequireAssertion(context => AdminRoleAuthorization.HasRole(
                                context.User, configuration["CAP:Dashboard:Role"]))));
                    o.UseDashboard(dashboard =>
                    {
                        dashboard.AuthorizationPolicy = DashboardAuthorizationPolicy;
                        dashboard.AllowAnonymousExplicit = false;
                    });
                }
                // 设置处理成功的数据在数据库中保存的时间（秒），为保证系统性能，数据会定期清理
                o.SucceedMessageExpiredAfter = 24 * 3600;
                // 设置失败重试次数
                o.FailedRetryCount = 5;
                o.Version = "admin";
                o.UseRabbitMQ(x =>
                {
                    x.HostName = rabbit.HostName;
                    x.Port = rabbit.Port;
                    x.UserName = rabbit.UserName;
                    x.Password = rabbit.Password;
                    x.VirtualHost = configuration["CAP:RabbitMQ:VirtualHost"] ?? "/";
                });
            });
            if (!consumerEnabled)
                services.EnableBingCapPublisherOnly();
            services.EnableBingCapConsumerCancellationRetry();
            var orderedEventsEnabled = configuration.GetValue<bool>("OrderedEvents:Enabled");
            if (orderedEventsEnabled)
                AddOrderedEventFoundation(services);
            ConfigureMetrics(services, configuration, role, orderedEventsEnabled);
            if (orderedEventsEnabled && consumerRole && consumerEnabled &&
                configuration.GetValue<bool>("Inventory:Enabled"))
            {
                services.AddTransient<ICapSubscribe, AdminInventoryOrderedEventSubscriber>();
                services.AddBingOrderedConsumer<AdminInventoryOrderedEventHandler>(
                    AdminInventoryService.OrderedConsumer);
            }
            if (configuration.GetValue<bool>("OrderedEvents:Recovery:Enabled"))
            {
                if (!orderedEventsEnabled)
                    throw new InvalidOperationException(
                        "启用 OrderedEvents:Recovery:Enabled 前必须启用 OrderedEvents:Enabled。");
                services.AddBingOrderedEventRecovery();
                services.AddBingOrderedEventRecoveryAuthorization<AdminOrderedEventRecoveryAuthorizer>();
                services.TryAddScoped<IOrderedEventRecoveryAudit, AdminOrderedEventRecoveryAudit>();
            }
            return services;
        }

        /// <summary>
        /// 注册 FreeSQL Admin 的有序事件基础设施。
        /// </summary>
        /// <param name="services">当前服务集合。</param>
        /// <remarks>事务工厂借用宿主拥有的 ORM，不会接管或释放根 ORM。</remarks>
        private static void AddOrderedEventFoundation(IServiceCollection services)
        {
            var maxPendingPerConsumer = ReadMaxPendingPerConsumer(services.GetConfiguration());
            services.TryAddSingleton<IOrderedEventStore>(
                new DbOrderedEventStore(OrderedEventDatabase.MySql,
                    maxPendingPerConsumer: maxPendingPerConsumer));
            services.AddBingCapOrderedEventIngress();
            services.TryAddScoped<OrderedEventPublisher>();
            services.TryAddScoped<FreeSqlOrderedEventTransactionFactory>(provider =>
                new FreeSqlOrderedEventTransactionFactory(
                    provider.GetRequiredService<FreeSqlWrapper>().Orm));
            services.AddBingCapOrderedEventTransaction<FreeSqlOrderedEventTransactionFactory>();
        }

        /// <summary>
        /// 读取有序事件消费者的可选全局积压上限。
        /// </summary>
        /// <param name="configuration">宿主配置。</param>
        /// <returns>已配置正整数时返回容量上限，否则返回 <see langword="null"/>。</returns>
        private static int? ReadMaxPendingPerConsumer(IConfiguration configuration)
        {
            var value = configuration["OrderedEvents:MaxPendingPerConsumer"];
            if (string.IsNullOrWhiteSpace(value))
                return null;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) || limit <= 0)
                throw new InvalidOperationException(
                    "OrderedEvents:MaxPendingPerConsumer 必须配置为正整数。");
            return limit;
        }

        /// <summary>
        /// 注册显式启用的有序事件指标诊断。
        /// </summary>
        /// <param name="services">当前服务集合。</param>
        /// <param name="configuration">宿主配置。</param>
        /// <param name="role">当前宿主角色。</param>
        /// <param name="orderedEventsEnabled">是否启用有序事件。</param>
        /// <remarks>指标只允许在 Consumer 角色开启，避免 Web 预热实例意外暴露或消费诊断数据。</remarks>
        private static void ConfigureMetrics(IServiceCollection services, IConfiguration configuration,
            BingHostRole role, bool orderedEventsEnabled)
        {
            if (!configuration.GetValue<bool>("OrderedEvents:Metrics:Enabled"))
                return;
            if (!orderedEventsEnabled)
                throw new InvalidOperationException(
                    "启用 OrderedEvents:Metrics:Enabled 前必须启用 OrderedEvents:Enabled。");
            if (role != BingHostRole.Consumer)
                throw new InvalidOperationException(
                    "有序事件指标诊断只能在 BING_HOST_ROLE=consumer 的宿主中启用。");

            services.AddBingOrderedEventMetricsDiagnostics();
            services.TryAddScoped<AdminOrderedEventMetricsAuthorizer>();
        }

        /// <summary>
        /// 读取 CAP 消费者角色开关。
        /// </summary>
        /// <param name="configuration">宿主配置。</param>
        /// <returns>启用 CAP 消费处理时返回 <see langword="true"/>。</returns>
        /// <param name="role">当前宿主角色。</param>
        /// <remarks>未配置时 Web/Consumer 保持完整 CAP 行为；Task 角色默认关闭消费。</remarks>
        private static bool ReadCapConsumerEnabled(IConfiguration configuration, BingHostRole role)
        {
            var value = configuration["BING_CAP_CONSUMER_ENABLED"];
            if (string.IsNullOrWhiteSpace(value))
                return role != BingHostRole.Task;
            if (!bool.TryParse(value, out var enabled))
                throw new InvalidOperationException("BING_CAP_CONSUMER_ENABLED 必须为 true 或 false。");
            return enabled;
        }

        /// <summary>
        /// 校验消费者使用稳定的逻辑组。
        /// </summary>
        /// <param name="configuration">宿主配置。</param>
        /// <remarks>未配置时使用内置组名；配置了其他值时直接失败，避免静默形成新的消费组。</remarks>
        private static void ValidateConsumerGroup(IConfiguration configuration)
        {
            var configured = configuration["BING_CAP_CONSUMER_GROUP"];
            if (configured == null)
                return;
            if (!string.Equals(configured, StableConsumerGroup, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"BING_CAP_CONSUMER_GROUP 必须为稳定值 {StableConsumerGroup}。当前 Admin 订阅组由代码固定。");
        }

        /// <summary>
        /// 读取宿主的 RabbitMQ 连接配置。
        /// </summary>
        /// <param name="configuration">包含部署配置或环境变量的宿主配置。</param>
        /// <returns>已验证的主机、端口及连接凭据。</returns>
        /// <remarks>不提供默认凭据；错误只说明缺失的配置项，不输出配置值。</remarks>
        private static (string HostName, int Port, string UserName, string Password) ReadRabbitConfiguration(
            IConfiguration configuration)
        {
            var host = configuration["CAP:RabbitMQ:HostName"];
            var user = configuration["CAP:RabbitMQ:UserName"];
            var password = configuration["CAP:RabbitMQ:Password"];
            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("缺少 CAP:RabbitMQ:HostName 配置。");
            if (string.IsNullOrWhiteSpace(user))
                throw new InvalidOperationException("缺少 CAP:RabbitMQ:UserName 配置。");
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("缺少 CAP:RabbitMQ:Password 配置。");
            var portValue = configuration["CAP:RabbitMQ:Port"];
            var port = 5672;
            if (portValue != null && (!int.TryParse(portValue, NumberStyles.None,
                    CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535))
                throw new InvalidOperationException("CAP:RabbitMQ:Port 必须为 1-65535 的整数。");
            return (host, port, user, password);
        }

        /// <summary>
        /// 加载事件
        /// </summary>
        /// <param name="services">服务集合</param>
        protected void LoadEvent(IServiceCollection services)
        {
            services.AddTransient<IUserLoginLogMessageEventHandler, UserLoginLogMessageEventHandler>();
            services.AddTransient<ITestMessageEventHandler, TestMessageEventHandler>();
        }
    }
}
