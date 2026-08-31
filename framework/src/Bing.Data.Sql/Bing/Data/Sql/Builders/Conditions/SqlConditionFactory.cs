using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// Sql查询条件工厂
/// </summary>
public static class SqlConditionFactory
{
    /// <summary>
    /// 验证操作符是否存在对应的条件实现。
    /// </summary>
    /// <param name="operator">待验证的操作符。</param>
    internal static void ValidateSupported(Operator @operator)
    {
        switch (@operator)
        {
            case Operator.Equal:
            case Operator.NotEqual:
            case Operator.Greater:
            case Operator.GreaterEqual:
            case Operator.Less:
            case Operator.LessEqual:
            case Operator.In:
            case Operator.NotIn:
            case Operator.Contains:
            case Operator.Starts:
            case Operator.Ends:
                return;
            default:
                var description = @operator.Description();
                var operatorName = string.IsNullOrWhiteSpace(description)
                    ? $"值 {(int)@operator}"
                    : $"{description}（值 {(int)@operator}）";
                throw new NotSupportedException(
                    $"运算符 {operatorName} 当前条件工厂不支持。请使用已支持的 Equal、NotEqual、Greater、GreaterEqual、Less、LessEqual、In、NotIn、Contains、Starts 或 Ends 运算符。");
        }
    }

    /// <summary>
    /// 创建Sql查询条件
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    /// <param name="operator">操作符</param>
    /// <returns>返回与指定运算符匹配的 SQL 条件实例。</returns>
    public static ICondition Create(string left, string right, Operator @operator)
    {
        ValidateSupported(@operator);
        switch (@operator)
        {
            case Operator.Equal:
                if (right == null)
                    return new IsNullCondition(left);
                return new EqualCondition(left, right);
            case Operator.NotEqual:
                if (right == null)
                    return new IsNotNullCondition(left);
                return new NotEqualCondition(left, right);
            case Operator.Greater:
                return new GreaterCondition(left, right);
            case Operator.GreaterEqual:
                return new GreaterEqualCondition(left, right);
            case Operator.Less:
                return new LessCondition(left, right);
            case Operator.LessEqual:
                return new LessEqualCondition(left, right);
            case Operator.In:
                return new InCondition(left, new[] { right });
            case Operator.NotIn:
                return new NotInCondition(left, new[] { right });
            case Operator.Contains:
                return new LikeCondition(left, right);
            case Operator.Starts:
                return new LikeCondition(left, right);
            case Operator.Ends:
                return new LikeCondition(left, right);
        }
        throw new InvalidOperationException($"运算符 {@operator.Description()} 未生成条件。");
    }
}