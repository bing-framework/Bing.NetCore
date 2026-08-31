using System.Linq.Expressions;
using Bing.DependencyInjection;

namespace Bing.Linq;

/// <summary>
/// 异步查询执行器
/// </summary>
public interface IAsyncQueryableExecuter : ITransientDependency
{
    #region Contains

    /// <summary>
    /// 是否包含指定对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="item">对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源是否包含指定对象。</returns>
    Task<bool> ContainsAsync<T>(IQueryable<T> queryable, T item, CancellationToken cancellationToken = default);

    #endregion

    #region Any/All

    /// <summary>
    /// 是否存在任意元素符合指定条件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为是否存在符合条件的元素。</returns>
    Task<bool> AnyAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否所有元素符合指定条件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为所有元素是否均符合条件。</returns>
    Task<bool> AllAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region Count/LongCount

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的元素数量。</returns>
    Task<int> CountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的元素数量。</returns>
    Task<int> CountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的元素数量。</returns>
    Task<long> LongCountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的元素数量。</returns>
    Task<long> LongCountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region First/FirstOrDefault

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的第一个元素。</returns>
    Task<T> FirstAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的第一个元素。</returns>
    Task<T> FirstAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的第一个元素；没有元素时返回默认值。</returns>
    Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的第一个元素；没有元素时返回默认值。</returns>
    Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region Last/LastOrDefault

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的最后一个元素。</returns>
    Task<T> LastAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的最后一个元素。</returns>
    Task<T> LastAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的最后一个元素；没有元素时返回默认值。</returns>
    Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的最后一个元素；没有元素时返回默认值。</returns>
    Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region Single/SingleOrDefault

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的唯一元素。</returns>
    Task<T> SingleAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的唯一元素。</returns>
    Task<T> SingleAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的唯一元素；没有元素时返回默认值。</returns>
    Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为符合条件的唯一元素；没有元素时返回默认值。</returns>
    Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region Min

    /// <summary>
    /// 查找最小值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的最小值。</returns>
    Task<T> MinAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找最小值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的最小值。</returns>
    Task<TResult> MinAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    #endregion

    #region Max

    /// <summary>
    /// 查找最大值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源中的最大值。</returns>
    Task<T> MaxAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找最大值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的最大值。</returns>
    Task<TResult> MaxAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    #endregion

    #region Sum

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的十进制总和。</returns>
    Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空十进制总和。</returns>
    Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的十进制总和。</returns>
    Task<decimal> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空十进制总和。</returns>
    Task<decimal?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的整数总和。</returns>
    Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空整数总和。</returns>
    Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的整数总和。</returns>
    Task<int> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空整数总和。</returns>
    Task<int?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的长整数总和。</returns>
    Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空长整数总和。</returns>
    Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的长整数总和。</returns>
    Task<long> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空长整数总和。</returns>
    Task<long?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的双精度总和。</returns>
    Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空双精度总和。</returns>
    Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的双精度总和。</returns>
    Task<double> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空双精度总和。</returns>
    Task<double?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的单精度总和。</returns>
    Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空单精度总和。</returns>
    Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的单精度总和。</returns>
    Task<float> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空单精度总和。</returns>
    Task<float?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default);

    #endregion

    #region Average

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的十进制平均值。</returns>
    Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源元素的可空十进制平均值。</returns>
    Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的十进制平均值。</returns>
    Task<decimal> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空十进制平均值。</returns>
    Task<decimal?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源整数元素的双精度平均值。</returns>
    Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源可空整数元素的可空双精度平均值。</returns>
    Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的双精度平均值。</returns>
    Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空双精度平均值。</returns>
    Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源长整数元素的双精度平均值。</returns>
    Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源可空长整数元素的可空双精度平均值。</returns>
    Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的双精度平均值。</returns>
    Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空双精度平均值。</returns>
    Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源双精度元素的双精度平均值。</returns>
    Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源可空双精度元素的可空双精度平均值。</returns>
    Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的双精度平均值。</returns>
    Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空双精度平均值。</returns>
    Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源单精度元素的单精度平均值。</returns>
    Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为数据源可空单精度元素的可空单精度平均值。</returns>
    Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的单精度平均值。</returns>
    Task<float> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为按选择器计算得到的可空单精度平均值。</returns>
    Task<float?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default);

    #endregion

    #region ToList/Array

    /// <summary>
    /// 转换为列表
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为查询结果列表。</returns>
    Task<List<T>> ToListAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 转换为数组
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为查询结果数组。</returns>
    Task<T[]> ToArrayAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    #endregion


}
