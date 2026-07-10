using Bing.Data;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// MySql Sql 生成器
/// </summary>
public class MySqlBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个<see cref="MySqlBuilder"/>类型的实例
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
    public MySqlBuilder(IEntityMetadata metadata = null, ITableDatabase tableDatabase = null,
        IParameterManager parameterManager = null, IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, ISqlParameterFactory sqlParameterFactory = null,
        SqlMetadataOptions metadataOptions = null, SqlOptions options = null,
        ISqlDatabaseContextResolver databaseContextResolver = null)
        : base(metadata, tableDatabase, parameterManager, entityMappingResolver, databaseContextAccessor,
            sqlParameterFactory, metadataOptions, options, databaseContextResolver)
    {

    }

    /// <inheritdoc />
    protected override IDialect GetDialect() => MySqlDialect.Instance;

    /// <inheritdoc />
    public override ISqlBuilder Clone()
    {
        var result = new MySqlBuilder();
        result.Clone(this);
        return result;
    }

    /// <inheritdoc />
    public override ISqlBuilder New() => new MySqlBuilder(EntityMetadata, TableDatabase, ParameterManager,
        EntityMappingResolver, DatabaseContextAccessor, SqlParameterFactory, MetadataOptions, Options,
        DatabaseContextResolver);

    /// <inheritdoc />
    protected override string CreateLimitSql() => $"Limit {GetLimitParam()} OFFSET {GetOffsetParam()}";

    /// <inheritdoc />
    protected override string GetCteKeyWord() => "With Recursive";

    /// <inheritdoc />
    protected override IFromClause CreateFromClause() => 
        new MySqlFromClause(this, GetDialect(), EntityResolver, AliasRegister, TableDatabase);

    /// <inheritdoc />
    protected override IJoinClause CreateJoinClause() =>
        new MySqlJoinClause(this, GetDialect(), EntityResolver, AliasRegister, ParameterManager,
            TableDatabase, EntityMappingResolver, DatabaseContextAccessor, SqlParameterFactory, MetadataOptions,
            Options, DatabaseContextResolver);
}
