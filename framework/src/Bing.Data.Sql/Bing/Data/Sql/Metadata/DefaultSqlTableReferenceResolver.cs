namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 默认 SQL 表引用解析器。
/// </summary>
public sealed class DefaultSqlTableReferenceResolver : ISqlTableReferenceResolver
{
    /// <summary>
    /// 实体映射解析器。
    /// </summary>
    private readonly IEntityMappingResolver _entityMappingResolver;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlTableReferenceResolver"/>类型的实例。
    /// </summary>
    /// <param name="entityMappingResolver">实体映射解析器。</param>
    public DefaultSqlTableReferenceResolver(IEntityMappingResolver entityMappingResolver) =>
        _entityMappingResolver = entityMappingResolver ?? new DefaultEntityMappingResolver();

    /// <inheritdoc />
    public SqlTableReference Resolve(Type entityType, DatabaseContext databaseContext)
    {
        if (entityType == null)
            throw new ArgumentNullException(nameof(entityType));
        return _entityMappingResolver.Resolve(entityType, databaseContext).TableReference;
    }

    /// <inheritdoc />
    public SqlTableReference Resolve(string tableName, DatabaseContext databaseContext)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空。", nameof(tableName));
        return new SqlTableReference
        {
            DbKey = databaseContext?.DbKey,
            DatabaseType = databaseContext?.DataSource?.DatabaseType,
            TableName = tableName,
            ResolvedTableName = tableName
        };
    }
}