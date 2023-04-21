using System.Text;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// 空查询条件
/// </summary>
public class NullSqlCondition : ISqlCondition
{
    /// <summary>
    /// 封闭构造函数
    /// </summary>
    private NullSqlCondition()
    {
    }

    /// <summary>
    /// 空查询条件实例
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static readonly ISqlCondition Instance = new NullSqlCondition();

    /// <summary>
    /// 添加到字符串生成器
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    public void AppendTo(StringBuilder builder)
    {
    }
}
