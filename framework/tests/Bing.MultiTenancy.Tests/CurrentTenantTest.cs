using System;
using Bing.MultiTenancy.ConfigurationStore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bing.MultiTenancy;

/// <summary>
/// 当前租户测试
/// </summary>
public class CurrentTenantTest
{
    /// <summary>
    /// 当前租户
    /// </summary>
    private readonly ICurrentTenant _currentTenant;

    /// <summary>
    /// 租户A
    /// </summary>
    private readonly Guid _tenantAId = Guid.NewGuid();

    /// <summary>
    /// 租户B
    /// </summary>
    private readonly Guid _tenantBId = Guid.NewGuid();

    /// <summary>
    /// 测试初始化
    /// </summary>
    public CurrentTenantTest()
    {
        var services = new ServiceCollection();
        services.AddBing();
        services.AddSingleton<ICurrentTenantAccessor>(AsyncLocalCurrentTenantAccessor.Instance);
        services.Configure<BingDefaultTenantStoreOptions>(options =>
        {
            options.Tenants =
            [
                new TenantConfiguration(_tenantAId.ToString(), "A"),
                new TenantConfiguration(_tenantBId.ToString(), "B")
            ];
        });
        services.Configure<BingTenantResolveOptions>(options =>
        {
            options.TenantResolvers.Insert(0, new CurrentUserTenantResolveContributor());
        });
        var provider = services.BuildServiceProvider();
        _currentTenant = provider.GetService<ICurrentTenant>();
    }

    /// <summary>
    /// 测试 - 当前租户标识为空
    /// </summary>
    [Fact]
    public void Test_CurrentTenant_Should_Be_Null_As_Default()
    {
        _currentTenant.Id.ShouldBeNull();
    }

    /// <summary>
    /// 测试 - 当前租户标识没有设置时，为空
    /// </summary>
    [Fact]
    public void Test_Get_Null_If_Not_Set()
    {
        _currentTenant.Id.ShouldBeNull();
    }

    /// <summary>
    /// 测试 - 当前租户标识设置后，获取到当前租户标识
    /// </summary>
    [Fact]
    public void Test_Should_Get_Changed_Tenant_If()
    {
        _currentTenant.Id.ShouldBe(null);

        using (_currentTenant.Change(_tenantAId.ToString()))
        {
            _currentTenant.Id.ShouldBe(_tenantAId.ToString());

            using (_currentTenant.Change(_tenantBId.ToString())) 
                _currentTenant.Id.ShouldBe(_tenantBId.ToString());

            _currentTenant.Id.ShouldBe(_tenantAId.ToString());
        }

        _currentTenant.Id.ShouldBe(null);
    }
}
