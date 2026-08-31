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
    /// <summary>
    /// 是否可执行
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <returns>查询提供程序支持 EF Core 异步执行时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public bool CanExecute<T>(IQueryable<T> queryable) => queryable.Provider is EntityQueryProvider;

    /// <summary>
    /// 是否包含指定对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="item">对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步判断操作的任务，结果指示数据源是否包含指定对象。</returns>
    public Task<bool> ContainsAsync<T>(IQueryable<T> queryable, T item, CancellationToken cancellationToken = default) => queryable.ContainsAsync(item, cancellationToken);

    /// <summary>
    /// 是否存在任意元素符合指定条件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步判断操作的任务，结果指示是否存在符合条件的元素。</returns>
    public Task<bool> AnyAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.AnyAsync(predicate, cancellationToken);

    /// <summary>
    /// 是否所有元素符合指定条件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步判断操作的任务，结果指示所有元素是否符合条件。</returns>
    public Task<bool> AllAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.AllAsync(predicate, cancellationToken);

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素数量。</returns>
    public Task<int> CountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.CountAsync(cancellationToken);

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的元素数量。</returns>
    public Task<int> CountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.CountAsync(predicate, cancellationToken);

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的长整数数量。</returns>
    public Task<long> LongCountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.LongCountAsync(cancellationToken);

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的元素长整数数量。</returns>
    public Task<long> LongCountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.LongCountAsync(predicate, cancellationToken);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为第一个元素。</returns>
    public Task<T> FirstAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.FirstAsync(cancellationToken);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的第一个元素。</returns>
    public Task<T> FirstAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.FirstAsync(predicate, cancellationToken);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为第一个元素；没有元素时返回默认值。</returns>
    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的第一个元素；没有元素时返回默认值。</returns>
    public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.FirstOrDefaultAsync(predicate, cancellationToken);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为最后一个元素。</returns>
    public Task<T> LastAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.LastAsync(cancellationToken);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的最后一个元素。</returns>
    public Task<T> LastAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.LastAsync(predicate, cancellationToken);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为最后一个元素；没有元素时返回默认值。</returns>
    public Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.LastOrDefaultAsync(cancellationToken);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的最后一个元素；没有元素时返回默认值。</returns>
    public Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.LastOrDefaultAsync(predicate, cancellationToken);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为唯一元素。</returns>
    public Task<T> SingleAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.SingleAsync(cancellationToken);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的唯一元素。</returns>
    public Task<T> SingleAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.SingleAsync(predicate, cancellationToken);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为唯一元素；没有元素时返回默认值。</returns>
    public Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的唯一元素；没有元素时返回默认值。</returns>
    public Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => queryable.SingleOrDefaultAsync(predicate, cancellationToken);

    /// <summary>
    /// 查找最小值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的最小值。</returns>
    public Task<T> MinAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.MinAsync(cancellationToken);

    /// <summary>
    /// 查找最小值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的最小值。</returns>
    public Task<TResult> MinAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => queryable.MinAsync(selector, cancellationToken);

    /// <summary>
    /// 查找最大值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的最大值。</returns>
    public Task<T> MaxAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.MaxAsync(cancellationToken);

    /// <summary>
    /// 查找最大值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的最大值。</returns>
    public Task<TResult> MaxAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default) => queryable.MaxAsync(selector, cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的十进制总和。</returns>
    public Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空十进制总和。</returns>
    public Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的十进制总和。</returns>
    public Task<decimal> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空十进制总和。</returns>
    public Task<decimal?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的整数总和。</returns>
    public Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空整数总和。</returns>
    public Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的整数总和。</returns>
    public Task<int> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空整数总和。</returns>
    public Task<int?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的长整数总和。</returns>
    public Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空长整数总和。</returns>
    public Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的长整数总和。</returns>
    public Task<long> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空长整数总和。</returns>
    public Task<long?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的双精度总和。</returns>
    public Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空双精度总和。</returns>
    public Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的双精度总和。</returns>
    public Task<double> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空双精度总和。</returns>
    public Task<double?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的单精度总和。</returns>
    public Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空单精度总和。</returns>
    public Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default) => source.SumAsync(cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的单精度总和。</returns>
    public Task<float> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空单精度总和。</returns>
    public Task<float?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default) => queryable.SumAsync(selector, cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的十进制平均值。</returns>
    public Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空十进制平均值。</returns>
    public Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的十进制平均值。</returns>
    public Task<decimal> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空十进制平均值。</returns>
    public Task<decimal?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源整数元素的双精度平均值。</returns>
    public Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源可空整数元素的可空双精度平均值。</returns>
    public Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的双精度平均值。</returns>
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空双精度平均值。</returns>
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源长整数元素的双精度平均值。</returns>
    public Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源可空长整数元素的可空双精度平均值。</returns>
    public Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的双精度平均值。</returns>
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空双精度平均值。</returns>
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源双精度元素的双精度平均值。</returns>
    public Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源可空双精度元素的可空双精度平均值。</returns>
    public Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的双精度平均值。</returns>
    public Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空双精度平均值。</returns>
    public Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源单精度元素的单精度平均值。</returns>
    public Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源可空单精度元素的可空单精度平均值。</returns>
    public Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default) => source.AverageAsync(cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的单精度平均值。</returns>
    public Task<float> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空单精度平均值。</returns>
    public Task<float?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default) => queryable.AverageAsync(selector, cancellationToken);

    /// <summary>
    /// 转换为列表
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为查询结果列表。</returns>
    public Task<List<T>> ToListAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.ToListAsync(cancellationToken);

    /// <summary>
    /// 转换为数组
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为查询结果数组。</returns>
    public Task<T[]> ToArrayAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default) => queryable.ToArrayAsync(cancellationToken);
}
