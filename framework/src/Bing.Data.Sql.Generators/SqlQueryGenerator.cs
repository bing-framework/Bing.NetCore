using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Bing.Data.Sql.Generators;

/// <summary>
/// 逻辑 SQL 查询描述和静态读取器生成器。
/// </summary>
/// <remarks>
/// 生成逻辑查询描述和静态读取器；不生成带物理对象名的 SQL。
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed partial class SqlQueryGenerator : IIncrementalGenerator
{
    /// <summary>
    /// 报告无法生成 SQL 的查询调用。
    /// </summary>
    private static readonly DiagnosticDescriptor Unsupported = new("BINGSQLG001", "不支持的生成式查询",
        "查询调用 {0} 无法生成：{1}", "Bing.Data.Sql.Generation", DiagnosticSeverity.Error, true);
    /// <summary>
    /// 报告保留运行时执行路径的查询调用。
    /// </summary>
    private static readonly DiagnosticDescriptor Fallback = new("BINGSQLG002", "查询使用运行时路径",
        "查询调用 {0} 保留运行时路径：{1}", "Bing.Data.Sql.Generation", DiagnosticSeverity.Info, true);
    /// <summary>
    /// 定义生成代码中 SQL 类型引用使用的全局命名空间前缀。
    /// </summary>
    private const string Prefix = "global::Bing.Data.Sql.";
    /// <summary>
    /// 将 Roslyn 类型符号格式化为完全限定的 C# 类型名。
    /// </summary>
    /// <param name="type">待格式化的类型符号。</param>
    /// <returns>ITypeSymbol 的完全限定 C# 类型名称。</returns>
    /// <remarks>
    /// 使用 FullyQualifiedFormat 输出带 global 命名空间前缀的名称。
    /// </remarks>
    private static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    /// <summary>
    /// 将字符串编码为 C# 字符串字面量。
    /// </summary>
    /// <param name="value">待编码的字符串；null 转换为 C# null 字面量。</param>
    /// <returns>表示 value 的 C# 字符串字面量；value 为 null 时返回 null 字面量。</returns>
    private static string Literal(string? value) => value == null ? "null" : SymbolDisplay.FormatLiteral(value, true);

    /// <summary>
    /// 将编译期常量格式化为可编译的 C# 表达式。
    /// </summary>
    /// <param name="value">已知非空的常量值。</param>
    /// <returns>保留非有限浮点值的 C# 表达式。</returns>
    private static string ConstantLiteral(object value) => value switch
    {
        float number when float.IsNaN(number) => "global::System.Single.NaN",
        float number when float.IsPositiveInfinity(number) => "global::System.Single.PositiveInfinity",
        float number when float.IsNegativeInfinity(number) => "global::System.Single.NegativeInfinity",
        double number when double.IsNaN(number) => "global::System.Double.NaN",
        double number when double.IsPositiveInfinity(number) => "global::System.Double.PositiveInfinity",
        double number when double.IsNegativeInfinity(number) => "global::System.Double.NegativeInfinity",
        _ => SymbolDisplay.FormatPrimitive(value, true, false)
            ?? throw new NotSupportedException("不支持生成此比较常量。")
    };

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var calls = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax,
            static (ctx, _) => Inspect(ctx)).Where(static call => call != null);
        var strict = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            (options.GlobalOptions.TryGetValue("build_property.BingSqlAotStrict", out var value) && value == "true") ||
            (options.GlobalOptions.TryGetValue("build_property.PublishAot", out value) && value == "true"));
        context.RegisterSourceOutput(calls.Collect().Combine(strict), static (output, input) =>
            Emit(output, input.Left, input.Right));
    }

    /// <summary>
    /// 识别符合生成器支持范围的 SQL 查询调用。
    /// </summary>
    /// <param name="context">当前 Roslyn 生成器语法上下文。</param>
    /// <returns>符合 SQL Lambda 查询筛选条件的调用描述；不符合时为 null。</returns>
    /// <remarks>
    /// 命名空间、接收类型或方法不匹配时返回 null。
    /// </remarks>
    private static Call? Inspect(GeneratorSyntaxContext context)
    {
        var syntax = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(syntax).Symbol is not IMethodSymbol method) return null;
        if (method.ContainingNamespace.ToDisplayString() != "Bing.Data.Sql") return null;
        if (IsReturningCall(method)) return new Call(syntax, method, context.SemanticModel);
        if (method.ContainingType.Name != "SqlLambdaQuery" &&
            !(method.Name == "From" && method.ReturnType.Name == "SqlLambdaQuery")) return null;
        return new Call(syntax, method, context.SemanticModel);
    }

    /// <summary>
    /// 根据已识别的调用生成查询拦截器与实体元数据。
    /// </summary>
    /// <param name="output">接收生成器诊断的上下文。</param>
    /// <param name="calls">待生成的查询调用集合。</param>
    /// <param name="strict">是否将不支持的调用报告为严格诊断。</param>
    /// <remarks>
    /// 不支持的调用会根据 strict 设置报告错误或回退诊断。
    /// </remarks>
    private static void Emit(SourceProductionContext output, ImmutableArray<Call?> calls, bool strict)
    {
        var methods = new StringBuilder();
        var generatedCalls = new Dictionary<InvocationExpressionSyntax, (Call Call, string Code)>();
        var entities = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var readers = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var index = 0;
        var hasReturning = false;
        foreach (var entry in calls)
        {
            var call = entry!;
            if (IsReturningCall(call.Method))
            {
                try
                {
                    methods.AppendLine(GenerateReturning(call, index++));
                    hasReturning = true;
                }
                catch (NotSupportedException error)
                {
                    // Returning 允许动态表达式，严格查询模式也保留其已有运行时语义。
                    output.ReportDiagnostic(Diagnostic.Create(Fallback, call.Syntax.GetLocation(),
                        call.Method.Name, error.Message));
                }
                continue;
            }
            try
            {
                var explicitValue = HasExplicitPredicateValue(call.Method);
                if (call.Method.TypeArguments.Select((type, index) => (type, index)).Any(argument =>
                    explicitValue && argument.index == call.Method.TypeArguments.Length - 1
                        ? !AccessibleValueType(argument.type)
                        : argument.type.TypeKind == TypeKind.TypeParameter ||
                          argument.type is INamedTypeSymbol named && named.TypeKind == TypeKind.Class && named.SpecialType != SpecialType.System_String && !Accessible(named)))
                    throw new NotSupportedException("首版生成要求可访问的非泛型实体和结果类型。");
                var entityTypes = explicitValue
                    ? call.Method.TypeArguments.Take(1) : call.Method.TypeArguments;
                foreach (var type in entityTypes.OfType<INamedTypeSymbol>())
                    if (type.TypeKind == TypeKind.Class && !type.IsAnonymousType && type.SpecialType != SpecialType.System_String)
                        entities[TypeName(type)] = type;
                if (call.Method.Name == "From") continue;
                var method = GenerateCall(call, index++, readers, strict);
                if (method != null) generatedCalls.Add(call.Syntax, (call, method));
            }
            catch (NotSupportedException error)
            {
                output.ReportDiagnostic(Diagnostic.Create(strict ? Unsupported : Fallback,
                    call.Syntax.GetLocation(), call.Method.Name, error.Message));
            }
        }
        methods.Append(GenerateTemplates(calls, generatedCalls));
        foreach (var generated in generatedCalls.Values) methods.AppendLine(generated.Code);
        foreach (var reader in readers)
            if (!Scalar(reader.Value)) entities[reader.Key] = reader.Value;
        if (methods.Length == 0 && entities.Count == 0) return;
        var code = new StringBuilder("// <auto-generated/>\n#nullable disable\n");
        code.AppendLine("namespace System.Runtime.CompilerServices { [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)] internal sealed class InterceptsLocationAttribute : global::System.Attribute { public InterceptsLocationAttribute(int version, string data) {} } }");
        code.AppendLine("namespace Bing.Data.Sql.Generated { internal static class QueryCalls {");
        code.Append(methods);
        if (hasReturning) code.AppendLine(CreateReturningHelper(calls.First(call => call != null)!.Model.Compilation));
        code.AppendLine(ParameterBinder);
        code.AppendLine("[global::System.Runtime.CompilerServices.ModuleInitializer] internal static void RegisterMetadata() {");
        foreach (var entity in entities.Values)
        {
            if (!Accessible(entity)) continue;
            code.AppendLine(Metadata(entity));
        }
        code.AppendLine("}");
        foreach (var reader in readers.Values) code.AppendLine(Reader(reader));
        code.AppendLine("} }");
        output.AddSource("BingSql.QueryCalls.g.cs", code.ToString());
    }

    private static bool HasExplicitPredicateValue(IMethodSymbol method)
    {
        if (method.Name is not ("Where" or "WhereIf") || method.TypeArguments.Length != 2)
            return false;
        var parameters = method.Parameters.ToList();
        var valueIndex = parameters.FindIndex(parameter => parameter.Name == "value");
        var expressionIndex = parameters.FindIndex(parameter => parameter.Type is INamedTypeSymbol named && named.Name == "Expression");
        return valueIndex >= 0 && expressionIndex > valueIndex;
    }

    /// <summary>
    /// 为单个 SQL 查询调用生成拦截代码。
    /// </summary>
    /// <param name="call">已识别的查询调用及语义模型。</param>
    /// <param name="id">生成代码中的调用序号。</param>
    /// <param name="readers">收集实体结果读取器的类型映射。</param>
    /// <param name="strict">是否要求条件值和结果执行使用完整静态路径。</param>
    /// <returns>供拦截器执行的生成调用代码；不需要拦截的调用返回 null。</returns>
    /// <remarks>
    /// 不需要生成时返回 null；不支持的表达式或调用形式会抛出 NotSupportedException。
    /// </remarks>
    private static string? GenerateCall(Call call, int id, Dictionary<string, INamedTypeSymbol> readers, bool strict)
    {
        var method = call.Method;
        var name = method.Name;
        var parameterIndex = method.Parameters.ToList().FindIndex(parameter =>
            parameter.Type is INamedTypeSymbol named && named.Name == "Expression");
        if (parameterIndex < 0)
        {
            if (name is "To" or "ToAsync" or "ToList" or "ToListAsync" or
                "First" or "FirstAsync" or "FirstOrDefault" or "FirstOrDefaultAsync" or
                "Single" or "SingleAsync" or "ToEntity" or "ToEntityAsync")
            {
                var expression = Terminal(method, readers, strict, id, out var descriptor);
                return descriptor + Interceptor(call, id, expression);
            }
            if (name is "Scalar" or "ScalarAsync")
                return Interceptor(call, id, Scalar(method));
            if (name is "ToPage" or "ToPageAsync")
                return Interceptor(call, id, Page(method));
            if (name is "ToSql" or "Clone" or "Distinct" or "SplitOn" or "SelectAll" or "ExcludeSelect") return null;
            if (name is "Select" or "ReplaceSelect" && method.Parameters.All(p => p.Type.SpecialType == SpecialType.System_Boolean)) return null;
            throw new NotSupportedException("该操作不在首版生成支持范围。");
        }
        var argument = call.Syntax.ArgumentList.Arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == method.Parameters[parameterIndex].Name)
            ?? call.Syntax.ArgumentList.Arguments.ElementAtOrDefault(parameterIndex);
        if (argument?.Expression is not LambdaExpressionSyntax lambda)
            throw new NotSupportedException("要求调用点内联 Lambda；动态表达式使用普通运行时路径。");
        var parameters = lambda is SimpleLambdaExpressionSyntax simple ? new[] { simple.Parameter } :
            ((ParenthesizedLambdaExpressionSyntax)lambda).ParameterList.Parameters.ToArray();
        var function = parameters.Length == 2 && IsMarkerType(call, ((IParameterSymbol)call.Model.GetDeclaredSymbol(parameters[1])!).Type, "Bing.Data.Sql.SqlAggregateFunctions`1");
        var explicitValue = HasExplicitPredicateValue(method);
        var sourceParameters = function || explicitValue ? parameters.Take(1).ToArray() : parameters;
        var valueSymbol = explicitValue ? call.Model.GetDeclaredSymbol(parameters[1]) : null;
        var tupleType = (valueSymbol as IParameterSymbol)?.Type as INamedTypeSymbol;
        if (tupleType?.IsTupleType != true) tupleType = null;
        var valueSlots = new List<(int Index, ITypeSymbol? ConvertedType)>();
        var types = sourceParameters.Select(p => ((IParameterSymbol)call.Model.GetDeclaredSymbol(p)!).Type).ToArray();
        var descriptors = new StringBuilder();
        var descriptorName = "Descriptor_" + id;
        string Descriptor(string type, string field, string initializer)
        {
            descriptors.Append("internal static readonly ").Append(type).Append(' ').Append(field)
                .Append(" = ").Append(initializer).AppendLine(";");
            return descriptorName + "." + field;
        }
        var isJoin = name is "Join" or "LeftJoin";
        var typeArray = isJoin ? null : Descriptor("global::System.Type[]", "Types",
            "new global::System.Type[]{" + string.Join(",", types.Select(t => "typeof(" + TypeName(t) + ")")) + "}");
        var names = isJoin ? null : Descriptor("string[]", "Names",
            "new string[]{" + string.Join(",", sourceParameters.Select(p => Literal(p.Identifier.ValueText))) + "}");
        var aliasParameters = method.Parameters.Where(p => p.Type.SpecialType == SpecialType.System_String && p.Name.IndexOf("alias", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        var aliases = aliasParameters.Length == sourceParameters.Length ? "new string[]{" + string.Join(",", aliasParameters.Select(p => "@" + p.Name)) + "}" : "null";
        if (lambda.Body is not ExpressionSyntax body) throw new NotSupportedException("不支持语句块 Lambda。");
        var columnIndex = 0;
        string Column(ExpressionSyntax expression, string? alias = null)
        {
            var initializer = GenerateColumn(call, expression, alias, sourceParameters, function ? parameters[1] : null);
            return name is "Select" or "ReplaceSelect" or "ExcludeSelect" or "GroupBy" or "OrderBy"
                ? initializer
                : Descriptor(Prefix + "SqlGeneratedColumn", "Column_" + columnIndex++, initializer);
        }
        string? constantField = null;
        string GeneratedInterceptor(string expression)
        {
            var interceptor = Interceptor(call, id, expression);
            // 每个调用点只保存不可变逻辑元数据，不保存动态别名、参数值或物理 SQL。
            // 显式静态构造函数使 WhereIf(false) 不会触发该调用点的元数据初始化。
            var descriptor = descriptors.Length == 0 ? string.Empty :
                "private static class " + descriptorName + " {\nstatic " + descriptorName + "() {}\n" + descriptors + "}\n";
            return (constantField ?? string.Empty) + descriptor + interceptor;
        }
        string ConstantValue(object? value, ExpressionSyntax expression)
        {
            if (value == null) return "null";
            var literal = "(" + TypeName(call.Model.GetTypeInfo(expression).ConvertedType!) + ")" + ConstantLiteral(value);
            // 字符串字面量本身可复用；值类型保存装箱结果，避免每次构造查询重复装箱。
            if (value is string) return literal;
            var field = "Constant_" + id;
            constantField = "private static readonly object " + field + " = " + literal + ";\n";
            return field;
        }
        bool TryBooleanPredicate(ExpressionSyntax expression, out ExpressionSyntax column, out bool value)
        {
            while (expression is ParenthesizedExpressionSyntax parenthesized) expression = parenthesized.Expression;
            value = true;
            if (expression is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not)
            {
                expression = not.Operand;
                while (expression is ParenthesizedExpressionSyntax operand) expression = operand.Expression;
                value = false;
            }
            column = expression;
            return expression is MemberAccessExpressionSyntax &&
                call.Model.GetTypeInfo(expression).Type?.SpecialType == SpecialType.System_Boolean;
        }
        string? StringPredicateCall(ExpressionSyntax expression, string? condition, bool compound = false)
        {
            while (expression is ParenthesizedExpressionSyntax parenthesized) expression = parenthesized.Expression;
            if (expression is not InvocationExpressionSyntax invocation ||
                invocation.Expression is not MemberAccessExpressionSyntax member ||
                invocation.ArgumentList.Arguments.Count != 1 ||
                call.Model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol ||
                methodSymbol.IsStatic || methodSymbol.ContainingType.SpecialType != SpecialType.System_String ||
                methodSymbol.Parameters.Length != 1 || methodSymbol.Parameters[0].Type.SpecialType != SpecialType.System_String)
                return null;
            var comparison = methodSymbol.Name switch
            {
                "Contains" => "Contains", "StartsWith" => "Starts", "EndsWith" => "Ends", _ => null
            };
            if (comparison == null) return null;
            var argument = invocation.ArgumentList.Arguments[0].Expression;
            if (compound && ExplicitValueIndex(argument) is { } valueIndex)
            {
                ValidateExplicitValue(argument);
                return ParameterPredicate(Column(member.Expression), comparison, valueIndex, argument);
            }
            var constant = call.Model.GetConstantValue(argument);
            if (!constant.HasValue)
                throw new NotSupportedException("生成字符串谓词要求编译期常量或显式条件值。");

            var column = Column(member.Expression);
            var value = ConstantValue(constant.Value, argument);
            if (compound)
                return Prefix + "SqlGeneratedPredicate.Compare(" + column + ", null, global::Bing.Operator." + comparison + ", " + value + ")";
            var bridge = condition == null ? "Compare(source, false, " : "OptionalCompare(source, " + condition + ", false, ";
            return Prefix + "SqlGeneratedQueryBridge." + bridge + typeArray + ", " + names + ", " + aliases + ", " +
                column + ", null, global::Bing.Operator." + comparison + ", " + value + ")";
        }
        ExpressionSyntax ConstantOperand(ExpressionSyntax expression)
        {
            // Nullable 常量转换没有 Roslyn ConstantValue，但内层常量仍可静态传入。
            return expression is CastExpressionSyntax cast &&
                call.Model.GetTypeInfo(cast.Type).Type is INamedTypeSymbol nullable &&
                nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                ? cast.Expression : expression;
        }
        void NormalizeConstantComparison(BinaryExpressionSyntax binary, ref ExpressionSyntax left, ref ExpressionSyntax right, ref string op)
        {
            if (right is not MemberAccessExpressionSyntax member || SourceIndex(call, member.Expression, sourceParameters) < 0 ||
                call.Model.GetSymbolInfo(binary).Symbol is not IMethodSymbol { MethodKind: MethodKind.BuiltinOperator } ||
                call.Model.GetConversion(right).IsUserDefined ||
                !call.Model.GetConstantValue(ConstantOperand(left)).HasValue)
                return;

            (left, right) = (right, left);
            op = op switch
            {
                "Greater" => "Less", "GreaterEqual" => "LessEqual",
                "Less" => "Greater", "LessEqual" => "GreaterEqual",
                _ => op
            };
        }
        ExpressionSyntax Unparenthesize(ExpressionSyntax expression)
        {
            while (expression is ParenthesizedExpressionSyntax parenthesized) expression = parenthesized.Expression;
            return expression;
        }
        int? ExplicitValueIndex(ExpressionSyntax expression)
        {
            expression = Unparenthesize(expression);
            if (valueSymbol == null) return null;
            if (SymbolEqualityComparer.Default.Equals(call.Model.GetSymbolInfo(expression).Symbol, valueSymbol))
                return -1;
            if (tupleType == null || expression is not MemberAccessExpressionSyntax member ||
                !SymbolEqualityComparer.Default.Equals(call.Model.GetSymbolInfo(Unparenthesize(member.Expression)).Symbol, valueSymbol) ||
                call.Model.GetSymbolInfo(member).Symbol is not IFieldSymbol field)
                return null;
            for (var i = 0; i < tupleType.TupleElements.Length; i++)
            {
                var element = tupleType.TupleElements[i];
                if (SymbolEqualityComparer.Default.Equals(field, element) ||
                    (field.CorrespondingTupleField != null && element.CorrespondingTupleField != null &&
                     SymbolEqualityComparer.Default.Equals(field.CorrespondingTupleField, element.CorrespondingTupleField)) ||
                    SymbolEqualityComparer.Default.Equals(field, element.CorrespondingTupleField))
                    return i;
            }
            return null;
        }
        ITypeSymbol? NumericConversionType(ExpressionSyntax expression)
        {
            // 括号上的转换才是比较使用的转换，内部节点可能仍显示为原类型。
            while (expression.Parent is ParenthesizedExpressionSyntax parent && parent.Expression == expression)
                expression = parent;
            var type = call.Model.GetTypeInfo(expression);
            var conversion = call.Model.GetConversion(expression);
            if (!conversion.IsImplicit || conversion.IsUserDefined || type.Type == null ||
                SymbolEqualityComparer.Default.Equals(type.Type, type.ConvertedType))
                return null;
            if (conversion.IsNumeric)
                return type.ConvertedType;
            // Roslyn 将可空提升标记为 IsNullable；按底层转换判断，生成 C# 转换以保留 null。
            if (conversion.IsNullable && type.ConvertedType is INamedTypeSymbol target &&
                target.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                var source = type.Type is INamedTypeSymbol nullable &&
                    nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                    ? nullable.TypeArguments[0] : type.Type;
                var underlying = ((CSharpCompilation)call.Model.Compilation).ClassifyConversion(source, target.TypeArguments[0]);
                if (underlying.IsIdentity || underlying.IsImplicit && underlying.IsNumeric && !underlying.IsUserDefined)
                    return target;
            }
            return null;
        }
        var indexedValues = tupleType != null;
        ITypeSymbol? scalarConversion = null;
        var hasScalarValue = false;
        if (explicitValue && tupleType == null)
        {
            foreach (var expression in body.DescendantNodesAndSelf().OfType<ExpressionSyntax>())
            {
                if (ExplicitValueIndex(expression) != -1) continue;
                var conversion = NumericConversionType(expression);
                if (!hasScalarValue)
                {
                    scalarConversion = conversion;
                    hasScalarValue = true;
                }
                else if (!SymbolEqualityComparer.Default.Equals(scalarConversion, conversion))
                {
                    indexedValues = true;
                    break;
                }
            }
        }
        string ParameterPredicate(string column, string comparison, int valueIndex, ExpressionSyntax expression)
        {
            if (!indexedValues)
                return Prefix + "SqlGeneratedPredicate.CompareParameter(" + column + ", global::Bing.Operator." + comparison + ")";
            var convertedType = NumericConversionType(expression);
            var slot = valueSlots.FindIndex(item => item.Index == valueIndex &&
                SymbolEqualityComparer.Default.Equals(item.ConvertedType, convertedType));
            if (slot < 0)
            {
                slot = valueSlots.Count;
                valueSlots.Add((valueIndex, convertedType));
            }
            return Prefix + "SqlGeneratedPredicate.CompareParameter(" + column + ", global::Bing.Operator." + comparison + ", " + slot + ")";
        }
        void ValidateExplicitValue(ExpressionSyntax expression)
        {
            var type = call.Model.GetTypeInfo(expression);
            var conversion = call.Model.GetConversion(expression);
            if (conversion.IsUserDefined ||
                !SymbolEqualityComparer.Default.Equals(type.Type, type.ConvertedType) &&
                !(conversion.IsImplicit && conversion.IsReference) && NumericConversionType(expression) == null)
                throw new NotSupportedException("显式条件值暂不支持在比较中转换类型，请先按列类型传值。");
        }
        string? CollectionPredicateCall(ExpressionSyntax expression)
        {
            if (!explicitValue || expression is not InvocationExpressionSyntax invocation ||
                call.Model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol ||
                methodSymbol.Name != "Contains")
                return null;

            ExpressionSyntax collection;
            ExpressionSyntax item;
            var original = methodSymbol.ReducedFrom ?? methodSymbol;
            var arraySpan = original.ContainingType.ToDisplayString() == "System.MemoryExtensions";
            if ((original.ContainingType.ToDisplayString() == "System.Linq.Enumerable" || arraySpan) && original.Parameters.Length == 2)
            {
                if (methodSymbol.ReducedFrom != null && invocation.Expression is MemberAccessExpressionSyntax member &&
                    invocation.ArgumentList.Arguments.Count == 1)
                {
                    collection = member.Expression;
                    item = invocation.ArgumentList.Arguments[0].Expression;
                }
                else if (methodSymbol.IsStatic && invocation.ArgumentList.Arguments.Count == 2)
                {
                    collection = invocation.ArgumentList.Arguments[0].Expression;
                    item = invocation.ArgumentList.Arguments[1].Expression;
                }
                else return null;
            }
            else if (!methodSymbol.IsStatic &&
                     original.ContainingType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" &&
                     invocation.Expression is MemberAccessExpressionSyntax member && invocation.ArgumentList.Arguments.Count == 1)
            {
                collection = member.Expression;
                item = invocation.ArgumentList.Arguments[0].Expression;
            }
            else return null;

            if (ExplicitValueIndex(collection) is not { } collectionIndex) return null;
            if (arraySpan)
            {
                if (call.Model.GetTypeInfo(collection).Type is not IArrayTypeSymbol) return null;
            }
            else ValidateExplicitValue(collection);
            var conversion = call.Model.GetConversion(item);
            var itemType = call.Model.GetTypeInfo(item);
            if (Unparenthesize(item) is not MemberAccessExpressionSyntax || conversion.IsUserDefined ||
                !SymbolEqualityComparer.Default.Equals(itemType.Type, itemType.ConvertedType) &&
                !(conversion.IsImplicit && conversion.IsReference))
                throw new NotSupportedException("集合条件仅支持未经数值转换的直接属性列。");
            return ParameterPredicate(Column(item), "In", collectionIndex, collection);
        }
        bool IsCompoundPredicate(ExpressionSyntax expression) => Unparenthesize(expression) switch
        {
            BinaryExpressionSyntax binary => binary.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression,
            PrefixUnaryExpressionSyntax unary => unary.IsKind(SyntaxKind.LogicalNotExpression),
            _ => false
        };
        string CompoundPredicate(ExpressionSyntax expression)
        {
            expression = Unparenthesize(expression);
            if (expression is BinaryExpressionSyntax binary &&
                binary.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression)
            {
                if (call.Model.GetOperation(binary) is not IBinaryOperation { OperatorMethod: null })
                    throw new NotSupportedException("复合条件仅支持内置布尔运算。");
                return Prefix + "SqlGeneratedPredicate." + (binary.IsKind(SyntaxKind.LogicalOrExpression) ? "Or" : "And") +
                    "(" + CompoundPredicate(binary.Left) + ", " + CompoundPredicate(binary.Right) + ")";
            }

            if (TryBooleanPredicate(expression, out var booleanColumn, out var booleanValue))
                return Prefix + "SqlGeneratedPredicate.Compare(" + Column(booleanColumn) +
                    ", null, global::Bing.Operator.Equal, " + (booleanValue ? "true" : "false") + ")";

            if (expression is PrefixUnaryExpressionSyntax unary && unary.IsKind(SyntaxKind.LogicalNotExpression))
            {
                if (call.Model.GetOperation(unary) is not IUnaryOperation { OperatorMethod: null } unaryOperation ||
                    unaryOperation.Type?.SpecialType != SpecialType.System_Boolean)
                    throw new NotSupportedException("取反条件仅支持内置布尔运算。");
                return Prefix + "SqlGeneratedPredicate.Not(" + CompoundPredicate(unary.Operand) + ")";
            }

            if (StringPredicateCall(expression, null, true) is { } stringPredicate)
                return stringPredicate;

            if (CollectionPredicateCall(expression) is { } collectionPredicate)
                return collectionPredicate;

            if (expression is not BinaryExpressionSyntax comparison ||
                call.Model.GetOperation(comparison) is not IBinaryOperation operation ||
                (operation.OperatorMethod != null && operation.OperatorMethod.ContainingType.SpecialType is not
                    (SpecialType.System_Decimal or SpecialType.System_String)) ||
                call.Model.GetConversion(comparison.Left).IsUserDefined || call.Model.GetConversion(comparison.Right).IsUserDefined)
                throw new NotSupportedException("复合条件仅支持直接列、列比较及编译期常量比较。");
            var op = comparison.Kind() switch
            {
                SyntaxKind.EqualsExpression => "Equal", SyntaxKind.NotEqualsExpression => "NotEqual",
                SyntaxKind.GreaterThanExpression => "Greater", SyntaxKind.GreaterThanOrEqualExpression => "GreaterEqual",
                SyntaxKind.LessThanExpression => "Less", SyntaxKind.LessThanOrEqualExpression => "LessEqual",
                _ => throw new NotSupportedException("复合条件不支持此比较运算。")
            };
            var left = Unparenthesize(comparison.Left);
            var right = Unparenthesize(comparison.Right);
            NormalizeConstantComparison(comparison, ref left, ref right, ref op);
            if (ExplicitValueIndex(left) != null && right is MemberAccessExpressionSyntax sourceColumn &&
                SourceIndex(call, sourceColumn.Expression, sourceParameters) >= 0)
            {
                ValidateExplicitValue(left);
                (left, right) = (right, left);
                op = op switch
                {
                    "Greater" => "Less", "GreaterEqual" => "LessEqual",
                    "Less" => "Greater", "LessEqual" => "GreaterEqual",
                    _ => op
                };
            }
            var rightColumn = right is MemberAccessExpressionSyntax member &&
                SourceIndex(call, member.Expression, sourceParameters) >= 0;
            if (ExplicitValueIndex(right) is { } valueIndex)
            {
                ValidateExplicitValue(right);
                return ParameterPredicate(Column(left), op, valueIndex, right);
            }
            var constant = call.Model.GetConstantValue(ConstantOperand(right));
            if (!rightColumn && !constant.HasValue)
                throw new NotSupportedException("复合条件仅支持编译期常量；动态值请使用独立的显式列值条件。");
            var value = rightColumn || constant.Value == null ? "null" :
                "(" + TypeName(call.Model.GetTypeInfo(right).ConvertedType!) + ")" + ConstantLiteral(constant.Value);
            return Prefix + "SqlGeneratedPredicate.Compare(" + Column(left) + ", " +
                (rightColumn ? Column(right) : "null") + ", global::Bing.Operator." + op + ", " + value + ")";
        }
        string GeneratedCompoundPredicate(ExpressionSyntax expression)
        {
            var predicate = Descriptor(Prefix + "SqlGeneratedPredicate", "Predicate", CompoundPredicate(expression));
            var value = !indexedValues ? (scalarConversion == null ? string.Empty : "(" + TypeName(scalarConversion) + ")") + "@value" :
                "new object[]{" + string.Join(",", valueSlots.Select(slot =>
                (slot.ConvertedType == null ? string.Empty : "(" + TypeName(slot.ConvertedType) + ")") +
                (slot.Index < 0 ? "@value" : "@value.Item" + (slot.Index + 1)))) + "}";
            return Prefix + "SqlGeneratedQueryBridge.WherePredicate(source, " + typeArray + ", " + names + ", " + aliases + ", " + predicate +
                (explicitValue ? ", " + value : string.Empty) + ")";
        }
        string expressionCode;
        if (name is "Join" or "LeftJoin")
        {
            if (body is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } binary ||
                binary.Left is not MemberAccessExpressionSyntax l || binary.Right is not MemberAccessExpressionSyntax r || sourceParameters.Length != 2 ||
                SourceIndex(call, l.Expression, sourceParameters) != 0 || SourceIndex(call, r.Expression, sourceParameters) != 1)
                throw new NotSupportedException("生成 Join 要求两个来源的直接属性等值比较。");
            var options = method.Parameters.FirstOrDefault(p => p.Type.ToDisplayString() == "Bing.Data.Sql.SqlJoinOptions");
            expressionCode = Prefix + "SqlGeneratedQueryBridge.Join(source, " + (name == "LeftJoin" ? "true" : "false") + ", typeof(" + TypeName(types[0]) + "), typeof(" + TypeName(types[1]) + "), " + Literal(sourceParameters[0].Identifier.ValueText) + ", " + Literal(sourceParameters[1].Identifier.ValueText) + ", " + Literal(l.Name.Identifier.ValueText) + ", " + Literal(r.Name.Identifier.ValueText) + ", " + (options != null ? "@" + options.Name : (method.Parameters.Any(p => p.Name == "rightAlias") ? "@rightAlias" : method.Parameters.Any(p => p.Name == "alias") ? "@alias" : "null") + ", " + (method.Parameters.Any(p => p.Name == "schema") ? "@schema" : "null")) + ")";
        }
        else if (name == "WhereIf")
        {
            var conditionParameter = method.Parameters.FirstOrDefault(parameter => parameter.Name == "condition")
                ?? throw new NotSupportedException("WhereIf 缺少 condition 参数。");
            var valueParameter = method.Parameters.FirstOrDefault(parameter => parameter.Name == "value");
            var comparison = method.Parameters.Any(parameter => parameter.Name == "operator")
                ? "@operator" : "global::Bing.Operator.Equal";
            if (explicitValue)
            {
                expressionCode = GeneratedCompoundPredicate(body);
            }
            else if (valueParameter != null)
            {
                expressionCode = Prefix + "SqlGeneratedQueryBridge.OptionalCompare(source, @" + conditionParameter.Name + ", false, " +
                    typeArray + ", " + names + ", " + aliases + ", " + Column(body) + ", null, " + comparison + ", @value)";
            }
            else if (TryBooleanPredicate(body, out var booleanColumn, out var booleanValue))
            {
                var column = Column(booleanColumn);
                var booleanParameter = Descriptor("object", "Value", booleanValue ? "true" : "false");
                expressionCode = Prefix + "SqlGeneratedQueryBridge.OptionalCompare(source, @" + conditionParameter.Name + ", false, " +
                    typeArray + ", " + names + ", " + aliases + ", " + column + ", null, global::Bing.Operator.Equal, " + booleanParameter + ")";
            }
            else if (StringPredicateCall(body, "@" + conditionParameter.Name) is { } stringPredicate)
            {
                expressionCode = stringPredicate;
            }
            else if (IsCompoundPredicate(body))
            {
                expressionCode = GeneratedCompoundPredicate(body);
            }
            else
            {
                if (body is not BinaryExpressionSyntax binary)
                    throw new NotSupportedException("首版生成 WhereIf 条件要求一个直接比较。");
                var op = binary.Kind() switch
                {
                    SyntaxKind.EqualsExpression => "Equal", SyntaxKind.NotEqualsExpression => "NotEqual",
                    SyntaxKind.GreaterThanExpression => "Greater", SyntaxKind.GreaterThanOrEqualExpression => "GreaterEqual",
                    SyntaxKind.LessThanExpression => "Less", SyntaxKind.LessThanOrEqualExpression => "LessEqual",
                    _ => throw new NotSupportedException("首版生成 WhereIf 条件不支持此组合运算。")
                };
                ExpressionSyntax left = binary.Left, right = binary.Right;
                NormalizeConstantComparison(binary, ref left, ref right, ref op);
                var rightColumn = right is MemberAccessExpressionSyntax member &&
                    SourceIndex(call, member.Expression, sourceParameters) >= 0;
                if (!rightColumn && !SafeValue(call, right))
                    throw new NotSupportedException("参数值仅支持常量和闭包字段，不执行属性或方法。");
                var constant = call.Model.GetConstantValue(ConstantOperand(right));
                if (strict && !rightColumn && !constant.HasValue)
                    throw new NotSupportedException("严格 AOT 不读取闭包字段；请使用 WhereIf<TEntity, TValue>(condition, column, value, operator) 显式传值。");
                var value = rightColumn ? "null" : constant.HasValue
                    ? ConstantValue(constant.Value, right)
                    : Prefix + "SqlGeneratedQueryBridge.ComparisonValue(@" + method.Parameters[parameterIndex].Name + ")";
                expressionCode = Prefix + "SqlGeneratedQueryBridge.OptionalCompare(source, @" + conditionParameter.Name + ", false, " +
                    typeArray + ", " + names + ", " + aliases + ", " + Column(left) + ", " +
                    (rightColumn ? Column(right) : "null") + ", global::Bing.Operator." + op + ", " + value + ")";
            }
            // 关闭条件时不构造列描述，也不读取捕获值；调用方实参仍按 C# 规则先求值。
            expressionCode = "@" + conditionParameter.Name + " ? " + expressionCode + " : source";
        }
        else if (name is "Where" or "Having")
        {
            var valueParameter = method.Parameters.FirstOrDefault(parameter => parameter.Name == "value");
            if (explicitValue)
                return GeneratedInterceptor(GeneratedCompoundPredicate(body));
            if (valueParameter != null)
            {
                var comparison = method.Parameters.Any(parameter => parameter.Name == "operator")
                    ? "@operator" : "global::Bing.Operator.Equal";
                expressionCode = Prefix + "SqlGeneratedQueryBridge.Compare(source, false, " + typeArray + ", " + names + ", " + aliases + ", " + Column(body) + ", null, " + comparison + ", @value)";
                return GeneratedInterceptor(expressionCode);
            }
            if (name == "Where" && TryBooleanPredicate(body, out var booleanColumn, out var booleanValue))
            {
                var column = Column(booleanColumn);
                var booleanParameter = Descriptor("object", "Value", booleanValue ? "true" : "false");
                expressionCode = Prefix + "SqlGeneratedQueryBridge.Compare(source, false, " + typeArray + ", " +
                    names + ", " + aliases + ", " + column + ", null, global::Bing.Operator.Equal, " + booleanParameter + ")";
                return GeneratedInterceptor(expressionCode);
            }
            if (name == "Where" && StringPredicateCall(body, null) is { } stringPredicate)
                return GeneratedInterceptor(stringPredicate);
            if (name == "Where" && IsCompoundPredicate(body))
                return GeneratedInterceptor(GeneratedCompoundPredicate(body));
            if (body is not BinaryExpressionSyntax binary) throw new NotSupportedException("首版生成条件要求一个直接比较。");
            var op = binary.Kind() switch
            {
                SyntaxKind.EqualsExpression => "Equal", SyntaxKind.NotEqualsExpression => "NotEqual",
                SyntaxKind.GreaterThanExpression => "Greater", SyntaxKind.GreaterThanOrEqualExpression => "GreaterEqual",
                SyntaxKind.LessThanExpression => "Less", SyntaxKind.LessThanOrEqualExpression => "LessEqual",
                _ => throw new NotSupportedException("首版生成条件不支持此组合运算。")
            };
            ExpressionSyntax left = binary.Left, right = binary.Right;
            if (name == "Where") NormalizeConstantComparison(binary, ref left, ref right, ref op);
            var rightColumn = right is MemberAccessExpressionSyntax member && SourceIndex(call, member.Expression, sourceParameters) >= 0;
            if (!rightColumn && !SafeValue(call, right)) throw new NotSupportedException("参数值仅支持常量和闭包字段，不执行属性或方法。");
            var constant = call.Model.GetConstantValue(ConstantOperand(right));
            if (strict && !rightColumn && !constant.HasValue)
                throw new NotSupportedException("严格 AOT 不读取闭包字段；请使用 Where<TEntity, TValue>(column, value, operator) 显式传值，或参数化 Raw SQL。");
            var value = rightColumn ? "null" : constant.HasValue
                ? ConstantValue(constant.Value, right)
                : Prefix + "SqlGeneratedQueryBridge.ComparisonValue(@" + method.Parameters[parameterIndex].Name + ")";
            expressionCode = Prefix + "SqlGeneratedQueryBridge.Compare(source, " + (name == "Having" ? "true" : "false") + ", " + typeArray + ", " + names + ", " + aliases + ", " + Column(left) + ", " + (rightColumn ? Column(right) : "null") + ", global::Bing.Operator." + op + ", " + value + ")";
        }
        else if (name is "Select" or "ReplaceSelect" or "ExcludeSelect" or "GroupBy" or "OrderBy")
        {
            IEnumerable<string> columns = body switch
            {
                AnonymousObjectCreationExpressionSyntax anonymous => anonymous.Initializers.Select(p => Column(p.Expression, name == "ExcludeSelect" ? null : p.NameEquals?.Name.Identifier.ValueText ?? (p.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText)),
                ArrayCreationExpressionSyntax array when array.Initializer != null => array.Initializer.Expressions.Select(e => Column(e)),
                ImplicitArrayCreationExpressionSyntax array => array.Initializer.Expressions.Select(e => Column(e)),
                ObjectCreationExpressionSyntax create when create.Initializer != null && (create.ArgumentList == null || create.ArgumentList.Arguments.Count == 0) => create.Initializer.Expressions.Select(e => e is AssignmentExpressionSyntax assignment ? Column(assignment.Right, call.Model.GetSymbolInfo(assignment.Left).Symbol?.Name ?? throw new NotSupportedException("DTO 赋值目标必须是可解析的成员。")) : throw new NotSupportedException("DTO 只支持直接成员赋值。")),
                _ => new[] { Column(body) }
            };
            var generatedColumns = columns.ToArray();
            if (generatedColumns.Length == 0) throw new NotSupportedException("投影至少需要一个成员。");
            var descending = method.Parameters.FirstOrDefault(p => p.Type.SpecialType == SpecialType.System_Boolean);
            var projectedColumns = Descriptor(Prefix + "SqlGeneratedColumn[]", "Columns",
                "new " + Prefix + "SqlGeneratedColumn[]{" + string.Join(",", generatedColumns) + "}");
            expressionCode = Prefix + "SqlGeneratedQueryBridge.Project(source, " + Literal(name) + ", " + typeArray + ", " + names + ", " + aliases + ", " + projectedColumns + ", " + (descending == null ? "false" : "@" + descending.Name) + ")";
        }
        else throw new NotSupportedException("表达式操作暂未生成。");
        return GeneratedInterceptor(expressionCode);
    }

    /// <summary>
    /// 判断查询表达式值能否安全地作为生成参数读取。
    /// </summary>
    /// <param name="call">提供语义模型的查询调用。</param>
    /// <param name="value">待检查的表达式。</param>
    /// <returns>表达式可安全作为 SQL 参数读取时返回 true，否则返回 false。</returns>
    /// <remarks>
    /// 返回 true 表示表达式是常量、局部变量、参数或字段，或可安全转换为上述形式；其他表达式返回 false。
    /// </remarks>
    private static bool SafeValue(Call call, ExpressionSyntax value)
    {
        if (value is ParenthesizedExpressionSyntax parenthesized) return SafeValue(call, parenthesized.Expression);
        if (value is CastExpressionSyntax cast)
        {
            var source = call.Model.GetTypeInfo(cast.Expression).Type;
            var target = call.Model.GetTypeInfo(cast.Type).Type;
            if (source == null || target == null) return false;
            var conversion = call.Model.Compilation.ClassifyConversion(source, target);
            return conversion.IsImplicit && !conversion.IsUserDefined && SafeValue(call, cast.Expression);
        }
        if (call.Model.GetConstantValue(value).HasValue) return true;
        var symbol = call.Model.GetSymbolInfo(value).Symbol;
        return symbol is ILocalSymbol or IParameterSymbol or IFieldSymbol;
    }

    /// <summary>
    /// 判断类型是否与指定程序集元数据名称的标记类型相同。
    /// </summary>
    /// <param name="call">提供引用程序集符号的查询调用。</param>
    /// <param name="type">待比较的类型符号。</param>
    /// <param name="metadataName">标记类型的元数据名称。</param>
    /// <returns>type 与 Bing.Data.Sql 程序集中的指定标记类型定义相同时返回 true，否则返回 false。</returns>
    /// <remarks>
    /// 返回 true 表示类型原始定义匹配目标标记类型；否则返回 false。
    /// </remarks>
    private static bool IsMarkerType(Call call, ITypeSymbol type, string metadataName)
    {
        var assembly = call.Model.Compilation.SourceModule.ReferencedAssemblySymbols
            .FirstOrDefault(item => item.Identity.Name == "Bing.Data.Sql");
        return SymbolEqualityComparer.Default.Equals((type as INamedTypeSymbol)?.OriginalDefinition,
            assembly?.GetTypeByMetadataName(metadataName));
    }

    /// <summary>
    /// 移除表达式外层的括号和装箱转换。
    /// </summary>
    /// <param name="call">提供语义模型的查询调用。</param>
    /// <param name="expression">待还原的列访问表达式。</param>
    /// <returns>移除受支持的装箱或可空包装转换后的列表达式。</returns>
    /// <remarks>
    /// 返回移除可忽略外层包装后的表达式节点。
    /// </remarks>
    private static ExpressionSyntax UnboxColumn(Call call, ExpressionSyntax expression)
    {
        while (true)
        {
            if (expression is ParenthesizedExpressionSyntax parenthesized) { expression = parenthesized.Expression; continue; }
            if (expression is not CastExpressionSyntax cast) return expression;
            var target = call.Model.GetTypeInfo(cast.Type).Type;
            var operand = call.Model.GetTypeInfo(cast.Expression).Type;
            if (target?.SpecialType != SpecialType.System_Object &&
                !(target is INamedTypeSymbol nullable && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                  SymbolEqualityComparer.Default.Equals(nullable.TypeArguments[0], operand)))
                throw new NotSupportedException("不支持改变数值类型的转换。");
            expression = cast.Expression;
        }
    }

    /// <summary>
    /// 查找列表达式引用的查询源参数位置。
    /// </summary>
    /// <param name="call">提供语义模型的查询调用。</param>
    /// <param name="expression">待解析来源的列表达式。</param>
    /// <param name="sources">查询 Lambda 中的来源参数。</param>
    /// <returns>表达式引用的 Lambda 数据源索引；未引用任何数据源参数时为 -1。</returns>
    /// <remarks>
    /// 返回匹配参数的从零开始索引；未匹配时返回 -1。
    /// </remarks>
    private static int SourceIndex(Call call, ExpressionSyntax expression, ParameterSyntax[] sources)
    {
        var symbol = call.Model.GetSymbolInfo(UnboxColumn(call, expression)).Symbol;
        return Array.FindIndex(sources, parameter =>
            SymbolEqualityComparer.Default.Equals(call.Model.GetDeclaredSymbol(parameter), symbol));
    }

    /// <summary>
    /// 为查询表达式中的列访问生成映射代码。
    /// </summary>
    /// <param name="call">已识别的查询调用及语义模型。</param>
    /// <param name="expression">列访问表达式。</param>
    /// <param name="alias">列对应的显式别名；未指定时为 null。</param>
    /// <param name="sources">查询 Lambda 中的来源参数。</param>
    /// <param name="function">聚合标记参数；不涉及聚合时为 null。</param>
    /// <returns>构造 SQL 投影列所需的 C# 代码。</returns>
    /// <remarks>
    /// 表达式不符合生成器支持的实体属性或聚合列形式时抛出 NotSupportedException。
    /// </remarks>
    private static string GenerateColumn(Call call, ExpressionSyntax expression, string? alias,
        ParameterSyntax[] sources, ParameterSyntax? function)
    {
        expression = UnboxColumn(call, expression);
        string? memberName;
        int sourceIndex;
        string? aggregate = null;
        var distinct = "false";
        var type = call.Model.GetTypeInfo(expression).Type ?? throw new NotSupportedException("未知列类型。");
        if (expression is InvocationExpressionSyntax invocation)
        {
            if (call.Model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol marker)
                throw new NotSupportedException("无法确定 SQL 函数符号。");
            var isStatic = IsMarkerType(call, marker.ContainingType, "Bing.Data.Sql.SqlFn") ||
                IsMarkerType(call, marker.ContainingType, "Bing.Data.Sql.SqlAggregates");
            if (!isStatic && (!IsMarkerType(call, marker.ContainingType, "Bing.Data.Sql.SqlAggregateFunctions`1") ||
                function == null || invocation.Expression is not MemberAccessExpressionSyntax target ||
                !SymbolEqualityComparer.Default.Equals(call.Model.GetSymbolInfo(UnboxColumn(call, target.Expression)).Symbol,
                    call.Model.GetDeclaredSymbol(function))))
                throw new NotSupportedException("函数必须来自 SqlFn、SqlAggregates 或当前 SqlAggregateFunctions 参数。");
            if (marker.Name is not ("CountAll" or "Count" or "Sum" or "Avg" or "Min" or "Max"))
                throw new NotSupportedException("未声明的 SQL 聚合函数。");
            if (call.Method.Name is "Where" or "Join" or "LeftJoin" or "GroupBy" or "ExcludeSelect")
                throw new NotSupportedException("当前子句不能包含聚合，请在 Select/Having/OrderBy 中使用。");
            aggregate = marker.Name == "CountAll" ? "Count" : marker.Name;
            sourceIndex = 0;
            memberName = null;
            var start = 0;
            ExpressionSyntax Argument(int index) =>
                (invocation.ArgumentList.Arguments.FirstOrDefault(argument =>
                    argument.NameColon?.Name.Identifier.ValueText == marker.Parameters[index].Name)
                 ?? invocation.ArgumentList.Arguments.ElementAtOrDefault(index))?.Expression
                ?? throw new NotSupportedException("聚合参数缺失。");
            if (marker.Name != "CountAll")
            {
                var column = UnboxColumn(call, Argument(0));
                IPropertySymbol property;
                if (!isStatic && column is LambdaExpressionSyntax selector)
                {
                    var selectorParameters = selector is SimpleLambdaExpressionSyntax simple
                        ? new[] { simple.Parameter } : ((ParenthesizedLambdaExpressionSyntax)selector).ParameterList.Parameters.ToArray();
                    if (selectorParameters.Length != 1 || selector.Body is not ExpressionSyntax selectorBody ||
                        UnboxColumn(call, selectorBody) is not MemberAccessExpressionSyntax selected ||
                        SourceIndex(call, selected.Expression, selectorParameters) != 0 ||
                        call.Model.GetSymbolInfo(selected).Symbol is not IPropertySymbol selectedProperty)
                        throw new NotSupportedException("聚合选择器必须直接引用自身参数属性。");
                    property = selectedProperty;
                }
                else
                {
                    if (column is not MemberAccessExpressionSyntax selected ||
                        call.Model.GetSymbolInfo(selected).Symbol is not IPropertySymbol directProperty)
                        throw new NotSupportedException("聚合参数必须是实体参数的直接属性。");
                    sourceIndex = SourceIndex(call, selected.Expression, sources);
                    if (sourceIndex < 0 || !isStatic && sourceIndex != 0)
                        throw new NotSupportedException("聚合成员不属于当前实体来源。");
                    property = directProperty;
                }
                if (property.GetMethod?.DeclaredAccessibility != Accessibility.Public || property.IsStatic || !Scalar(property.Type))
                    throw new NotSupportedException("聚合参数必须是公开可读取的标量属性。");
                memberName = property.Name;
                start = 1;
            }
            for (var index = start; index < marker.Parameters.Length; index++)
            {
                var constant = call.Model.GetConstantValue(Argument(index));
                if (!constant.HasValue) throw new NotSupportedException("聚合别名和 Distinct 必须为常量。");
                if (constant.Value is string explicitAlias)
                {
                    if (string.IsNullOrWhiteSpace(explicitAlias) ||
                        !(char.IsLetter(explicitAlias[0]) || explicitAlias[0] == '_') ||
                        explicitAlias.Any(character => !(char.IsLetterOrDigit(character) || character == '_')))
                        throw new NotSupportedException("聚合结果名必须为有效标识符。");
                    if (alias != null && alias != explicitAlias) throw new NotSupportedException("函数结果别名与投影成员冲突。");
                    alias = explicitAlias;
                }
                else if (constant.Value is bool flag) distinct = flag ? "true" : "false";
                else if (constant.Value != null) throw new NotSupportedException("不支持的聚合参数。");
            }
            if (alias != null && call.Method.Name is "Having" or "OrderBy")
                throw new NotSupportedException("Having/OrderBy 中不能设置聚合结果别名。");
        }
        else
        {
            if (expression is not MemberAccessExpressionSyntax member || call.Model.GetSymbolInfo(member).Symbol is not IPropertySymbol)
                throw new NotSupportedException("投影只支持直接属性和已声明聚合。");
            sourceIndex = SourceIndex(call, member.Expression, sources);
            if (sourceIndex < 0) throw new NotSupportedException("成员不属于当前来源。");
            memberName = member.Name.Identifier.ValueText;
        }
        return "new " + Prefix + "SqlGeneratedColumn(" + sourceIndex + ", " + Literal(memberName) + ", " + Literal(alias) + ", typeof(" + TypeName(type) + "), " + (aggregate == null ? "null" : Prefix + "Builders.SqlAggregateFunction." + aggregate) + ", " + distinct + ")";
    }

    /// <summary>
    /// 为查询调用构造拦截器方法源码。
    /// </summary>
    /// <param name="call">已识别的查询调用。</param>
    /// <param name="id">生成代码中的调用序号。</param>
    /// <param name="expression">需在生成方法中执行的表达式源码。</param>
    /// <returns>包含调用位置属性及生成方法体的拦截器源码。</returns>
    /// <remarks>
    /// 调用方必须先确认 InterceptorsNamespaces 包含生成器命名空间，否则抛出 NotSupportedException。
    /// </remarks>
    private static string Interceptor(Call call, int id, string expression)
    {
        return InterceptionLocation(call) + "\ninternal static " + TypeName(call.Method.ReturnType) + " Call_" + id + "(this " + TypeName(call.Method.ContainingType) + " source" +
            (call.Method.Parameters.Length == 0 ? "" : ", " + string.Join(", ", call.Method.Parameters.Select(p => TypeName(p.Type) + " @" + p.Name))) + ") => " + expression + ";";
    }

    /// <summary>获取调用位置；查询与写入生成共用同一编译器配置约束。</summary>
    private static string InterceptionLocation(Call call)
    {
#pragma warning disable RSEXPERIMENTAL002
        if (!call.Model.SyntaxTree.Options.Features.TryGetValue("InterceptorsNamespaces", out var namespaces) ||
            !namespaces.Split(';').Contains("Bing.Data.Sql.Generated"))
            throw new NotSupportedException("消费项目未启用 Bing.Data.Sql.Generated 拦截器命名空间。");
        var location = call.Model.GetInterceptableLocation(call.Syntax) ?? throw new NotSupportedException("编译器未提供调用点位置。");
        return location.GetInterceptsLocationAttributeSyntax();
#pragma warning restore RSEXPERIMENTAL002
    }

    /// <summary>
    /// 为终结查询调用生成结果读取表达式。
    /// </summary>
    /// <param name="method">终结查询方法符号。</param>
    /// <param name="readers">收集需要生成的实体读取器映射。</param>
    /// <param name="strict">是否将单类型终结方法交给统一静态读取管线。</param>
    /// <param name="id">当前生成调用点的序号。</param>
    /// <param name="descriptor">可复用的结果原型成员描述声明。</param>
    /// <returns>执行单类型或双类型映射查询的生成调用代码。</returns>
    /// <remarks>
    /// 仅支持单类型或双类型结果映射；其他泛型参数数量会抛出 NotSupportedException。
    /// </remarks>
    private static string Terminal(IMethodSymbol method, Dictionary<string, INamedTypeSymbol> readers, bool strict,
        int id, out string descriptor)
    {
        descriptor = string.Empty;
        if (method.TypeArguments.Length != 1 && method.TypeArguments.Length != 3)
            throw new NotSupportedException("首版生成列表支持单类型或二类型映射。");
        var types = method.TypeArguments.Length == 1 ? method.TypeArguments : method.TypeArguments.Take(2);
        var readNames = new List<string>();
        foreach (var type in types)
        {
            if (type is not INamedTypeSymbol entity ||
                (Scalar(entity) ? !AccessibleValueType(entity) : !Accessible(entity)))
                throw new NotSupportedException("结果需要可访问的具体类型和标量属性。");
            var scalar = Scalar(entity);
            var constructor = scalar ? null : SelectConstructor(entity);
            if (!scalar && constructor == null)
                throw new NotSupportedException("结果需要可访问的无参构造函数，或参数能够按名称绑定标量属性的构造函数。");
            if (!scalar)
            {
                var hasJsonColumn = false;
                foreach (var property in Properties(entity).Where(p => !Ignored(p)))
                {
                    if (ReadableProperty(property))
                        continue;
                    if (IsJsonColumn(property))
                        hasJsonColumn = true;
                    else if (property.Type.IsValueType || Attribute(property, "ColumnAttribute") != null || Attribute(property, "TimestampAttribute") != null)
                        throw new NotSupportedException("结果读取类型尚未支持：" + property.Type);
                }
                // 普通引用导航由显式 map 设置，JSON 列由实体模型和字段转换器处理。
                foreach (var property in Properties(entity).Where(p => !Ignored(p) && (ReadableProperty(p) || IsJsonColumn(p))))
                    if ((property.SetMethod?.DeclaredAccessibility != Accessibility.Public || property.SetMethod.IsInitOnly) &&
                        !constructor!.Properties.Any(item => SymbolEqualityComparer.Default.Equals(item, property)))
                        throw new NotSupportedException("结果映射属性必须可公开写入且不是 init-only，或由结果构造函数参数绑定。");
                if (hasJsonColumn)
                {
                    if (method.TypeArguments.Length != 1 || constructor!.Constructor.Parameters.Length != 0 ||
                        method.Parameters.Any(p => p.Name == "projectionPrototype"))
                        throw new NotSupportedException("JSON 对象列需要带无参构造函数的单实体结果和已注册的生成模型。");
                    // 生成文件中的终结调用不会被再次拦截。查询执行器使用已注册模型及列转换器读取。
                    return "source." + method.Name + "<" + TypeName(entity) + ">(" +
                        string.Join(", ", method.Parameters.Select(p => "@" + p.Name)) + ")";
                }
            }
            Reader(entity); // 在调用点阶段校验读取类型，让不支持项产生诊断而非生成器异常。
            readers[TypeName(entity)] = entity;
            readNames.Add(ReaderName(entity));
        }
        var timeout = method.Parameters.Any(p => p.Name == "timeout") ? "@timeout" : "null";
        var token = method.Parameters.Any(p => p.Name == "cancellationToken") ? "@cancellationToken" : "default";
        var prototype = method.Parameters.FirstOrDefault(p => p.Name == "projectionPrototype");
        if (prototype != null)
        {
            if (method.TypeArguments.Length != 1 || method.Name is not ("ToList" or "ToListAsync" or "To" or "ToAsync"))
                throw new NotSupportedException("终结 DTO 原型只支持单类型查询。");
            var prototypeArgument = "@" + prototype.Name;
            var resultType = method.TypeArguments[0] as INamedTypeSymbol
                ?? throw new NotSupportedException("终结 DTO 原型需要具体结果类型。");
            var resultConstructor = SelectConstructor(resultType)
                ?? throw new NotSupportedException("结果 DTO 没有可生成的构造函数。");
            var projectionMembers = "new string[]{" + string.Join(",", Properties(resultType)
                .Where(property => !Ignored(property) && ReadableProperty(property) &&
                    (property.SetMethod?.DeclaredAccessibility == Accessibility.Public && !property.SetMethod.IsInitOnly ||
                     resultConstructor.Properties.Any(item => SymbolEqualityComparer.Default.Equals(item, property))))
                .Select(property => Literal(property.Name))) + "}";
            var descriptorName = "PrototypeDescriptor_" + id;
            descriptor = "private static class " + descriptorName + " {\nstatic " + descriptorName + "() {}\n" +
                "internal static readonly string[] ProjectionMembers = " + projectionMembers + ";\n}\n";
            var methodName = method.Name.EndsWith("Async", StringComparison.Ordinal)
                ? method.Name.StartsWith("ToList", StringComparison.Ordinal) ? "ToBoundListPrototypeAsync" : "ToBoundSinglePrototypeAsync"
                : method.Name.StartsWith("ToList", StringComparison.Ordinal) ? "ToBoundListPrototype" : "ToBoundSinglePrototype";
            return Prefix + "SqlGeneratedExecution." + methodName + "<" + TypeName(method.TypeArguments[0]) + ">(" +
                "source, " + prototypeArgument + ", " + descriptorName + ".ProjectionMembers, " + readNames[0] + ", BindParameters, " + timeout +
                (method.Name.EndsWith("Async", StringComparison.Ordinal) ? ", " + token : "") + ")";
        }
        // 生成文件中的调用不在原调用点拦截位置内，不会再次进入该拦截器。
        // 单行和列表终结统一进入静态读取管线，保留相同的映射、NULL/缺列和生命周期语义。
        if (method.Name is ("To" or "ToAsync") && method.TypeArguments.Length != 1)
            throw new NotSupportedException("单行终结只支持单类型结果。");
        var execution = method.Name switch
        {
            "To" => "ToBoundSingle",
            "ToAsync" => "ToBoundSingleAsync",
            "ToList" => "ToBoundList",
            "ToListAsync" => "ToBoundListAsync",
            "First" => "First",
            "FirstAsync" => "FirstAsync",
            "FirstOrDefault" => "FirstOrDefault",
            "FirstOrDefaultAsync" => "FirstOrDefaultAsync",
            "Single" => "Single",
            "SingleAsync" => "SingleAsync",
            "ToEntity" => "SingleOrDefault",
            "ToEntityAsync" => "SingleOrDefaultAsync",
            _ => throw new NotSupportedException("不支持的生成终结操作。")
        };
        return Prefix + "SqlGeneratedExecution." + execution + "<" + string.Join(",", method.TypeArguments.Select(TypeName)) + ">(source, " + string.Join(",", readNames) +
            (method.TypeArguments.Length == 3 ? ", @map" : "") + ", BindParameters, " + timeout + (method.Name.EndsWith("Async", StringComparison.Ordinal) ? ", " + token : "") + ")";
    }

    /// <summary>
    /// 为标量终结调用生成静态执行入口。
    /// </summary>
    private static string Scalar(IMethodSymbol method)
    {
        if (method.TypeArguments.Length != 1)
            throw new NotSupportedException("标量终结只支持一个结果类型参数。");
        var timeout = method.Parameters.Any(p => p.Name == "timeout") ? "@timeout" : "null";
        var token = method.Parameters.Any(p => p.Name == "cancellationToken") ? "@cancellationToken" : "default";
        var execution = method.Name.EndsWith("Async", StringComparison.Ordinal) ? "ScalarAsync" : "Scalar";
        return Prefix + "SqlGeneratedExecution." + execution + "<" + TypeName(method.TypeArguments[0]) + ">(source, BindParameters, " + timeout +
            (method.Name.EndsWith("Async", StringComparison.Ordinal) ? ", " + token : "") + ")";
    }

    /// <summary>
    /// 为分页终结调用生成静态计划执行入口。
    /// </summary>
    private static string Page(IMethodSymbol method)
    {
        if (method.TypeArguments.Length != 1)
            throw new NotSupportedException("分页终结只支持一个结果类型参数。");
        var timeout = method.Parameters.Any(p => p.Name == "timeout") ? "@timeout" : "null";
        var token = method.Parameters.Any(p => p.Name == "cancellationToken") ? "@cancellationToken" : "default";
        var pager = method.Parameters.Any(p => p.Name == "pager") ? "@pager" : "null";
        var execution = method.Name.EndsWith("Async", StringComparison.Ordinal) ? "ToPageAsync" : "ToPage";
        return Prefix + "SqlGeneratedExecution." + execution + "<" + TypeName(method.TypeArguments[0] ?? throw new NotSupportedException("分页结果类型不能为空。")) + ">(source, " + pager +
            ", BindParameters, " + timeout + (method.Name.EndsWith("Async", StringComparison.Ordinal) ? ", " + token : "") + ")";
    }

    /// <summary>
    /// 嵌入生成代码的参数绑定辅助方法源码。
    /// </summary>
    private const string ParameterBinder = @"
private static void BindParameters(global::System.Data.Common.DbCommand command,
    global::System.Collections.Generic.IEnumerable<global::Bing.Data.Sql.Builders.Params.SqlParam> parameters) =>
    global::Bing.Data.Sql.SqlGeneratedExecution.BindParameters(command, parameters);
";

    /// <summary>
    /// 根据实体类型生成唯一的读取器方法名。
    /// </summary>
    /// <param name="type">实体类型符号。</param>
    /// <returns>由完全限定类型名称转换得到的 Read_ 前缀读取器方法名。</returns>
    /// <remarks>
    /// 类型名称中的非字母数字字符会替换为下划线。
    /// </remarks>
    private static string ReaderName(INamedTypeSymbol type) => "Read_" + string.Concat(TypeName(type).Select(c => char.IsLetterOrDigit(c) ? c : '_'));
    /// <summary>
    /// 判断实体类型是否可由生成代码访问。
    /// </summary>
    /// <param name="type">待检查的实体类型符号。</param>
    /// <returns>类型可用于生成读取器时返回 true，否则返回 false。</returns>
    /// <remarks>
    /// 返回 true 表示类型不是匿名或开放泛型，且为 public/internal 并具有可访问的外层类型；否则返回 false。
    /// </remarks>
    private static bool Accessible(INamedTypeSymbol type) => !type.IsAnonymousType && type.TypeParameters.Length == 0 &&
        type.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal && (type.ContainingType == null || Accessible(type.ContainingType));
    /// <summary>显式条件值允许使用可访问的封闭泛型及数组类型。</summary>
    private static bool AccessibleValueType(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => AccessibleValueType(array.ElementType),
        INamedTypeSymbol named => !named.IsAnonymousType &&
            named.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal &&
            (named.ContainingType == null || AccessibleValueType(named.ContainingType)) &&
            named.TypeArguments.All(AccessibleValueType),
        _ => false
    };
    /// <summary>
    /// 枚举实体类型及其基类型中可供生成代码赋值的属性。
    /// </summary>
    /// <param name="type">实体类型符号。</param>
    /// <returns>当前类型及其基类型中未被派生类型同名属性遮蔽的公开实例属性。</returns>
    /// <remarks>
    /// 按派生类型到基类型收集属性，并跳过不可读、不可写、已忽略或重复名称的属性。
    /// </remarks>
    private static IEnumerable<IPropertySymbol> Properties(INamedTypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current != null; current = current.BaseType)
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                if (!property.IsStatic && !property.IsIndexer && property.DeclaredAccessibility == Accessibility.Public && seen.Add(property.Name)) yield return property;
    }

    /// <summary>
    /// 选择生成读取器可调用的构造函数，并解析其参数到结果属性的绑定。
    /// </summary>
    /// <param name="type">结果类型。</param>
    /// <returns>无参构造函数或可按属性名称绑定的构造函数；不存在时返回 null。</returns>
    /// <remarks>
    /// 参数绑定只接受公开可读的标量属性，避免生成代码通过反射或隐式转换访问 DTO 成员。
    /// </remarks>
    private static ConstructorBinding? SelectConstructor(INamedTypeSymbol type)
    {
        var constructors = type.InstanceConstructors
            .Where(constructor => constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal)
            .ToArray();
        var parameterless = constructors.FirstOrDefault(constructor => constructor.Parameters.Length == 0);
        if (parameterless != null)
            return new ConstructorBinding(parameterless, Array.Empty<IPropertySymbol>());

        var properties = Properties(type)
            .Where(property => !Ignored(property) && property.GetMethod?.DeclaredAccessibility == Accessibility.Public &&
                               ReadableProperty(property))
            .ToArray();
        foreach (var constructor in constructors.OrderByDescending(item => item.Parameters.Length))
        {
            var mapped = new List<IPropertySymbol>(constructor.Parameters.Length);
            var valid = true;
            foreach (var parameter in constructor.Parameters)
            {
                if (parameter.RefKind != RefKind.None)
                {
                    valid = false;
                    break;
                }
                var property = properties.FirstOrDefault(item =>
                    (string.Equals(item.Name, parameter.Name, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(ColumnName(item), parameter.Name, StringComparison.OrdinalIgnoreCase)) &&
                    SymbolEqualityComparer.Default.Equals(item.Type, parameter.Type));
                if (property == null || mapped.Any(item => SymbolEqualityComparer.Default.Equals(item, property)))
                {
                    valid = false;
                    break;
                }
                mapped.Add(property);
            }
            if (valid && mapped.Count == constructor.Parameters.Length)
                return new ConstructorBinding(constructor, mapped);
        }
        return null;
    }

    /// <summary>生成读取器使用的构造函数和属性绑定。</summary>
    private sealed class ConstructorBinding
    {
        internal ConstructorBinding(IMethodSymbol constructor, IReadOnlyList<IPropertySymbol> properties)
        {
            Constructor = constructor;
            Properties = properties;
        }

        internal IMethodSymbol Constructor { get; }
        internal IReadOnlyList<IPropertySymbol> Properties { get; }
    }
    /// <summary>
    /// 查找符号及其继承链或重写链上的指定特性。
    /// </summary>
    /// <param name="symbol">要检查的类型或成员符号。</param>
    /// <param name="name">要查找的特性名称。</param>
    /// <returns>找到的特性；未找到时为 null。</returns>
    /// <remarks>按完整特性类型名匹配；继承链包括基类型和属性重写成员。</remarks>
    private static AttributeData? Attribute(ISymbol symbol, string name)
    {
        var fullName = (name is "TableAttribute" or "ColumnAttribute" or "NotMappedAttribute" or "DatabaseGeneratedAttribute"
            ? "System.ComponentModel.DataAnnotations.Schema." : "System.ComponentModel.DataAnnotations.") + name;
        for (ISymbol? current = symbol; current != null; current = current switch
        {
            INamedTypeSymbol type => type.BaseType,
            IPropertySymbol property => property.OverriddenProperty,
            _ => null
        })
        {
            var attribute = current.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == fullName);
            if (attribute != null) return attribute;
        }
        return null;
    }
    /// <summary>
    /// 读取属性的最大字符串长度约束。
    /// </summary>
    /// <param name="property">待检查的实体属性符号。</param>
    /// <returns>MaxLength 或 StringLength 特性中的正长度；未设置时为 null。</returns>
    /// <remarks>
    /// 依次读取 MaxLength 和 StringLength 特性；未配置正数长度时返回 null。
    /// </remarks>
    private static int? MaxLength(IPropertySymbol property)
    {
        var maxLength = Attribute(property, "MaxLengthAttribute")?.ConstructorArguments.FirstOrDefault().Value as int?;
        if (maxLength > 0) return maxLength;
        var stringLength = Attribute(property, "StringLengthAttribute")?.ConstructorArguments.FirstOrDefault().Value as int?;
        return stringLength > 0 ? stringLength : null;
    }
    /// <summary>
    /// 判断实体属性是否标记为不映射。
    /// </summary>
    /// <param name="property">待检查的实体属性符号。</param>
    /// <returns>属性带有 NotMapped 特性时返回 true，否则返回 false。</returns>
    /// <remarks>
    /// 返回 true 表示属性标有 NotMapped 特性；否则返回 false。
    /// </remarks>
    private static bool Ignored(IPropertySymbol property) => Attribute(property, "NotMappedAttribute") != null;

    /// <summary>
    /// 获取属性对应的数据库列名。
    /// </summary>
    /// <param name="property">待解析的属性。</param>
    /// <returns>Column 特性指定的列名；未指定时返回属性名。</returns>
    private static string ColumnName(IPropertySymbol property) =>
        Attribute(property, "ColumnAttribute")?.ConstructorArguments.FirstOrDefault().Value as string ?? property.Name;
    /// <summary>
    /// 为实体类型生成表、架构及属性元数据源码。
    /// </summary>
    /// <param name="entity">待生成元数据的实体类型符号。</param>
    /// <returns>注册实体表、架构和属性映射元数据的生成源码。</returns>
    /// <remarks>
    /// 名称与长度约束取自实体及其属性上的 DataAnnotations 特性。
    /// </remarks>
    private static string Metadata(INamedTypeSymbol entity)
    {
        var table = Attribute(entity, "TableAttribute");
        var name = table?.ConstructorArguments.FirstOrDefault().Value as string ?? entity.Name;
        var schema = table?.NamedArguments.FirstOrDefault(p => p.Key == "Schema").Value.Value as string ?? "";
        var columns = new List<string>();
        foreach (var property in Properties(entity))
        {
            var column = Attribute(property, "ColumnAttribute");
            var columnName = column?.ConstructorArguments.FirstOrDefault().Value as string ?? property.Name;
            var typeName = column?.NamedArguments.FirstOrDefault(p => p.Key == "TypeName").Value.Value as string;
            var key = Attribute(property, "KeyAttribute") != null || string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase) || string.Equals(property.Name, entity.Name + "Id", StringComparison.OrdinalIgnoreCase);
            var generated = Attribute(property, "TimestampAttribute") != null ? 2 : Attribute(property, "DatabaseGeneratedAttribute")?.ConstructorArguments.FirstOrDefault().Value ?? 0;
            var length = MaxLength(property);
            var canRead = property.GetMethod?.DeclaredAccessibility == Accessibility.Public;
            var canWrite = property.SetMethod?.DeclaredAccessibility == Accessibility.Public;
            var hasNonPublicSetter = property.SetMethod != null && !canWrite;
            var memberName = EscapeIdentifier(property.Name);
            var getter = canRead
                ? "static instance => ((" + TypeName(entity) + ")instance)." + memberName
                : "null";
            // init 只能在对象初始化器中赋值，不能生成运行时 Setter；保留 CanWrite 元数据供结果验证。
            var setter = canWrite && property.SetMethod?.IsInitOnly != true
                ? "static (instance, value) => ((" + TypeName(entity) + ")instance)." + memberName + " = (" + TypeName(property.Type) + ")value"
                : "null";
            columns.Add(Prefix + "Metadata.EntityPropertyMetadata.CreateGenerated(" + Literal(property.Name) + ", typeof(" + TypeName(property.Type) + "), " + (canRead ? "true" : "false") + ", " + (canWrite ? "true" : "false") + ", " + Literal(columnName) + ", " + (Ignored(property) ? "true" : "false") + ", " + (key ? "true" : "false") + ", (global::System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption)" + generated + ", " + (Attribute(property, "ConcurrencyCheckAttribute") != null || Attribute(property, "TimestampAttribute") != null ? "true" : "false") + ", " + (Attribute(property, "RequiredAttribute") != null ? "true" : "false") + ", " + (length?.ToString() ?? "null") + ", " + Literal(typeName) + ", " + getter + ", " + setter + ", " + (hasNonPublicSetter ? "true" : "false") + ")");
        }
        return Prefix + "Metadata.GeneratedEntityModelMetadataProvider.Register(new " + Prefix + "Metadata.EntityModelMetadata(typeof(" + TypeName(entity) + "), " + Literal(name) + ", " + Literal(schema) + ", new " + Prefix + "Metadata.EntityPropertyMetadata[]{" + string.Join(",", columns) + "}));";
    }

    /// <summary>将属性名转为可安全嵌入生成源码的成员标识符。</summary>
    private static string EscapeIdentifier(string value) =>
        SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value : value;

    // 按明确类型集合判断，不依赖 Roslyn SpecialType 的枚举顺序。
    /// <summary>
    /// 判断类型是否属于 SQL 读取器支持的标量类型。
    /// </summary>
    /// <param name="type">待检查的类型符号。</param>
    /// <returns>type 属于生成 SQL 读取器支持的标量类型时返回 true，否则返回 false。</returns>
    /// <remarks>
    /// 可空类型先按其实际值类型检查；支持的类型包括一维字节数组和枚举。
    /// </remarks>
    private static bool Scalar(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) type = named.TypeArguments[0];
        return type is IArrayTypeSymbol { Rank: 1, ElementType.SpecialType: SpecialType.System_Byte } ||
            type.TypeKind == TypeKind.Enum || type.SpecialType is
                SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte or SpecialType.System_Byte or
                SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
                SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal or
                SpecialType.System_Single or SpecialType.System_Double ||
            type.ToDisplayString() is "System.DateTime" or "System.DateTimeOffset" or "System.TimeSpan" or "System.Guid" || type.SpecialType == SpecialType.System_String;
    }
    // object 结果承载数据库原值；普通导航引用仍由显式 map 装配。
    /// <summary>
    /// 判断类型是否可以由生成的读取器读取为标量。
    /// </summary>
    /// <param name="type">待检查的类型符号。</param>
    /// <returns>type 为 object 或属于受支持标量类型时返回 true，否则返回 false。</returns>
    /// <remarks>
    /// 返回 true 表示类型为 object 或受支持的标量；否则返回 false。
    /// </remarks>
    private static bool ReadableScalar(ITypeSymbol type) => type.SpecialType == SpecialType.System_Object || Scalar(type);

    /// <summary>判断属性能否直接用生成的列读取器读取。</summary>
    private static bool ReadableProperty(IPropertySymbol property) =>
        ReadableScalar(property.Type) || IsPostgreSqlRange(property.Type) && !IsJsonColumn(property);

    /// <summary>识别受支持的 Npgsql 内置范围及其一维多范围数组。</summary>
    private static bool IsPostgreSqlRange(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol nullable && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            type = nullable.TypeArguments[0];
        if (type is IArrayTypeSymbol { Rank: 1 } array)
            type = array.ElementType;
        return type is INamedTypeSymbol { MetadataName: "NpgsqlRange`1" } range &&
            range.ContainingNamespace.ToDisplayString() == "NpgsqlTypes" &&
            range.TypeArguments[0].SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 or
                SpecialType.System_Decimal or SpecialType.System_DateTime;
    }

    /// <summary>识别需要通过实体模型和字段转换器读取的 JSON 列。</summary>
    private static bool IsJsonColumn(IPropertySymbol property)
    {
        var typeName = Attribute(property, "ColumnAttribute")?.NamedArguments
            .FirstOrDefault(argument => argument.Key == "TypeName").Value.Value as string;
        return string.Equals(typeName, "json", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(typeName, "jsonb", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 为实体或命名标量结果生成绑定读取器源码。
    /// </summary>
    /// <param name="type">实体或命名标量结果类型符号。</param>
    /// <returns>读取结果列并绑定实体属性或标量值的生成读取器方法源码。</returns>
    /// <remarks>
    /// 实体仅读写可访问、可读取且可赋值的属性；命名标量读取最小的非负列序号。
    /// </remarks>
    private static string Reader(INamedTypeSymbol type)
    {
        if (Scalar(type))
        {
            var scalarReader = new StringBuilder("private static global::System.Func<global::System.Data.Common.DbDataReader," + TypeName(type) + "> " + ReaderName(type) +
                "(global::System.Collections.Generic.IReadOnlyDictionary<string,int> ordinals) {");
            scalarReader.Append("var ordinal = -1; foreach (var candidate in ordinals.Values) if (candidate >= 0 && (ordinal < 0 || candidate < ordinal)) ordinal = candidate;")
                .Append("return reader => ").Append(ReaderValue(type, "ordinal")).Append("; }");
            return scalarReader.ToString();
        }

        var constructor = SelectConstructor(type) ?? throw new NotSupportedException("结果类型没有可生成的构造函数。");
        var scalarProperties = Properties(type)
            .Where(property => !Ignored(property) && ReadableProperty(property))
            .ToArray();
        var readableProperties = constructor.Properties
            .Concat(scalarProperties.Where(property => property.SetMethod?.DeclaredAccessibility == Accessibility.Public &&
                                                       !property.SetMethod.IsInitOnly &&
                                                       !constructor.Properties.Any(item => SymbolEqualityComparer.Default.Equals(item, property))))
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var result = new StringBuilder("private static global::System.Func<global::System.Data.Common.DbDataReader," + TypeName(type) + "> " + ReaderName(type) + "(global::System.Collections.Generic.IReadOnlyDictionary<string,int> ordinals) {");
        var assignments = new StringBuilder("return reader => {");
        var ordinalsByProperty = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var property in readableProperties)
        {
            var ordinal = "ordinal" + index++;
            var getter = ReaderGetter(property.Type);
            if (getter.Length == 0) throw new NotSupportedException("结果读取类型尚未支持：" + property.Type);
            var columnName = ColumnName(property);
            result.Append("var " + ordinal + " = ordinals.TryGetValue(" + Literal(property.Name) + ", out var found" + ordinal + ") ? found" + ordinal +
                (string.Equals(columnName, property.Name, StringComparison.Ordinal) ? "" :
                    " : ordinals.TryGetValue(" + Literal(columnName) + ", out var mapped" + ordinal + ") ? mapped" + ordinal) + " : -1;");
            ordinalsByProperty[property.Name] = ordinal;
        }
        if (constructor.Constructor.Parameters.Length == 0)
        {
            assignments.Append("var result = new ").Append(TypeName(type)).Append("();");
        }
        else
        {
            assignments.Append("var result = new ").Append(TypeName(type)).Append("(");
            assignments.Append(string.Join(",", constructor.Properties.Select(property => ReaderValue(property, ordinalsByProperty[property.Name]))));
            assignments.Append(");");
        }
        foreach (var property in readableProperties.Where(property => !constructor.Properties.Any(item => SymbolEqualityComparer.Default.Equals(item, property))))
        {
            assignments.Append("if(").Append(ordinalsByProperty[property.Name]).Append(">=0) result.@").Append(property.Name)
                .Append(" = ").Append(ReaderValue(property, ordinalsByProperty[property.Name])).Append(";");
        }
        return result.Append(assignments).Append("return result; }; }").ToString();
    }

    /// <summary>获取生成读取器使用的 DbDataReader getter。</summary>
    private static string ReaderGetter(ITypeSymbol original)
    {
        var valueType = original is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? named.TypeArguments[0] : original;
        if (IsPostgreSqlRange(valueType))
            return "GetFieldValue<" + TypeName(valueType) + ">";
        var enumType = valueType as INamedTypeSymbol;
        var readType = valueType.TypeKind == TypeKind.Enum ? enumType!.EnumUnderlyingType! : valueType;
        var getter = readType.SpecialType switch
        {
            SpecialType.System_Object => "GetValue",
            SpecialType.System_String => "GetString", SpecialType.System_Boolean => "GetBoolean", SpecialType.System_Byte => "GetByte",
            SpecialType.System_Int16 => "GetInt16", SpecialType.System_Int32 => "GetInt32", SpecialType.System_Int64 => "GetInt64",
            // DbDataReader 没有统一的无符号/有符号窄整数 Getter；这些类型由 ReaderValue
            // 在需要时按实际字段类型执行受检 Int64 转换，否则读取已装箱的精确值。
            SpecialType.System_SByte or SpecialType.System_UInt16 or SpecialType.System_UInt32 or
            SpecialType.System_UInt64 => "GetValue",
            SpecialType.System_Char => "GetChar",
            SpecialType.System_Single => "GetFloat", SpecialType.System_Double => "GetDouble", SpecialType.System_Decimal => "GetDecimal",
            _ => readType.ToDisplayString() switch { "System.DateTime" => "GetDateTime", "System.Guid" => "GetGuid", _ => "" }
        };
        return readType is IArrayTypeSymbol { Rank: 1, ElementType.SpecialType: SpecialType.System_Byte }
            ? "GetValue" : getter;
    }

    /// <summary>生成单个结果成员的读取表达式。</summary>
    private static string ReaderValue(IPropertySymbol property, string ordinal)
        => ReaderValue(property.Type, ordinal);

    /// <summary>生成实体成员和命名标量共用的读取表达式，并保留现有转换及 DBNull 默认值语义。</summary>
    private static string ReaderValue(ITypeSymbol type, string ordinal)
    {
        var getter = ReaderGetter(type);
        if (getter.Length == 0) throw new NotSupportedException("结果读取类型尚未支持：" + type);
        var valueType = type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? named.TypeArguments[0] : type;
        var integerType = valueType is INamedTypeSymbol enumType && valueType.TypeKind == TypeKind.Enum
            ? enumType.EnumUnderlyingType!
            : valueType;
        var readType = integerType.SpecialType switch
        {
            SpecialType.System_Int16 => "reader.GetFieldType(" + ordinal + ")==typeof(long) ? checked((short)reader.GetInt64(" + ordinal + ")) : reader.GetInt16(" + ordinal + ")",
            SpecialType.System_Int32 => "reader.GetFieldType(" + ordinal + ")==typeof(long) ? checked((int)reader.GetInt64(" + ordinal + ")) : reader.GetInt32(" + ordinal + ")",
            SpecialType.System_Byte => "reader.GetFieldType(" + ordinal + ")==typeof(long) ? checked((byte)reader.GetInt64(" + ordinal + ")) : reader.GetByte(" + ordinal + ")",
            SpecialType.System_SByte => "reader.GetFieldType(" + ordinal + ")==typeof(long) ? checked((sbyte)reader.GetInt64(" + ordinal + ")) : (sbyte)reader.GetValue(" + ordinal + ")",
            SpecialType.System_UInt16 => "reader.GetFieldType(" + ordinal + ")==typeof(long) ? checked((ushort)reader.GetInt64(" + ordinal + ")) : (ushort)reader.GetValue(" + ordinal + ")",
            SpecialType.System_UInt32 => "reader.GetFieldType(" + ordinal + ")==typeof(long) ? checked((uint)reader.GetInt64(" + ordinal + ")) : (uint)reader.GetValue(" + ordinal + ")",
            SpecialType.System_UInt64 => "reader.GetFieldType(" + ordinal + ")==typeof(long) ? checked((ulong)reader.GetInt64(" + ordinal + ")) : (ulong)reader.GetValue(" + ordinal + ")",
            SpecialType.System_Char => "reader.GetFieldType(" + ordinal + ")==typeof(long) ? checked((char)reader.GetInt64(" + ordinal + ")) : reader.GetChar(" + ordinal + ")",
            _ => "reader." + getter + "(" + ordinal + ")"
        };
        return "(" + ordinal + " < 0 || reader.IsDBNull(" + ordinal + ")) ? default(" + TypeName(type) + ") : (" + TypeName(type) + ")(" + readType + ")";
    }
    /// <summary>
    /// 待分析的 SQL 查询调用信息。
    /// </summary>
    private sealed class Call
    {
        /// <summary>
        /// 初始化一个 <see cref="Call" /> 类型的实例。
        /// </summary>
        /// <param name="syntax">表示待分析调用的语法节点。</param>
        /// <param name="method">待分析调用对应的方法符号。</param>
        /// <param name="model">用于分析当前调用的语义模型。</param>
        internal Call(InvocationExpressionSyntax syntax, IMethodSymbol method, SemanticModel model) { Syntax = syntax; Method = method; Model = model; }
        /// <summary>
        /// 获取待分析调用的语法节点。
        /// </summary>
        internal InvocationExpressionSyntax Syntax { get; }
        /// <summary>
        /// 获取调用的方法符号。
        /// </summary>
        internal IMethodSymbol Method { get; }
        /// <summary>
        /// 获取调用所在语法树的语义模型。
        /// </summary>
        internal SemanticModel Model { get; }
    }
}
