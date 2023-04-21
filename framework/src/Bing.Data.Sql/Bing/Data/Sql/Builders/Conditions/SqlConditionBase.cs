using System.Text;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// Sql查询条件基类
/// </summary>
public abstract class SqlConditionBase : ISqlCondition
{
    /// <summary>
    /// 初始化一个<see cref="SqlConditionBase"/>类型的实例
    /// </summary>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="isParameterization">是否参数化</param>
    protected SqlConditionBase(IParameterManager parameterManager, string column, object value, bool isParameterization)
    {
        ParameterManager = parameterManager ?? throw new ArgumentNullException(nameof(parameterManager));
        if (string.IsNullOrWhiteSpace(column))
            throw new ArgumentNullException(nameof(column));
        Column = column;
        Value = value;
        IsParameterization = isParameterization;
    }

    /// <summary>
    /// 参数管理器
    /// </summary>
    protected IParameterManager ParameterManager { get; }

    /// <summary>
    /// 列名
    /// </summary>
    protected string Column { get; }

    /// <summary>
    /// 值
    /// </summary>
    protected object Value { get; }

    /// <summary>
    /// 是否参数化
    /// </summary>
    protected bool IsParameterization { get; }

    /// <summary>
    /// 添加到字符串生成器
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    public virtual void AppendTo(StringBuilder builder)
    {
        if (Value is ISqlBuilder sqlBuilder)
        {
            AppendSqlBuilder(builder, Column, sqlBuilder);
            return;
        }
        if (IsParameterization)
        {
            AppendParameterizedCondition(builder);
            return;
        }
        AppendNonParameterizedCondition(builder);
    }

    /// <summary>
    /// 添加Sql生成器
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    /// <param name="column">列名</param>
    /// <param name="sqlBuilder">Sql生成器</param>
    protected virtual void AppendSqlBuilder(StringBuilder builder, string column, ISqlBuilder sqlBuilder) => throw new NotImplementedException();

    /// <summary>
    /// 添加参数化条件
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    protected virtual void AppendParameterizedCondition(StringBuilder builder)
    {
        var paramName = GenerateParamName();
        var value = GetValue();
        ParameterManager.Add(paramName, value);
        AppendCondition(builder, Column, paramName);
    }

    /// <summary>
    /// 创建参数名
    /// </summary>
    protected virtual string GenerateParamName() => ParameterManager.GenerateName();

    /// <summary>
    /// 获取参数值
    /// </summary>
    protected virtual object GetValue() => Value;

    /// <summary>
    /// 添加Sql条件
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    protected abstract void AppendCondition(StringBuilder builder, string column, object value);

    /// <summary>
    /// 添加非参数化条件
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    protected virtual void AppendNonParameterizedCondition(StringBuilder builder) => AppendCondition(builder, Column, GetValue());
}
