using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Bing.Linq;

namespace Bing.FreeSQL
{
    /// <summary>
    /// 基于FreeSql的异步查询提供程序
    /// </summary>
    public class FreeSqlAsyncQueryableProvider : IAsyncQueryableProvider
    {
        /// <summary>
        /// 是否可执行
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        public bool CanExecute<T>(IQueryable<T> queryable)
        {
            return false;
        }

        /// <summary>
        /// 是否包含指定对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="item">对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<bool> ContainsAsync<T>(IQueryable<T> queryable, T item, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找数量
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<int> CountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找数量
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<long> LongCountAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找单个实体
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<T> FirstAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找单个实体。如果找不到则返回默认值
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找单个实体
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<T> LastAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找单个实体。如果找不到则返回默认值
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<T> LastOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找单个实体
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<T> SingleAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找单个实体。如果找不到则返回默认值
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<T> SingleOrDefaultAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找最小值
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<T> MinAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找最大值
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<T> MaxAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<decimal> SumAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<decimal?> SumAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<int> SumAsync(IQueryable<int> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<int?> SumAsync(IQueryable<int?> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<long> SumAsync(IQueryable<long> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<long?> SumAsync(IQueryable<long?> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<double> SumAsync(IQueryable<double> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<double?> SumAsync(IQueryable<double?> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<float> SumAsync(IQueryable<float> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 求和
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<float?> SumAsync(IQueryable<float?> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 平均值
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<decimal> AverageAsync(IQueryable<decimal> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 平均值
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<decimal?> AverageAsync(IQueryable<decimal?> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 平均值
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<double> AverageAsync(IQueryable<int> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 平均值
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<double?> AverageAsync(IQueryable<int?> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 平均值
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<double> AverageAsync(IQueryable<long> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 平均值
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<double?> AverageAsync(IQueryable<long?> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 平均值
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<double> AverageAsync(IQueryable<double> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 平均值
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<double?> AverageAsync(IQueryable<double?> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 平均值
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<float> AverageAsync(IQueryable<float> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 平均值
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<float?> AverageAsync(IQueryable<float?> source, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 转换为列表
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<List<T>> ToListAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 转换为数组
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<T[]> ToArrayAsync<T>(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
