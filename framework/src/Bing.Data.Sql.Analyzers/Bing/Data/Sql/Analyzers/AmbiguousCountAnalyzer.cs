using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bing.Data.Sql.Analyzers;

/// <summary>
/// 检测统一 Count API 的单个位置字符串参数调用，避免升级后将旧别名静默解释为列名。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AmbiguousCountAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// 当前分析器支持的诊断规则。
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(BingSqlDiagnosticDescriptors.AmbiguousCount);

    /// <summary>
    /// 初始化语法分析注册。
    /// </summary>
    /// <param name="context">分析器初始化上下文。</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>
    /// 分析 Count 调用是否使用了单个位置字符串参数。
    /// </summary>
    /// <param name="context">语法节点分析上下文。</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 1)
            return;

        var argument = invocation.ArgumentList.Arguments[0];
        if (argument.NameColon != null || argument.Expression.IsKind(SyntaxKind.StringLiteralExpression) == false)
            return;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method ||
            IsSqlCountExtension(method) == false)
            return;

        context.ReportDiagnostic(Diagnostic.Create(BingSqlDiagnosticDescriptors.AmbiguousCount, argument.GetLocation()));
    }

    /// <summary>
    /// 判断方法是否为 Bing.Data.Sql 的 Count 扩展入口。
    /// </summary>
    /// <param name="method">调用目标方法。</param>
    /// <returns>是统一 Count 扩展入口时返回 <c>true</c>。</returns>
    private static bool IsSqlCountExtension(IMethodSymbol method)
    {
        var definition = method.ReducedFrom ?? method.OriginalDefinition;
        return definition.Name == "Count" &&
               definition.ContainingType.ToDisplayString() == "Bing.Data.Sql.Extensions" &&
               (method.ReducedFrom != null || definition.IsExtensionMethod);
    }
}