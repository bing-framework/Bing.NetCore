using System.Text;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// Sql查询条件
/// </summary>
public class SqlSqlCondition : ISqlCondition
{
    /// <summary>
    /// Sql查询条件
    /// </summary>
    private readonly string _condition;

    /// <summary>
    /// 初始化一个<see cref="SqlSqlCondition"/>类型的实例
    /// </summary>
    /// <param name="condition">查询条件</param>
    public SqlSqlCondition(string condition)
    {
        _condition = condition;
    }

    /// <summary>
    /// 添加到字符串生成器
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    public void AppendTo(StringBuilder builder)
    {
        builder.Append(_condition);
    }
}
