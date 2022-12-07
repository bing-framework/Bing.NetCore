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
    protected virtual IAsyncQueryableProvider FindProvider<T>(IQueryable<T> queryable) => Providers.FirstOrDefault(p => p.CanExecute(queryable));

    /// <summary>
    /// 是否包含指定对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="item">对象</param>
    /// <param name="cancellationToken">取消令牌</param>
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
    public Task<T[]> ToArrayAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.ToArrayAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.ToArray());
    }
}
