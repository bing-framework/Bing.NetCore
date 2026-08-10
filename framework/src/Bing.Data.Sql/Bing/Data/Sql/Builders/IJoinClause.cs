using System.Linq.Expressions;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// 表连接子句
/// </summary>
public interface IJoinClause : ISqlClause, ISqlClauseCloneable<IJoinClause>
{

    /// <summary>
    /// 查找连接项
    /// </summary>
    /// <param name="type">表实体类型</param>
    IJoinOn Find(Type type);

    /// <summary>
    /// 内连接
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    void Join(string table, string alias = null);

    /// <summary>
    /// 内连接结构化表引用。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    void Join(SqlTableReference reference);

    /// <summary>
    /// 内连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    void Join<TEntity>(string alias = null, string schema = null) where TEntity : class;

    /// <summary>
    /// 内连接子查询
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    void Join(ISqlBuilder builder, string alias);

    /// <summary>
    /// 内连接子查询
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    void Join(Action<ISqlBuilder> action, string alias);

    /// <summary>
    /// 追加原始内连接表表达式。
    /// 原始文本不会经过标识符解析、Schema 解析、方言格式化或别名注册；可通过 <see cref="AppendOn"/> 向最后一个连接继续添加条件。
    /// 调用方负责 SQL 安全性及显式提供占位符参数。
    /// </summary>
    /// <param name="sql">原始连接文本；空白文本将被忽略。</param>
    void AppendJoin(string sql);

    /// <summary>
    /// 左外连接
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    void LeftJoin(string table, string alias = null);

    /// <summary>
    /// 左外连接结构化表引用。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    void LeftJoin(SqlTableReference reference);

    /// <summary>
    /// 左外连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    void LeftJoin<TEntity>(string alias = null, string schema = null) where TEntity : class;

    /// <summary>
    /// 左外连接子查询
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    void LeftJoin(ISqlBuilder builder, string alias);

    /// <summary>
    /// 左外连接子查询
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    void LeftJoin(Action<ISqlBuilder> action, string alias);

    /// <summary>
    /// 追加原始左连接表表达式。
    /// 原始文本不会经过标识符解析、Schema 解析、方言格式化或别名注册；可通过 <see cref="AppendOn"/> 向最后一个连接继续添加条件。
    /// 调用方负责 SQL 安全性及显式提供占位符参数。
    /// </summary>
    /// <param name="sql">原始连接文本；空白文本将被忽略。</param>
    void AppendLeftJoin(string sql);

    /// <summary>
    /// 右外连接
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    void RightJoin(string table, string alias = null);

    /// <summary>
    /// 右外连接结构化表引用。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    void RightJoin(SqlTableReference reference);

    /// <summary>
    /// 右外连接
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    void RightJoin<TEntity>(string alias = null, string schema = null) where TEntity : class;

    /// <summary>
    /// 右外连接子查询
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    void RightJoin(ISqlBuilder builder, string alias);

    /// <summary>
    /// 右外连接子查询
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    void RightJoin(Action<ISqlBuilder> action, string alias);

    /// <summary>
    /// 追加原始右连接表表达式。
    /// 原始文本不会经过标识符解析、Schema 解析、方言格式化或别名注册；可通过 <see cref="AppendOn"/> 向最后一个连接继续添加条件。
    /// 调用方负责 SQL 安全性及显式提供占位符参数。
    /// </summary>
    /// <param name="sql">原始连接文本；空白文本将被忽略。</param>
    void AppendRightJoin(string sql);

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <param name="condition">连接条件</param>
    void On(ICondition condition);

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    void On(string column, object value, Operator @operator = Operator.Equal);

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <typeparam name="TLeft">左表实体类型</typeparam>
    /// <typeparam name="TRight">右表实体类型</typeparam>
    /// <param name="left">左表列名</param>
    /// <param name="right">右表列名</param>
    /// <param name="operator">条件运算符</param>
    void On<TLeft, TRight>(Expression<Func<TLeft, object>> left, Expression<Func<TRight, object>> right,
        Operator @operator = Operator.Equal) where TLeft : class where TRight : class;

    /// <summary>
    /// 设置连接条件
    /// </summary>
    /// <typeparam name="TLeft">左表实体类型</typeparam>
    /// <typeparam name="TRight">右表实体类型</typeparam>
    /// <param name="expression">条件表达式</param>
    void On<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> expression)
        where TLeft : class where TRight : class;

    /// <summary>
    /// 向最后一个连接添加 On 原始条件。
    /// 没有连接时此调用会被忽略，条件不会保存并应用到后续连接。
    /// </summary>
    /// <param name="sql">On 条件文本；方括号标识符会按当前方言解析。</param>
    void AppendOn(string sql);

    /// <summary>
    /// 输出Sql
    /// </summary>
    string ToSql();
}