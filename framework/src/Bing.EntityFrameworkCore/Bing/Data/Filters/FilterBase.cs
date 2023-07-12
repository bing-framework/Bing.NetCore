using System.Linq.Expressions;

namespace Bing.Data.Filters;

/// <summary>
/// 数据过滤器基类
/// </summary>
/// <typeparam name="TFilterType">过滤器类型</typeparam>
public abstract class FilterBase<TFilterType> : IFilter<TFilterType> where TFilterType : class
{
    /// <summary>
    /// 过滤器是否启用
    /// </summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// 实体是否启用过滤器
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public bool IsEntityEnabled<TEntity>() => typeof(TFilterType).IsAssignableFrom(typeof(TEntity));

    /// <summary>
    /// 启用
    /// </summary>
    public void Enable() => IsEnabled = true;

    /// <summary>
    /// 禁用
    /// </summary>
    public IDisposable Disable()
    {
        if (IsEnabled == false)
            return new DisposeAction(null);
        IsEnabled = false;
        return new DisposeAction(Enable);
    }

    /// <summary>
    /// 获取过滤表达式
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public abstract Expression<Func<TEntity, bool>> GetExpression<TEntity>() where TEntity : class;// TODO: 由于EF Core 自身的原因，导致无法从外部传递表达式进去，需要定义 值在 DbContext 中方可正常使用
}
