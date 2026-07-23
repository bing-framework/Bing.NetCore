using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// From子句
/// </summary>
public interface IFromClause
{
    /// <summary>
    /// 克隆
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="register">实体别名注册器</param>
    IFromClause Clone(ISqlBuilder builder, IEntityAliasRegister register);

    /// <summary>
    /// 设置表名
    /// </summary>
    /// <param name="table">表名</param>
    /// <param name="alias">别名</param>
    void From(string table, string alias = null);

    /// <summary>
    /// 设置结构化表引用。
    /// </summary>
    /// <param name="reference">结构化表引用。</param>
    void From(SqlTableReference reference);

    /// <summary>
    /// 设置表名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="alias">别名</param>
    /// <param name="schema">架构名</param>
    void From<TEntity>(string alias = null, string schema = null) where TEntity : class;

    /// <summary>
    /// 设置子查询表
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    /// <param name="alias">表别名</param>
    void From(ISqlBuilder builder, string alias);

    /// <summary>
    /// 设置子查询表
    /// </summary>
    /// <param name="action">子查询操作</param>
    /// <param name="alias">表别名</param>
    void From(Action<ISqlBuilder> action, string alias);

    /// <summary>
    /// 设置或追加完整原始 From 表达式。
    /// 首次追加时，如果存在结构化 From，将替换原 From；后续追加仅按调用顺序直接拼接，不会自动添加空格、逗号或其他分隔符。
    /// 原始文本不会经过标识符解析、Schema 解析、方言格式化或别名注册；调用方负责 SQL 安全性及显式提供占位符参数。
    /// </summary>
    /// <param name="sql">原始 From 文本；空白文本将被忽略。</param>
    void AppendSql(string sql);

    /// <summary>
    /// 验证
    /// </summary>
    void Validate();

    /// <summary>
    /// 输出Sql
    /// </summary>
    string ToSql();
}