using System.Text;
using Bing.Data.Queries;
using Bing.Data.Sql.Builders.Params;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// 范围过滤条件
/// </summary>
public class SegmentSqlCondition : ISqlCondition
{
    /// <summary>
    /// 参数管理器
    /// </summary>
    protected readonly IParameterManager ParameterManager;

    /// <summary>
    /// 列名
    /// </summary>
    protected readonly string Column;

    /// <summary>
    /// 最小值
    /// </summary>
    protected readonly object MinValue;

    /// <summary>
    /// 最大值
    /// </summary>
    protected readonly object MaxValue;

    /// <summary>
    /// 包含边界
    /// </summary>
    protected Boundary Boundary;

    /// <summary>
    /// 是否参数化
    /// </summary>
    protected readonly bool IsParameterization;

    /// <summary>
    /// 初始化一个<see cref="SegmentSqlCondition"/>类型的实例
    /// </summary>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="column">列名</param>
    /// <param name="minValue">最小值</param>
    /// <param name="maxValue">最大值</param>
    /// <param name="boundary">包含边界</param>
    /// <param name="isParameterization">是否参数化</param>
    public SegmentSqlCondition(IParameterManager parameterManager, string column, object minValue, object maxValue, Boundary boundary, bool isParameterization)
    {
        ParameterManager = parameterManager ?? throw new ArgumentNullException(nameof(parameterManager));
        if (string.IsNullOrWhiteSpace(column))
            throw new ArgumentNullException(nameof(column));
        Column = column;
        MinValue = minValue;
        MaxValue = maxValue;
        Boundary = boundary;
        IsParameterization = isParameterization;
    }

    /// <summary>
    /// 添加到字符串生成器
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    public void AppendTo(StringBuilder builder)
    {
        new AndSqlCondition(CreateLeftCondition(), CreateRightCondition()).AppendTo(builder);
    }

    /// <summary>
    /// 创建左条件
    /// </summary>
    private ISqlCondition CreateLeftCondition()
    {
        if (string.IsNullOrWhiteSpace(MinValue.SafeString()))
            return NullSqlCondition.Instance;
        switch (Boundary)
        {
            case Boundary.Left:
                return new GreaterEqualSqlCondition(ParameterManager, Column, MinValue, IsParameterization);
            case Boundary.Both:
                return new GreaterEqualSqlCondition(ParameterManager, Column, MinValue, IsParameterization);
            default:
                return new GreaterSqlCondition(ParameterManager, Column, MinValue, IsParameterization);
        }
    }

    /// <summary>
    /// 创建右条件
    /// </summary>
    private ISqlCondition CreateRightCondition()
    {
        if (string.IsNullOrWhiteSpace(MaxValue.SafeString()))
            return NullSqlCondition.Instance;
        switch (Boundary)
        {
            case Boundary.Right:
                return new LessEqualSqlCondition(ParameterManager, Column, MaxValue, IsParameterization);
            case Boundary.Both:
                return new LessEqualSqlCondition(ParameterManager, Column, MaxValue, IsParameterization);
            default:
                return new LessSqlCondition(ParameterManager, Column, MaxValue, IsParameterization);
        }
    }
}
