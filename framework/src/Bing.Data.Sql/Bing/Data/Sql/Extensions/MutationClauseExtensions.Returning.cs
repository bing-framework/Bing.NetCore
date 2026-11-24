using System.Linq.Expressions;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

public static partial class MutationClauseExtensions
{
    /// <summary>将有限的列间算术展平为不可变片段；不编译 Lambda 或读取捕获值。</summary>
    private static SqlReturningColumn ResolveComputedReturning(Expression expression, ParameterExpression parameter,
        EntityMappingMetadata mapping, string qualifier, string alias)
    {
        var parts = new List<string> { "" };
        void Text(string value) => parts[parts.Count - 1] += value;
        void Visit(Expression value)
        {
            if (value is MemberExpression { Expression: ParameterExpression source } member && source == parameter)
            {
                var column = mapping?.FindColumn(member.Member.Name) ??
                    throw new InvalidOperationException($"实体 {parameter.Type.Name} 的属性 {member.Member.Name} 未映射到数据库列。");
                parts.Add(column.ColumnName);
                parts.Add("");
                return;
            }
            if (value is UnaryExpression unary)
            {
                if (unary.NodeType != ExpressionType.Negate || unary.Type != unary.Operand.Type ||
                    !ReturningNumericType(unary.Type) ||
                    unary.Method != null && unary.Method.DeclaringType != typeof(decimal))
                    throw new ArgumentException("Returning 一元投影仅支持未使用 checked 的数值负号。", "columns");
                Text("(-");
                Visit(unary.Operand);
                Text(")");
                return;
            }
            if (value is not BinaryExpression binary || binary.Left.Type != binary.Right.Type ||
                binary.Type != binary.Left.Type || !ReturningNumericType(binary.Type) ||
                binary.Method != null && binary.Method.DeclaringType != typeof(decimal))
                throw new ArgumentException("Returning 计算投影仅支持同类型数值列的四则运算。", "columns");
            var operation = binary.NodeType switch
            {
                ExpressionType.Add => " + ",
                ExpressionType.Subtract => " - ",
                ExpressionType.Multiply => " * ",
                ExpressionType.Divide => " / ",
                _ => throw new ArgumentException("Returning 计算投影仅支持未使用 checked 的四则运算。", "columns")
            };
            Text("(");
            Visit(binary.Left);
            Text(operation);
            Visit(binary.Right);
            Text(")");
        }
        Visit(expression);
        return SqlReturningColumn.CreateComputed(parts, qualifier, alias);
    }

    private static bool ReturningNumericType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(int) || type == typeof(long) || type == typeof(decimal) ||
            type == typeof(double) || type == typeof(float);
    }
}
