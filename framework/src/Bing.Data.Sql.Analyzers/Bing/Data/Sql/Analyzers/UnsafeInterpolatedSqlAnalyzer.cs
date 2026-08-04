using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bing.Data.Sql.Analyzers;

/// <summary>
/// 检测插值字符串或其拼接结果传入普通 SQL 文本入口的情况。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsafeInterpolatedSqlAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(BingSqlDiagnosticDescriptors.UnsafeInterpolatedSql);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>
    /// 分析普通 SQL 文本入口的首个参数是否来自插值表达式。
    /// </summary>
    /// <param name="context">语法节点分析上下文。</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count == 0 ||
            context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method ||
            IsSqlTextEntryPoint(method) == false)
            return;
        var argument = invocation.ArgumentList.Arguments[0];
        if (ContainsInterpolatedValue(argument.Expression, context.SemanticModel, context.CancellationToken))
            context.ReportDiagnostic(Diagnostic.Create(BingSqlDiagnosticDescriptors.UnsafeInterpolatedSql,
                argument.Expression.GetLocation()));
    }

    /// <summary>
    /// 判断目标是否为 ISqlQuery 实现上的普通字符串 SQL 入口。
    /// </summary>
    private static bool IsSqlTextEntryPoint(IMethodSymbol method)
    {
        if (method.Name != "Sql")
            return false;
        var extensionMethod = method.ReducedFrom ?? method;
        if (extensionMethod.IsExtensionMethod)
            return extensionMethod.Parameters.Length > 1 &&
                   extensionMethod.Parameters[0].Type.ToDisplayString() == "Bing.Data.Sql.ISqlQuery" &&
                   extensionMethod.Parameters[1].Type.SpecialType == SpecialType.System_String;
        if (method.Parameters.Length == 0 || method.Parameters[0].Type.SpecialType != SpecialType.System_String)
            return false;
        var type = method.ContainingType;
        return type.ToDisplayString() == "Bing.Data.Sql.ISqlQuery" || type.AllInterfaces.Any(@interface =>
            @interface.ToDisplayString() == "Bing.Data.Sql.ISqlQuery");
    }

    /// <summary>
    /// 判断表达式是否包含实际插值值，支持字符串拼接和局部变量初始化。
    /// </summary>
    private static bool ContainsInterpolatedValue(ExpressionSyntax expression, SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        switch (expression)
        {
            case InterpolatedStringExpressionSyntax interpolated:
                return interpolated.Contents.OfType<InterpolationSyntax>().Any();
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } binary:
                return ContainsInterpolatedValue(binary.Left, semanticModel, cancellationToken) ||
                       ContainsInterpolatedValue(binary.Right, semanticModel, cancellationToken);
            case ParenthesizedExpressionSyntax parenthesized:
                return ContainsInterpolatedValue(parenthesized.Expression, semanticModel, cancellationToken);
            case IdentifierNameSyntax identifier when semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol is ILocalSymbol local:
                return local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) is VariableDeclaratorSyntax
                {
                    Initializer.Value: var initializer
                } && ContainsInterpolatedValue(initializer, semanticModel, cancellationToken);
            default:
                return false;
        }
    }
}