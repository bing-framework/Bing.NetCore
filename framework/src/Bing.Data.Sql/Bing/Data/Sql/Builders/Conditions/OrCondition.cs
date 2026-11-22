using Bing.Data.Sql.Builders.Core;

﻿namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// Or连接条件
/// </summary>
public class OrCondition : ICondition, ISqlBoundCondition
{
    /// <summary>
    /// 左操作数
    /// </summary>
    private readonly ICondition _left;

    /// <summary>
    /// 右操作数
    /// </summary>
    private readonly ICondition _right;

    /// <summary>
    /// 初始化一个 <see cref="OrCondition"/> 类型的实例。
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    public OrCondition(string left, string right)
    {
        _left = new SqlCondition(left);
        _right = new SqlCondition(right);
    }

    /// <summary>
    /// 初始化一个 <see cref="OrCondition"/> 类型的实例。
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    public OrCondition(ICondition left, ICondition right)
    {
        _left = SqlConditionSnapshot.Capture(left);
        _right = SqlConditionSnapshot.Capture(right);
    }

    /// <inheritdoc />
    IEnumerable<SqlProjectionColumn> ISqlBoundCondition.Columns => SqlConditionSnapshot.Columns(_left).Concat(SqlConditionSnapshot.Columns(_right));

    /// <inheritdoc />
    ICondition ISqlBoundCondition.Rebind(SqlClauseContext context) => new OrCondition(SqlConditionSnapshot.Rebind(_left, context), SqlConditionSnapshot.Rebind(_right, context));

    /// <inheritdoc />
    public string GetCondition()
    {
        var left = _left?.GetCondition();
        var right = _right?.GetCondition();
        if (string.IsNullOrWhiteSpace(left))
            return right;
        if (string.IsNullOrWhiteSpace(right))
            return left;
        return $"({left} Or {right})";
    }
}