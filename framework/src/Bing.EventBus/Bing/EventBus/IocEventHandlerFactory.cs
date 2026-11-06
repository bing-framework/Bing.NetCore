using Microsoft.Extensions.DependencyInjection;

namespace Bing.EventBus;

/// <summary>
/// 从独立依赖注入服务作用域创建事件处理器的工厂。
/// </summary>
public class IocEventHandlerFactory : IEventHandlerFactory, IAsyncEventHandlerFactory, IDisposable
{
    /// <summary>
    /// 获取要从服务容器解析的事件处理器类型。
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// 获取用于创建处理器独立服务作用域的工厂。
    /// </summary>
    protected IServiceScopeFactory ScopeFactory { get; }

    /// <summary>
    /// 初始化一个 <see cref="IocEventHandlerFactory" /> 类型的实例。
    /// </summary>
    /// <param name="scopeFactory">创建处理器独立服务作用域的工厂。</param>
    ///
    /// <param name="handlerType">要从服务容器解析的事件处理器类型。</param>
    /// <remarks>
    /// 初始化一个 <see cref="IocEventHandlerFactory" /> 类的新实例。
    /// </remarks>
    public IocEventHandlerFactory(IServiceScopeFactory scopeFactory, Type handlerType)
    {
        ScopeFactory = scopeFactory;
        HandlerType = handlerType;
    }

    /// <summary>
    /// 处理器作用域清理异常的数据键。
    /// </summary>
    private const string CleanupErrorKey = "Bing.EventBus.HandlerScopeCleanupError";

    /// <inheritdoc />
    /// <remarks>
    /// 返回包装器会在释放时释放为该处理器创建的依赖注入服务作用域；
    /// 解析或转换失败时会清理尚未交接的作用域，并保留主构造异常。
    /// </remarks>
    public IEventHandlerDisposeWrapper GetHandler()
    {
        var scope = ScopeFactory.CreateScope();
        try
        {
            var handler = (IEventHandler)scope.ServiceProvider.GetRequiredService(HandlerType);
            return new EventHandlerDisposeWrapper(handler, () => scope.Dispose(),
                () => DisposeScopeAsync(scope));
        }
        catch (Exception error)
        {
            try { scope.Dispose(); }
            catch (Exception cleanup) { error.Data[CleanupErrorKey] = cleanup; }
            throw;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 使用异步作用域创建处理器，使仅实现 <see cref="IAsyncDisposable"/> 的依赖可以异步释放；
    /// 创建失败时等待作用域清理完成，并保留主构造异常与清理异常。
    /// </remarks>
    public async ValueTask<IEventHandlerDisposeWrapper> GetHandlerAsync()
    {
        var scope = ScopeFactory.CreateAsyncScope();
        try
        {
            var handler = (IEventHandler)scope.ServiceProvider.GetRequiredService(HandlerType);
            return new EventHandlerDisposeWrapper(handler, () => scope.Dispose(),
                () => scope.DisposeAsync());
        }
        catch (Exception error)
        {
            try { await scope.DisposeAsync().ConfigureAwait(false); }
            catch (Exception cleanup) { error.Data[CleanupErrorKey] = cleanup; }
            throw;
        }
    }

    /// <summary>
    /// 异步释放处理器作用域；自定义作用域不支持异步释放时回退到同步释放。
    /// </summary>
    /// <param name="scope">要释放的服务作用域。</param>
    private static ValueTask DisposeScopeAsync(IServiceScope scope)
    {
        if (scope is IAsyncDisposable asyncDisposable)
            return asyncDisposable.DisposeAsync();
        scope.Dispose();
        return default;
    }

    /// <inheritdoc />
    public bool IsInFactories(List<IEventHandlerFactory> handlerFactories)
    {
        return handlerFactories
            .OfType<IocEventHandlerFactory>()
            .Any(t => t.HandlerType == HandlerType);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 当前工厂不持有需释放的资源；处理器服务作用域由 <see cref="GetHandler"/> 返回的包装器释放。
    /// </remarks>
    public void Dispose()
    {
    }
}