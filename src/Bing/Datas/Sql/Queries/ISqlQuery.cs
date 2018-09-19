using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Bing.Datas.Queries;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
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
        /// 清空并初始化，用于多次执行不同Sql语句，当执行完一个Sql语句后，清空即可继续使用
        /// </summary>
        void Clear();

        /// <summary>
        /// 获取调试Sql语句
        /// </summary>
        /// <returns></returns>
        string GetDebugSql();

        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        /// <returns></returns>
        ISqlBuilder NewBuilder();

        /// <summary>
        /// 获取字符串
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        string ToString(IDbConnection connection = null);

        /// <summary>
        /// 获取字符串
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <returns></returns>
        Task<string> ToStringAsync(IDbConnection connection = null);

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
        /// <param name="columns">列名，范例：a.AppId As Id, a.Name</param>
        /// <param name="tableAlias">表别名</param>
        /// <returns></returns>
        ISqlQuery Select(string columns, string tableAlias = null);

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="columns">列名，范例：t => new object[] { t.Id, t.Name }</param>
        /// <returns></returns>
        ISqlQuery Select<TEntity>(Expression<Func<TEntity, object[]>> columns) where TEntity : class;

        /// <summary>
        /// 设置列名
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">列名，范例：t => t.Name</param>
        /// <param name="columnAlias">列别名</param>
        /// <returns></returns>
        ISqlQuery Select<TEntity>(Expression<Func<TEntity, object>> column, string columnAlias = null)
            where TEntity : class;

        /// <summary>
        /// 添加到Select子句
        /// </summary>
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
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
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
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
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
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
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
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
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
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
        /// <param name="condition">查询条件</param>
        /// <returns></returns>
        ISqlQuery Where(ICondition condition);

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
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <param name="operator">运算符</param>
        /// <returns></returns>
        ISqlQuery Where<TEntity>(Expression<Func<TEntity, object>> expression, object value,
            Operator @operator = Operator.Equal) where TEntity : class;

        /// <summary>
        /// 设置查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">查询条件表达式，范例：t => t.Name.Contains("a") &amp;&amp; ( t.Code == "b" || t.Age>1 )</param>
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
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
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
        /// <param name="expression">查询条件表达式，范例：t => t.Name.Contains("a") &amp;&amp; ( t.Code == "b" || t.Age>1 )</param>
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
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
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
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlQuery AppendWhere(string sql);

        /// <summary>
        /// 设置相等查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery Equal(string column, object value);

        /// <summary>
        /// 设置相等查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery Equal<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置不相等查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery NotEqual(string column, object value);

        /// <summary>
        /// 设置不相等查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery NotEqual<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置大于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery Greater(string column, object value);

        /// <summary>
        /// 设置大于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery Greater<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置大于等于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery GreaterEqual(string column, object value);

        /// <summary>
        /// 设置大于等于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery GreaterEqual<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置小于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery Less(string column, object value);

        /// <summary>
        /// 设置小于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery Less<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置小于等于查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery LessEqual(string column, object value);

        /// <summary>
        /// 设置小于等于查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery LessEqual<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置模糊匹配查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery Contains(string column, object value);

        /// <summary>
        /// 设置模糊匹配查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery Contains<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置头匹配查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery Starts(string column, object value);

        /// <summary>
        /// 设置头匹配查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery Starts<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置尾匹配查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery Ends(string column, object value);

        /// <summary>
        /// 设置尾匹配查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        ISqlQuery Ends<TEntity>(Expression<Func<TEntity, object>> expression, object value) where TEntity : class;

        /// <summary>
        /// 设置Is Null查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        ISqlQuery IsNull(string column);

        /// <summary>
        /// 设置Is Null查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <returns></returns>
        ISqlQuery IsNull<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

        /// <summary>
        /// 设置Is Not Null查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        ISqlQuery IsNotNull(string column);

        /// <summary>
        /// 设置Is Not Null查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <returns></returns>
        ISqlQuery IsNotNull<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

        /// <summary>
        /// 设置空条件，范例：[Name] Is Null Or [Name]=''
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        ISqlQuery IsEmpty(string column);

        /// <summary>
        /// 设置空条件，范例：[Name] Is Null Or [Name]=''
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <returns></returns>
        ISqlQuery IsEmpty<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

        /// <summary>
        /// 设置非空条件，范例：[Name] Is Null Or [Name]&lt;&gt;''
        /// </summary>
        /// <param name="column">列名</param>
        /// <returns></returns>
        ISqlQuery IsNotEmpty(string column);

        /// <summary>
        /// 设置非空条件，范例：[Name] Is Null Or [Name]&lt;&gt;''
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <returns></returns>
        ISqlQuery IsNotEmpty<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class;

        /// <summary>
        /// 设置In条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        ISqlQuery In(string column, IEnumerable<object> values);

        /// <summary>
        /// 设置In条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="values">值集合</param>
        /// <returns></returns>
        ISqlQuery In<TEntity>(Expression<Func<TEntity, object>> expression, IEnumerable<object> values) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="expression">列名表达式，范例：t => t.Name</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlQuery Between<TEntity>(Expression<Func<TEntity, object>> expression, int? min, int? max,
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
        ISqlQuery Between<TEntity>(Expression<Func<TEntity, object>> expression, long? min, long? max,
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
        ISqlQuery Between<TEntity>(Expression<Func<TEntity, object>> expression, float? min, float? max,
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
        ISqlQuery Between<TEntity>(Expression<Func<TEntity, object>> expression, double? min, double? max,
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
        ISqlQuery Between<TEntity>(Expression<Func<TEntity, object>> expression, decimal? min, decimal? max,
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
        ISqlQuery Between<TEntity>(Expression<Func<TEntity, object>> expression, DateTime? min, DateTime? max,
            bool includeTime = true, Boundary? boundary = null) where TEntity : class;

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlQuery Between(string column, int? min, int? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlQuery Between(string column, long? min, long? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlQuery Between(string column, float? min, float? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlQuery Between(string column, double? min, double? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlQuery Between(string column, decimal? min, decimal? max, Boundary boundary = Boundary.Both);

        /// <summary>
        /// 设置范围查询条件
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="includeTime">是否包含时间</param>
        /// <param name="boundary">包含边界</param>
        /// <returns></returns>
        ISqlQuery Between(string column, DateTime? min, DateTime? max, bool includeTime = true, Boundary? boundary = null);

        /// <summary>
        /// 分组
        /// </summary>
        /// <param name="group">分组字段，范例：a.Id,b.Name</param>
        /// <param name="having">分组条件，范例：Count(*) > 1</param>
        /// <returns></returns>
        ISqlQuery GroupBy(string group, string having = null);

        /// <summary>
        /// 分组
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">分组字段，范例：t => t.Name</param>
        /// <param name="having">分组条件，范例：Count(*) > 1</param>
        /// <returns></returns>
        ISqlQuery GroupBy<TEntity>(Expression<Func<TEntity, object>> column, string having = null)
            where TEntity : class;

        /// <summary>
        /// 添加到GroupBy子句
        /// </summary>
        /// <param name="sql">Sql语句，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlQuery AppendGroupBy(string sql);

        /// <summary>
        /// 排序
        /// </summary>
        /// <param name="order">排序列表，范例：a.Id, b.Name desc</param>
        /// <returns></returns>
        ISqlQuery OrderBy(string order);

        /// <summary>
        /// 排序
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="column">排序列，范例：t => t.Name</param>
        /// <param name="desc">是否降序</param>
        /// <returns></returns>
        ISqlQuery OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc = false);

        /// <summary>
        /// 添加到OrderBy子句
        /// </summary>
        /// <param name="sql">排序列表，说明：将会原样添加到Sql中，不会进行任何处理</param>
        /// <returns></returns>
        ISqlQuery AppendOrderBy(string sql);
    }
}
