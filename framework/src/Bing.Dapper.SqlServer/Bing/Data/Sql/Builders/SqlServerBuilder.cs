using Bing.Data;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Sql Server Sql生成器
/// </summary>
public class SqlServerBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个<see cref="SqlServerBuilder"/>类型的实例
    /// </summary>
    /// <param name="metadata">实体元数据解析器</param>
    /// <param name="tableDatabase">表数据库</param>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="sqlParameterFactory">Sql 参数工厂</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <param name="options">Sql 配置</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器</param>
    public SqlServerBuilder(IEntityMetadata metadata = null, ITableDatabase tableDatabase = null,
        IParameterManager parameterManager = null, IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, ISqlParameterFactory sqlParameterFactory = null,
        SqlMetadataOptions metadataOptions = null, SqlOptions options = null,
        ISqlDatabaseContextResolver databaseContextResolver = null)
        : base(metadata, tableDatabase, parameterManager, entityMappingResolver, databaseContextAccessor,
            sqlParameterFactory, metadataOptions, options, databaseContextResolver) { }

    /// <inheritdoc />
    protected override IDialect GetDialect() => SqlServerDialect.Instance;

    /// <inheritdoc />
    public override ISqlBuilder Clone()
    {
        var sqlBuilder = new SqlServerBuilder();
        sqlBuilder.Clone(this);
        return sqlBuilder;
    }

    /// <inheritdoc />
    public override ISqlBuilder New() => new SqlServerBuilder(EntityMetadata, TableDatabase, ParameterManager,
        EntityMappingResolver, DatabaseContextAccessor, SqlParameterFactory, MetadataOptions, Options,
        DatabaseContextResolver);

    /// <inheritdoc />
    protected override string CreateLimitSql() => $"Offset {GetOffsetParam()} Rows Fetch Next {GetLimitParam()} Rows Only";
}
