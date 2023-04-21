using System.Text;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// Not Exists查询条件
/// </summary>
public class NotExistsSqlCondition : ISqlCondition
{
    /// <summary>
    /// 子查询Sql生成器
    /// </summary>
    private readonly ISqlBuilder _sqlBuilder;

    /// <summary>
    /// 初始化一个<see cref="NotExistsSqlCondition"/>类型的实例
    /// </summary>
    /// <param name="sqlBuilder">子查询Sql生成器</param>
    public NotExistsSqlCondition(ISqlBuilder sqlBuilder) => _sqlBuilder = sqlBuilder;

    /// <summary>
    /// 添加到字符串生成器
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    public void AppendTo(StringBuilder builder)
    {
        if (_sqlBuilder == null)
            return;
        builder.Append("Not Exists (");
        _sqlBuilder.AppendTo(builder);
        builder.Append(")");
    }
}
