using Bing.DependencyInjection;

namespace Bing.Data.Filters;

/// <summary>
/// 当前异步执行流的数据过滤状态。
/// </summary>
/// <remarks>
/// 状态以不可变快照保存于 <see cref="AsyncLocal{T}"/>，每个启用或禁用作用域在释放时只移除自身覆盖，
/// 因而支持嵌套作用域、异常退出和并行异步调用隔离。
/// </remarks>
public interface IDataFilter : IScopedDependency
{
    /// <summary>
    /// 判断指定过滤器在当前执行流中是否启用。
    /// </summary>
    /// <typeparam name="TFilter">过滤器标识类型。</typeparam>
    /// <returns>已启用时返回 <see langword="true"/>。</returns>
    bool IsEnabled<TFilter>() where TFilter : class;

    /// <summary>
    /// 在当前执行流中临时启用指定过滤器。
    /// </summary>
    /// <typeparam name="TFilter">过滤器标识类型。</typeparam>
    /// <returns>释放后恢复此前状态的作用域句柄。</returns>
    IDisposable Enable<TFilter>() where TFilter : class;

    /// <summary>
    /// 在当前执行流中临时禁用指定过滤器。
    /// </summary>
    /// <typeparam name="TFilter">过滤器标识类型。</typeparam>
    /// <returns>释放后恢复此前状态的作用域句柄。</returns>
    IDisposable Disable<TFilter>() where TFilter : class;
}
