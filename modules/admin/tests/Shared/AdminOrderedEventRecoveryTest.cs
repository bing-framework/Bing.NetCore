using Bing.Admin.Service.Events;
using Bing.Auditing;
using Bing.Core.Modularity;
using Bing.Data.Events.Ordering;
using Bing.Events.Ordering;
using Bing.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Bing.Admin.Tests.Shared;

/// <summary>
/// 验证 Admin 有序事件人工恢复的显式注册和安全边界。
/// </summary>
public sealed class AdminOrderedEventRecoveryTest
{
    /// <summary>
    /// 启用恢复时注册授权器、恢复服务和审计适配器。
    /// </summary>
    [Fact]
    public void AddServices_WhenRecoveryIsEnabled_ShouldRegisterExplicitAdapters()
    {
        var services = CreateServices(new Dictionary<string, string>
        {
            ["OrderedEvents:Enabled"] = "true",
            ["OrderedEvents:Recovery:Enabled"] = "true",
            ["OrderedEvents:Recovery:Role"] = "ops",
            ["OrderedEvents:Recovery:Consumers:0"] = "inventory"
        });

        new Bing.Admin.Modules.CapModule().AddServices(services);

        Assert.Contains(services, item => item.ServiceType == typeof(IOrderedEventRecoveryService));
        Assert.Contains(services, item => item.ServiceType == typeof(IOrderedEventRecoveryAuthorizer) &&
            item.ImplementationType == typeof(AdminOrderedEventRecoveryAuthorizer));
        Assert.Contains(services, item => item.ServiceType == typeof(IOrderedEventRecoveryAudit) &&
            item.ImplementationType == typeof(AdminOrderedEventRecoveryAudit));
    }

    /// <summary>
    /// 恢复授权必须同时满足认证、角色、消费者和租户条件。
    /// </summary>
    [Fact]
    public async Task Authorizer_ShouldRejectUnauthorizedConsumerAndTenant()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string>
        {
            ["OrderedEvents:Recovery:Role"] = "ops",
            ["OrderedEvents:Recovery:Consumers:0"] = "inventory"
        });
        var currentUser = CreateUser(true, "tenant-a", "ops");
        var authorizer = new AdminOrderedEventRecoveryAuthorizer(configuration, currentUser.Object);

        await authorizer.AuthorizeAsync("inventory", new OrderedEventStream("tenant-a", "stock", "sku-1"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authorizer.AuthorizeAsync("billing", new OrderedEventStream("tenant-a", "stock", "sku-1")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authorizer.AuthorizeAsync("inventory", new OrderedEventStream("tenant-b", "stock", "sku-1")));
    }

    /// <summary>
    /// 恢复授权应按有序消费者的精确大小写匹配白名单。
    /// </summary>
    [Fact]
    public async Task Authorizer_ShouldRejectConsumerWithDifferentCasing()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string>
        {
            ["OrderedEvents:Recovery:Role"] = "ops",
            ["OrderedEvents:Recovery:Consumers:0"] = "inventory"
        });
        var currentUser = CreateUser(true, "tenant-a", "ops");
        var authorizer = new AdminOrderedEventRecoveryAuthorizer(configuration, currentUser.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authorizer.AuthorizeAsync("Inventory", new OrderedEventStream("tenant-a", "stock", "sku-1")));
    }

    /// <summary>
    /// 恢复消费者配置包含边界空白时应在启动配置阶段失败。
    /// </summary>
    [Fact]
    public void Authorizer_WhenConsumerConfigurationHasBoundaryWhitespace_ShouldReject()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string>
        {
            ["OrderedEvents:Recovery:Role"] = "ops",
            ["OrderedEvents:Recovery:Consumers:0"] = "inventory "
        });
        var currentUser = CreateUser(true, "tenant-a", "ops");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AdminOrderedEventRecoveryAuthorizer(configuration, currentUser.Object));

        Assert.Contains("首尾空白", exception.Message);
    }

    /// <summary>
    /// 恢复审计只保存安全字段，不保存事件正文。
    /// </summary>
    [Fact]
    public async Task Audit_ShouldPersistSafeRecoveryProjection()
    {
        var store = new RecordingAuditStore();
        var manager = new AuditingManager(store);
        var user = CreateUser(true, "tenant-a", "ops");
        var audit = new AdminOrderedEventRecoveryAudit(manager, user.Object,
            CreateConfiguration(new Dictionary<string, string>
            {
                ["ApplicationName"] = "Admin.Tests"
            }));

        await audit.RecordAsync(new OrderedEventRecoveryAuditEntry("Retry", "inventory",
            new OrderedEventStream("tenant-a", "stock", "sku-1"), true));

        var record = Assert.Single(store.Entries);
        Assert.Equal("Admin.Tests", record.ApplicationName);
        Assert.Contains(record.Comments, item => item == "Consumer=inventory");
        Assert.DoesNotContain(record.Comments, item => item.Contains("payload", StringComparison.OrdinalIgnoreCase));
        Assert.Null(manager.Current);
    }

    /// <summary>
    /// 创建测试配置。
    /// </summary>
    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    /// <summary>
    /// 创建不连接外部基础设施的服务集合。
    /// </summary>
    private static ServiceCollection CreateServices(IReadOnlyDictionary<string, string> values)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateConfiguration(values.Concat(new Dictionary<string, string>
        {
            ["CAP:RabbitMQ:HostName"] = "test-broker",
            ["CAP:RabbitMQ:UserName"] = "test-user",
            ["CAP:RabbitMQ:Password"] = "test-secret-value",
            ["ConnectionStrings:DefaultConnection"] = "Server=127.0.0.1;Database=registration_test;User ID=test-user;Password=test-secret-value"
        }).ToDictionary(item => item.Key, item => item.Value)));
        services.AddLogging();
        return services;
    }

    /// <summary>
    /// 创建当前用户替身。
    /// </summary>
    private static Mock<ICurrentUser> CreateUser(bool authenticated, string tenant, string role)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(item => item.IsAuthenticated).Returns(authenticated);
        user.SetupGet(item => item.UserId).Returns("user-1");
        user.SetupGet(item => item.UserName).Returns("operator");
        user.SetupGet(item => item.TenantId).Returns(tenant);
        user.Setup(item => item.IsInRole(role)).Returns(true);
        return user;
    }

    /// <summary>
    /// 捕获测试期间保存的审计日志。
    /// </summary>
    private sealed class RecordingAuditStore : IAuditingStore
    {
        /// <summary>
        /// 获取测试期间记录的审计日志。
        /// </summary>
        internal List<AuditLogInfo> Entries { get; } = new();

        /// <inheritdoc />
        public Task SaveAsync(AuditLogInfo auditInfo)
        {
            Entries.Add(auditInfo);
            return Task.CompletedTask;
        }
    }
}
