using System.Text;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// Sql相等查询条件
/// </summary>
public class EqualSqlCondition : SqlConditionBase
{
    /// <summary>
    /// 初始化一个<see cref="EqualSqlCondition"/>类型的实例
    /// </summary>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="isParameterization">是否参数化</param>
    public EqualSqlCondition(IParameterManager parameterManager, string column, object value, bool isParameterization)
        : base(parameterManager, column, value, isParameterization)
    {
    }

    /// <inheritdoc />
    public override void AppendTo(StringBuilder builder)
    {
        if (Value == null)
        {
            new IsNullSqlCondition(Column).AppendTo(builder);
            return;
        }
        base.AppendTo(builder);
    }

    /// <inheritdoc />
    protected override void AppendCondition(StringBuilder builder, string column, object value)
    {
        builder.AppendFormat("{0}={1}", column, value);
    }

    /// <inheritdoc />
    protected override void AppendSqlBuilder(StringBuilder builder, string column, ISqlBuilder sqlBuilder)
    {
        builder.AppendFormat("{0}=", column);
        builder.Append("(");
        sqlBuilder.AppendTo(builder);
        builder.Append(")");
    }
}
