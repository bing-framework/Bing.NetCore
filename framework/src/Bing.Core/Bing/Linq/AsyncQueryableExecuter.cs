using System.Linq.Expressions;

namespace Bing.Linq;

/// <summary>
/// 异步查询执行器
/// </summary>
public class AsyncQueryableExecuter : IAsyncQueryableExecuter
{
    /// <summary>
    /// 异步执行提供程序集合
    /// </summary>
    protected IEnumerable<IAsyncQueryableProvider> Providers { get; }

    /// <summary>
    /// 初始化一个<see cref="AsyncQueryableExecuter"/>类型的实例
    /// </summary>
    /// <param name="providers">异步执行提供程序集合</param>
    public AsyncQueryableExecuter(IEnumerable<IAsyncQueryableProvider> providers) => Providers = providers ?? throw new ArgumentNullException(nameof(providers));

    /// <summary>
    /// 查找提供程序
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <returns>能够执行该查询的数据提供程序；没有匹配提供程序时返回 <see langword="null"/>。</returns>
    protected virtual IAsyncQueryableProvider FindProvider<T>(IQueryable<T> queryable) => Providers.FirstOrDefault(p => p.CanExecute(queryable));

    /// <summary>
    /// 是否包含指定对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="item">对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源是否包含指定对象。</returns>
    public Task<bool> ContainsAsync<T>(IQueryable<T> queryable, T item, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.ContainsAsync(queryable, item, cancellationToken)
            : Task.FromResult(queryable.Contains(item));
    }

    /// <summary>
    /// 是否存在任意元素符合指定条件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为是否存在符合条件的元素。</returns>
    public Task<bool> AnyAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AnyAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.Any(predicate));
    }

    /// <summary>
    /// 是否所有元素符合指定条件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为所有元素是否均符合条件。</returns>
    public Task<bool> AllAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AllAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.All(predicate));
    }

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的元素数量。</returns>
    public Task<int> CountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.CountAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.Count());
    }

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的元素数量。</returns>
    public Task<int> CountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.CountAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.Count(predicate));
    }

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的元素数量。</returns>
    public Task<long> LongCountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.LongCountAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.LongCount());
    }

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的元素数量。</returns>
    public Task<long> LongCountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.LongCountAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.LongCount(predicate));
    }

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的第一个元素。</returns>
    public Task<T> FirstAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.FirstAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.First());
    }

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的第一个元素。</returns>
    public Task<T> FirstAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.FirstAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.First(predicate));
    }

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的第一个元素；没有元素时返回默认值。</returns>
    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.FirstOrDefaultAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.FirstOrDefault());
    }

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的第一个元素；没有元素时返回默认值。</returns>
    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.FirstOrDefaultAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.FirstOrDefault(predicate));
    }

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的最后一个元素。</returns>
    public Task<T> LastAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.LastAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.Last());
    }

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的最后一个元素。</returns>
    public Task<T> LastAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.LastAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.Last(predicate));
    }

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的最后一个元素；没有元素时返回默认值。</returns>
    public Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.LastOrDefaultAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.LastOrDefault());
    }

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的最后一个元素；没有元素时返回默认值。</returns>
    public Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.LastOrDefaultAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.LastOrDefault(predicate));
    }

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的唯一元素。</returns>
    public Task<T> SingleAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SingleAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.Single());
    }

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的唯一元素。</returns>
    public Task<T> SingleAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SingleAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.Single(predicate));
    }

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的唯一元素；没有元素时返回默认值。</returns>
    public Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SingleOrDefaultAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.SingleOrDefault());
    }

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的唯一元素；没有元素时返回默认值。</returns>
    public Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SingleOrDefaultAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.SingleOrDefault(predicate));
    }

    /// <summary>
    /// 查找最小值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的最小值。</returns>
    public Task<T> MinAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.MinAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.Min());
    }

    /// <summary>
    /// 查找最小值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的最小值。</returns>
    public Task<TResult> MinAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.MinAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Min(selector));
    }

    /// <summary>
    /// 查找最大值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的最大值。</returns>
    public Task<T> MaxAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.MaxAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.Max());
    }

    /// <summary>
    /// 查找最大值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的最大值。</returns>
    public Task<TResult> MaxAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.MaxAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Max(selector));
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的十进制总和。</returns>
    public Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空十进制总和。</returns>
    public Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的十进制总和。</returns>
    public Task<decimal> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空十进制总和。</returns>
    public Task<decimal?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的整数总和。</returns>
    public Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空整数总和。</returns>
    public Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的整数总和。</returns>
    public Task<int> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空整数总和。</returns>
    public Task<int?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的长整数总和。</returns>
    public Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空长整数总和。</returns>
    public Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的长整数总和。</returns>
    public Task<long> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空长整数总和。</returns>
    public Task<long?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的双精度总和。</returns>
    public Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空双精度总和。</returns>
    public Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的双精度总和。</returns>
    public Task<double> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空双精度总和。</returns>
    public Task<double?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的单精度总和。</returns>
    public Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空单精度总和。</returns>
    public Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的单精度总和。</returns>
    public Task<float> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空单精度总和。</returns>
    public Task<float?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的十进制平均值。</returns>
    public Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空十进制平均值。</returns>
    public Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的十进制平均值。</returns>
    public Task<decimal> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空十进制平均值。</returns>
    public Task<decimal?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源整数元素的双精度平均值。</returns>
    public Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源可空整数元素的可空双精度平均值。</returns>
    public Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的双精度平均值。</returns>
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空双精度平均值。</returns>
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源长整数元素的双精度平均值。</returns>
    public Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源可空长整数元素的可空双精度平均值。</returns>
    public Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的双精度平均值。</returns>
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空双精度平均值。</returns>
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源双精度元素的双精度平均值。</returns>
    public Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源可空双精度元素的可空双精度平均值。</returns>
    public Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的双精度平均值。</returns>
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空双精度平均值。</returns>
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源单精度元素的单精度平均值。</returns>
    public Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源可空单精度元素的可空单精度平均值。</returns>
    public Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的单精度平均值。</returns>
    public Task<float> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空单精度平均值。</returns>
    public Task<float?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <summary>
    /// 转换为列表
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为查询结果列表。</returns>
    public Task<List<T>> ToListAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.ToListAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.ToList());
    }

    /// <summary>
    /// 转换为数组
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为查询结果数组。</returns>
    public Task<T[]> ToArrayAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.ToArrayAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.ToArray());
    }
}
