using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Bing.Data.Sql.Generators;

public sealed partial class SqlQueryGenerator
{
    /// <summary>生成同类型数值列的负号和四则运算，不引入常量槽位或通用表达式翻译器。</summary>
    private static string GenerateComputedReturning(Call call, int id, IInvocationOperation invocation,
        ITypeSymbol entity, ISymbol parameter, IReadOnlyList<(IOperation Value, string? Alias)> operands, bool cached)
    {
        if (!SupportsComputedReturning(call.Model.Compilation))
            throw new NotSupportedException("当前运行库缺少计算型 Returning 合同，保留原 Returning 调用。");
        var dependencies = new List<(string Property, string Column)>();
        var projections = new List<(string? Alias, List<string> Parts)>();
        var hasUnary = false;
        foreach (var operand in operands)
        {
            var parts = new List<string> { "" };
            void Text(string value) => parts[parts.Count - 1] += value;
            void Visit(IOperation value)
            {
                while (value is IParenthesizedOperation parenthesized) value = parenthesized.Operand;
                if (value is IPropertyReferenceOperation { Arguments.Length: 0, Instance: IParameterReferenceOperation source } property &&
                    SymbolEqualityComparer.Default.Equals(source.Parameter, parameter))
                {
                    var column = ColumnName(property.Property);
                    var dependency = (property.Property.Name, column);
                    if (!dependencies.Contains(dependency)) dependencies.Add(dependency);
                    // 保留属性身份：不同属性可先映射同一列，再由当前配置分别重映射。
                    parts.Add(property.Property.Name);
                    parts.Add("");
                    return;
                }
                if (value is IUnaryOperation unary)
                {
                    if (unary.OperatorKind != UnaryOperatorKind.Minus || unary.IsChecked ||
                        !SymbolEqualityComparer.Default.Equals(unary.Type, unary.Operand.Type) ||
                        !ReturningNumericType(unary.Type) ||
                        unary.OperatorMethod != null && unary.OperatorMethod.ContainingType.SpecialType != SpecialType.System_Decimal)
                        throw new NotSupportedException("Returning 一元投影仅支持未使用 checked 的数值负号。");
                    hasUnary = true;
                    Text("(-");
                    Visit(unary.Operand);
                    Text(")");
                    return;
                }
                if (value is not IBinaryOperation binary || binary.IsChecked ||
                    !SymbolEqualityComparer.Default.Equals(binary.LeftOperand.Type, binary.RightOperand.Type) ||
                    !SymbolEqualityComparer.Default.Equals(binary.Type, binary.LeftOperand.Type) ||
                    !ReturningNumericType(binary.Type) ||
                    binary.OperatorMethod != null && binary.OperatorMethod.ContainingType.SpecialType != SpecialType.System_Decimal)
                    throw new NotSupportedException("Returning 计算投影仅支持同类型数值列的四则运算。");
                var operation = binary.OperatorKind switch
                {
                    BinaryOperatorKind.Add => " + ",
                    BinaryOperatorKind.Subtract => " - ",
                    BinaryOperatorKind.Multiply => " * ",
                    BinaryOperatorKind.Divide => " / ",
                    _ => throw new NotSupportedException("Returning 计算投影仅支持四则运算。")
                };
                Text("(");
                Visit(binary.LeftOperand);
                Text(operation);
                Visit(binary.RightOperand);
                Text(")");
            }
            var value = ReturningOperand(operand.Value);
            Visit(value);
            var alias = operand.Alias ?? (value as IPropertyReferenceOperation)?.Property.Name;
            projections.Add((alias, parts));
        }
        if (hasUnary && !SupportsComputedReturning(call.Model.Compilation, "CreateArithmetic"))
            throw new NotSupportedException("当前运行库缺少数值负号 Returning 合同，保留原 Returning 调用。");
        // 固定名称按默认方言提前引用，禁止字符仍交由原路径校验。
        if (dependencies.Any(item => !ReturningIdentifier(item.Column)) ||
            projections.Any(item => item.Alias != null && !ReturningIdentifier(item.Alias)))
            throw new NotSupportedException("计算投影包含无效标识符或通配符。");

        int Binding(string property) => dependencies.FindIndex(item => item.Property == property);
        string Projection(string open, string close, string qualifier = "") => string.Join(", ", projections.Select(item =>
            string.Concat(item.Parts.Select((part, index) => index % 2 == 0 ? part :
                qualifier + ReturningName(dependencies[Binding(part)].Column, open, close))) +
            (item.Alias == null ? "" : " As " + ReturningName(item.Alias, open, close))));
        const string kind = "global::Bing.Data.Sql.SqlExecutionKind.";
        const string columnType = "global::Bing.Data.Sql.Builders.Mutations.SqlReturningColumn";
        var allKinds = kind + "Insert or " + kind + "Update or " + kind + "Delete";
        var columnsName = "ReturningColumns_" + id;
        var factoryName = "ReturningSql_" + id;
        var layoutName = "ReturningDependencies_" + id;
        var builderType = TypeName(invocation.TargetMethod.Parameters[0].Type);
        string Column(string property, bool bind) => bind
            ? "column_" + Binding(property) + ".ColumnName"
            : Literal(dependencies[Binding(property)].Column);
        var columnFactory = hasUnary ? ".CreateArithmetic(" : ".CreateComputed(";
        string Columns(bool bind) => string.Join(",", projections.Select(item =>
            item.Parts.Count == 3 && item.Parts[0].Length == 0 && item.Parts[2].Length == 0
            ? "new " + columnType + "(" + Column(item.Parts[1], bind) + ", " +
                (bind ? "qualifier" : "null") + ", " + Literal(item.Alias) + ")"
            : columnType + columnFactory + "new string[]{" +
                string.Join(",", item.Parts.Select((part, index) => index % 2 == 0 ? Literal(part) : Column(part, bind))) +
                "}, " + (bind ? "qualifier" : "null") + ", " + Literal(item.Alias) + ")"));
        // 桥接方法全部传位置参数，不依赖可选参数标记或参数名称。
        return "private static readonly (string Property, string Column)[] " + layoutName + " = new (string, string)[]{" +
            string.Join(",", dependencies.Select(item => "(" + Literal(item.Property) + "," + Literal(item.Column) + ")")) + "};\n" +
            "private static readonly " + columnType + "[] " + columnsName + " = new " + columnType + "[]{" +
            Columns(false) + "};\n" +
            "private static string " + factoryName + "(string provider, global::Bing.Data.Sql.SqlExecutionKind kind) => (provider, kind) switch {\n" +
            "(\"bing.sqlite\", " + allKinds + ") => " + Literal(" Returning " + Projection("`", "`")) + ",\n" +
            "(\"bing.postgresql\", " + allKinds + ") => " + Literal(" Returning " + Projection("\"", "\"")) + ",\n" +
            "(\"bing.sqlserver\", " + kind + "Insert or " + kind + "Update) => " + Literal(" Output " + Projection("[", "]", "[Inserted].")) + ",\n" +
            "(\"bing.sqlserver\", " + kind + "Delete) => " + Literal(" Output " + Projection("[", "]", "[Deleted].")) + ",\n_ => null };\n" +
            InterceptionLocation(call) + "\ninternal static " + TypeName(invocation.TargetMethod.ReturnType) + " Returning_" + id +
            "(this " + builderType + " source, " + TypeName(invocation.TargetMethod.Parameters[1].Type) + " columns) {\n" +
            (cached ? ReturningArgumentChecks : "") +
            "if (TryApplyComputedReturning<" + builderType + ", " + TypeName(entity) + ">(source, " +
            layoutName + ", " + columnsName + ", " + factoryName + ", out var mapping, out var qualifier)) return source;\n" +
            string.Join("\n", dependencies.Select((item, index) =>
                "var column_" + index + " = mapping?.FindColumn(" + Literal(item.Property) + ") ?? throw new global::System.InvalidOperationException(" +
                Literal("实体 " + entity.Name + " 的属性 " + item.Property + " 未映射到数据库列。") + ");")) + "\n" +
            "source.ReturningClause.AddRange(new " + columnType + "[]{" + Columns(true) + "});\nreturn source;\n}";
    }

    private static bool ReturningNumericType(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        return type?.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Decimal or
            SpecialType.System_Double or SpecialType.System_Single;
    }

    /// <summary>同时检查生成文本、映射查找与不可变计算列合同，允许生成器和运行库独立升级。</summary>
    private static bool SupportsComputedReturning(Compilation compilation, string factoryName = "CreateComputed")
    {
        if (!SupportsGeneratedReturning(compilation)) return false;
        var column = compilation.GetTypeByMetadataName("Bing.Data.Sql.Builders.Mutations.SqlReturningColumn")!;
        var list = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyList`1");
        var mapping = compilation.GetTypeByMetadataName("Bing.Data.Sql.Metadata.EntityMappingMetadata");
        if (list == null || mapping == null) return false;
        var parts = list.Construct(compilation.GetSpecialType(SpecialType.System_String));
        return column.GetMembers(factoryName).OfType<IMethodSymbol>().Any(method =>
            method.DeclaredAccessibility == Accessibility.Public && method.IsStatic && !method.IsGenericMethod &&
            SymbolEqualityComparer.Default.Equals(method.ReturnType, column) && method.Parameters.Length == 3 &&
            method.Parameters.All(item => item.RefKind == RefKind.None) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, parts) &&
            method.Parameters.Skip(1).All(item => item.Type.SpecialType == SpecialType.System_String)) &&
            mapping.GetMembers("FindColumn").OfType<IMethodSymbol>().Any(method =>
                method.DeclaredAccessibility == Accessibility.Public && !method.IsStatic && !method.IsGenericMethod &&
                method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                method.ReturnType.ToDisplayString() == "Bing.Data.Sql.Metadata.ColumnMappingMetadata");
    }

    private const string ComputedReturningHelper = """
        private static bool TryApplyComputedReturning<T, TEntity>(T source,
            (string Property, string Column)[] dependencies,
            global::Bing.Data.Sql.Builders.Mutations.SqlReturningColumn[] generatedColumns,
            global::System.Func<string, global::Bing.Data.Sql.SqlExecutionKind, string> sqlFactory,
            out global::Bing.Data.Sql.Metadata.EntityMappingMetadata mapping, out string qualifier)
            where T : global::Bing.Data.Sql.Builders.Mutations.Accessors.IReturningClauseAccessor,
                global::Bing.Data.Sql.Builders.Mutations.Accessors.ISqlMutationContextAccessor
            where TEntity : class
        {
            if (source == null) throw new global::System.ArgumentNullException(nameof(source));
            var context = source.MutationContext;
            mapping = context.Services.EntityMappingResolver.Resolve(typeof(TEntity), context.ExecutionContext.DatabaseContext);
            qualifier = source is global::Bing.Data.Sql.Builders.Mutations.Accessors.IUpdateClauseAccessor update
                ? update.UpdateClause.Table?.Alias
                : source is global::Bing.Data.Sql.Builders.Mutations.Accessors.IDeleteClauseAccessor delete
                    ? delete.DeleteClause.Table?.Alias : null;
            if (qualifier == null)
            {
                var matches = true;
                for (var i = 0; i < dependencies.Length; i++)
                {
                    var column = mapping?.FindColumn(dependencies[i].Property);
                    if (column == null || !global::System.String.Equals(column.ColumnName, dependencies[i].Column,
                        global::System.StringComparison.Ordinal))
                    { matches = false; break; }
                }
                if (matches)
                {
                    if (source.ReturningClause is global::Bing.Data.Sql.Builders.Mutations.Clauses.ReturningClause returning &&
                        returning.TryAddGenerated(generatedColumns, sqlFactory)) return true;
                    source.ReturningClause.AddRange(generatedColumns);
                    return true;
                }
            }
            return false;
        }
        """;
}
