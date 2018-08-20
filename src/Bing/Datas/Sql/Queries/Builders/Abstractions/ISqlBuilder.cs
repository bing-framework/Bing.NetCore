using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Bing.Datas.Sql.Queries.Builders.Conditions;
using Bing.Domains.Repositories;
using Bing.Utils;

namespace Bing.Datas.Sql.Queries.Builders.Abstractions
{
    /// <summary>
    /// Sql生成器
    /// </summary>
    public interface ISqlBuilder:ICondition
    {
        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        /// <returns></returns>
        ISqlBuilder New();

        /// <summary>
        /// 生成Sql语句
        /// </summary>
        /// <returns></returns>
        string ToSql();

        /// <summary>
        /// 获取参数列表
        /// </summary>
        /// <returns></returns>
        IDictionary<string, object> GetParams();

        /// <summary>
        /// 获取Select语句
        /// </summary>
        /// <returns></returns>
        string GetSelect();

        /// <summary>
        /// 获取From语句
        /// </summary>
        /// <returns></returns>
        string GetFrom();

        /// <summary>
        /// 获取Join语句
        /// </summary>
        /// <returns></returns>
        string GetJoin();

        /// <summary>
        /// 获取Where语句
        /// </summary>
        /// <returns></returns>
        string GetWhere();

        /// <summary>
        /// 获取OrderBy语句
        /// </summary>
        /// <returns></returns>
        string GetOrderBy();

        /// <summary>
        /// 获取Having语句
        /// </summary>
        /// <returns></returns>
        string GetHaving();

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <param name="columns">列名</param>
        /// <param name="tableAlias">表别名</param>
        /// <returns></returns>
        ISqlBuilder Select(string columns, string tableAlias = null);

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="columns">列名</param>
        /// <returns></returns>
        ISqlBuilder Select<TEntity>(Expression<Func<TEntity, object[]>> columns) where TEntity : class;

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">列名</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Select<TEntity>(Expression<Func<TEntity, object>> column, string columnAlias = null)
            where TEntity : class;

        /// <summary>
        /// 添加到Select子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        ISqlBuilder AppendSelect(string sql);

        /// <summary>
        /// 设置表名
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="alias">别名</param>
        /// <returns></returns>
        ISqlBuilder From(string table, string alias = null);

        /// <summary>
        /// 设置表名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="alias">别名</param>
        /// <param name="schema">架构名</param>
        /// <returns></returns>
        ISqlBuilder From<TEntity>(string alias = null, string schema = null) where TEntity : class;

        /// <summary>
        /// 添加到From子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        ISqlBuilder AppendFrom(string sql);

        /// <summary>
        /// 内连接
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="alias">别名</param>
        /// <returns></returns>
        ISqlBuilder Join(string table, string alias = null);

        /// <summary>
        /// 内连接
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="alias">别名</param>
        /// <param name="schema">架构名</param>
        /// <returns></returns>
        ISqlBuilder Join<TEntity>(string alias = null, string schema = null) where TEntity : class;

        /// <summary>
        /// 添加到内连接子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        ISqlBuilder AppendJoin(string sql);

        /// <summary>
        /// 左外连接
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="alias">别名</param>
        /// <returns></returns>
        ISqlBuilder LeftJoin(string table, string alias = null);

        /// <summary>
        /// 左外连接
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="alias">别名</param>
        /// <param name="schema">架构名</param>
        /// <returns></returns>
        ISqlBuilder LeftJoin<TEntity>(string alias = null, string schema = null) where TEntity : class;

        /// <summary>
        /// 添加到左外连接子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        ISqlBuilder AppendLeftJoin(string sql);

        /// <summary>
        /// 右外连接
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="alias">别名</param>
        /// <returns></returns>
        ISqlBuilder RightJoin(string table, string alias = null);

        /// <summary>
        /// 右外连接
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="alias">别名</param>
        /// <param name="schema">架构名</param>
        /// <returns></returns>
        ISqlBuilder RightJoin<TEntity>(string alias = null, string schema = null) where TEntity : class;

        /// <summary>
        /// 添加到右外连接子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        ISqlBuilder AppendRightJoin(string sql);

        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <param name="left">左表列名</param>
        /// <param name="right">右表列名</param>
        /// <param name="operator">条件运算符</param>
        /// <returns></returns>
        ISqlBuilder On(string left, string right, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <typeparam name="TLeft">左表实体类型</typeparam>
        /// <typeparam name="TRight">右表实体类型</typeparam>
        /// <param name="left">左表列名</param>
        /// <param name="right">右表列名</param>
        /// <param name="operator">条件运算符</param>
        /// <returns></returns>
        ISqlBuilder On<TLeft, TRight>(Expression<Func<TLeft, object>> left, Expression<Func<TRight, object>> right,
            Operator @operator = Operator.Equal) where TLeft : class where TRight : class;

        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <typeparam name="TLeft">左表实体类型</typeparam>
        /// <typeparam name="TRight">右表实体类型</typeparam>
        /// <param name="expression">条件表达式</param>
        /// <returns></returns>
        ISqlBuilder On<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> expression)
            where TLeft : class where TRight : class;

        /// <summary>
        /// And连接条件
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <returns></returns>
        ISqlBuilder And(ICondition condition);

        /// <summary>
        /// Or连接条件
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <returns></returns>
        ISqlBuilder Or(ICondition condition);

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder Where(string column, object value, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder Where<TEntity>(Expression<Func<TEntity, object>> expression, object value,
            Operator @operator = Operator.Equal) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">查询条件表达式</param>
        /// <returns></returns>
        ISqlBuilder Where<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <param name="condition">拼接条件，该值为true时添加查询条件，否则忽略</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder WhereIf(string column, object value, bool condition, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值</param>
        /// <param name="condition">拼接条件，该值为true时添加查询条件，否则忽略</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder WhereIf<TEntity>(Expression<Func<TEntity, object>> expression, object value, bool condition,
            Operator @operator = Operator.Equal) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">查询条件表达式</param>
        /// <param name="condition">拼接条件，该值为true时添加查询条件，否则忽略</param>
        /// <returns></returns>
        ISqlBuilder WhereIf<TEntity>(Expression<Func<TEntity, bool>> expression,bool condition) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值，如果该值为空，则忽略该查询条件</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder WhereIfNotEmpty(string column, object value, Operator @operator = Operator.Equal);

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式</param>
        /// <param name="value">值，如果该值为空，则忽略该查询条件</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder WhereIfNotEmpty<TEntity>(Expression<Func<TEntity, object>> expression, object value,
            Operator @operator = Operator.Equal) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">查询条件表达式，如果参数值为空，则忽略该查询条件</param>
        /// <returns></returns>
        ISqlBuilder WhereIfNotEmpty<TEntity>(Expression<Func<TEntity, bool>> expression) where TEntity : class;

        /// <summary>
        /// 添加到Where子句
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        ISqlBuilder AppendWhere(string sql);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="order">排序列表</param>
        /// <returns></returns>
        ISqlBuilder OrderBy(string order);

        /// <summary>
        /// 排序
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">排序列</param>
        /// <param name="desc">是否降序</param>
        /// <returns></returns>
        ISqlBuilder OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc = false);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="order">排序列表</param>
        /// <returns></returns>
        ISqlBuilder AppendOrderBy(string order);

        /// <summary>
        /// 设置分页
        /// </summary>
        /// <param name="pager">分页参数</param>
        /// <returns></returns>
        ISqlBuilder Pager(IPager pager);
    }
}
