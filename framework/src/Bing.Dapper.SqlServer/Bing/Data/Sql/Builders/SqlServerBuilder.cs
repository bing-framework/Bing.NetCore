using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
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
    public SqlServerBuilder(IEntityMetadata metadata = null, ITableDatabase tableDatabase = null, IParameterManager parameterManager = null) : base(metadata, tableDatabase, parameterManager) { }

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
    public override ISqlBuilder New() => new SqlServerBuilder(EntityMetadata, TableDatabase, ParameterManager);

    /// <inheritdoc />
    protected override string CreateLimitSql() => $"Offset {GetOffsetParam()} Rows Fetch Next {GetLimitParam()} Rows Only";
}
