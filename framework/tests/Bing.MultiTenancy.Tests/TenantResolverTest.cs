using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Bing.MultiTenancy;

/// <summary>
/// TenantResolver 租户解析器优先级测试
/// </summary>
public class TenantResolverTest
{
    /// <summary>
    /// 构建包含若干 ActionTenantResolveContributor 的 TenantResolver
    /// </summary>
    private static ITenantResolver BuildResolver(
        params Action<ITenantResolveContext>[] actions)
    {
        var services = new ServiceCollection();
        services.Configure<BingTenantResolveOptions>(opts =>
        {
            // 清空默认贡献者，仅使用测试贡献者
            opts.TenantResolvers.Clear();
            foreach (var action in actions)
                opts.TenantResolvers.Add(new ActionTenantResolveContributor(action));
        });
        services.AddSingleton<ITenantResolver, TenantResolver>();
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<ITenantResolver>();
    }

    // ==================== 基本解析 ====================

    /// <summary>
    /// 测试目的：第一个设置 TenantIdOrName 并标记 Handled 的贡献者应获胜，后续贡献者不执行。
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdOrNameAsync_FirstContributorWins()
    {
        // Arrange
        var secondCalled = false;
        var resolver = BuildResolver(
            ctx =>
            {
                ctx.TenantIdOrName = "first";
                ctx.Handled = true;
            },
            ctx => { secondCalled = true; }
        );

        // Act
        var result = await resolver.ResolveTenantIdOrNameAsync();

        // Assert
        result.TenantIdOrName.ShouldBe("first");
        secondCalled.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：第一个贡献者未设置 TenantIdOrName，第二个贡献者应被执行并设置值。
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdOrNameAsync_SecondContributorUsed_WhenFirstSkips()
    {
        // Arrange
        var resolver = BuildResolver(
            ctx => { /* 第一个什么都不做 */ },
            ctx =>
            {
                ctx.TenantIdOrName = "second";
                ctx.Handled = true;
            }
        );

        // Act
        var result = await resolver.ResolveTenantIdOrNameAsync();

        // Assert
        result.TenantIdOrName.ShouldBe("second");
    }

    /// <summary>
    /// 测试目的：所有贡献者均不设置值时，TenantIdOrName 应为 null（宿主租户）。
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdOrNameAsync_AllSkip_ReturnsNullTenantId()
    {
        // Arrange
        var resolver = BuildResolver(
            ctx => { /* 不做任何事 */ },
            ctx => { /* 不做任何事 */ }
        );

        // Act
        var result = await resolver.ResolveTenantIdOrNameAsync();

        // Assert
        result.TenantIdOrName.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：无贡献者时，TenantIdOrName 应为 null，AppliedResolvers 为空。
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdOrNameAsync_NoContributors_ReturnsNullTenantId()
    {
        // Arrange
        var resolver = BuildResolver(/* 无任何贡献者 */);

        // Act
        var result = await resolver.ResolveTenantIdOrNameAsync();

        // Assert
        result.TenantIdOrName.ShouldBeNull();
        result.AppliedResolvers.ShouldBeEmpty();
    }

    // ==================== AppliedResolvers 跟踪 ====================

    /// <summary>
    /// 测试目的：AppliedResolvers 应记录所有执行过（被调用到）的贡献者名称。
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdOrNameAsync_AppliedResolvers_TracksExecutedContributors()
    {
        // Arrange：两个贡献者，第二个才解析到租户
        var resolver = BuildResolver(
            ctx => { /* 第一个未处理 */ },
            ctx =>
            {
                ctx.TenantIdOrName = "tenant-x";
                ctx.Handled = true;
            }
        );

        // Act
        var result = await resolver.ResolveTenantIdOrNameAsync();

        // Assert：两个都被记录
        result.AppliedResolvers.Count.ShouldBe(2);
        result.TenantIdOrName.ShouldBe("tenant-x");
    }

    /// <summary>
    /// 测试目的：第一个贡献者已 Handled，后续贡献者不应出现在 AppliedResolvers 中。
    /// </summary>
    [Fact]
    public async Task ResolveTenantIdOrNameAsync_AppliedResolvers_StopsAtFirstHandled()
    {
        // Arrange
        var resolver = BuildResolver(
            ctx =>
            {
                ctx.TenantIdOrName = "tenant-first";
                ctx.Handled = true;
            },
            ctx => { /* 不应被执行 */ }
        );

        // Act
        var result = await resolver.ResolveTenantIdOrNameAsync();

        // Assert：只有第一个贡献者被记录
        result.AppliedResolvers.Count.ShouldBe(1);
    }
}
