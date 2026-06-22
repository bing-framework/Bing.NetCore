using Bing.MultiTenancy.ConfigurationStore;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Bing.MultiTenancy;

/// <summary>
/// 测试目的：验证 <see cref="TenantResolveContext.HasResolvedTenantOrHost"/> 的四种边界逻辑。
/// </summary>
public class TenantResolveContextTest
{
    private static TenantResolveContext CreateContext() =>
        new(Mock.Of<IServiceProvider>());

    // ── HasResolvedTenantOrHost ─────────────────────────────────────────────

    /// <summary>
    /// 测试目的：Handled=false 且 TenantIdOrName=null 时，HasResolvedTenantOrHost 应返回 false。
    /// </summary>
    [Fact]
    public void HasResolvedTenantOrHost_WhenBothFalseAndNull_ShouldReturnFalse()
    {
        // Arrange
        var context = CreateContext();

        // Act & Assert
        context.Handled.ShouldBeFalse();
        context.TenantIdOrName.ShouldBeNull();
        context.HasResolvedTenantOrHost().ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：Handled=true 时，HasResolvedTenantOrHost 应返回 true（即使 TenantIdOrName 为 null）。
    /// </summary>
    [Fact]
    public void HasResolvedTenantOrHost_WhenHandledTrue_ShouldReturnTrue()
    {
        // Arrange
        var context = CreateContext();
        context.Handled = true;

        // Act & Assert
        context.HasResolvedTenantOrHost().ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：TenantIdOrName 有值时，HasResolvedTenantOrHost 应返回 true（即使 Handled=false）。
    /// </summary>
    [Fact]
    public void HasResolvedTenantOrHost_WhenTenantIdOrNameSet_ShouldReturnTrue()
    {
        // Arrange
        var context = CreateContext();
        context.TenantIdOrName = "tenant-a";

        // Act & Assert
        context.HasResolvedTenantOrHost().ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：Handled=true 且 TenantIdOrName 有值时，HasResolvedTenantOrHost 应返回 true。
    /// </summary>
    [Fact]
    public void HasResolvedTenantOrHost_WhenBothSet_ShouldReturnTrue()
    {
        // Arrange
        var context = CreateContext();
        context.Handled = true;
        context.TenantIdOrName = "tenant-b";

        // Act & Assert
        context.HasResolvedTenantOrHost().ShouldBeTrue();
    }

    // ── ServiceProvider 属性 ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：构造时传入的 ServiceProvider 应能通过属性读取（引用相同）。
    /// </summary>
    [Fact]
    public void ServiceProvider_ShouldBeTheOnePassedInCtor()
    {
        // Arrange
        var sp = Mock.Of<IServiceProvider>();

        // Act
        var context = new TenantResolveContext(sp);

        // Assert
        context.ServiceProvider.ShouldBeSameAs(sp);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 测试目的：验证 <see cref="NullTenantResolveResultAccessor"/> 的 Null Object 行为。
/// </summary>
public class NullTenantResolveResultAccessorTest
{
    /// <summary>
    /// 测试目的：Result getter 应始终返回 null（Null Object 模式，永远不持有状态）。
    /// </summary>
    [Fact]
    public void Result_Getter_ShouldAlwaysReturnNull()
    {
        // Arrange
        var accessor = new NullTenantResolveResultAccessor();

        // Assert
        accessor.Result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：Result setter 赋值后，getter 应仍返回 null（setter 是 no-op）。
    /// </summary>
    [Fact]
    public void Result_Setter_ShouldBeNoOp_GettterStillNull()
    {
        // Arrange
        var accessor = new NullTenantResolveResultAccessor();
        var result = new TenantResolveResult { TenantIdOrName = "tenant-x" };

        // Act
        accessor.Result = result;

        // Assert
        accessor.Result.ShouldBeNull();
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 测试目的：验证 <see cref="DefaultTenantStore"/> 在不同查询场景下的行为，
/// 使用 IOptionsMonitor&lt;BingDefaultTenantStoreOptions&gt; Mock 隔离真实配置系统。
/// </summary>
public class DefaultTenantStoreTest
{
    private static DefaultTenantStore CreateStore(params TenantConfiguration[] tenants)
    {
        var options = new BingDefaultTenantStoreOptions { Tenants = tenants };
        var mockMonitor = new Mock<IOptionsMonitor<BingDefaultTenantStoreOptions>>();
        mockMonitor.Setup(m => m.CurrentValue).Returns(options);
        return new DefaultTenantStore(mockMonitor.Object);
    }

    // ── FindByNameAsync ────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：FindByNameAsync 按 NormalizedName 匹配，存在时返回对应租户配置。
    /// </summary>
    [Fact]
    public async Task FindByNameAsync_WhenNameExists_ShouldReturnTenant()
    {
        // Arrange
        var tenant = new TenantConfiguration("t1", "tenant-a") { NormalizedName = "TENANT-A" };
        var store = CreateStore(tenant);

        // Act
        var result = await store.FindByNameAsync("TENANT-A");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("t1");
    }

    /// <summary>
    /// 测试目的：FindByNameAsync 查询不存在的名称时，应返回 null。
    /// </summary>
    [Fact]
    public async Task FindByNameAsync_WhenNameNotExists_ShouldReturnNull()
    {
        // Arrange
        var store = CreateStore(new TenantConfiguration("t1", "tenant-a") { NormalizedName = "TENANT-A" });

        // Act
        var result = await store.FindByNameAsync("NONEXISTENT");

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：Tenants 为空数组时 FindByNameAsync 应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public async Task FindByNameAsync_WhenNoTenants_ShouldReturnNull()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var result = await store.FindByNameAsync("any");

        // Assert
        result.ShouldBeNull();
    }

    // ── FindByIdAsync ──────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：FindByIdAsync 按 Id 匹配，存在时返回对应租户配置。
    /// </summary>
    [Fact]
    public async Task FindByIdAsync_WhenIdExists_ShouldReturnTenant()
    {
        // Arrange
        var tenant = new TenantConfiguration("id-001", "tenant-b");
        var store = CreateStore(tenant);

        // Act
        var result = await store.FindByIdAsync("id-001");

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("tenant-b");
    }

    /// <summary>
    /// 测试目的：FindByIdAsync 查询不存在的 Id 时，应返回 null。
    /// </summary>
    [Fact]
    public async Task FindByIdAsync_WhenIdNotExists_ShouldReturnNull()
    {
        // Arrange
        var store = CreateStore(new TenantConfiguration("id-001", "tenant-b"));

        // Act
        var result = await store.FindByIdAsync("no-such-id");

        // Assert
        result.ShouldBeNull();
    }

    // ── GetListAsync ───────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：GetListAsync 应返回所有配置的租户列表，数量与配置一致。
    /// </summary>
    [Fact]
    public async Task GetListAsync_ShouldReturnAllTenants()
    {
        // Arrange
        var t1 = new TenantConfiguration("t1", "a");
        var t2 = new TenantConfiguration("t2", "b");
        var store = CreateStore(t1, t2);

        // Act
        var list = await store.GetListAsync();

        // Assert
        list.ShouldNotBeNull();
        list.Count.ShouldBe(2);
    }

    /// <summary>
    /// 测试目的：Tenants 为空数组时 GetListAsync 应返回空列表，不抛异常。
    /// </summary>
    [Fact]
    public async Task GetListAsync_WhenEmpty_ShouldReturnEmptyList()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var list = await store.GetListAsync();

        // Assert
        list.ShouldNotBeNull();
        list.Count.ShouldBe(0);
    }
}
