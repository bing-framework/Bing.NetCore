using System.Linq.Expressions;
using System.Reflection;
using Bing.Data.Sql.Builders.Conditions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 已参数化的 SQL 聚合比较条件。
/// </summary>
/// <remarks>保存已参数化的聚合条件，列标识符在快照中渲染。</remarks>
/// <param name="LeftColumn">比较左侧的逻辑列。</param>
/// <param name="LeftValue">比较左侧已绑定的参数值。</param>
/// <param name="RightColumn">比较右侧的逻辑列。</param>
/// <param name="RightValue">比较右侧已绑定的参数值。</param>
/// <param name="Comparison">SQL 比较运算符。</param>
/// <param name="Left">比较左侧的绑定操作数。</param>
/// <param name="Right">比较右侧的绑定操作数。</param>
/// <param name="Or">当前条件是否与前一条件按或连接。</param>
internal sealed record SqlFunctionPredicate(SqlProjectionColumn LeftColumn, string LeftValue,
    SqlProjectionColumn RightColumn, string RightValue, Operator Comparison,
    SqlFunctionPredicate Left = null, SqlFunctionPredicate Right = null, bool Or = false)
{
    /// <summary>
    /// 获取当前比较条件引用的逻辑列集合。
    /// </summary>
    internal IEnumerable<SqlProjectionColumn> Columns => Left != null
        ? Left.Columns.Concat(Right.Columns)
        : new[] { LeftColumn, RightColumn }.Where(column => column != null);

    /// <summary>
    /// 将组合谓词或比较谓词渲染为 SQL 条件。
    /// </summary>
    /// <param name="context">提供数据库方言、别名和参数信息的 SQL 子句上下文。</param>
    /// <returns>渲染后的 SQL 条件文本。</returns>
    internal string Render(SqlClauseContext context) => Left != null
        ? $"({Left.Render(context)}{(Or ? " Or " : " And ")}{Right.Render(context)})"
        : SqlConditionFactory.Create(LeftColumn?.ToSql(context) ?? LeftValue,
            RightColumn?.ToSql(context) ?? RightValue, Comparison).GetCondition();

    /// <summary>
    /// 将 Lambda 谓词解析为包含列和参数的 SQL 谓词。
    /// </summary>
    /// <param name="expression">描述条件的 Lambda 表达式。</param>
    /// <param name="sources">表达式参数对应的查询数据源。</param>
    /// <param name="context">用于解析列和注册参数的 SQL 子句上下文。</param>
    /// <param name="functions">是否允许解析聚合函数。</param>
    /// <returns>由表达式解析出的 SQL 谓词。</returns>
    /// <remarks>
    /// 支持 And、Or 组合及相等、不等和大小比较；比较值由表达式常量或闭包字段提供。
    /// </remarks>
    internal static SqlFunctionPredicate Parse(LambdaExpression expression, IReadOnlyList<TableSource> sources,
        SqlClauseContext context, bool functions = true)
    {
        (SqlProjectionColumn Column, string Parameter) Operand(Expression value)
        {
            // 先区分 SQL 列与业务值；仅业务值允许数值提升，列转换继续由投影解析器限制。
            var leaf = value;
            while (leaf is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
                leaf = unary.Operand;
            if (leaf is MethodCallExpression || leaf is MemberExpression { Expression: ParameterExpression })
            {
                var column = SqlFunctionProjectionParser.ParseItem(value, null, expression, sources, functions);
                if (column.ResultAlias != null) throw new ArgumentException("Having/OrderBy 中不能设置聚合结果别名。");
                column.ToColumn(context);
                return (column, null);
            }
            var literal = ReadValue(value);
            if (literal == null) return (null, null);
            var name = context.ParameterManager.GenerateName();
            context.ParameterManager.Add(name, literal);
            return (null, name);
        }
        SqlFunctionPredicate Visit(Expression value)
        {
            if (value is BinaryExpression binary)
            {
                if (binary.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
                    return new(null, null, null, null, default, Visit(binary.Left), Visit(binary.Right), binary.NodeType == ExpressionType.OrElse);
                var op = binary.NodeType switch
                {
                    ExpressionType.Equal => Operator.Equal,
                    ExpressionType.NotEqual => Operator.NotEqual,
                    ExpressionType.GreaterThan => Operator.Greater,
                    ExpressionType.GreaterThanOrEqual => Operator.GreaterEqual,
                    ExpressionType.LessThan => Operator.Less,
                    ExpressionType.LessThanOrEqual => Operator.LessEqual,
                    _ => throw new NotSupportedException("聚合条件仅支持比较与 And/Or 组合。")
                };
                var left = Operand(binary.Left);
                var right = Operand(binary.Right);
                return new(left.Column, left.Parameter, right.Column, right.Parameter, op);
            }
            var boolean = Operand(value);
            var parameter = Operand(Expression.Constant(true));
            return new(boolean.Column, boolean.Parameter, null, parameter.Parameter, Operator.Equal);
        }
        return Visit(expression.Body);
    }

    /// <summary>
    /// 读取常量、闭包字段或支持的转换表达式值。
    /// </summary>
    /// <param name="expression">要读取值的表达式。</param>
    /// <returns>从表达式中读取的值。</returns>
    /// <remarks>
    /// 不执行用户属性或方法；其他表达式类型会抛出 NotSupportedException。
    /// </remarks>
    internal static object ReadValue(Expression expression) => expression switch
    {
        ConstantExpression constant => constant.Value,
        MemberExpression { Member: FieldInfo field } member => field.GetValue(member.Expression == null ? null : ReadValue(member.Expression)),
        UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert => ReadConvertedValue(convert),
        _ => throw new NotSupportedException("条件值仅支持常量及闭包字段，不执行用户属性或方法。")
    };

    /// <summary>
    /// 读取并转换受支持的一元转换表达式值。
    /// </summary>
    /// <param name="convert">要读取值的一元转换表达式。</param>
    /// <returns>按目标类型转换后的值。</returns>
    /// <remarks>
    /// 仅允许安全数值提升、转换为 object 或保持底层类型的转换；不支持的转换会抛出 NotSupportedException。
    /// </remarks>
    private static object ReadConvertedValue(UnaryExpression convert)
    {
        var source = Nullable.GetUnderlyingType(convert.Operand.Type) ?? convert.Operand.Type;
        var target = Nullable.GetUnderlyingType(convert.Type) ?? convert.Type;
        var unwrappingNullable = Nullable.GetUnderlyingType(convert.Operand.Type) != null &&
            convert.Type.IsValueType && Nullable.GetUnderlyingType(convert.Type) == null;
        if (convert.Method != null && convert.Method.DeclaringType != typeof(decimal) ||
            unwrappingNullable ||
            target != typeof(object) && source != target && !IsNumericPromotion(source, target))
            throw new NotSupportedException("条件参数仅支持内置安全数值提升或可空转换。");
        var value = ReadValue(convert.Operand);
        if (value == null || target == typeof(object) || source == target) return value;
        if (source == typeof(char)) value = (ushort)(char)value;
        return Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 检查源类型到目标类型是否为支持的数值提升。
    /// </summary>
    /// <param name="source">转换源类型。</param>
    /// <param name="target">转换目标类型。</param>
    /// <returns>属于支持的数值提升且不涉及枚举时返回 true，否则返回 false。</returns>
    private static bool IsNumericPromotion(Type source, Type target)
    {
        if (source.IsEnum || target.IsEnum) return false;
        var destination = Type.GetTypeCode(target);
        return Type.GetTypeCode(source) switch
        {
            TypeCode.SByte => destination is TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.Byte => destination is TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.Int16 => destination is TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.UInt16 or TypeCode.Char => destination is TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal ||
                destination == TypeCode.UInt16 && source == typeof(char),
            TypeCode.Int32 => destination is TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.UInt32 => destination is TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.Int64 or TypeCode.UInt64 => destination is TypeCode.Single or TypeCode.Double or TypeCode.Decimal,
            TypeCode.Single => destination == TypeCode.Double,
            _ => false
        };
    }
}
