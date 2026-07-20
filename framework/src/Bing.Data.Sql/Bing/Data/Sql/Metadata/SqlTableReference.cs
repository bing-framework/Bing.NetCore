using Bing.Data.Enums;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 结构化 SQL 表引用
/// </summary>
public sealed class SqlTableReference
{
    /// <summary>
    /// 实体类型。
    /// </summary>
    public Type EntityType { get; init; }

    /// <summary>
    /// 执行数据源标识
    /// </summary>
    public string DbKey { get; init; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType? DatabaseType { get; init; }

    /// <summary>
    /// 数据库目录或同连接限定名
    /// </summary>
    public string Catalog { get; init; }

    /// <summary>
    /// 物理架构
    /// </summary>
    public string PhysicalSchema { get; init; }

    /// <summary>
    /// 逻辑架构
    /// </summary>
    public string LogicalSchema { get; init; }

    /// <summary>
    /// 原始模型表名
    /// </summary>
    public string TableName { get; init; }

    /// <summary>
    /// 命名策略解析后的表名
    /// </summary>
    public string ResolvedTableName { get; init; }

    /// <summary>
    /// Oracle 数据库链接
    /// </summary>
    public string DatabaseLink { get; init; }

    /// <summary>
    /// SQLite 已附加数据库别名
    /// </summary>
    public string AttachedAlias { get; init; }

    /// <summary>
    /// 表别名
    /// </summary>
    public string Alias { get; init; }

    /// <summary>
    /// 返回带别名的引用副本
    /// </summary>
    /// <param name="alias">表别名</param>
    public SqlTableReference WithAlias(string alias) => new()
    {
        EntityType = EntityType,
        DbKey = DbKey,
        DatabaseType = DatabaseType,
        Catalog = Catalog,
        PhysicalSchema = PhysicalSchema,
        LogicalSchema = LogicalSchema,
        TableName = TableName,
        ResolvedTableName = ResolvedTableName,
        DatabaseLink = DatabaseLink,
        AttachedAlias = AttachedAlias,
        Alias = alias
    };

    /// <summary>
    /// 返回带物理架构的引用副本
    /// </summary>
    /// <param name="physicalSchema">物理架构</param>
    public SqlTableReference WithPhysicalSchema(string physicalSchema) => new()
    {
        EntityType = EntityType,
        DbKey = DbKey,
        DatabaseType = DatabaseType,
        Catalog = Catalog,
        PhysicalSchema = physicalSchema,
        LogicalSchema = LogicalSchema,
        TableName = TableName,
        ResolvedTableName = ResolvedTableName,
        DatabaseLink = DatabaseLink,
        AttachedAlias = AttachedAlias,
        Alias = Alias
    };
}