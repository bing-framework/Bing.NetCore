using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Bing.Data.Sql.Generators;

public sealed partial class SqlQueryGenerator
{
    /// <summary>只识别本库的实体 Returning 重载，不影响字符串列或其他同名方法。</summary>
    private static bool IsReturningCall(IMethodSymbol method) =>
        method.Name == "Returning" && method.ContainingType.ToDisplayString() == "Bing.Data.Sql.MutationClauseExtensions" &&
        method.Parameters.Any(parameter => parameter.Type is INamedTypeSymbol { Name: "Expression" });

    /// <summary>生成直接属性或有界列运算的 Returning 文本，运行时核对当前映射后采用。</summary>
    private static string GenerateReturning(Call call, int id)
    {
        var invocation = (IInvocationOperation)call.Model.GetOperation(call.Syntax)!;
        var method = invocation.TargetMethod;
        var entity = method.TypeArguments.Last();
        if (entity is not INamedTypeSymbol named || !Accessible(named))
            throw new NotSupportedException("Returning 生成要求可访问的具体实体类型。");
        var argument = invocation.Arguments.Single(item => item.Parameter!.Name == "columns");
        var lambda = argument.Syntax is ArgumentSyntax { Expression: LambdaExpressionSyntax inline } ? inline : null;
        var cached = false;
        if (lambda == null && ReturningOperand(argument.Value) is IFieldReferenceOperation field &&
            field.Field.IsStatic && field.Field.IsReadOnly &&
            !field.Field.ContainingType.StaticConstructors.Any(constructor => !constructor.IsImplicitlyDeclared) &&
            field.Field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is
                VariableDeclaratorSyntax { Initializer.Value: LambdaExpressionSyntax initializer })
        {
            // 只采用声明处固定的 Lambda；显式静态构造器仍可能重写 readonly 字段。
            lambda = initializer;
            cached = true;
        }
        if (lambda == null || lambda.Body is not ExpressionSyntax)
            throw new NotSupportedException("Returning 动态表达式保留运行时布局解析。");
        var model = cached ? call.Model.Compilation.GetSemanticModel(lambda.SyntaxTree) : call.Model;
        var parameter = lambda is SimpleLambdaExpressionSyntax simple ? simple.Parameter :
            ((ParenthesizedLambdaExpressionSyntax)lambda).ParameterList.Parameters.Single();
        var parameterSymbol = model.GetDeclaredSymbol(parameter);
        var columns = new List<(string Property, string Alias, string Column)>();

        void Add(IOperation value, string? alias)
        {
            if (ReturningOperand(value) is not IPropertyReferenceOperation { Arguments.Length: 0, Instance: IParameterReferenceOperation source } property ||
                !SymbolEqualityComparer.Default.Equals(source.Parameter, parameterSymbol))
                throw new NotSupportedException("Returning 生成仅支持直接实体属性投影。");
            columns.Add((property.Property.Name, alias ?? property.Property.Name, ColumnName(property.Property)));
        }

        // 返回操作保留编译器补入的装箱转换，与 Expression.Body 的层级一致。
        var function = (IAnonymousFunctionOperation)model.GetOperation(lambda)!;
        var returnValue = function.Body.Operations.OfType<IReturnOperation>().Single().ReturnedValue!;
        var projection = ReturningOperand(returnValue);
        var operands = ReturningProjectionOperands(projection);
        if (operands.Any(item => ReturningOperand(item.Value) is IBinaryOperation or IUnaryOperation))
            return GenerateComputedReturning(call, id, invocation, entity, parameterSymbol!, operands, cached);
        foreach (var item in operands) Add(item.Value, item.Alias);
        if (columns.Count == 0) throw new NotSupportedException("Returning 需要至少一个属性。");

        var builderType = TypeName(method.Parameters[0].Type);
        var layout = "ReturningLayout_" + id;
        var generated = SupportsGeneratedReturning(call.Model.Compilation) &&
            columns.All(column => ReturningIdentifier(column.Column) && ReturningIdentifier(column.Alias));
        var generatedColumns = "ReturningColumns_" + id;
        var generatedSql = "ReturningSql_" + id;
        return "private static readonly (string Property, string Alias)[] " + layout +
            " = new (string, string)[]{" + string.Join(",", columns.Select(column =>
                "(" + Literal(column.Property) + "," + Literal(column.Alias) + ")")) + "};\n" +
            (generated ? GenerateReturningSql(columns, generatedColumns, generatedSql) : "") +
            InterceptionLocation(call) + "\ninternal static " + TypeName(method.ReturnType) + " Returning_" + id +
            "(this " + builderType + " source, " + TypeName(method.Parameters[1].Type) + " columns) {\n" +
            (cached ? ReturningArgumentChecks : "") + "return ApplyReturning<" +
            builderType + ", " + TypeName(entity) + ">(source, " + layout +
            (generated ? ", " + generatedColumns + ", " + generatedSql : "") + ");\n}";
    }

    // 类型初始化递归期间字段可能尚未赋值，保持原重载的参数校验顺序。
    private const string ReturningArgumentChecks = """
        if (source == null) throw new global::System.ArgumentNullException(nameof(source));
        if (columns == null) throw new global::System.ArgumentNullException(nameof(columns));

        """;

    /// <summary>直接列与计算列复用匿名对象及 DTO 的投影顺序。</summary>
    private static List<(IOperation Value, string? Alias)> ReturningProjectionOperands(IOperation projection)
    {
        var result = new List<(IOperation Value, string? Alias)>();
        if (projection is IAnonymousObjectCreationOperation anonymous)
        {
            foreach (var operation in anonymous.Initializers)
            {
                if (operation is not ISimpleAssignmentOperation { Target: IPropertyReferenceOperation target } assignment)
                    throw new NotSupportedException("Returning 匿名投影需要直接属性赋值。");
                result.Add((assignment.Value, target.Property.Name));
            }
        }
        else if (projection is IObjectCreationOperation { Constructor.Parameters.Length: 0, Initializer: not null } creation)
        {
            foreach (var operation in creation.Initializer.Initializers)
            {
                if (operation is not ISimpleAssignmentOperation assignment)
                    throw new NotSupportedException("Returning DTO 仅支持直接成员赋值。");
                var alias = assignment.Target switch
                {
                    IPropertyReferenceOperation target => target.Property.Name,
                    IFieldReferenceOperation target => target.Field.Name,
                    _ => throw new NotSupportedException("Returning DTO 仅支持直接成员赋值。")
                };
                result.Add((assignment.Value, alias));
            }
        }
        else result.Add((projection, null));
        return result;
    }

    /// <summary>固定物理名可在编译期引用；无效名称和通配符保留原处理路径。</summary>
    private static bool ReturningIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) && value != "*" &&
        !value.Any(character => character is '\r' or '\n' or ';');

    /// <summary>与默认 DialectBase.SafeName 保持一致，清理外层引用符并转义内部结束符。</summary>
    private static string ReturningName(string value, string open, string close)
    {
        var name = value.Trim().TrimStart('[').TrimEnd(']').TrimStart('`').TrimEnd('`').TrimStart('"').TrimEnd('"');
        return open + name.Replace(close, close + close) + close;
    }

    /// <summary>固定投影顺序和别名，SQL Server 根据实际写入类型选择 Inserted 或 Deleted。</summary>
    private static string GenerateReturningSql(IReadOnlyList<(string Property, string Alias, string Column)> columns,
        string columnsName, string factoryName)
    {
        string Projection(string open, string close, string qualifier = "") => string.Join(", ", columns.Select(column =>
            qualifier + ReturningName(column.Column, open, close) + " As " + ReturningName(column.Alias, open, close)));
        const string kind = "global::Bing.Data.Sql.SqlExecutionKind.";
        var allKinds = kind + "Insert or " + kind + "Update or " + kind + "Delete";
        return "private static readonly global::Bing.Data.Sql.Builders.Mutations.SqlReturningColumn[] " + columnsName +
            " = new global::Bing.Data.Sql.Builders.Mutations.SqlReturningColumn[]{" + string.Join(",", columns.Select(column =>
                "new global::Bing.Data.Sql.Builders.Mutations.SqlReturningColumn(" + Literal(column.Column) +
                ", null, " + Literal(column.Alias) + ")")) + "};\n" +
            "private static string " + factoryName + "(string provider, global::Bing.Data.Sql.SqlExecutionKind kind) => (provider, kind) switch {\n" +
            "(\"bing.sqlite\", " + allKinds + ") => " + Literal(" Returning " + Projection("`", "`")) + ",\n" +
            "(\"bing.postgresql\", " + allKinds + ") => " + Literal(" Returning " + Projection("\"", "\"")) + ",\n" +
            "(\"bing.sqlserver\", " + kind + "Insert or " + kind + "Update) => " + Literal(" Output " + Projection("[", "]", "[Inserted].")) + ",\n" +
            "(\"bing.sqlserver\", " + kind + "Delete) => " + Literal(" Output " + Projection("[", "]", "[Deleted].")) + ",\n" +
            "_ => null };\n";
    }

    /// <summary>生成器可独立升级；仅在运行库具有精确的生成文本合同后引用该入口。</summary>
    private static bool SupportsGeneratedReturning(Compilation compilation)
    {
        var clause = compilation.GetTypeByMetadataName("Bing.Data.Sql.Builders.Mutations.Clauses.ReturningClause");
        var column = compilation.GetTypeByMetadataName("Bing.Data.Sql.Builders.Mutations.SqlReturningColumn");
        var kind = compilation.GetTypeByMetadataName("Bing.Data.Sql.SqlExecutionKind");
        var list = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyList`1");
        var func = compilation.GetTypeByMetadataName("System.Func`3");
        if (clause == null || column == null || kind == null || list == null || func == null) return false;
        var text = compilation.GetSpecialType(SpecialType.System_String);
        var columnsType = list.Construct(column);
        var factoryType = func.Construct(text, kind, text);
        return clause.GetMembers("TryAddGenerated").OfType<IMethodSymbol>().Any(method =>
            method.DeclaredAccessibility == Accessibility.Public && !method.IsStatic && !method.IsGenericMethod &&
            method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 2 &&
            method.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, columnsType) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, factoryType));
    }

    /// <summary>投影项仅剥离一次装箱或直接列内建转换；计算内部和结果的数值转换保持原状。</summary>
    private static IOperation ReturningOperand(IOperation operation)
    {
        while (operation is IParenthesizedOperation parenthesized) operation = parenthesized.Operand;
        if (operation is IConversionOperation { OperatorMethod: null } conversion)
        {
            var operand = conversion.Operand;
            while (operand is IParenthesizedOperation parentheses) operand = parentheses.Operand;
            if (conversion.Type?.SpecialType == SpecialType.System_Object || operand is IPropertyReferenceOperation)
                operation = operand;
        }
        return operation;
    }

    /// <summary>新运行库复用映射查找；旧运行库继续生成原有扫描，允许生成器独立升级。</summary>
    private static string CreateReturningHelper(Compilation compilation)
    {
        var mapping = compilation.GetTypeByMetadataName("Bing.Data.Sql.Metadata.EntityMappingMetadata");
        var supportsLookup = mapping?.GetMembers("FindColumn").OfType<IMethodSymbol>().Any(method =>
            method.DeclaredAccessibility == Accessibility.Public && !method.IsStatic && !method.IsGenericMethod &&
            method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
            method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
            method.ReturnType.ToDisplayString() == "Bing.Data.Sql.Metadata.ColumnMappingMetadata") == true;
        var supportsGenerated = SupportsGeneratedReturning(compilation);
        return ReturningHelper
            .Replace("/*GENERATED_PARAMETERS*/", supportsGenerated
                ? ", global::Bing.Data.Sql.Builders.Mutations.SqlReturningColumn[] generatedColumns = null, global::System.Func<string, global::Bing.Data.Sql.SqlExecutionKind, string> sqlFactory = null"
                : "")
            .Replace("/*GENERATED_LOOKUP*/", supportsGenerated ? GeneratedReturningLookup : "")
            .Replace("/*COLUMN_LOOKUP*/", supportsLookup
                ? "var column = mapping?.FindColumn(layout[i].Property);"
                : LegacyReturningLookup) +
            (SupportsComputedReturning(compilation) ? ComputedReturningHelper : "");
    }

    private const string GeneratedReturningLookup = """
        if (qualifier == null && generatedColumns != null &&
            source.ReturningClause is global::Bing.Data.Sql.Builders.Mutations.Clauses.ReturningClause returning)
        {
            var matches = true;
            for (var i = 0; i < layout.Length; i++)
            {
                /*COLUMN_LOOKUP*/
                if (column == null || !global::System.String.Equals(column.ColumnName, generatedColumns[i].Column,
                    global::System.StringComparison.Ordinal))
                { matches = false; break; }
            }
            if (matches && returning.TryAddGenerated(generatedColumns, sqlFactory)) return source;
        }
        """;

    private const string LegacyReturningLookup = """
        global::Bing.Data.Sql.Metadata.ColumnMappingMetadata column = null;
        if (mapping?.Columns != null)
            foreach (var item in mapping.Columns.Values)
                if (global::System.String.Equals(item.PropertyName, layout[i].Property, global::System.StringComparison.Ordinal))
                { column = item; break; }
        """;

    // 布局是生成类型的私有只读字段，不增加表达式缓存。
    private const string ReturningHelper = """
        private static T ApplyReturning<T, TEntity>(T source, (string Property, string Alias)[] layout/*GENERATED_PARAMETERS*/)
            where T : global::Bing.Data.Sql.Builders.Mutations.Accessors.IReturningClauseAccessor,
                global::Bing.Data.Sql.Builders.Mutations.Accessors.ISqlMutationContextAccessor
            where TEntity : class
        {
            if (source == null) throw new global::System.ArgumentNullException(nameof(source));
            var context = source.MutationContext;
            var mapping = context.Services.EntityMappingResolver.Resolve(typeof(TEntity), context.ExecutionContext.DatabaseContext);
            var qualifier = source is global::Bing.Data.Sql.Builders.Mutations.Accessors.IUpdateClauseAccessor update
                ? update.UpdateClause.Table?.Alias
                : source is global::Bing.Data.Sql.Builders.Mutations.Accessors.IDeleteClauseAccessor delete
                    ? delete.DeleteClause.Table?.Alias : null;
            /*GENERATED_LOOKUP*/
            var columns = new global::Bing.Data.Sql.Builders.Mutations.SqlReturningColumn[layout.Length];
            for (var i = 0; i < layout.Length; i++)
            {
                /*COLUMN_LOOKUP*/
                if (column == null)
                    throw new global::System.InvalidOperationException($"实体 {typeof(TEntity).Name} 的属性 {layout[i].Property} 未映射到数据库列。");
                columns[i] = new global::Bing.Data.Sql.Builders.Mutations.SqlReturningColumn(column.ColumnName, qualifier, layout[i].Alias);
            }
            source.ReturningClause.AddRange(columns);
            return source;
        }
        """;
}
