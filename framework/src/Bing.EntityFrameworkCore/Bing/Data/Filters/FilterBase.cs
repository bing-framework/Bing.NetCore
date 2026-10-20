using System.Linq.Expressions;

namespace Bing.Data.Filters;

/// <summary>
/// 数据过滤器基类
/// </summary>
/// <typeparam name="TFilterType">过滤器类型</typeparam>
public abstract class FilterBase<TFilterType> : IFilter<TFilterType> where TFilterType : class
{
    /// <inheritdoc />
    public bool IsEnabled { get; private set; } = true;

    /// <inheritdoc />
    public bool IsEntityEnabled<TEntity>() => typeof(TFilterType).IsAssignableFrom(typeof(TEntity));

    /// <inheritdoc />
    public void Enable() => IsEnabled = true;

    /// <inheritdoc />
    public IDisposable Disable()
    {
        if (IsEnabled == false)
            return new DisposeAction(() => { });
        IsEnabled = false;
        return new DisposeAction(Enable);
    }

    /// <inheritdoc />
    public abstract Expression<Func<TEntity, bool>> GetExpression<TEntity>() where TEntity : class;// TODO: 由于EF Core 自身的原因，导致无法从外部传递表达式进去，需要定义 值在 DbContext 中方可正常使用
}
