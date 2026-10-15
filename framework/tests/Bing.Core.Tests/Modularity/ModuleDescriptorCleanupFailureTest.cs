using System.Collections.ObjectModel;
using System.Reflection;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tests.Modularity;

/// <summary>
/// 验证服务描述符移除失败时的生命周期清理。
/// </summary>
[Collection("Module static compatibility")]
public sealed class ModuleDescriptorCleanupFailureTest
{
    /// <summary>
    /// 描述符清理失败应保留启动异常并完成内部资源清理。
    /// </summary>
    [Fact]
    public async Task InitializationFailure_WhenRemoveAtThrows_ShouldPreserveCauseAndFinishCleanup()
    {
        var state = new FailureState();
        var services = new FailingRemovalServices();
        services.AddSingleton(state);
        await services.AddBingApplicationAsync<FailingInitializationModule>(options => options.AutoRegisterServices = false);
        var provider = services.BuildBingServiceProvider();
        var descriptors = provider.GetRequiredService<IBingModuleContainer>().Modules.ToArray();
        var instanceProperty = typeof(BingModuleDescriptor).GetProperty("Instance", BindingFlags.Instance | BindingFlags.NonPublic)!;
        try
        {
            ((IServiceCollection)services).IsReadOnly.ShouldBeFalse();
            descriptors.ShouldNotBeEmpty();
            instanceProperty.GetValue(descriptors.Single(x => x.ModuleType == typeof(FailingInitializationModule)))
                .ShouldNotBeNull();

            var error = await Should.ThrowAsync<InvalidOperationException>(() => provider.UseBingAsync());

            error.ShouldBeSameAs(state.InitializationError);
            error.Data["Bing.ModulePhase"].ShouldBe("Initialize");
            var cleanup = error.Data["Bing.ModuleCleanupErrors"].ShouldBeOfType<AggregateException>();
            cleanup.Flatten().InnerExceptions.ShouldContain(services.RemovalError);
            foreach (var descriptor in descriptors)
                instanceProperty.GetValue(descriptor).ShouldBeNull();
            state.Initializations.ShouldBe(1);
            state.Shutdowns.ShouldBe(1);
            state.Disposals.ShouldBe(1);
            services.RemovalAttempts.ShouldBe(1);

            var retry = await Should.ThrowAsync<InvalidOperationException>(() => provider.UseBingAsync());
            retry.Message.ShouldContain("Failed");
            await provider.ShutdownBingAsync();
            await provider.ShutdownBingAsync();
        }
        finally
        {
            await provider.DisposeAsync();
            await provider.DisposeAsync();
        }
        state.Initializations.ShouldBe(1);
        state.Shutdowns.ShouldBe(1);
        state.Disposals.ShouldBe(1);
        services.RemovalAttempts.ShouldBe(1);
    }

    /// <summary>
    /// 模拟服务描述符移除异常。
    /// </summary>
    private sealed class FailingRemovalServices : Collection<ServiceDescriptor>, IServiceCollection
    {
        /// <summary>
        /// 获取移除描述符时抛出的异常。
        /// </summary>
        public InvalidOperationException RemovalError { get; } = new("descriptor-removal-error");

        /// <summary>
        /// 获取描述符移除尝试次数。
        /// </summary>
        public int RemovalAttempts { get; private set; }

        /// <inheritdoc />
        protected override void RemoveItem(int index)
        {
            RemovalAttempts++;
            throw RemovalError;
        }
    }

    /// <summary>
    /// 保存失败模块的生命周期调用。
    /// </summary>
    private sealed class FailureState
    {
        /// <summary>
        /// 获取或设置初始化调用次数。
        /// </summary>
        public int Initializations { get; set; }

        /// <summary>
        /// 获取或设置关闭调用次数。
        /// </summary>
        public int Shutdowns { get; set; }

        /// <summary>
        /// 获取或设置释放调用次数。
        /// </summary>
        public int Disposals { get; set; }

        /// <summary>
        /// 获取原始启动异常。
        /// </summary>
        public InvalidOperationException InitializationError { get; } = new("original-initialization-error");
    }

    /// <summary>
    /// 初始化失败并记录清理次数的模块。
    /// </summary>
    public sealed class FailingInitializationModule : BingModule, IAsyncDisposable
    {
        /// <summary>
        /// 当前测试的生命周期状态。
        /// </summary>
        private FailureState _state;

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context)
        {
            _state = context.ServiceProvider.GetRequiredService<FailureState>();
            _state.Initializations++;
            throw _state.InitializationError;
        }

        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context) => _state.Shutdowns++;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _state.Disposals++;
            return default;
        }
    }
}
