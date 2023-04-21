using System.Text;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// Is Null查询条件
/// </summary>
public class IsNullSqlCondition : ISqlCondition
{
    /// <summary>
    /// 列名
    /// </summary>
    private readonly string _name;

    /// <summary>
    /// 初始化一个<see cref="IsNullSqlCondition"/>类型的实例
    /// </summary>
    /// <param name="name">列名</param>
    public IsNullSqlCondition(string name) => _name = name;

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(_name))
            return;
        builder.AppendFormat("{0} Is Null", _name);
    }
}
