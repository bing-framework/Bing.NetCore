using System.Linq.Expressions;
using Bing.Linq;

namespace Bing.FreeSQL;

/// <summary>
/// 基于FreeSql的异步查询提供程序
/// </summary>
public class FreeSqlAsyncQueryableProvider : IAsyncQueryableProvider
{
    /// <summary>
    /// 在取消检查后执行同步 LINQ 查询并包装为异步结果。
    /// </summary>
    /// <typeparam name="TResult">查询结果类型。</typeparam>
    /// <param name="operation">同步查询操作。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包装同步结果的异步任务。</returns>
    private static Task<TResult> Execute<TResult>(Func<TResult> operation, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<TResult>(cancellationToken);
        try
        {
            return Task.FromResult(operation());
        }
        catch (Exception exception)
        {
            return Task.FromException<TResult>(exception);
        }
    }

    /// <summary>
    /// 是否可执行
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <returns>当前提供程序支持该查询时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public bool CanExecute<T>(IQueryable<T> queryable)
    {
        return false;
    }

    /// <inheritdoc />
    public Task<bool> ContainsAsync<T>(IQueryable<T> queryable, T item, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Contains(item), cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> AnyAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Any(predicate), cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> AllAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.All(predicate), cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Count(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Count(predicate), cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> LongCountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.LongCount(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> LongCountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.LongCount(predicate), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> FirstAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.First(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> FirstAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.First(predicate), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.FirstOrDefault(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.FirstOrDefault(predicate), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> LastAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Last(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> LastAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Last(predicate), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.LastOrDefault(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.LastOrDefault(predicate), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> SingleAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Single(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> SingleAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Single(predicate), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.SingleOrDefault(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.SingleOrDefault(predicate), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> MinAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Min(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> MinAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Min(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> MaxAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Max(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> MaxAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Max(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Sum(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Sum(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<decimal> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Sum(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<decimal?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Sum(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Sum(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Sum(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Sum(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<int?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Sum(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Sum(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Sum(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Sum(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<long?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Sum(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Sum(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Sum(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Sum(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Sum(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Sum(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Sum(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<float> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Sum(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<float?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Sum(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Average(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Average(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<decimal> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Average(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<decimal?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Average(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Average(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Average(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Average(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Average(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Average(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Average(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Average(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Average(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Average(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Average(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Average(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Average(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Average(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default)
    {
        return Execute(() => source.Average(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<float> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Average(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<float?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.Average(selector), cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<T>> ToListAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.ToList(), cancellationToken);
    }

    /// <inheritdoc />
    public Task<T[]> ToArrayAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
    {
        return Execute(() => queryable.ToArray(), cancellationToken);
    }
}
