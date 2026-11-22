using System.ComponentModel;

namespace Bing.Data.Sql;

/// <summary>编译期生成的不可变逻辑条件，不包含物理 SQL 或请求参数。</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SqlGeneratedPredicate
{
    private static readonly object ParameterMarker = new();

    private SqlGeneratedPredicate(SqlGeneratedColumn leftColumn, SqlGeneratedColumn rightColumn,
        Operator comparison, object value, SqlGeneratedPredicate left, SqlGeneratedPredicate right, bool isOr, bool isNot = false,
        int parameterIndex = -1)
    {
        LeftColumn = leftColumn;
        RightColumn = rightColumn;
        Comparison = comparison;
        Value = value;
        Left = left;
        Right = right;
        IsOr = isOr;
        IsNot = isNot;
        ParameterIndex = parameterIndex;
    }

    internal SqlGeneratedColumn LeftColumn { get; }
    internal SqlGeneratedColumn RightColumn { get; }
    internal Operator Comparison { get; }
    internal object Value { get; }
    internal bool UsesParameter => ReferenceEquals(Value, ParameterMarker);
    internal int ParameterIndex { get; }
    internal SqlGeneratedPredicate Left { get; }
    internal SqlGeneratedPredicate Right { get; }
    internal bool IsOr { get; }
    internal bool IsNot { get; }

    /// <summary>创建列与列或编译期常量的比较条件。</summary>
    /// <param name="left">比较左侧的列。</param>
    /// <param name="right">比较右侧的列；常量比较时为空。</param>
    /// <param name="comparison">比较运算符。</param>
    /// <param name="value">比较常量；列比较时为空。</param>
    /// <returns>不可变比较描述。</returns>
    public static SqlGeneratedPredicate Compare(SqlGeneratedColumn left, SqlGeneratedColumn right, Operator comparison, object value) =>
        new(left, right, comparison, value, null, null, false);

    /// <summary>创建使用本次查询显式值的列比较条件。</summary>
    /// <param name="left">比较左侧的列。</param>
    /// <param name="comparison">比较运算符。</param>
    /// <returns>不包含请求值的不可变比较描述。</returns>
    public static SqlGeneratedPredicate CompareParameter(SqlGeneratedColumn left, Operator comparison) =>
        new(left, null, comparison, ParameterMarker, null, null, false);

    /// <summary>创建使用本次查询指定值槽的列比较条件。</summary>
    /// <param name="left">比较左侧的列。</param>
    /// <param name="comparison">比较运算符。</param>
    /// <param name="parameterIndex">生成的请求值数组中的槽位。</param>
    /// <returns>不包含请求值的不可变比较描述。</returns>
    public static SqlGeneratedPredicate CompareParameter(SqlGeneratedColumn left, Operator comparison, int parameterIndex) =>
        new(left, null, comparison, ParameterMarker, null, null, false, parameterIndex: parameterIndex);

    /// <summary>使用逻辑与组合条件。</summary>
    /// <param name="left">左条件。</param>
    /// <param name="right">右条件。</param>
    /// <returns>不可变条件组合。</returns>
    public static SqlGeneratedPredicate And(SqlGeneratedPredicate left, SqlGeneratedPredicate right) =>
        new(null, null, default, null, left, right, false);

    /// <summary>使用逻辑或组合条件。</summary>
    /// <param name="left">左条件。</param>
    /// <param name="right">右条件。</param>
    /// <returns>不可变条件组合。</returns>
    public static SqlGeneratedPredicate Or(SqlGeneratedPredicate left, SqlGeneratedPredicate right) =>
        new(null, null, default, null, left, right, true);

    /// <summary>对条件整体取反并保留其逻辑分组。</summary>
    /// <param name="operand">需要取反的条件。</param>
    /// <returns>不可变取反条件。</returns>
    public static SqlGeneratedPredicate Not(SqlGeneratedPredicate operand) =>
        new(null, null, default, null, operand, null, false, true);
}
