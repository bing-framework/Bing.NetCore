using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bing.Data.Sql.Generators;

public sealed partial class SqlQueryGenerator
{
    /// <summary>只优化完整且独占的 fluent 链，未知操作保留原来的逐调用生成路径。</summary>
    private static string GenerateTemplates(ImmutableArray<Call?> calls,
        Dictionary<InvocationExpressionSyntax, (Call Call, string Code)> generated)
    {
        var descriptors = new StringBuilder();
        var id = 0;
        var bySyntax = calls.Where(call => call != null).ToDictionary(call => call!.Syntax, call => call!);
        foreach (var terminal in calls.Where(call => call != null).Select(call => call!))
        {
            if (terminal.Model.Compilation.GetTypeByMetadataName("Bing.Data.Sql.SqlGeneratedQueryTemplate") == null ||
                terminal.Method.Name is not ("ToSql" or "To" or "ToAsync" or "ToList" or "ToListAsync" or
                    "First" or "FirstAsync" or "FirstOrDefault" or "FirstOrDefaultAsync" or "Single" or
                    "SingleAsync" or "ToEntity" or "ToEntityAsync" or "Scalar" or "ScalarAsync" or "ToPage" or "ToPageAsync") ||
                terminal.Method.TypeArguments.Length > 1 ||
                terminal.Method.Parameters.Any(parameter => parameter.Name == "projectionPrototype") ||
                terminal.Method.Name != "ToSql" && !generated.ContainsKey(terminal.Syntax)) continue;
            var chain = new List<Call>();
            var cursor = terminal.Syntax;
            Call? root = null;
            while (cursor.Expression is MemberAccessExpressionSyntax member && member.Expression is InvocationExpressionSyntax receiver &&
                   bySyntax.TryGetValue(receiver, out var previous))
            {
                if (previous.Method.Name == "From") { root = previous; break; }
                chain.Add(previous);
                cursor = previous.Syntax;
            }
            if (root == null || !FreshRoot(root) || !chain.Any(call => call.Method.Name == "WhereIf")) continue;
            chain.Reverse();
            var operations = new List<string>();
            var replacements = new List<(Call Call, string Code)>();
            var templateName = "Template_" + id++;
            foreach (var call in chain)
            {
                if (call.Method.Name is not ("Where" or "WhereIf" or "Select" or "ReplaceSelect" or "OrderBy") ||
                    !generated.TryGetValue(call.Syntax, out var item) ||
                    !TryTemplateOperation(call, item.Code, templateName, operations.Count, out var operation, out var replacement)) break;
                operations.Add(operation);
                replacements.Add((call, replacement));
            }
            if (operations.Count != chain.Count) continue;
            descriptors.Append("private static readonly ").Append(Prefix).Append("SqlGeneratedQueryTemplate ")
                .Append(templateName).Append(" = new ").Append(Prefix).Append("SqlGeneratedQueryTemplate(new ")
                .Append(Prefix).Append("SqlGeneratedQueryTemplate.Operation[]{")
                .Append(string.Join(",\n", operations)).Append("}, ")
                .Append(chain.Any(call => call.Method.Name is "Select" or "ReplaceSelect") ? "true" : "false").AppendLine(");");
            foreach (var replacement in replacements) generated[replacement.Call.Syntax] = (replacement.Call, replacement.Code);
        }
        return descriptors.ToString();
    }

    private static bool FreshRoot(Call call)
    {
        if (call.Method.TypeArguments.Length != 1 || call.Syntax.Expression is not MemberAccessExpressionSyntax member) return false;
        // ISqlQuery.From 每次创建新查询；已有 SqlLambdaQuery 可能被调用方共享，不能延迟其修改。
        if (call.Model.GetTypeInfo(member.Expression).Type?.Name == "ISqlQuery") return true;
        return member.Expression is InvocationExpressionSyntax invocation &&
            call.Model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
            method.ContainingType.ToDisplayString() == "Bing.Data.Sql.SqlQueryRuntimeFactory" && method.Name == "CreateLambdaQuery";
    }

    private static bool TryTemplateOperation(Call call, string code, string templateName, int index,
        out string operation, out string replacement)
    {
        operation = replacement = string.Empty;
        var tree = CSharpSyntaxTree.ParseText("class C {" + code + "}");
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().SingleOrDefault();
        if (method?.ExpressionBody == null) return false;
        var expression = method.ExpressionBody.Expression;
        // 闭包取值含嵌套桥接调用，保留原有路径后再识别单个模板操作。
        if (expression.ToString().Contains("ComparisonValue")) return false;
        var bridge = expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
            .SingleOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax member &&
                member.Expression.ToString() == Prefix + "SqlGeneratedQueryBridge");
        if (bridge?.Expression is not MemberAccessExpressionSyntax access) return false;
        var name = access.Name.Identifier.ValueText;
        if (name is not ("Compare" or "OptionalCompare" or "Project")) return false;
        // 一次调用只涉及一个来源；复杂谓词继续由原生成桥接处理。
        var expressionParameter = call.Method.Parameters.FirstOrDefault(parameter =>
            parameter.Type is INamedTypeSymbol type && type.Name == "Expression");
        if (expressionParameter?.Type is not INamedTypeSymbol expressionType ||
            expressionType.TypeArguments[0] is not INamedTypeSymbol function || function.TypeArguments.Length != 2) return false;
        var arguments = call.Method.Parameters.Where(parameter => !SymbolEqualityComparer.Default.Equals(parameter, expressionParameter)).ToArray();
        var rewriter = new TemplateArguments(arguments);
        var replay = rewriter.Visit(expression)!.ToString();
        var valueIndex = Array.FindIndex(arguments, parameter => parameter.Name == "value");
        var conditionIndex = Array.FindIndex(arguments, parameter => parameter.Name == "condition");
        var value = "null";
        var comparison = "null";
        if (name != "Project")
        {
            var args = bridge.ArgumentList.Arguments;
            // 列比较不创建值槽；标量和集合只读取静态常量或显式传入的值。
            if (args[args.Count - 3].Expression is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression }) return false;
            value = "static arguments => (object)(" + rewriter.Visit(args[args.Count - 1].Expression) + ")";
            comparison = "static arguments => " + rewriter.Visit(args[args.Count - 2].Expression);
        }
        operation = "new " + Prefix + "SqlGeneratedQueryTemplate.Operation(static (source, arguments) => { _ = " + replay +
            "; }, " + value + ", " + comparison + ", " + valueIndex + ", " + conditionIndex + ")";
        var apply = Prefix + "SqlGeneratedQueryTemplate.Apply(source, " + templateName + ", " + index +
            ", new object[]{" + string.Join(",", arguments.Select(parameter => "@" + parameter.Name)) + "})";
        var rewritten = method.WithExpressionBody(method.ExpressionBody.WithExpression(SyntaxFactory.ParseExpression(apply)));
        replacement = code.Replace(method.ToString(), rewritten.ToString());
        return true;
    }

    /// <summary>参数值与表达式树分离，缓存中的静态委托不捕获调用对象。</summary>
    private sealed class TemplateArguments : CSharpSyntaxRewriter
    {
        private readonly IParameterSymbol[] _parameters;
        internal TemplateArguments(IParameterSymbol[] parameters) => _parameters = parameters;
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var index = Array.FindIndex(_parameters, parameter => parameter.Name == node.Identifier.ValueText);
            return index < 0 ? base.VisitIdentifierName(node) :
                SyntaxFactory.ParseExpression("((" + (_parameters[index].Name == "value" ? "object" : TypeName(_parameters[index].Type)) +
                    ")arguments[" + index + "])").WithTriviaFrom(node);
        }
    }
}
