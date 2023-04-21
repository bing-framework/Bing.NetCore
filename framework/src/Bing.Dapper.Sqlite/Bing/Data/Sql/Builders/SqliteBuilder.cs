using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Matedatas;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Sqlite Sql生成器
/// </summary>
public class SqliteBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个<see cref="SqliteBuilder"/>类型的实例
    /// </summary>
    /// <param name="metadata">实体元数据解析器</param>
    /// <param name="tableDatabase">表数据库</param>
    /// <param name="parameterManager">参数管理器</param>
    public SqliteBuilder(IEntityMatedata metadata = null
        , ITableDatabase tableDatabase = null
        , IParameterManager parameterManager = null)
        : base(metadata, tableDatabase, parameterManager)
    {
    }

    /// <inheritdoc />
    protected override IDialect GetDialect() => SqliteDialect.Instance;

    /// <inheritdoc />
    public override ISqlBuilder Clone()
    {
        var sqlBuilder = new SqliteBuilder();
        sqlBuilder.Clone(this);
        return sqlBuilder;
    }

    /// <inheritdoc />
    public override ISqlBuilder New() => new SqliteBuilder(EntityMetadata, TableDatabase, ParameterManager);

    /// <inheritdoc />
    protected override string CreateLimitSql() => $"Limit {GetLimitParam()} OFFSET {GetOffsetParam()}";

    /// <inheritdoc />
    protected override IFromClause CreateFromClause() =>
        new SqliteFromClause(this, GetDialect(), EntityResolver, AliasRegister, TableDatabase);

    /// <inheritdoc />
    protected override IJoinClause CreateJoinClause() =>
        new SqliteJoinClause(this, GetDialect(), EntityResolver, AliasRegister, ParameterManager, TableDatabase);

    /// <inheritdoc />
    protected override string GetCteKeyWord() => "With Recursive";
}
