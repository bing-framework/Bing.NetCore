using System.Linq.Expressions;
using Bing.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// 基于EFCore的异步查询提供程序
/// </summary>
public class EfCoreAsyncQueryableProvider : IAsyncQueryableProvider
{
    /// <inheritdoc />
    public bool CanExecute<T>(IQueryable<T> queryable) => queryable.Provider is EntityQueryProvider;

    /// <inheritdoc />
    public Task<bool> ContainsAsync<T>(IQueryable<T> queryable, T item, CancellationToken cancellationToken = default) => queryable.ContainsAsync(item, cancellationToken);

    /// <inheritdoc />
    public Task<bool> AnyAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.AnyAsync(predicate, cancellationToken);

    /// <inheritdoc />
    public Task<bool> AllAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.AllAsync(predicate, cancellationToken);

    /// <inheritdoc />
    public Task<int> CountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.CountAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> CountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.CountAsync(predicate, cancellationToken);

    /// <inheritdoc />
    public Task<long> LongCountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.LongCountAsync(cancellationToken);

    /// <inheritdoc />
    public Task<long> LongCountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.LongCountAsync(predicate, cancellationToken);

    /// <inheritdoc />
    public Task<T> FirstAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.FirstAsync(cancellationToken);

    /// <inheritdoc />
    public Task<T> FirstAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.FirstAsync(predicate, cancellationToken);

    /// <inheritdoc />
    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.FirstOrDefaultAsync(predicate, cancellationToken);

    /// <inheritdoc />
    public Task<T> LastAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.LastAsync(cancellationToken);

    /// <inheritdoc />
    public Task<T> LastAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.LastAsync(predicate, cancellationToken);

    /// <inheritdoc />
    public Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.LastOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.LastOrDefaultAsync(predicate, cancellationToken);

    /// <inheritdoc />
    public Task<T> SingleAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.SingleAsync(cancellationToken);

    /// <inheritdoc />
    public Task<T> SingleAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.SingleAsync(predicate, cancellationToken);

    /// <inheritdoc />
    public Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.SingleOrDefaultAsync(predicate, cancellationToken);

    /// <inheritdoc />
    public Task<T> MinAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.MinAsync(cancellationToken);

    /// <inheritdoc />
    public Task<TResult> MinAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => queryable.MinAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<T> MaxAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.MaxAsync(cancellationToken);

    /// <inheritdoc />
    public Task<TResult> MaxAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => queryable.MaxAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <inheritdoc />
    public Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <inheritdoc />
    public Task<decimal> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<decimal?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<int?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <inheritdoc />
    public Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <inheritdoc />
    public Task<long> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<long?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <inheritdoc />
    public Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <inheritdoc />
    public Task<double> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<double?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <inheritdoc />
    public Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <inheritdoc />
    public Task<float> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<float?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <inheritdoc />
    public Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <inheritdoc />
    public Task<decimal> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<decimal?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <inheritdoc />
    public Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <inheritdoc />
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <inheritdoc />
    public Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <inheritdoc />
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <inheritdoc />
    public Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <inheritdoc />
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <inheritdoc />
    public Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <inheritdoc />
    public Task<float> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<float?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <inheritdoc />
    public Task<List<T>> ToListAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<T[]> ToArrayAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.ToArrayAsync(cancellationToken);
}
