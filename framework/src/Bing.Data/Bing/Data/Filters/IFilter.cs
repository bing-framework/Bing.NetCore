using System.Linq.Expressions;
using Bing.DependencyInjection;

namespace Bing.Data.Filters;

/// <summary>
/// 可在当前作用域启用或禁用的实体数据过滤器。
/// </summary>
public interface IFilter : ITransientDependency
{
    /// <summary>
    /// 获取过滤器当前是否启用。
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 判断指定实体类型是否应用当前过滤器。
    /// </summary>
    /// <typeparam name="TEntity">待判断的实体类型。</typeparam>
    /// <returns>实体类型启用过滤器时返回 true；否则返回 false。</returns>
    bool IsEntityEnabled<TEntity>();

    /// <summary>
    /// 在当前作用域启用过滤器。
    /// </summary>
    void Enable();

    /// <summary>
    /// 在当前作用域临时禁用过滤器。
    /// </summary>
    /// <returns>用于恢复禁用前状态的释放句柄。</returns>
    IDisposable Disable();

    /// <summary>
    /// 获取应用于指定实体类型的过滤表达式。
    /// </summary>
    /// <typeparam name="TEntity">要生成过滤表达式的实体类型。</typeparam>
    /// <returns>过滤表达式；该实体类型不适用时返回 null。</returns>
    Expression<Func<TEntity, bool>> GetExpression<TEntity>() where TEntity : class;
}

/// <summary>
/// 指定过滤器类型的数据过滤器契约。
/// </summary>
/// <typeparam name="TFilterType">过滤器实现类型。</typeparam>
public interface IFilter<TFilterType> : IFilter where TFilterType : class
{
}
