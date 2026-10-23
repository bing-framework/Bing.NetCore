using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bing.Uow.Tests;

/// <summary>
/// 验证业务工作单元 scope 的公开契约和 DI 生命周期。
/// </summary>
public class UnitOfWorkScopeApiContractTest
{
    /// <summary>
    /// 验证参与式嵌套跨 await 共用 root，并在退出时恢复 Current。
    /// </summary>
    [Fact]
    public async Task Begin_ShouldFlowAcrossAwaitAndRestoreOuter()
    {
        var services = new ServiceCollection();
        services.AddUnitOfWorkScopes();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();

        manager.Current.ShouldBeNull();
        var outer = manager.Begin();
        outer.IsActive.ShouldBeTrue();
        var participant = manager.Begin();
        participant.Id.ShouldBe(outer.Id);

        await Task.Yield();
        manager.Current.Id.ShouldBe(outer.Id);
        participant.Complete();
        participant.IsActive.ShouldBeFalse();
        participant.Dispose();
        manager.Current.Id.ShouldBe(outer.Id);

        await outer.CompleteAsync();
        outer.IsCompleted.ShouldBeTrue();
        outer.IsActive.ShouldBeFalse();
        await outer.DisposeAsync();
        manager.Current.ShouldBeNull();
    }

    /// <summary>
    /// 验证 requiresNew 使用独立 root，并在退出时恢复外层 root。
    /// </summary>
    [Fact]
    public async Task RequiresNew_ShouldUseIndependentRoot()
    {
        var services = new ServiceCollection();
        services.AddUnitOfWorkScopes();
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();

        var outer = manager.Begin();
        var inner = manager.Begin(requiresNew: true);
        inner.Id.ShouldNotBe(outer.Id);
        await inner.CompleteAsync();
        await inner.DisposeAsync();
        manager.Current.Id.ShouldBe(outer.Id);

        await outer.RollbackAsync();
        await outer.DisposeAsync();
        manager.Current.ShouldBeNull();
    }

    /// <summary>
    /// 验证两个独立 DI scope 不共享 Current。
    /// </summary>
    [Fact]
    public void IndependentDiScopes_ShouldNotShareCurrent()
    {
        var services = new ServiceCollection();
        services.AddUnitOfWorkScopes();
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        var second = secondScope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();

        var firstRoot = first.Begin();
        second.Current.ShouldBeNull();

        firstRoot.Rollback();
        firstRoot.Dispose();
    }
}
