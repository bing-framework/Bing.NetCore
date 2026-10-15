using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Apis.Auditing;
using Bing.Admin.Service.Events;
using Bing.Auditing.Persistence;
using Bing.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Bing.Admin.Tests.Shared;

/// <summary>
/// 验证 Admin 审计日志查询的授权、租户边界和禁用行为。
/// </summary>
public sealed class AdminAuditLogQueryTest
{
    /// <summary>
    /// 未注册查询服务时返回 404。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenQueryIsDisabled_ShouldReturnNotFound()
    {
        var controller = new AuditLogController
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.QueryAsync(new AuditLogQuery { TenantId = "tenant-a" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// 已注册查询服务时执行查询并返回业务结果。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenQueryIsEnabled_ShouldCallService()
    {
        var service = new StubQueryService();
        var controller = new AuditLogController(service)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.QueryAsync(new AuditLogQuery { TenantId = "tenant-a" },
            CancellationToken.None);

        Assert.True(service.Called);
        Assert.NotNull(result);
    }

    /// <summary>
    /// 缺少租户条件时返回 400，避免把无范围查询交给存储层。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenTenantIsMissing_ShouldReturnBadRequest()
    {
        var service = new StubQueryService();
        var controller = new AuditLogController(service)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.QueryAsync(new AuditLogQuery(), CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.False(service.Called);
    }

    /// <summary>
    /// 空白租户条件返回 400，避免与非租户审计范围混淆。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenTenantIsWhitespace_ShouldReturnBadRequest()
    {
        var service = new StubQueryService();
        var controller = new AuditLogController(service)
        {
            ControllerContext = CreateControllerContext()
        };

        var result = await controller.QueryAsync(new AuditLogQuery { TenantId = "  " },
            CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.False(service.Called);
    }

    /// <summary>
    /// 查询角色未配置时拒绝创建授权器。
    /// </summary>
    [Fact]
    public void Authorizer_WhenRoleIsMissing_ShouldRejectConfiguration()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AdminAuditLogQueryAuthorizer(CreateConfiguration(null), CreateUser().Object));

        Assert.Contains("Auditing:Query:Role", exception.Message);
    }

    /// <summary>
    /// 非认证用户或角色不匹配时拒绝查询。
    /// </summary>
    [Fact]
    public async Task Authorizer_WhenUserIsNotAuthorized_ShouldReject()
    {
        var user = CreateUser(isAuthenticated: true, tenantId: "tenant-a");
        user.Setup(item => item.IsInRole("audit-reader")).Returns(false);
        var authorizer = CreateAuthorizer(user.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authorizer.AuthorizeAsync(new AuditLogQuery { TenantId = "tenant-a" }));
    }

    /// <summary>
    /// 默认只允许查询当前用户所属租户。
    /// </summary>
    [Fact]
    public async Task Authorizer_WhenTenantDiffers_ShouldReject()
    {
        var user = CreateUser(isAuthenticated: true, tenantId: "tenant-a");
        user.Setup(item => item.IsInRole("audit-reader")).Returns(true);
        var authorizer = CreateAuthorizer(user.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authorizer.AuthorizeAsync(new AuditLogQuery { TenantId = "tenant-b" }));
    }

    /// <summary>
    /// 当前用户租户匹配时允许查询。
    /// </summary>
    [Fact]
    public async Task Authorizer_WhenTenantMatches_ShouldAllow()
    {
        var user = CreateUser(isAuthenticated: true, tenantId: "tenant-a");
        user.Setup(item => item.IsInRole("audit-reader")).Returns(true);
        var authorizer = CreateAuthorizer(user.Object);

        await authorizer.AuthorizeAsync(new AuditLogQuery { TenantId = "tenant-a" });
    }

    /// <summary>
    /// 显式允许跨租户并具备独立角色时允许查询其他租户。
    /// </summary>
    [Fact]
    public async Task Authorizer_WhenCrossTenantIsEnabled_ShouldAllowOtherTenant()
    {
        var user = CreateUser(isAuthenticated: true, tenantId: "tenant-a");
        user.Setup(item => item.IsInRole("audit-reader")).Returns(true);
        user.Setup(item => item.IsInRole("tenant-audit-reader")).Returns(true);
        var authorizer = new AdminAuditLogQueryAuthorizer(
            CreateConfiguration(new Dictionary<string, string>
            {
                ["Auditing:Query:Role"] = "audit-reader",
                ["Auditing:Query:AllowCrossTenant"] = "true",
                ["Auditing:Query:CrossTenantRole"] = "tenant-audit-reader"
            }), user.Object);

        await authorizer.AuthorizeAsync(new AuditLogQuery { TenantId = "tenant-b" });
    }

    /// <summary>
    /// 跨租户配置缺少独立角色时拒绝启动配置。
    /// </summary>
    [Fact]
    public void Authorizer_WhenCrossTenantRoleIsMissing_ShouldRejectConfiguration()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AdminAuditLogQueryAuthorizer(CreateConfiguration(new Dictionary<string, string>
            {
                ["Auditing:Query:Role"] = "audit-reader",
                ["Auditing:Query:AllowCrossTenant"] = "true"
            }), CreateUser().Object));

        Assert.Contains("CrossTenantRole", exception.Message);
    }

    /// <summary>
    /// 空租户查询始终被拒绝，避免误用为全局查询。
    /// </summary>
    [Fact]
    public async Task Authorizer_WhenTenantIsEmpty_ShouldRejectGlobalQuery()
    {
        var user = CreateUser(isAuthenticated: true, tenantId: "tenant-a");
        user.Setup(item => item.IsInRole("audit-reader")).Returns(true);
        var authorizer = new AdminAuditLogQueryAuthorizer(CreateConfiguration(new Dictionary<string, string>
        {
            ["Auditing:Query:Role"] = "audit-reader"
        }), user.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authorizer.AuthorizeAsync(new AuditLogQuery { TenantId = string.Empty }));
    }

    /// <summary>
    /// 审计数据库未启用时拒绝注册查询入口。
    /// </summary>
    [Fact]
    public void AppModule_WhenQueryIsEnabledWithoutDatabase_ShouldReject()
    {
        var services = CreateServices(new Dictionary<string, string>
        {
            ["Auditing:Query:Enabled"] = "true",
            ["Auditing:Query:Role"] = "audit-reader"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new Bing.Admin.Modules.AppModule().AddServices(services));

        Assert.Contains("Auditing:DatabaseEnabled", exception.Message);
    }

    /// <summary>
    /// 跨租户查询缺少独立角色时在模块注册阶段拒绝配置。
    /// </summary>
    [Fact]
    public void AppModule_WhenCrossTenantRoleIsMissing_ShouldReject()
    {
        var services = CreateServices(new Dictionary<string, string>
        {
            ["Auditing:DatabaseEnabled"] = "true",
            ["Auditing:Query:Enabled"] = "true",
            ["Auditing:Query:Role"] = "audit-reader",
            ["Auditing:Query:AllowCrossTenant"] = "true",
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=registration_test"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new Bing.Admin.Modules.AppModule().AddServices(services));

        Assert.Contains("CrossTenantRole", exception.Message);
    }

    /// <summary>
    /// 查询入口未启用时不注册查询服务。
    /// </summary>
    [Fact]
    public void AppModule_WhenQueryIsDisabled_ShouldNotRegisterQueryService()
    {
        var services = CreateServices(null);

        new Bing.Admin.Modules.AppModule().AddServices(services);

        Assert.DoesNotContain(services, item => item.ServiceType == typeof(IAuditLogQueryService));
    }

    /// <summary>
    /// 创建使用指定当前用户的审计查询授权器。
    /// </summary>
    /// <param name="user">当前认证用户。</param>
    /// <returns>配置完成的授权器。</returns>
    private static AdminAuditLogQueryAuthorizer CreateAuthorizer(ICurrentUser user) =>
        new AdminAuditLogQueryAuthorizer(CreateConfiguration(new Dictionary<string, string>
        {
            ["Auditing:Query:Role"] = "audit-reader"
        }), user);

    /// <summary>
    /// 创建可配置认证状态和租户的当前用户模拟对象。
    /// </summary>
    /// <param name="isAuthenticated">用户是否已认证。</param>
    /// <param name="tenantId">当前租户标识。</param>
    /// <returns>当前用户模拟对象。</returns>
    private static Mock<ICurrentUser> CreateUser(bool isAuthenticated = false, string tenantId = null)
    {
        var user = new Mock<ICurrentUser>();
        user.SetupGet(item => item.IsAuthenticated).Returns(isAuthenticated);
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        return user;
    }

    /// <summary>
    /// 创建包含指定键值的内存配置。
    /// </summary>
    /// <param name="values">配置项集合。</param>
    /// <returns>构建完成的配置。</returns>
    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string> values)
    {
        var builder = new ConfigurationBuilder();
        if (values != null)
            builder.AddInMemoryCollection(values);
        return builder.Build();
    }

    /// <summary>
    /// 创建带有配置实例的模块注册服务集合。
    /// </summary>
    private static ServiceCollection CreateServices(IReadOnlyDictionary<string, string> values)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateConfiguration(values));
        services.AddLogging();
        return services;
    }

    /// <summary>
    /// 创建包含请求服务的测试控制器上下文。
    /// </summary>
    /// <returns>测试控制器上下文。</returns>
    private static ControllerContext CreateControllerContext() => new()
    {
        HttpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        }
    };

    /// <summary>
    /// 为审计日志查询测试提供可配置结果的服务替身。
    /// </summary>
    private sealed class StubQueryService : IAuditLogQueryService
    {
        /// <summary>
        /// 获取查询方法是否已被调用。
        /// </summary>
        public bool Called { get; private set; }

        /// <inheritdoc />
        public Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogQuery query,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult<IReadOnlyList<AuditLogEntry>>(Array.Empty<AuditLogEntry>());
        }
    }
}
