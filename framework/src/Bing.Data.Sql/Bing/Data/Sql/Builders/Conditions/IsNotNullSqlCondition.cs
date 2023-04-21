using System.Text;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// Is Not Null查询条件
/// </summary>
public class IsNotNullSqlCondition : ISqlCondition
{
    /// <summary>
    /// 列名
    /// </summary>
    private readonly string _name;

    /// <summary>
    /// 初始化一个<see cref="IsNotNullSqlCondition"/>类型的实例
    /// </summary>
    /// <param name="name">列名</param>
    public IsNotNullSqlCondition(string name) => _name = name;

    /// <summary>
    /// 添加到字符串生成器
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    public void AppendTo(StringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(_name))
            return;
        builder.AppendFormat("{0} Is Not Null", _name);
    }
}
