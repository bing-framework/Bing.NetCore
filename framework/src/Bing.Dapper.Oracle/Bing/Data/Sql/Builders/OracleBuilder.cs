﻿using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
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
    public OracleBuilder(IEntityMetadata metadata = null, ITableDatabase tableDatabase = null, IParameterManager parameterManager = null) 
        : base(metadata, tableDatabase, parameterManager)
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
    public override ISqlBuilder New() => new OracleBuilder(EntityMetadata, TableDatabase, ParameterManager);

    /// <inheritdoc />
    protected override string CreateLimitSql() => $"Limit {GetLimitParam()} OFFSET {GetOffsetParam()}";

    /// <inheritdoc />
    protected override IFromClause CreateFromClause() => new OracleFromClause(this, GetDialect(), EntityResolver, AliasRegister, TableDatabase);

    /// <inheritdoc />
    protected override IJoinClause CreateJoinClause() => new OracleJoinClause(this, GetDialect(), EntityResolver, AliasRegister, ParameterManager,
        TableDatabase);
}
