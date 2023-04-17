using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Matedatas;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// PostgreSql Sql生成器
/// </summary>
public class PostgreSqlBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个<see cref="PostgreSqlBuilder"/>类型的实例
    /// </summary>
    /// <param name="matedata">实体元数据解析器</param>
    /// <param name="tableDatabase">表数据库</param>
    /// <param name="parameterManager">参数管理器</param>
    public PostgreSqlBuilder(IEntityMatedata matedata = null, ITableDatabase tableDatabase = null, IParameterManager parameterManager = null) : base(matedata, tableDatabase, parameterManager) { }

    /// <inheritdoc />
    protected override IDialect GetDialect() => PostgreSqlDialect.Instance;

    /// <inheritdoc />
    public override ISqlBuilder Clone()
    {
        var sqlBuilder = new PostgreSqlBuilder();
        sqlBuilder.Clone(this);
        return sqlBuilder;
    }

    /// <inheritdoc />
    public override ISqlBuilder New() => new PostgreSqlBuilder(EntityMatedata, TableDatabase, ParameterManager);

    /// <inheritdoc />
    protected override string CreateLimitSql() => $"Limit {GetLimitParam()} OFFSET {GetOffsetParam()}";

    /// <inheritdoc />
    protected override IParamLiteralsResolver GetParamLiteralsResolver() => PostgreSqlParamLiteralsResolver.Instance;
}
