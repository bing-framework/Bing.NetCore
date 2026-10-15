using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Apis.Events;
using Bing.Admin.Service.Events;
using Bing.Events.Ordering;
using Bing.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Bing.Admin.Tests.Shared;

/// <summary>
/// 验证 Admin 有序事件指标诊断的授权、输出和禁用边界。
/// </summary>
public sealed class AdminOrderedEventMetricsTest
{
    /// <summary>
    /// 未注册指标提供程序或授权器时返回 404。
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenDiagnosticsAreDisabled_ShouldReturnNotFound()
    {
        var controller = new OrderedEventMetricsController
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.GetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// 指标授权失败时不应生成 Prometheus 内容。
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenUserIsNotInConfiguredRole_ShouldReject()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(item => item.IsAuthenticated).Returns(true);
        user.Setup(item => item.IsInRole("metrics-reader")).Returns(false);
        var authorizer = new AdminOrderedEventMetricsAuthorizer(
            CreateConfiguration(new Dictionary<string, string>
            {
                ["OrderedEvents:Metrics:Role"] = "metrics-reader"
            }), user.Object);
        var controller = new OrderedEventMetricsController(new StubMetricsProvider(), authorizer)
        {
            ControllerContext = CreateControllerContext()
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            controller.GetAsync(CancellationToken.None));
    }

    /// <summary>
    /// 授权通过时返回低基数 Prometheus 文本。
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenAuthorized_ShouldReturnPrometheusText()
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(item => item.IsAuthenticated).Returns(true);
        user.Setup(item => item.IsInRole("metrics-reader")).Returns(true);
        var authorizer = new AdminOrderedEventMetricsAuthorizer(
            CreateConfiguration(new Dictionary<string, string>
            {
                ["OrderedEvents:Metrics:Role"] = "metrics-reader"
            }), user.Object);
        var controller = new OrderedEventMetricsController(new StubMetricsProvider(), authorizer)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = Assert.IsType<ContentResult>(await controller.GetAsync(CancellationToken.None));

        Assert.StartsWith("text/plain; version=0.0.4", result.ContentType, StringComparison.Ordinal);
        Assert.Contains("bing_ordered_pending_streams", result.Content);
        Assert.DoesNotContain("payload", result.Content);
    }

    /// <summary>
    /// 指标角色未配置时拒绝创建授权器。
    /// </summary>
    [Fact]
    public void Authorizer_WhenRoleIsMissing_ShouldRejectConfiguration()
    {
        var user = new Mock<ICurrentUser>();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AdminOrderedEventMetricsAuthorizer(CreateConfiguration(null), user.Object));

        Assert.Contains("OrderedEvents:Metrics:Role", exception.Message);
    }

    /// <summary>
    /// 指标诊断在 Web 角色中启用时应在注册阶段失败。
    /// </summary>
    [Fact]
    public void CapModule_WhenMetricsAreEnabledOnWebRole_ShouldReject()
    {
        var values = CapConfigurationValues();
        values["BING_HOST_ROLE"] = "web";
        values["OrderedEvents:Enabled"] = "true";
        values["OrderedEvents:Metrics:Enabled"] = "true";
        values["OrderedEvents:Metrics:Role"] = "metrics-reader";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new Bing.Admin.Modules.CapModule().AddServices(CreateServices(values)));

        Assert.Contains("BING_HOST_ROLE=consumer", exception.Message);
    }

    /// <summary>
    /// 指标诊断在 Consumer 角色中启用时注册快照提供程序和授权器。
    /// </summary>
    [Fact]
    public void CapModule_WhenMetricsAreEnabledOnConsumerRole_ShouldRegisterAdapters()
    {
        var values = CapConfigurationValues();
        values["BING_HOST_ROLE"] = "consumer";
        values["BING_CAP_CONSUMER_ENABLED"] = "true";
        values["BING_CAP_CONSUMER_GROUP"] = "bing-admin";
        values["OrderedEvents:Enabled"] = "true";
        values["OrderedEvents:Metrics:Enabled"] = "true";
        values["OrderedEvents:Metrics:Role"] = "metrics-reader";

        var services = CreateServices(values);
        new Bing.Admin.Modules.CapModule().AddServices(services);

        Assert.Contains(services, item => item.ServiceType ==
            typeof(IOrderedEventMetricsSnapshotProvider));
        Assert.Contains(services, item => item.ServiceType ==
            typeof(AdminOrderedEventMetricsAuthorizer));
    }

    /// <summary>
    /// 创建控制器执行上下文。
    /// </summary>
    private static ControllerContext CreateControllerContext() => new()
    {
        HttpContext = new DefaultHttpContext()
    };

    /// <summary>
    /// 创建测试配置。
    /// </summary>
    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string> values)
    {
        var builder = new ConfigurationBuilder();
        if (values != null)
            builder.AddInMemoryCollection(values);
        return builder.Build();
    }

    /// <summary>
    /// 创建不连接外部基础设施的 CAP 注册配置。
    /// </summary>
    private static Dictionary<string, string> CapConfigurationValues() => new()
    {
        ["CAP:RabbitMQ:HostName"] = "test-broker",
        ["CAP:RabbitMQ:UserName"] = "test-user",
        ["CAP:RabbitMQ:Password"] = "test-secret-value",
        ["ConnectionStrings:DefaultConnection"] =
            "Server=127.0.0.1;Database=registration_test;User ID=test-user;Password=test-secret-value"
    };

    /// <summary>
    /// 创建 CAP 注册测试服务集合。
    /// </summary>
    private static ServiceCollection CreateServices(IReadOnlyDictionary<string, string> values)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateConfiguration(values));
        services.AddLogging();
        return services;
    }

    /// <summary>
    /// 提供不含事件正文的测试指标。
    /// </summary>
    private sealed class StubMetricsProvider : IOrderedEventMetricsSnapshotProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<BingOrderedEventMetricsSnapshot> GetSnapshots() =>
            new[]
            {
                new BingOrderedEventMetricsSnapshot("inventory", 2, 3, 4, 0, 0, 0, 0, 5)
            };
    }
}
