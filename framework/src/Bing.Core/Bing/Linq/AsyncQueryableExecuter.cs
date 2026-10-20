using System.Linq.Expressions;

namespace Bing.Linq;

/// <summary>
/// 异步查询执行器
/// </summary>
public class AsyncQueryableExecuter : IAsyncQueryableExecuter
{
    /// <summary>
    /// 获取异步执行提供程序集合
    /// </summary>
    protected IEnumerable<IAsyncQueryableProvider> Providers { get; }

    /// <summary>
    /// 初始化一个 <see cref="AsyncQueryableExecuter"/> 类型的实例。
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

    /// <inheritdoc />
    public Task<bool> ContainsAsync<T>(IQueryable<T> queryable, T item, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.ContainsAsync(queryable, item, cancellationToken)
            : Task.FromResult(queryable.Contains(item));
    }

    /// <inheritdoc />
    public Task<bool> AnyAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AnyAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.Any(predicate));
    }

    /// <inheritdoc />
    public Task<bool> AllAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AllAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.All(predicate));
    }

    /// <inheritdoc />
    public Task<int> CountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.CountAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.Count());
    }

    /// <inheritdoc />
    public Task<int> CountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.CountAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.Count(predicate));
    }

    /// <inheritdoc />
    public Task<long> LongCountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.LongCountAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.LongCount());
    }

    /// <inheritdoc />
    public Task<long> LongCountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.LongCountAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.LongCount(predicate));
    }

    /// <inheritdoc />
    public Task<T> FirstAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.FirstAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.First());
    }

    /// <inheritdoc />
    public Task<T> FirstAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.FirstAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.First(predicate));
    }

    /// <inheritdoc />
    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.FirstOrDefaultAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.FirstOrDefault());
    }

    /// <inheritdoc />
    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.FirstOrDefaultAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.FirstOrDefault(predicate));
    }

    /// <inheritdoc />
    public Task<T> LastAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.LastAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.Last());
    }

    /// <inheritdoc />
    public Task<T> LastAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.LastAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.Last(predicate));
    }

    /// <inheritdoc />
    public Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.LastOrDefaultAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.LastOrDefault());
    }

    /// <inheritdoc />
    public Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.LastOrDefaultAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.LastOrDefault(predicate));
    }

    /// <inheritdoc />
    public Task<T> SingleAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SingleAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.Single());
    }

    /// <inheritdoc />
    public Task<T> SingleAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SingleAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.Single(predicate));
    }

    /// <inheritdoc />
    public Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SingleOrDefaultAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.SingleOrDefault());
    }

    /// <inheritdoc />
    public Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SingleOrDefaultAsync(queryable, predicate, cancellationToken)
            : Task.FromResult(queryable.SingleOrDefault(predicate));
    }

    /// <inheritdoc />
    public Task<T> MinAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.MinAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.Min());
    }

    /// <inheritdoc />
    public Task<TResult> MinAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.MinAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Min(selector));
    }

    /// <inheritdoc />
    public Task<T> MaxAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.MaxAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.Max());
    }

    /// <inheritdoc />
    public Task<TResult> MaxAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.MaxAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Max(selector));
    }

    /// <inheritdoc />
    public Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <inheritdoc />
    public Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <inheritdoc />
    public Task<decimal> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <inheritdoc />
    public Task<decimal?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <inheritdoc />
    public Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <inheritdoc />
    public Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <inheritdoc />
    public Task<int> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <inheritdoc />
    public Task<int?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <inheritdoc />
    public Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <inheritdoc />
    public Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <inheritdoc />
    public Task<long> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <inheritdoc />
    public Task<long?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <inheritdoc />
    public Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <inheritdoc />
    public Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <inheritdoc />
    public Task<double> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <inheritdoc />
    public Task<double?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <inheritdoc />
    public Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <inheritdoc />
    public Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.SumAsync(source, cancellationToken)
            : Task.FromResult(source.Sum());
    }

    /// <inheritdoc />
    public Task<float> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <inheritdoc />
    public Task<float?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.SumAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Sum(selector));
    }

    /// <inheritdoc />
    public Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <inheritdoc />
    public Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <inheritdoc />
    public Task<decimal> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <inheritdoc />
    public Task<decimal?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <inheritdoc />
    public Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <inheritdoc />
    public Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <inheritdoc />
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <inheritdoc />
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <inheritdoc />
    public Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <inheritdoc />
    public Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <inheritdoc />
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <inheritdoc />
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <inheritdoc />
    public Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <inheritdoc />
    public Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <inheritdoc />
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <inheritdoc />
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <inheritdoc />
    public Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <inheritdoc />
    public Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(source);
        return provider != null
            ? provider.AverageAsync(source, cancellationToken)
            : Task.FromResult(source.Average());
    }

    /// <inheritdoc />
    public Task<float> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <inheritdoc />
    public Task<float?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.AverageAsync(queryable, selector, cancellationToken)
            : Task.FromResult(queryable.Average(selector));
    }

    /// <inheritdoc />
    public Task<List<T>> ToListAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.ToListAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.ToList());
    }

    /// <inheritdoc />
    public Task<T[]> ToArrayAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        var provider = FindProvider(queryable);
        return provider != null
            ? provider.ToArrayAsync(queryable, cancellationToken)
            : Task.FromResult(queryable.ToArray());
    }
}
