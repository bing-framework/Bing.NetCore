using System.Text;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// 提供 SQL 查询条件的参数、列和值管理及渲染基础能力。
/// </summary>
public abstract class SqlConditionBase : ISqlCondition
{
    /// <summary>
    /// 参数化渲染后固定的参数名称。
    /// </summary>
    private string _parameterName;

    /// <summary>
    /// 使用参数管理器、列和值初始化一个 <see cref="SqlConditionBase"/> 实例。
    /// </summary>
    /// <param name="parameterManager">用于保存参数的参数管理器。</param>
    /// <param name="column">条件左侧列名。</param>
    /// <param name="value">条件右侧值或子查询。</param>
    /// <param name="isParameterization">是否将右侧值作为 SQL 参数处理。</param>
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
    /// 获取条件使用的参数管理器。
    /// </summary>
    protected IParameterManager ParameterManager { get; }

    /// <summary>
    /// 获取条件左侧列名。
    /// </summary>
    protected string Column { get; }

    /// <summary>
    /// 获取条件右侧值或子查询。
    /// </summary>
    protected object Value { get; }

    /// <summary>
    /// 是否参数化
    /// </summary>
    protected bool IsParameterization { get; }

    /// <inheritdoc />
    public virtual void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var startIndex = builder.Length;
        try
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
        catch
        {
            builder.Remove(startIndex, builder.Length - startIndex);
            throw;
        }
    }

    /// <summary>
    /// 添加Sql生成器
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    /// <param name="column">列名</param>
    /// <param name="sqlBuilder">Sql生成器</param>
    protected virtual void AppendSqlBuilder(StringBuilder builder, string column, ISqlBuilder sqlBuilder)
    {
        throw new NotSupportedException(
            $"条件类型 {GetType().Name} 当前不支持将 ISqlBuilder 作为右值。请使用支持子查询的 In、NotIn 或 Exists 条件。");
    }

    /// <summary>
    /// 添加参数化条件
    /// </summary>
    /// <param name="builder">字符串生成器</param>
    /// <remarks>
    /// 仅在 SQL 条件写入成功后提交参数和缓存参数名称，
    /// 避免派生条件渲染异常污染后续条件的参数状态。
    /// </remarks>
    protected virtual void AppendParameterizedCondition(StringBuilder builder)
    {
        var paramName = _parameterName ?? GenerateParamName();
        var value = GetValue();
        AppendCondition(builder, Column, paramName);
        ParameterManager.Add(paramName, value);
        _parameterName ??= paramName;
    }

    /// <summary>
    /// 创建参数名
    /// </summary>
    /// <returns>返回由参数管理器生成的新参数名称。</returns>
    protected virtual string GenerateParamName() => ParameterManager.GenerateName();

    /// <summary>
    /// 获取参数值
    /// </summary>
    /// <returns>返回当前条件的右侧值。</returns>
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
