using System.Linq.Expressions;
using Bing.Expressions;

namespace Bing.Data.Queries.Conditions;

/// <summary>
/// 将可选最小值和最大值组合为实体属性范围条件的基类。
/// </summary>
/// <typeparam name="TEntity">要筛选的实体类型。</typeparam>
/// <typeparam name="TProperty">参与筛选的实体属性类型。</typeparam>
/// <typeparam name="TValue">用于比较最小值和最大值的值类型。</typeparam>
public abstract class SegmentConditionBase<TEntity, TProperty, TValue> : ICondition<TEntity> 
    where TEntity : class 
    where TValue : struct
{
    /// <summary>
    /// 属性表达式
    /// </summary>
    private readonly Expression<Func<TEntity, TProperty>> _propertyExpression;

    /// <summary>
    /// 表达式生成器
    /// </summary>
    private readonly PredicateExpressionBuilder<TEntity> _builder;

    /// <summary>
    /// 最小值
    /// </summary>
    private TValue? _min;

    /// <summary>
    /// 最大值
    /// </summary>
    private TValue? _max;

    /// <summary>
    /// 包含边界
    /// </summary>
    private readonly Boundary _boundary;

    /// <summary>
    /// 初始化一个<see cref="SegmentConditionBase{TEntity,TProperty,TValue}"/>类型的实例
    /// </summary>
    /// <param name="propertyExpression">属性表达式</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    protected SegmentConditionBase(Expression<Func<TEntity, TProperty>> propertyExpression
        , TValue? min
        , TValue? max
        , Boundary boundary)
    {
        _builder = new PredicateExpressionBuilder<TEntity>();
        _propertyExpression = propertyExpression;
        _min = min;
        _max = max;
        _boundary = boundary;
    }

    /// <summary>
    /// 获取范围条件对应的实体属性类型。
    /// </summary>
    /// <returns>属性表达式表示的属性类型。</returns>
    protected Type GetPropertyType() => Lambdas.GetType(_propertyExpression);

    /// <summary>
    /// 获取当前范围条件的端点包含规则。
    /// </summary>
    /// <returns>范围端点的包含规则。</returns>
    protected Boundary GetBoundary() => _boundary;

    /// <summary>
    /// 根据当前最小值、最大值和端点规则创建查询条件。
    /// </summary>
    /// <returns>用于筛选目标实体的谓词表达式。</returns>
    public Expression<Func<TEntity, bool>> GetCondition()
    {
        _builder.Clear();
        Adjust(_min, _max);
        CreateLeftExpression();
        CreateRightExpression();
        return _builder.ToLambda();
    }

    /// <summary>
    /// 当最小值大于最大值时交换两个端点。
    /// </summary>
    /// <param name="min">待校正的最小值。</param>
    /// <param name="max">待校正的最大值。</param>
    private void Adjust(TValue? min, TValue? max)
    {
        if (IsMinGreaterMax(min, max) == false)
            return;
        _min = max;
        _max = min;
    }

    /// <summary>
    /// 确定最小值是否大于最大值。
    /// </summary>
    /// <param name="min">要比较的最小值。</param>
    /// <param name="max">要比较的最大值。</param>
    /// <returns>最小值大于最大值时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    protected abstract bool IsMinGreaterMax(TValue? min, TValue? max);

    /// <summary>
    /// 创建左操作数，即 t => t.Property >= Min
    /// </summary>
    private void CreateLeftExpression()
    {
        if (_min == null)
            return;
        _builder.Append(_propertyExpression, CreateLeftOperator(_boundary), GetMinValueExpression());
    }

    /// <summary>
    /// 根据左端点包含规则创建比较操作符。
    /// </summary>
    /// <param name="boundary">要转换的查询边界。</param>
    /// <returns>用于比较最小值的操作符。</returns>
    protected virtual Operator CreateLeftOperator(Boundary? boundary)
    {
        switch (boundary)
        {
            case Boundary.Left:
                return Operator.GreaterEqual;

            case Boundary.Both:
                return Operator.GreaterEqual;

            default:
                return Operator.Greater;
        }
    }

    /// <summary>
    /// 获取当前范围的最小值。
    /// </summary>
    /// <returns>最小值；未指定时为 <c>null</c>。</returns>
    protected TValue? GetMinValue() => _min;

    /// <summary>
    /// 创建最小值对应的表达式。
    /// </summary>
    /// <returns>用于构造左侧比较条件的最小值表达式。</returns>
    protected virtual Expression GetMinValueExpression() => Lambdas.Constant(_min, _propertyExpression);

    /// <summary>
    /// 创建右操作数，即 t => t.Property &lt;= Max
    /// </summary>
    private void CreateRightExpression()
    {
        if (_max == null)
            return;
        _builder.Append(_propertyExpression, CreateRightOperator(_boundary), GetMaxValueExpression());
    }

    /// <summary>
    /// 根据右端点包含规则创建比较操作符。
    /// </summary>
    /// <param name="boundary">要转换的查询边界。</param>
    /// <returns>用于比较最大值的操作符。</returns>
    protected virtual Operator CreateRightOperator(Boundary? boundary)
    {
        switch (boundary)
        {
            case Boundary.Right:
                return Operator.LessEqual;

            case Boundary.Both:
                return Operator.LessEqual;

            default:
                return Operator.Less;
        }
    }

    /// <summary>
    /// 获取当前范围的最大值。
    /// </summary>
    /// <returns>最大值；未指定时为 <c>null</c>。</returns>
    protected TValue? GetMaxValue() => _max;

    /// <summary>
    /// 创建最大值对应的表达式。
    /// </summary>
    /// <returns>用于构造右侧比较条件的最大值表达式。</returns>
    protected virtual Expression GetMaxValueExpression() => Lambdas.Constant(_max, _propertyExpression);
}