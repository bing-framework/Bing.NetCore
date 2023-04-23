using Bing.Data.Queries;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Params;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Sql查询条件工厂
/// </summary>
public class ConditionFactory : ISqlConditionFactory
{
    /// <summary>
    /// Sql参数管理器
    /// </summary>
    private readonly IParameterManager _parameterManager;

    /// <summary>
    /// 初始化一个<see cref="ConditionFactory"/>类型的实例
    /// </summary>
    /// <param name="parameterManager">Sql参数管理器</param>
    public ConditionFactory(IParameterManager parameterManager)
    {
        _parameterManager = parameterManager ?? throw new ArgumentNullException(nameof(parameterManager));
    }

    /// <summary>
    /// 创建Sql条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="operator">操作符</param>
    /// <param name="isParameterization">是否参数化</param>
    public ISqlCondition Create(string column, object value, Operator @operator, bool isParameterization = true)
    {
        switch (@operator)
        {
            case Operator.Equal:
                return new EqualSqlCondition(_parameterManager, column, value, isParameterization);
            case Operator.NotEqual:
                return new NotEqualSqlCondition(_parameterManager, column, value, isParameterization);
            case Operator.Greater:
                return new GreaterSqlCondition(_parameterManager, column, value, isParameterization);
            case Operator.GreaterEqual:
                return new GreaterSqlCondition(_parameterManager, column, value, isParameterization);
            case Operator.Less:
                return new LessSqlCondition(_parameterManager, column, value, isParameterization);
            case Operator.LessEqual:
                return new LessEqualSqlCondition(_parameterManager, column, value, isParameterization);
            case Operator.Starts:
                return new StartsSqlCondition(_parameterManager, column, value, isParameterization);
            case Operator.Ends:
                return new EndsSqlCondition(_parameterManager, column, value, isParameterization);
            case Operator.Contains:
                return new ContainsSqlCondition(_parameterManager, column, value, isParameterization);
            case Operator.In:
                return new InSqlCondition(_parameterManager, column, value, isParameterization);
            case Operator.NotIn:
                return new NotInSqlCondition(_parameterManager, column, value, isParameterization);
            default:
                throw new NotImplementedException($"运算符 {@operator.Description()} 尚未实现");
        }
    }

    /// <summary>
    /// 创建Sql范围条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="minValue">最小值</param>
    /// <param name="maxValue">最大值</param>
    /// <param name="boundary">包含边界</param>
    /// <param name="isParameterization">是否参数化</param>
    public ISqlCondition Create(string column, object minValue, object maxValue, Boundary boundary, bool isParameterization = true)
    {
        return new SegmentSqlCondition(_parameterManager, column, minValue, maxValue, boundary, isParameterization);
    }
}
