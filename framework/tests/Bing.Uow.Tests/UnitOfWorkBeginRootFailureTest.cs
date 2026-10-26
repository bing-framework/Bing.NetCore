using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bing.Uow.Tests;

/// <summary>
/// 禁用并行的启动失败资源测试集合。
/// </summary>
/// <remarks>计时器断言只比较本次操作前后的增量，不使用进程绝对计数作为通过条件。</remarks>
[CollectionDefinition("Uow begin root resources", DisableParallelization = true)]
public sealed class UnitOfWorkBeginRootResourceCollection { }

/// <summary>
/// 验证工作单元 root 启动失败时取消资源的所有权交接与清理。
/// </summary>
[Collection("Uow begin root resources")]
public sealed class UnitOfWorkBeginRootFailureTest
{
    /// <summary>
    /// 验证处理器解析失败会移除调用方取消注册并保留主异常。
    /// </summary>
    [Fact]
    public async Task BeginOperation_WhenSaveHandlerConstructionFails_ShouldReleaseCallerRegistration()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        services.AddScoped<IUnitOfWorkSaveChangesHandler>(_ =>
            throw new InvalidOperationException("save-handler-construction-failure"));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        using var caller = new CancellationTokenSource();
        CallbackStates(caller).ShouldBeEmpty();

        var timerBefore = Timer.ActiveCount;
        var error = Should.Throw<InvalidOperationException>(() => manager.BeginOperation(
            new UnitOfWorkScopeOptions { Timeout = TimeSpan.FromHours(1) }, caller.Token));
        var timerAfter = Timer.ActiveCount;

        error.Message.ShouldBe("save-handler-construction-failure");
        manager.Current.ShouldBeNull();
        CallbackStates(caller).ShouldBeEmpty();
        timerAfter.ShouldBe(timerBefore);
    }

    /// <summary>
    /// 验证调用方不可取消时启动失败仍释放内部计时器。
    /// </summary>
    [Fact]
    public async Task BeginOperation_WhenCallerCannotCancel_ShouldReleaseDeadlineTimer()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        services.AddScoped<IUnitOfWorkSaveChangesHandler>(_ =>
            throw new InvalidOperationException("save-handler-construction-failure"));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();

        var timerBefore = Timer.ActiveCount;
        var error = Should.Throw<InvalidOperationException>(() => manager.BeginOperation(
            new UnitOfWorkScopeOptions { Timeout = TimeSpan.FromHours(1) }, CancellationToken.None));
        var timerAfter = Timer.ActiveCount;

        error.Message.ShouldBe("save-handler-construction-failure");
        manager.Current.ShouldBeNull();
        timerAfter.ShouldBe(timerBefore);
    }

    /// <summary>
    /// 验证嵌套新建 root 的启动观察器失败不会改变外层当前范围。
    /// </summary>
    [Fact]
    public async Task Begin_WhenRootStartingObserverFails_ShouldPreserveOuterCurrent()
    {
        var observer = new FailingRootStartingObserver();
        var services = new ServiceCollection();
        services.AddBingOperations();
        services.AddSingleton<IUnitOfWorkSaveChangesHandler>(new NoOpSaveChangesHandler());
        services.AddSingleton<IUnitOfWorkRootStartingObserver>(observer);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        var outer = manager.Begin();
        try
        {
            var error = Should.Throw<InvalidOperationException>(() => manager.Begin(requiresNew: true));

            error.Message.ShouldBe("observer-root-start-failure");
            manager.Current.ShouldBeSameAs(outer);
            observer.FailedRoots.Count.ShouldBe(1);
        }
        finally
        {
            await outer.DisposeAsync();
        }
    }

    /// <summary>
    /// 验证正常建立并在释放后清理取消资源，调用方注册回到基线。
    /// </summary>
    [Fact]
    public async Task BeginOperation_OnNormalDispose_ShouldReleaseSourcesAndCallerRegistration()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        services.AddSingleton<IUnitOfWorkSaveChangesHandler>(new NoOpSaveChangesHandler());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
        using var caller = new CancellationTokenSource();

        var root = manager.BeginOperation(new UnitOfWorkScopeOptions { Timeout = TimeSpan.FromHours(1) },
            caller.Token);
        var unitOfWorkRoot = (UnitOfWorkScope)root;
        CallbackStates(caller).Count.ShouldBe(1);
        var deadline = GetSource(unitOfWorkRoot, "_deadlineSource");
        var linked = GetSource(unitOfWorkRoot, "_linkedCancellationSource");
        deadline.ShouldNotBeNull();
        linked.ShouldNotBeNull();

        await root.DisposeAsync();

        manager.Current.ShouldBeNull();
        CallbackStates(caller).ShouldBeEmpty();
        ShouldBeDisposed(deadline);
        ShouldBeDisposed(linked);
    }

    /// <summary>
    /// 通过反射读取 root 工作单元的私有取消令牌源字段。
    /// </summary>
    /// <param name="root">待检查的 root 工作单元。</param>
    /// <param name="fieldName">私有字段名称。</param>
    /// <returns>字段值；字段不存在或类型不匹配时返回 <see langword="null" />。</returns>
    private static CancellationTokenSource GetSource(UnitOfWorkScope root, string fieldName)
    {
        var field = typeof(UnitOfWorkScope).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(root) as CancellationTokenSource;
    }

    /// <summary>
    /// 断言取消令牌源已释放，访问其令牌会抛出对象已释放异常。
    /// </summary>
    /// <param name="source">待检查的取消令牌源。</param>
    private static void ShouldBeDisposed(CancellationTokenSource source)
    {
        source.ShouldNotBeNull();
        Should.Throw<ObjectDisposedException>(() =>
        {
            _ = source.Token;
        });
    }

    /// <summary>
    /// 通过反射读取取消令牌源当前登记的调用方回调状态。
    /// </summary>
    /// <param name="source">待检查的取消令牌源。</param>
    /// <returns>当前登记的回调状态列表。</returns>
    private static List<object> CallbackStates(CancellationTokenSource source)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var registrations = typeof(CancellationTokenSource).GetField("_registrations", flags)
            ?.GetValue(source);
        var result = new List<object>();
        if (registrations == null)
            return result;
        var node = registrations.GetType().GetField("Callbacks", flags)?.GetValue(registrations);
        var seen = new HashSet<object>();
        while (node != null && seen.Add(node))
        {
            result.Add(node.GetType().GetField("CallbackState", flags)?.GetValue(node));
            node = node.GetType().GetField("Next", flags)?.GetValue(node);
        }
        return result;
    }

    /// <summary>
    /// 不执行实际保存的空操作保存处理器。
    /// </summary>
    private sealed class NoOpSaveChangesHandler : IUnitOfWorkSaveChangesHandler
    {
        /// <inheritdoc />
        public void SaveChanges(IUnitOfWorkScope scope) { }

        /// <inheritdoc />
        public Task SaveChangesAsync(IUnitOfWorkScope scope, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// 在 root 启动时抛出异常并记录失败 root 的观察器。
    /// </summary>
    private sealed class FailingRootStartingObserver : IUnitOfWorkRootStartingObserver
    {
        /// <summary>
        /// 获取启动失败的 root 工作单元列表。
        /// </summary>
        public List<IUnitOfWorkScope> FailedRoots { get; } = new();

        /// <inheritdoc />
        /// <remarks>
        /// 始终抛出异常，用于模拟启动观察失败。
        /// </remarks>
        /// <exception cref="InvalidOperationException">始终抛出。</exception>
        public void OnRootStarting(IUnitOfWorkScope scope, IUnitOfWorkScope parentScope) =>
            throw new InvalidOperationException("observer-root-start-failure");

        /// <inheritdoc />
        public void OnRootExited(IUnitOfWorkScope scope) { }

        /// <inheritdoc />
        /// <remarks>
        /// 记录启动失败的 root 工作单元。
        /// </remarks>
        public void OnRootStartFailed(IUnitOfWorkScope scope) => FailedRoots.Add(scope);
    }
}
