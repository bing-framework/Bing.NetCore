using System.Linq.Expressions;
using System.Reflection;
using Bing.Data.Sql.Builders.Extensions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 延迟绑定物理名称的逻辑投影列。
/// </summary>
/// <remarks>保留来源身份与 CLR 成员的逻辑投影，物理名称在快照中解析。</remarks>
/// <param name="SourceId">查询来源的稳定标识。</param>
/// <param name="MemberName">CLR 成员名称。</param>
/// <param name="ResultAlias">投影结果列的别名。</param>
/// <param name="ResultType">投影结果列的 CLR 类型。</param>
/// <param name="Function">投影或比较使用的 SQL 聚合函数。</param>
/// <param name="Distinct">聚合是否去除重复值。</param>
internal sealed record SqlProjectionColumn(string SourceId, string MemberName, string ResultAlias,
    Type ResultType, SqlAggregateFunction? Function = null, bool Distinct = false)
{
    /// <summary>
    /// 获取由来源与成员共同组成的投影身份。
    /// </summary>
    internal string Identity => $"{SourceId}:{MemberName}:{Function}:{Distinct}:{ResultAlias}";

    /// <summary>
    /// 将投影列解析为 SQL 列项。
    /// </summary>
    /// <param name="context">提供查询构建和实体映射信息的 SQL 子句上下文。</param>
    /// <param name="fallback">当前查询中未按 SourceId 找到数据源时使用的备用数据源。</param>
    /// <returns>根据数据源和映射生成的 SQL 列项。</returns>
    /// <remarks>
    /// 数据源、投影成员或实体映射不存在，或聚合目标属性不可读时会抛出异常。
    /// </remarks>
    internal ColumnItem ToColumn(SqlClauseContext context, TableSource fallback = null)
    {
        var source = SqlLambdaSourceResolver.Sources((ISqlQueryClauseAccessor)context.Builder)
            .SingleOrDefault(item => item.SourceId == SourceId) ?? fallback
            ?? throw new InvalidOperationException("逻辑列所属来源不在当前查询中。");
        if (Function.HasValue && MemberName == null)
            return ColumnItem.CreateAggregateWildcard(Function.Value, ResultAlias);
        string name;
        if (source.ProjectedMembers != null)
        {
            if (!source.ProjectedMembers.Contains(MemberName))
                throw new InvalidOperationException($"派生表未输出成员 {MemberName}。");
            name = MemberName;
        }
        else
        {
            var mapping = ((EntityResolver)context.EntityResolver).GetMapping(source.EntityType);
            if (!mapping.Columns.TryGetValue(MemberName, out var member))
                throw new InvalidOperationException($"实体 {source.EntityType.Name} 的成员 {MemberName} 没有可用列映射。");
            if (Function.HasValue)
            {
                var metadata = context.Services.EntityModelMetadataProvider.GetMetadata(source.EntityType);
                if (!metadata.Properties.TryGetValue(MemberName, out var property) || !property.CanRead)
                    throw new NotSupportedException("聚合参数必须可读取。");
                SqlFunctionProjectionParser.ValidateAggregateType(Function.Value, member.ClrType);
            }
            name = member.ColumnName;
        }
        return Function.HasValue
            ? ColumnItem.CreateAggregate(Function.Value, name, source.Alias, ResultAlias, Distinct)
            : ColumnItem.CreateColumn(name, source.Alias, ResultAlias);
    }

    /// <summary>
    /// 将投影列转换为 SQL 文本。
    /// </summary>
    /// <param name="context">提供方言和别名信息的 SQL 子句上下文。</param>
    /// <returns>按当前数据库方言及别名注册表生成的列 SQL。</returns>
    internal string ToSql(SqlClauseContext context) => ToColumn(context).ToSql(context.Dialect, context.AliasRegister);
}

/// <summary>
/// 解析直接成员和受支持聚合标记的投影解析器。
/// </summary>
/// <remarks>
/// 只解析直接成员和受支持的聚合标记，不执行用户函数。
/// </remarks>
internal static class SqlFunctionProjectionParser
{
    /// <summary>
    /// 将 Lambda 表达式解析为 SQL 投影列。
    /// </summary>
    /// <param name="expression">描述投影内容的 Lambda 表达式。</param>
    /// <param name="sources">表达式参数对应的查询数据源。</param>
    /// <param name="functions">是否允许解析聚合函数。</param>
    /// <returns>按表达式成员顺序生成的 SQL 投影列。</returns>
    /// <remarks>
    /// 支持数组、成员构造、对象初始化和单项投影；表达式为空或未包含投影项时会抛出异常。
    /// </remarks>
    internal static IReadOnlyList<SqlProjectionColumn> Parse(LambdaExpression expression,
        IReadOnlyList<TableSource> sources, bool functions = false)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));
        var result = new List<SqlProjectionColumn>();
        var body = Unbox(expression.Body);
        if (body is NewArrayExpression array)
            foreach (var item in array.Expressions) result.Add(ParseItem(item, null, expression, sources, functions));
        else if (body is NewExpression create && create.Members != null)
            for (var i = 0; i < create.Arguments.Count; i++)
                result.Add(ParseItem(create.Arguments[i], create.Members[i].Name, expression, sources, functions));
        else if (body is MemberInitExpression init)
        {
            if (init.Bindings.Count == 0)
                throw new NotSupportedException("多表 DTO 投影至少需要一个成员初始化绑定。");
            if (init.NewExpression.Arguments.Count != 0)
                throw new NotSupportedException("DTO 投影必须使用无参构造函数。");
            foreach (var binding in init.Bindings)
            {
                if (binding is not MemberAssignment assignment)
                    throw new NotSupportedException("DTO 投影仅支持直接成员赋值。");
                result.Add(ParseItem(assignment.Expression, assignment.Member.Name, expression, sources, functions));
            }
        }
        else result.Add(ParseItem(body, null, expression, sources, functions));
        if (result.Count == 0) throw new ArgumentException("投影至少需要一个成员。", nameof(expression));
        return result;
    }

    /// <summary>
    /// 将单个投影表达式解析为 SQL 投影列。
    /// </summary>
    /// <param name="item">单个投影表达式。</param>
    /// <param name="alias">投影结果别名。</param>
    /// <param name="expression">包含 Lambda 参数和数据源的外层表达式。</param>
    /// <param name="sources">外层参数对应的查询数据源。</param>
    /// <param name="functions">是否允许解析聚合函数。</param>
    /// <returns>解析出的属性或聚合函数投影列。</returns>
    /// <remarks>
    /// 属性投影必须是 Lambda 参数的直接属性；其他表达式须为受支持的聚合函数调用。
    /// </remarks>
    internal static SqlProjectionColumn ParseItem(Expression item, string alias, LambdaExpression expression,
        IReadOnlyList<TableSource> sources, bool functions)
    {
        item = Unbox(item);
        if (item is MethodCallExpression call)
            return ParseFunction(call, alias, expression, sources, functions);
        if (item is not MemberExpression member || member.Expression is not ParameterExpression parameter ||
            member.Member is not PropertyInfo property)
            throw new NotSupportedException("投影只支持 Lambda 参数的直接属性和 SQL 聚合函数。");
        var index = expression.Parameters.IndexOf(parameter);
        if (index < 0 || index >= sources.Count)
            throw new NotSupportedException("投影成员不属于当前实体参数。");
        return new SqlProjectionColumn(sources[index].SourceId, property.Name, alias, property.PropertyType);
    }

    /// <summary>
    /// 检查方法是否由受支持的 SQL 聚合函数类型声明。
    /// </summary>
    /// <param name="method">要检查的方法。</param>
    /// <returns>方法声明于 SqlFn、SqlAggregates 或 SqlAggregateFunctions&lt;T&gt; 时返回 true，否则返回 false。</returns>
    internal static bool IsAggregateMethod(MethodInfo method) =>
        method.DeclaringType == typeof(SqlFn) ||
        method.DeclaringType == typeof(SqlAggregates) ||
        method.DeclaringType?.IsGenericType == true &&
        method.DeclaringType.GetGenericTypeDefinition() == typeof(SqlAggregateFunctions<>);

    /// <summary>
    /// 检查表达式树中是否包含 SQL 聚合函数。
    /// </summary>
    /// <param name="expression">要检查的表达式树。</param>
    /// <returns>表达式树包含受支持的聚合函数调用时返回 true，否则返回 false。</returns>
    internal static bool ContainsAggregate(Expression expression)
    {
        var visitor = new AggregateFinder();
        visitor.Visit(expression);
        return visitor.Found;
    }

    /// <summary>
    /// 查找表达式中的聚合函数调用。
    /// </summary>
    private sealed class AggregateFinder : ExpressionVisitor
    {
        /// <summary>
        /// 获取是否已找到聚合函数表达式。
        /// </summary>
        internal bool Found { get; private set; }
        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Found |= IsAggregateMethod(node.Method);
            return Found ? node : base.VisitMethodCall(node);
        }
    }

    /// <summary>
    /// 将 SQL 聚合函数调用解析为投影列。
    /// </summary>
    /// <param name="call">聚合函数调用表达式。</param>
    /// <param name="alias">投影结果别名。</param>
    /// <param name="outer">包含聚合调用的外层 Lambda 表达式。</param>
    /// <param name="sources">Lambda 参数对应的查询数据源。</param>
    /// <param name="functions">是否允许实例聚合函数。</param>
    /// <returns>包含数据源、属性、聚合类型及别名的投影列。</returns>
    /// <remarks>
    /// 仅支持 Count、CountAll、Sum、Avg、Min 和 Max；实例聚合函数还须满足 Lambda 参数约束，属性选择器必须指向可读的直接属性。
    /// </remarks>
    private static SqlProjectionColumn ParseFunction(MethodCallExpression call, string alias,
        LambdaExpression outer, IReadOnlyList<TableSource> sources, bool functions)
    {
        if (!IsAggregateMethod(call.Method))
            throw new NotSupportedException("仅支持 SqlFn、SqlAggregates 或当前函数上下文声明的聚合方法。");
        var staticFunction = call.Method.DeclaringType == typeof(SqlFn) ||
            call.Method.DeclaringType == typeof(SqlAggregates);
        if (!staticFunction && (!functions || outer.Parameters.Count != 2 || call.Object != outer.Parameters[1]))
            throw new NotSupportedException("聚合必须使用当前 Lambda 的函数上下文。");
        var name = call.Method.Name;
        var function = name switch
        {
            "CountAll" or "Count" => SqlAggregateFunction.Count,
            "Sum" => SqlAggregateFunction.Sum,
            "Avg" => SqlAggregateFunction.Avg,
            "Min" => SqlAggregateFunction.Min,
            "Max" => SqlAggregateFunction.Max,
            _ => throw new NotSupportedException("未声明的 SQL 聚合函数。")
        };
        var source = sources[0];
        string memberName = null;
        var start = 0;
        if (name != "CountAll")
        {
            var argument = call.Arguments[0];
            var selector = argument is UnaryExpression { NodeType: ExpressionType.Quote } quote
                ? quote.Operand as LambdaExpression : argument as LambdaExpression;
            PropertyInfo property;
            if (selector != null && !staticFunction)
            {
                if (selector.Parameters.Count != 1 || Unbox(selector.Body) is not MemberExpression selected ||
                    selected.Expression != selector.Parameters[0] || selected.Member is not PropertyInfo selectedProperty)
                    throw new NotSupportedException("聚合选择器必须直接引用其自身参数属性。");
                property = selectedProperty;
            }
            else
            {
                if (Unbox(argument) is not MemberExpression member ||
                    member.Expression is not ParameterExpression parameter || member.Member is not PropertyInfo directProperty)
                    throw new NotSupportedException("聚合参数必须是当前实体参数的直接属性。");
                var index = outer.Parameters.IndexOf(parameter);
                if (index < 0 || index >= sources.Count || !staticFunction && index != 0)
                    throw new NotSupportedException("聚合成员不属于当前实体来源。");
                source = sources[index];
                property = directProperty;
            }
            if (property.GetMethod?.IsPublic != true)
                throw new NotSupportedException("聚合参数必须是公开可读取的属性。");
            memberName = property.Name;
            ValidateAggregateType(function, property.PropertyType);
            start = 1;
        }
        var distinct = false;
        string explicitAlias = null;
        for (var i = start; i < call.Arguments.Count; i++)
        {
            if (call.Arguments[i] is not ConstantExpression constant)
                throw new NotSupportedException("聚合结果名和 Distinct 必须使用常量。");
            if (constant.Type == typeof(string)) explicitAlias = constant.Value as string;
            else if (constant.Type == typeof(bool)) distinct = (bool)constant.Value;
            else throw new NotSupportedException("不支持的聚合参数。");
        }
        if (explicitAlias != null)
        {
            SqlLambdaSourceResolver.AliasHint(explicitAlias);
            if (string.IsNullOrWhiteSpace(explicitAlias)) throw new ArgumentException("聚合结果别名不能为空。");
            if (alias != null && !string.Equals(alias, explicitAlias, StringComparison.Ordinal))
                throw new ArgumentException("聚合结果别名必须与 DTO/匿名对象成员名一致。");
        }
        return new SqlProjectionColumn(source.SourceId, memberName, alias ?? explicitAlias, call.Type, function, distinct);
    }

    /// <summary>
    /// 验证聚合函数支持指定属性类型。
    /// </summary>
    /// <param name="function">要验证的 SQL 聚合函数。</param>
    /// <param name="propertyType">聚合目标属性的类型。</param>
    /// <remarks>
    /// Sum 仅支持 int、long、decimal 和 double；Avg 仅支持 decimal 和 double。
    /// </remarks>
    internal static void ValidateAggregateType(SqlAggregateFunction function, Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (!IsScalar(type)) throw new NotSupportedException("聚合参数必须是可读取的标量属性。");
        if (function is SqlAggregateFunction.Sum or SqlAggregateFunction.Avg &&
            type != typeof(int) && type != typeof(long) && type != typeof(decimal) && type != typeof(double))
            throw new NotSupportedException("此数值类型没有受支持的聚合契约。");
        if (function == SqlAggregateFunction.Avg && type != typeof(decimal) && type != typeof(double))
            throw new NotSupportedException("Avg 仅接受 decimal 或 double 属性。");
    }

    /// <summary>
    /// 移除表达式中不改变值类型的转换节点。
    /// </summary>
    /// <param name="expression">要处理的表达式。</param>
    /// <returns>移除支持的转换后的表达式。</returns>
    /// <remarks>
    /// 只允许转换到 object 或可空底层类型的转换；改变值类型的转换会抛出异常。
    /// </remarks>
    internal static Expression Unbox(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert)
        {
            if (convert.Type != typeof(object) && Nullable.GetUnderlyingType(convert.Type) != convert.Operand.Type)
                throw new NotSupportedException("投影不支持改变数值类型的转换，请使用明确的 SQL 表达式入口。");
            expression = convert.Operand;
        }
        return expression;
    }

    /// <summary>
    /// 检查类型是否属于支持的 SQL 标量类型。
    /// </summary>
    /// <param name="type">要检查的类型。</param>
    /// <returns>类型为枚举、受支持的基元类型、字符串、数值、日期时间、时间段、Guid、字节数组、DateOnly 或 TimeOnly 时返回 true，否则返回 false。</returns>
    internal static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsEnum || type.IsPrimitive && type != typeof(IntPtr) && type != typeof(UIntPtr) ||
            type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid) ||
            type == typeof(byte[]) || type.FullName is "System.DateOnly" or "System.TimeOnly";
    }
}
