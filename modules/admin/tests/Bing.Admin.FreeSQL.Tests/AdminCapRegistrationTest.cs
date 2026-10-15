using Bing.Events.Cap;
using DotNetCore.CAP.Internal;
using Bing.Admin.Data.Inventory;
using Bing.Admin.Service.Events;
using Bing.Admin.Modules;
using Bing.Auditing.Persistence;
using Bing.Data.Inventory;
using Bing.Data.Events.Ordering;
using Bing.Events.Ordering;
using Bing.FreeSQL.Events;
using DotNetCore.CAP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace Bing.Admin.FreeSQL.Tests;

/// <summary>
/// 验证 Admin CAP 连接配置与诊断入口的注册边界。
/// </summary>
public sealed class AdminCapRegistrationTest
{

    /// <summary>
    /// 验证宿主显式启用消费取消重试且重复启用不改变执行器。
    /// </summary>
    [Fact]
    public void AddServices_ShouldEnableConsumerCancellationRetryOnce()
    {
        var services = CreateServices(ConfigurationValues());
        new CapModule().AddServices(services);
        var descriptor = services.Last(item => item.ServiceType == typeof(ISubscribeInvoker));
        Assert.True(typeof(BingSubscribeInvoker).IsAssignableFrom(descriptor.ImplementationType));
        services.EnableBingCapConsumerCancellationRetry();
        Assert.Same(descriptor, services.Last(item => item.ServiceType == typeof(ISubscribeInvoker)));
    }

    /// <summary>
    /// 未提供连接字段时在注册阶段失败且错误不输出秘密。
    /// </summary>
    [Theory]
    [InlineData("HostName")]
    [InlineData("UserName")]
    [InlineData("Password")]
    public void AddServices_WhenRabbitFieldIsMissing_ShouldRejectWithoutExposingValues(string field)
    {
        var values = ConfigurationValues();
        values.Remove("CAP:RabbitMQ:" + field);
        var services = CreateServices(values);

        var exception = Assert.Throws<InvalidOperationException>(() => new CapModule().AddServices(services));

        Assert.Contains("CAP:RabbitMQ:" + field, exception.Message);
        Assert.DoesNotContain("test-secret-value", exception.Message);
    }

    /// <summary>
    /// 消费者组为空白或不一致时在注册阶段失败且错误不输出秘密。
    /// </summary>
    /// <param name="consumerGroup">待验证的消费者组。</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bing-admin-v2")]
    public void AddServices_WhenConsumerGroupIsBlankOrMismatched_ShouldRejectWithoutExposingValues(string consumerGroup)
    {
        var values = ConfigurationValues();
        values["BING_HOST_ROLE"] = "consumer";
        values["BING_CAP_CONSUMER_ENABLED"] = "true";
        values["BING_CAP_CONSUMER_GROUP"] = consumerGroup;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new CapModule().AddServices(CreateServices(values)));

        Assert.Contains("BING_CAP_CONSUMER_GROUP", exception.Message);
        Assert.DoesNotContain("test-secret-value", exception.Message);
    }

    /// <summary>
    /// 非法端口在注册阶段失败。
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void AddServices_WhenRabbitPortIsInvalid_ShouldReject(string port)
    {
        var values = ConfigurationValues();
        values["CAP:RabbitMQ:Port"] = port;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new CapModule().AddServices(CreateServices(values)));

        Assert.Contains("CAP:RabbitMQ:Port", exception.Message);
        Assert.DoesNotContain("test-secret-value", exception.Message);
    }

    /// <summary>
    /// 使用部署配置且默认关闭 Dashboard。
    /// </summary>
    [Fact]
    public void AddServices_ShouldUseConfiguredRabbitAndDisableDashboardByDefault()
    {
        var services = CreateServices(ConfigurationValues());
        new CapModule().AddServices(services);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RabbitMQOptions>>().Value;

        Assert.Equal("test-broker", options.HostName);
        Assert.Equal("test-user", options.UserName);
        Assert.Equal("test-secret-value", options.Password);
        Assert.Equal(5672, options.Port);
        Assert.Equal("/", options.VirtualHost);
        Assert.DoesNotContain(services, IsDashboardConfiguration);
    }

    /// <summary>
    /// 显式配置端口及虚拟主机并启用 Dashboard 注册。
    /// </summary>
    [Fact]
    public void AddServices_WhenDashboardIsExplicitlyEnabled_ShouldRegisterConfiguredTransport()
    {
        var values = ConfigurationValues();
        values["CAP:RabbitMQ:Port"] = "35672";
        values["CAP:RabbitMQ:VirtualHost"] = "/test";
        values["CAP:Dashboard:Enabled"] = "true";
        values["CAP:Dashboard:Role"] = "ops";
        var services = CreateServices(values);
        new CapModule().AddServices(services);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RabbitMQOptions>>().Value;

        Assert.Equal(35672, options.Port);
        Assert.Equal("/test", options.VirtualHost);
        Assert.Contains(services, IsDashboardConfiguration);
    }

    /// <summary>
    /// CAP Dashboard 应要求已认证用户具备配置角色。
    /// </summary>
    [Fact]
    public async Task AddServices_WhenDashboardRoleIsConfigured_ShouldRequireMatchingRole()
    {
        var values = ConfigurationValues();
        values["CAP:Dashboard:Enabled"] = "true";
        values["CAP:Dashboard:Role"] = "ops";
        var services = CreateServices(values);

        new CapModule().AddServices(services);

        using var provider = services.BuildServiceProvider();
        var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync("Bing.Admin.CapDashboard");
        Assert.NotNull(policy);
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var allowed = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("role_code", "operator,ops")
            }, "jwt")), null, policy);
        var denied = await authorization.AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("role_code", "operator")
            }, "jwt")), null, policy);

        Assert.True(allowed.Succeeded);
        Assert.False(denied.Succeeded);
    }

    /// <summary>
    /// 显式启用 Dashboard 但缺少角色时保持默认拒绝。
    /// </summary>
    [Fact]
    public async Task AddServices_WhenDashboardRoleIsMissing_ShouldDenyAuthenticatedUser()
    {
        var values = ConfigurationValues();
        values["CAP:Dashboard:Enabled"] = "true";
        var services = CreateServices(values);

        new CapModule().AddServices(services);

        using var provider = services.BuildServiceProvider();
        var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync("Bing.Admin.CapDashboard");
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("role_code", "ops")
            }, "jwt")), null, policy);

        Assert.NotNull(policy);
        Assert.False(result.Succeeded);
    }

    /// <summary>
    /// 验证非 Web 角色不会注册 CAP Dashboard。
    /// </summary>
    /// <param name="hostRole">宿主角色。</param>
    [Theory]
    [InlineData("consumer")]
    [InlineData("task")]
    public void AddServices_WhenDashboardIsEnabledForNonWebRole_ShouldNotRegisterDashboard(string hostRole)
    {
        var values = ConfigurationValues();
        values["BING_HOST_ROLE"] = hostRole;
        values["CAP:Dashboard:Enabled"] = "true";
        values["CAP:Dashboard:Role"] = "ops";
        var services = CreateServices(values);

        new CapModule().AddServices(services);

        Assert.DoesNotContain(services, IsDashboardConfiguration);
    }

    /// <summary>
    /// 验证未配置库存开关时默认不注册库存业务。
    /// </summary>
    [Fact]
    public void AddServices_ShouldKeepInventoryDisabledByDefault()
    {
        var services = CreateServices(ConfigurationValues());
        new CapModule().AddServices(services);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(AdminInventoryService));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IInventoryOrderStore));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ITransactionalAuditingStore));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IOrderedEventStore));
    }

    /// <summary>
    /// 验证显式启用有序事件时注册数据库存储和 FreeSQL CAP 事务适配器。
    /// </summary>
    [Fact]
    public void AddServices_WhenOrderedEventsEnabled_ShouldRegisterFoundation()
    {
        var values = ConfigurationValues();
        values["OrderedEvents:Enabled"] = "true";
        var services = CreateServices(values);

        new CapModule().AddServices(services);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOrderedEventStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(CapOrderedEventIngress));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(FreeSqlOrderedEventTransactionFactory));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOrderedEventTransactionFactory));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(AdminInventoryOrderedEventHandler));
    }

    /// <summary>
    /// 验证有序事件消费者容量配置会传递到数据库存储。
    /// </summary>
    [Fact]
    public void AddServices_WhenOrderedEventCapacityIsConfigured_ShouldApplyCapacity()
    {
        var values = ConfigurationValues();
        values["OrderedEvents:Enabled"] = "true";
        values["OrderedEvents:MaxPendingPerConsumer"] = "3";
        var services = CreateServices(values);

        new CapModule().AddServices(services);

        var descriptor = Assert.Single(services,
            item => item.ServiceType == typeof(IOrderedEventStore));
        var store = Assert.IsType<DbOrderedEventStore>(descriptor.ImplementationInstance);
        var field = typeof(DbOrderedEventStore).GetField("_maxPendingPerConsumer",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.Equal(3, field.GetValue(store));
    }

    /// <summary>
    /// 验证有序事件消费者容量配置必须为正整数。
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void AddServices_WhenOrderedEventCapacityIsInvalid_ShouldReject(string value)
    {
        var values = ConfigurationValues();
        values["OrderedEvents:Enabled"] = "true";
        values["OrderedEvents:MaxPendingPerConsumer"] = value;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new CapModule().AddServices(CreateServices(values)));

        Assert.Contains("OrderedEvents:MaxPendingPerConsumer", exception.Message);
    }

    /// <summary>
    /// 验证库存有序消费者只在独立 CAP 消费宿主中注册。
    /// </summary>
    [Fact]
    public void AddServices_WhenConsumerRoleAndInventoryAreEnabled_ShouldRegisterInventoryConsumer()
    {
        var values = ConfigurationValues();
        values["BING_HOST_ROLE"] = "consumer";
        values["BING_CAP_CONSUMER_ENABLED"] = "true";
        values["BING_CAP_CONSUMER_GROUP"] = "bing-admin";
        values["OrderedEvents:Enabled"] = "true";
        values["Inventory:Enabled"] = "true";
        var services = CreateServices(values);

        new CapModule().AddServices(services);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICapSubscribe) &&
            descriptor.ImplementationType == typeof(AdminInventoryOrderedEventSubscriber));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(AdminInventoryOrderedEventHandler));
    }

    /// <summary>
    /// 验证库存开关按配置决定整单业务及事务审计注册。
    /// </summary>
    /// <param name="enabled">是否启用库存业务。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddServices_WhenInventoryEnabledSettingIsApplied_ShouldRegisterInventoryServicesAccordingly(bool enabled)
    {
        var values = ConfigurationValues();
        values["Inventory:Enabled"] = enabled.ToString();
        var services = CreateServices(values);
        new CapModule().AddServices(services);

        if (enabled)
        {
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(AdminInventoryService));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IInventoryOrderStore));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITransactionalAuditingStore));
        }
        else
        {
            Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(AdminInventoryService));
            Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IInventoryOrderStore));
            Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ITransactionalAuditingStore));
        }
    }

    /// <summary>
    /// 验证重复注册只保留一个库存业务服务并尊重自定义审计存储。
    /// </summary>
    [Fact]
    public void AddAdminInventory_WhenCalledTwice_ShouldKeepOneBusinessServiceAndCustomAuditingStore()
    {
        var services = new ServiceCollection();
        var auditing = new DbAuditingStore(AuditLogDatabase.MySql);
        services.AddSingleton<ITransactionalAuditingStore>(auditing);

        services.AddAdminInventory();
        services.AddAdminInventory();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(AdminInventoryService));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(AdminInventoryOrderExecutor));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IInventoryOrderStore));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ITransactionalAuditingStore));
        using var provider = services.BuildServiceProvider();
        Assert.Same(auditing, provider.GetRequiredService<ITransactionalAuditingStore>());
    }

    /// <summary>
    /// 识别 CAP Dashboard 的选项注册。
    /// </summary>
    /// <param name="descriptor">当前服务描述符。</param>
    /// <returns>注册 Dashboard 选项时返回 true，否则返回 false。</returns>
    private static bool IsDashboardConfiguration(ServiceDescriptor descriptor) =>
        descriptor.ServiceType.Name == "DashboardOptions" ||
        descriptor.ServiceType.IsGenericType && descriptor.ServiceType.GetGenericArguments()
            .Any(type => type.Name == "DashboardOptions");

    /// <summary>
    /// 创建不连接外部基础设施的注册测试配置。
    /// </summary>
    /// <returns>仅包含测试标记值的配置。</returns>
    private static Dictionary<string, string> ConfigurationValues() => new()
    {
        ["CAP:RabbitMQ:HostName"] = "test-broker",
        ["CAP:RabbitMQ:UserName"] = "test-user",
        ["CAP:RabbitMQ:Password"] = "test-secret-value",
        ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1;Database=registration_test;User ID=test-user;Password=test-secret-value"
    };

    /// <summary>
    /// 创建 CAP 注册测试的服务集合。
    /// </summary>
    /// <param name="values">本测试的配置值。</param>
    /// <returns>包含配置和日志服务的服务集合。</returns>
    private static ServiceCollection CreateServices(Dictionary<string, string> values)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        services.AddLogging();
        return services;
    }
}
