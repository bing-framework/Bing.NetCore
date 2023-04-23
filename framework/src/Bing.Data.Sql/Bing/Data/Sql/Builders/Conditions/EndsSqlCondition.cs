using System.Text;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// Sql尾匹配查询条件
/// </summary>
public class EndsSqlCondition: SqlConditionBase
{
    /// <summary>
    /// 初始化一个<see cref="EndsSqlCondition"/>类型的实例
    /// </summary>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="isParameterization">是否参数化</param>
    public EndsSqlCondition(IParameterManager parameterManager, string column, object value, bool isParameterization) 
        : base(parameterManager, column, value, isParameterization)
    {
    }

    /// <summary>
    /// 获取参数值
    /// </summary>
    protected override object GetValue() => $"%{Value}";

    /// <summary>
    /// 添加Sql条件
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    protected override void AppendCondition(StringBuilder builder, string column, object value)
    {
        builder.AppendFormat("{0} Like {1}", column, value);
    }

    /// <summary>
    /// 添加非参数化条件
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    protected override void AppendNonParameterizedCondition(StringBuilder builder)
    {
        AppendCondition(builder, Column, $"'{GetValue()}'");
    }
}
