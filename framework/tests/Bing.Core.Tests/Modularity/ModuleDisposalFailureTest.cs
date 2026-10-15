using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 验证模块释放失败后的继续清理。
/// </summary>
[Collection("Module static compatibility")]
public class ModuleDisposalFailureTest
{
    /// <summary>
    /// 验证同步释放异常不会阻断依赖模块释放。
    /// </summary>
    [Fact]
    public void Shutdown_SyncModuleDisposeFailure_ShouldContinueInReverseOrder()
    {
        var state = new DisposalState();
        var provider = Create(state);
        provider.UseBing();

        var error = Assert.Throws<AggregateException>(() => provider.ShutdownBing());

        Assert.Contains(error.InnerExceptions, item => item.Message == "parent sync dispose");
        Assert.Equal(new[] { "dispose-parent-sync", "dispose-child-sync" }, state.Events);
        provider.ShutdownBing();
        provider.Dispose();
        Assert.Equal(2, state.Events.Count);
    }

    /// <summary>
    /// 验证异步释放异常不会阻断依赖模块释放。
    /// </summary>
    [Fact]
    public async Task Shutdown_AsyncModuleDisposeFailure_ShouldContinueInReverseOrder()
    {
        var state = new DisposalState();
        var provider = Create(state);
        await provider.UseBingAsync();

        var error = await Assert.ThrowsAsync<AggregateException>(() => provider.ShutdownBingAsync());

        Assert.Contains(error.InnerExceptions, item => item.Message == "parent async dispose");
        Assert.Equal(new[] { "dispose-parent-async", "dispose-child-async" }, state.Events);
        await provider.ShutdownBingAsync();
        await provider.DisposeAsync();
        Assert.Equal(2, state.Events.Count);
    }

    /// <summary>
    /// 验证原始初始化异常与模块释放异常均可定位。
    /// </summary>
    [Fact]
    public async Task InitializationFailure_ShouldPreserveOriginalAndCollectDisposeFailure()
    {
        var state = new DisposalState { FailInitialization = true };
        var provider = Create(state);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.UseBingAsync());

        Assert.Same(state.InitializationError, error);
        var cleanup = Assert.IsType<AggregateException>(error.Data["Bing.ModuleCleanupErrors"]);
        Assert.Contains(cleanup.Flatten().InnerExceptions, item => item.Message == "parent async dispose");
        Assert.Equal(new[] { "dispose-parent-async", "dispose-child-async" }, state.Events);
        await provider.DisposeAsync();
        Assert.Equal(2, state.Events.Count);
    }

    /// <summary>
    /// 创建仅由显式模块管理的容器。
    /// </summary>
    private static ServiceProvider Create(DisposalState state)
    {
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddBingApplication<ParentModule>(options => options.AutoRegisterServices = false);
        return services.BuildBingServiceProvider();
    }

    /// <summary>
    /// 记录释放顺序和初始化错误。
    /// </summary>
    private sealed class DisposalState
    {
        /// <summary>
        /// 释放事件。
        /// </summary>
        public List<string> Events { get; } = new();

        /// <summary>
        /// 是否令初始化失败。
        /// </summary>
        public bool FailInitialization { get; set; }

        /// <summary>
        /// 原始初始化异常。
        /// </summary>
        public InvalidOperationException InitializationError { get; } = new("original initialization failure");
    }

    /// <summary>
    /// 先初始化、后释放的依赖模块。
    /// </summary>
    private sealed class ChildModule : BingModule, IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 测试状态。
        /// </summary>
        private DisposalState _state;

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            _state = context.ServiceProvider.GetRequiredService<DisposalState>();

        /// <inheritdoc />
        public void Dispose() => _state.Events.Add("dispose-child-sync");

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _state.Events.Add("dispose-child-async");
            return default;
        }
    }

    /// <summary>
    /// 释放时抛错的启动模块。
    /// </summary>
    [DependsOnModule(typeof(ChildModule))]
    private sealed class ParentModule : BingModule, IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 测试状态。
        /// </summary>
        private DisposalState _state;

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context)
        {
            _state = context.ServiceProvider.GetRequiredService<DisposalState>();
            if (_state.FailInitialization) throw _state.InitializationError;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _state.Events.Add("dispose-parent-sync");
            throw new InvalidOperationException("parent sync dispose");
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _state.Events.Add("dispose-parent-async");
            throw new InvalidOperationException("parent async dispose");
        }
    }
}
