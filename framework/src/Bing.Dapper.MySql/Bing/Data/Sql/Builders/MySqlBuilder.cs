using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Matedatas;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// MySql Sql 生成器
/// </summary>
public class MySqlBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个<see cref="MySqlBuilder"/>类型的实例
    /// </summary>
    /// <param name="matedata">实体元数据解析器</param>
    /// <param name="tableDatabase">表数据库</param>
    /// <param name="parameterManager">参数管理器</param>
    public MySqlBuilder(IEntityMatedata matedata = null, ITableDatabase tableDatabase = null, IParameterManager parameterManager = null)
        : base(matedata, tableDatabase, parameterManager)
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
    public override ISqlBuilder New() => new MySqlBuilder(EntityMatedata, TableDatabase, ParameterManager);

    /// <inheritdoc />
    protected override string CreateLimitSql() => $"Limit {GetLimitParam()} OFFSET {GetOffsetParam()}";

    /// <inheritdoc />
    protected override string GetCteKeyWord() => "With Recursive";

    /// <inheritdoc />
    protected override IJoinClause CreateJoinClause() =>
        new MySqlJoinClause(this, GetDialect(), EntityResolver, AliasRegister, ParameterManager,
            TableDatabase);
}
