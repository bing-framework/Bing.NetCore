using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Oracle Sql生成器
/// </summary>
public class OracleBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个<see cref="OracleBuilder"/>类型的实例
    /// </summary>
    /// <param name="metadata">实体元数据解析器</param>
    /// <param name="tableDatabase">表数据库</param>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="sqlParameterFactory">Sql 参数工厂</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    public OracleBuilder(IEntityMetadata metadata = null, ITableDatabase tableDatabase = null,
        IParameterManager parameterManager = null, IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, ISqlParameterFactory sqlParameterFactory = null,
        SqlMetadataOptions metadataOptions = null) 
        : base(metadata, tableDatabase, parameterManager, entityMappingResolver, databaseContextAccessor,
            sqlParameterFactory, metadataOptions)
    {
    }

    /// <inheritdoc />
    protected override IDialect GetDialect() => OracleDialect.Instance;

    /// <inheritdoc />
    public override ISqlBuilder Clone()
    {
        var sqlBuilder = new OracleBuilder();
        sqlBuilder.Clone(this);
        return sqlBuilder;
    }

    /// <inheritdoc />
    public override ISqlBuilder New() => new OracleBuilder(EntityMetadata, TableDatabase, ParameterManager,
        EntityMappingResolver, DatabaseContextAccessor, SqlParameterFactory, MetadataOptions);

    /// <inheritdoc />
    protected override string CreateLimitSql() => $"Limit {GetLimitParam()} OFFSET {GetOffsetParam()}";

    /// <inheritdoc />
    protected override IFromClause CreateFromClause() => new OracleFromClause(this, GetDialect(), EntityResolver, AliasRegister, TableDatabase);

    /// <inheritdoc />
    protected override IJoinClause CreateJoinClause() => new OracleJoinClause(this, GetDialect(), EntityResolver, AliasRegister, ParameterManager,
        TableDatabase);
}
