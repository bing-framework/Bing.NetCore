using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Bing.Datas.Queries;
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
        /// 克隆
        /// </summary>
        /// <returns></returns>
        ISqlBuilder Clone();

        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        /// <returns></returns>
        ISqlBuilder New();

        /// <summary>
        /// 清空并初始化
        /// </summary>
        void Clear();

        /// <summary>
        /// 生成调试Sql语句，Sql语句中的参数被替换为参数值
        /// </summary>
        /// <returns></returns>
        string ToDebugSql();

        /// <summary>
        /// 生成Sql语句
        /// </summary>
        /// <returns></returns>
        string ToSql();

        /// <summary>
        /// 生成获取行数调试Sql语句
        /// </summary>
        /// <returns></returns>
        string ToCountDebugSql();

        /// <summary>
        /// 生成获取行数Sql语句
        /// </summary>
        /// <returns></returns>
        string ToCountSql();

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
        /// 获取分组语句
        /// </summary>
        /// <returns></returns>
        string GetGroupBy();

        /// <summary>
        /// 获取排序语句
        /// </summary>
        /// <returns></returns>
        string GetOrderBy();

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <param name="columns">列名，范例：a.AppId As Id,a.Name</param>
        /// <param name="tableAlias">表别名</param>
        /// <returns></returns>
        ISqlBuilder Select(string columns, string tableAlias = null);

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="columns">列名，范例：t => new object[] { t.Id, t.Name }</param>
        /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
        /// <returns></returns>
        ISqlBuilder Select<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias = false)
            where TEntity : class;

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">列名，范例：t => t.Name</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder Select<TEntity>(Expression<Func<TEntity, object>> column, string columnAlias = null)
            where TEntity : class;

        /// <summary>
        /// 添加到Select子句
        /// </summary>
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlBuilder AppendSelect(string sql);

        /// <summary>
        /// 添加到Select子句
        /// </summary>
        /// <param name="builder">Sql生成器</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder AppendSelect(ISqlBuilder builder, string columnAlias);

        /// <summary>
        /// 添加到Select子句
        /// </summary>
        /// <param name="action">子查询操作</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlBuilder AppendSelect(Action<ISqlBuilder> action, string columnAlias);

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
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
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
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlBuilder AppendJoin(string sql);

        /// <summary>
        /// 添加到内连接子句
        /// </summary>
        /// <param name="builder">Sql生成器</param>
        /// <param name="alias">表别名</param>
        /// <returns></returns>
        ISqlBuilder AppendJoin(ISqlBuilder builder, string alias);

        /// <summary>
        /// 添加到内连接子句
        /// </summary>
        /// <param name="action">子查询操作</param>
        /// <param name="alias">表别名</param>
        /// <returns></returns>
        ISqlBuilder AppendJoin(Action<ISqlBuilder> action, string alias);

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
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlBuilder AppendLeftJoin(string sql);

        /// <summary>
        /// 添加到左外连接子句
        /// </summary>
        /// <param name="builder">Sql生成器</param>
        /// <param name="alias">表别名</param>
        /// <returns></returns>
        ISqlBuilder AppendLeftJoin(ISqlBuilder builder, string alias);

        /// <summary>
        /// 添加到左外连接子句
        /// </summary>
        /// <param name="action">子查询操作</param>
        /// <param name="alias">表别名</param>
        /// <returns></returns>
        ISqlBuilder AppendLeftJoin(Action<ISqlBuilder> action, string alias);

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
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlBuilder AppendRightJoin(string sql);

        /// <summary>
        /// 添加到右外连接子句
        /// </summary>
        /// <param name="builder">Sql生成器</param>
        /// <param name="alias">表别名</param>
        /// <returns></returns>
        ISqlBuilder AppendRightJoin(ISqlBuilder builder, string alias);

        /// <summary>
        /// 添加到右外连接子句
        /// </summary>
        /// <param name="action">子查询操作</param>
        /// <param name="alias">表别名</param>
        /// <returns></returns>
        ISqlBuilder AppendRightJoin(Action<ISqlBuilder> action, string alias);

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
        /// <param name="left">左表列名，范例：t => t.Name</param>
        /// <param name="right">右表列名，范例：t => t.Name</param>
        /// <param name="operator">条件运算符</param>
        /// <returns></returns>
        ISqlBuilder On<TLeft, TRight>(Expression<Func<TLeft, object>> left, Expression<Func<TRight, object>> right,
            Operator @operator = Operator.Equal) where TLeft : class where TRight : class;

        /// <summary>
        /// 设置连接条件
        /// </summary>
        /// <typeparam name="TLeft">左表实体类型</typeparam>
        /// <typeparam name="TRight">右表实体类型</typeparam>
        /// <param name="expression">条件表达式，范例：(l,r) => l.Id == r.Id</param>
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
        /// Or连接条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="conditions">查询条件</param>
        /// <returns></returns>
        ISqlBuilder Or<TEntity>(params Expression<Func<TEntity, bool>>[] conditions);

        /// <summary>
        /// Or连接条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="conditions">查询条件，如果表达式中的值为空，泽忽略该查询条件</param>
        /// <returns></returns>
        ISqlBuilder OrIfNotEmpty<TEntity>(params Expression<Func<TEntity, bool>>[] conditions);

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <returns></returns>
        ISqlBuilder Where(ICondition condition);

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
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlBuilder Where<TEntity>(Expression<Func<TEntity, object>> expression, object value,
            Operator @operator = Operator.Equal) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">查询条件表达式，范例：t => t.Name.Contains("a") &amp;&amp; ( t.Code == "b" || t.Age>1 )</param>
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
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
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
        /// <param name="expression">查询条件表达式，范例：t => t.Name.Contains("a") &amp;&amp; ( t.Code == "b" || t.Age>1 )</param>
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
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
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
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlBuilder AppendWhere(string sql);

        /// <summary>
        /// 设置相等查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Equal(string column, object value);

        /// <summary>
        /// 设置相等查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Equal<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置不相等查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder NotEqual(string column, object value);

        /// <summary>
        /// 设置不相等查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder NotEqual<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置大于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Greater(string column, object value);

        /// <summary>
        /// 设置大于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Greater<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置大于等于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder GreaterEqual(string column, object value);

        /// <summary>
        /// 设置大于等于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder GreaterEqual<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置小于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Less(string column, object value);

        /// <summary>
        /// 设置小于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Less<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置小于等于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder LessEqual(string column, object value);

        /// <summary>
        /// 设置小于等于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder LessEqual<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置模糊匹配查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Contains(string column, object value);

        /// <summary>
        /// 设置模糊匹配查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Contains<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置头匹配查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Starts(string column, object value);

        /// <summary>
        /// 设置头匹配查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Starts<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置尾匹配查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Ends(string column, object value);

        /// <summary>
        /// 设置尾匹配查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlBuilder Ends<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置Is Null查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        ISqlBuilder IsNull(string column);

        /// <summary>
        /// 设置Is Null查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <returns></returns>
        ISqlBuilder IsNull<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

        /// <summary>
        /// 设置Is Not Null查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        ISqlBuilder IsNotNull(string column);

        /// <summary>
        /// 设置Is Not Null查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <returns></returns>
        ISqlBuilder IsNotNull<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

        /// <summary>
        /// 设置空条件，范例：[Name] Is Null Or [Name]=''
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        ISqlBuilder IsEmpty(string column);

        /// <summary>
        /// 设置空条件，范例：[Name] Is Null Or [Name]=''
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <returns></returns>
        ISqlBuilder IsEmpty<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

        /// <summary>
        /// 设置非空条件，范例：[Name] Is Null Or [Name]&lt;&gt;''
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        ISqlBuilder IsNotEmpty(string column);

        /// <summary>
        /// 设置非空条件，范例：[Name] Is Null Or [Name]&lt;&gt;''
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <returns></returns>
        ISqlBuilder IsNotEmpty<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

        /// <summary>
        /// 设置In条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        ISqlBuilder In(string column, IEnumerable<object> values);

        /// <summary>
        /// 设置In条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        ISqlBuilder In<TEntity>(Expression<Func<TEntity, object>> expression, IEnumerable<object> values) where TEntity : class;

        /// <summary>
        /// 设置Not In条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        ISqlBuilder NotIn(string column, IEnumerable<object> values);

        /// <summary>
        /// 设置Not In条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        ISqlBuilder NotIn<TEntity>(Expression<Func<TEntity, object>> expression, IEnumerable<object> values) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, int? min, int? max,
            Boundary boundary = Boundary.Both) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, long? min, long? max,
            Boundary boundary = Boundary.Both) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, float? min, float? max,
            Boundary boundary = Boundary.Both) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, double? min, double? max,
            Boundary boundary = Boundary.Both) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, decimal? min, decimal? max,
            Boundary boundary = Boundary.Both) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="includeTime">是否包含时间</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between<TEntity>(Expression<Func<TEntity, object>> expression, DateTime? min, DateTime? max,
            bool includeTime = true, Boundary? boundary = null) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between(string column, int? min, int? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between(string column, long? min, long? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between(string column, float? min, float? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between(string column, double? min, double? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between(string column, decimal? min, decimal? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="includeTime">是否包含时间</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlBuilder Between(string column, DateTime? min, DateTime? max, bool includeTime = true, Boundary? boundary = null);

        /// <summary>
        /// 分组
        /// </summary>
        /// <param name="columns">分组字段，范例：a.Id,b.Name</param>
        /// <param name="having">分组条件，范例：Count(*) > 1</param>
        /// <returns></returns>
        ISqlBuilder GroupBy(string columns, string having = null);

        /// <summary>
        /// 分组
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="columns">分组字段</param>
        /// <returns></returns>
        ISqlBuilder GroupBy<TEntity>(params Expression<Func<TEntity, object>>[] columns);

        /// <summary>
        /// 分组
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">分组字段，范例：t => t.Name</param>
        /// <param name="having">分组条件，范例：Count(*) > 1</param>
        /// <returns></returns>
        ISqlBuilder GroupBy<TEntity>(Expression<Func<TEntity, object>> column, string having = null)
            where TEntity : class;

        /// <summary>
        /// 添加到GroupBy子句
        /// </summary>
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlBuilder AppendGroupBy(string sql);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="order">排序列表，范例：a.Id, b.Name desc</param>
        /// <param name="tableAlias">表别名</param>
        /// <returns></returns>
        ISqlBuilder OrderBy(string order, string tableAlias = null);

        /// <summary>
        /// 排序
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">排序列，范例：t => t.Name</param>
        /// <param name="desc">是否降序</param>
        /// <returns></returns>
        ISqlBuilder OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc = false);

        /// <summary>
        /// 添加到OrderBy子句
        /// </summary>
        /// <param name="sql">排序列表，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlBuilder AppendOrderBy(string sql);

        /// <summary>
        /// 设置分页
        /// </summary>
        /// <param name="pager">分页参数</param>
        /// <returns></returns>
        ISqlBuilder Page(IPager pager);
    }
}
