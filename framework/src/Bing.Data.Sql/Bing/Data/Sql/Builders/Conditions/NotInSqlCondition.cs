using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// Not In查询条件
/// </summary>
public class NotInSqlCondition : InSqlCondition
{
    /// <summary>
    /// 初始化一个<see cref="NotInSqlCondition"/>类型的实例
    /// </summary>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="isParameterization">是否参数化</param>
    public NotInSqlCondition(IParameterManager parameterManager, string column, object value, bool isParameterization)
        : base(parameterManager, column, value, isParameterization)
    {
    }

    /// <summary>
    /// 获取操作符关键字
    /// </summary>
    protected override string GetOperator() => "Not In";
}
