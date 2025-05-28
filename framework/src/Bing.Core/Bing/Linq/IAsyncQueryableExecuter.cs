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
    Task<bool> AnyAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否所有元素符合指定条件
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> AllAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region Count/LongCount

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> CountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> CountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long> LongCountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找数量
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long> LongCountAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region First/FirstOrDefault

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> FirstAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> FirstAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region Last/LastOrDefault

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> LastAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> LastAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region Single/SingleOrDefault

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> SingleAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> SingleAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体。如果找不到则返回默认值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region Min

    /// <summary>
    /// 查找最小值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> MinAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找最小值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TResult> MinAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    #endregion

    #region Max

    /// <summary>
    /// 查找最大值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T> MaxAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找最大值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TResult> MaxAsync<T, TResult>(IQueryable<T> queryable, Expression<Func<T, TResult>> selector, CancellationToken cancellationToken = default);

    #endregion

    #region Sum

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<decimal> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<decimal?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<int?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<long?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<float> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 求和
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<float?> SumAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default);

    #endregion

    #region Average

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<decimal> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<decimal?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, decimal?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, int?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, long?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<double?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, double?>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <param name="source">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<float> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float>> selector, CancellationToken cancellationToken = default);

    /// <summary>
    /// 平均值
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="selector">选择器</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<float?> AverageAsync<T>(IQueryable<T> queryable, Expression<Func<T, float?>> selector, CancellationToken cancellationToken = default);

    #endregion

    #region ToList/Array

    /// <summary>
    /// 转换为列表
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<List<T>> ToListAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    /// <summary>
    /// 转换为数组
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<T[]> ToArrayAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default);

    #endregion


}
