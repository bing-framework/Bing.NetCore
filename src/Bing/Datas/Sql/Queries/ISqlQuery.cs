using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Datas.Sql.Queries.Builders.Conditions;
using Bing.Domains.Repositories;
using Bing.Utils;

namespace Bing.Datas.Sql.Queries
{
    /// <summary>
    /// Sql查询对象
    /// </summary>
    public interface ISqlQuery
    {
        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        /// <returns></returns>
        ISqlBuilder NewBuilder();

        /// <summary>
        /// 获取整型
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        int ToInt(IDbConnection connection = null);

        /// <summary>
        /// 获取整型
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        Task<int> ToIntAsync(IDbConnection connection = null);

        /// <summary>
        /// 获取可空整型
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        int? ToIntOrNull(IDbConnection connection = null);

        /// <summary>
        /// 获取可空整型
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        Task<int?> ToIntOrNullAsync(IDbConnection connection = null);

        /// <summary>
        /// 获取单个实体
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        TResult To<TResult>(IDbConnection connection = null);

        /// <summary>
        /// 获取单个实体
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        Task<TResult> ToAsync<TResult>(IDbConnection connection = null);

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        List<TResult> ToList<TResult>(IDbConnection connection = null);

        /// <summary>
        /// 获取列表
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        Task<List<TResult>> ToListAsync<TResult>(IDbConnection connection = null);

        /// <summary>
        /// 获取分页列表
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="parameter">分页参数</param>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        PagerList<TResult> ToPagerList<TResult>(IPager parameter, IDbConnection connection = null);

        /// <summary>
        /// 获取分页列表
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="parameter">分页参数</param>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        Task<PagerList<TResult>> ToPagerListAsync<TResult>(IPager parameter, IDbConnection connection = null);

        /// <summary>
        /// 获取分页列表
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="page">页数</param>
        /// <param name="pageSize">每页显示行数</param>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        PagerList<TResult> ToPagerList<TResult>(int page, int pageSize, IDbConnection connection = null);

        /// <summary>
        /// 获取分页列表
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="page">页数</param>
        /// <param name="pageSize">每页显示行数</param>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        Task<PagerList<TResult>> ToPagerListAsync<TResult>(int page, int pageSize, IDbConnection connection = null);

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <param name="columns">列名，范例：a,b.c as d</param>
        /// <param name="tableAlias">表别名</param>
        /// <returns></returns>
        ISqlQuery Select(string columns, string tableAlias = null);

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="columns">列名，范例：t => new object[] { t.A, t.B }</param>
        /// <returns></returns>
        ISqlQuery Select<TEntity>(Expression<Func<TEntity, object[]>> columns) where TEntity : class;

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">列名，范例：t => t.A</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlQuery Select<TEntity>(Expression<Func<TEntity, object>> column, string columnAlias = null)
            where TEntity : class;

        /// <summary>
        /// 添加到Select子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        ISqlQuery AppendSelect(string sql);

        /// <summary>
        /// 设置表名
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="alias">别名</param>
        /// <returns></returns>
        ISqlQuery From(string table, string alias = null);

        /// <summary>
        /// 设置表名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="alias">别名</param>
        /// <param name="schema">架构名</param>
        /// <returns></returns>
        ISqlQuery From<TEntity>(string alias = null, string schema = null) where TEntity : class;

        /// <summary>
        /// 添加到From子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        ISqlQuery AppendFrom(string sql);

        /// <summary>
        /// 内连接
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="alias">别名</param>
        /// <returns></returns>
        ISqlQuery Join(string table, string alias = null);

        /// <summary>
        /// 内连接
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="alias">别名</param>
        /// <param name="schema">架构名</param>
        /// <returns></returns>
        ISqlQuery Join<TEntity>(string alias = null, string schema = null) where TEntity : class;

        /// <summary>
        /// 添加到内连接子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        ISqlQuery AppendJoin(string sql);

        /// <summary>
        /// 左外连接
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="alias">别名</param>
        /// <returns></returns>
        ISqlQuery LeftJoin(string table, string alias = null);

        /// <summary>
        /// 左外连接
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="alias">别名</param>
        /// <param name="schema">架构名</param>
        /// <returns></returns>
        ISqlQuery LeftJoin<TEntity>(string alias = null, string schema = null) where TEntity : class;

        /// <summary>
        /// 添加到左外连接子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        ISqlQuery AppendLeftJoin(string sql);

        /// <summary>
        /// 右外连接
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="alias">别名</param>
        /// <returns></returns>
        ISqlQuery RightJoin(string table, string alias = null);

        /// <summary>
        /// 右外连接
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="alias">别名</param>
        /// <param name="schema">架构名</param>
        /// <returns></returns>
        ISqlQuery RightJoin<TEntity>(string alias = null, string schema = null) where TEntity : class;

        /// <summary>
        /// 添加到右外连接子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        ISqlQuery AppendRightJoin(string sql);

        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <param name="left">左表列名</param>
        /// <param name="right">右表列名</param>
        /// <param name="operator">条件运算符</param>
        /// <returns></returns>
        ISqlQuery On(string left, string right, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <typeparam name="TLeft">左表实体类型</typeparam>
        /// <typeparam name="TRight">右表实体类型</typeparam>
        /// <param name="left">左表列名</param>
        /// <param name="right">右表列名</param>
        /// <param name="operator">条件运算符</param>
        /// <returns></returns>
        ISqlQuery On<TLeft, TRight>(Expression<Func<TLeft, object>> left, Expression<Func<TRight, object>> right,
            Operator @operator = Operator.Equal) where TLeft : class where TRight : class;

        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <typeparam name="TLeft">左表实体类型</typeparam>
        /// <typeparam name="TRight">右表实体类型</typeparam>
        /// <param name="expression">条件表达式</param>
        /// <returns></returns>
        ISqlQuery On<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> expression)
            where TLeft : class where TRight : class;

        /// <summary>
        /// And连接条件
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <returns></returns>
        ISqlQuery And(ICondition condition);

        /// <summary>
        /// Or连接条件
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <returns></returns>
        ISqlQuery Or(ICondition condition);

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlQuery Where(string column, object value, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlQuery Where<TEntity>(Expression<Func<TEntity, object>> expression, object value,
            Operator @operator = Operator.Equal) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">查询条件表达式</param>
        /// <returns></returns>
        ISqlQuery Where<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <param name="condition">拼接条件，该值为true时添加查询条件，否则忽略</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlQuery WhereIf(string column, object value, bool condition, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <param name="condition">拼接条件，该值为true时添加查询条件，否则忽略</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlQuery WhereIf<TEntity>(Expression<Func<TEntity, object>> expression, object value, bool condition,
            Operator @operator = Operator.Equal) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">查询条件表达式</param>
        /// <param name="condition">拼接条件，该值为true时添加查询条件，否则忽略</param>
        /// <returns></returns>
        ISqlQuery WhereIf<TEntity>(Expression<Func<TEntity, bool>> expression, bool condition) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值，如果该值为空，则忽略该查询条件</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlQuery WhereIfNotEmpty(string column, object value, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值，如果该值为空，则忽略该查询条件</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlQuery WhereIfNotEmpty<TEntity>(Expression<Func<TEntity, object>> expression, object value,
            Operator @operator = Operator.Equal) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">查询条件表达式，如果参数值为空，则忽略该查询条件</param>
        /// <returns></returns>
        ISqlQuery WhereIfNotEmpty<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : class;

        /// <summary>
        /// 添加到Where子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        ISqlQuery AppendWhere(string sql);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="order">排序列表</param>
        /// <returns></returns>
        ISqlQuery OrderBy(string order);
        
        /// <summary>
        /// 排序
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">排序列</param>
        /// <param name="desc">是否倒序</param>
        /// <returns></returns>
        ISqlQuery OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc = false);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="order">排序列表</param>
        /// <returns></returns>
        ISqlQuery AppendOrderBy(string order);
    }
}
