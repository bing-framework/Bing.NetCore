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
    /// 判断目标是否为 ISqlQuery 或 ISqlExecutor 实现上的普通字符串 SQL 入口。
    /// </summary>
    private static bool IsSqlTextEntryPoint(IMethodSymbol method)
    {
        if (method.Name is not ("Sql" or "ExecuteSql" or "ExecuteSqlAsync"))
            return false;
        var extensionMethod = method.ReducedFrom ?? method;
        if (extensionMethod.IsExtensionMethod)
            return extensionMethod.Parameters.Length > 1 &&
                   IsSqlTextReceiver(extensionMethod.Parameters[0].Type, method.Name) &&
                   extensionMethod.Parameters[1].Type.SpecialType == SpecialType.System_String;
        if (method.Parameters.Length == 0 || method.Parameters[0].Type.SpecialType != SpecialType.System_String)
            return false;
        var type = method.ContainingType;
        return IsSqlTextReceiver(type, method.Name) || type.AllInterfaces.Any(@interface =>
            IsSqlTextReceiver(@interface, method.Name));
    }

    /// <summary>
    /// 判断接收方是否为普通 SQL 文本入口所属的框架契约。
    /// </summary>
    /// <param name="type">待检查的接收方类型。</param>
    /// <param name="methodName">待检查的方法名称。</param>
    /// <returns>是受支持的 SQL 接收方时返回 <see langword="true"/>。</returns>
    private static bool IsSqlTextReceiver(ITypeSymbol type, string methodName)
    {
        var contractName = type.ToDisplayString();
        return methodName == "Sql"
            ? contractName == "Bing.Data.Sql.ISqlQuery"
            : contractName == "Bing.Data.Sql.ISqlExecutor";
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
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression or (int)SyntaxKind.CoalesceExpression } binary:
                return ContainsInterpolatedValue(binary.Left, semanticModel, cancellationToken) ||
                       ContainsInterpolatedValue(binary.Right, semanticModel, cancellationToken);
            case ParenthesizedExpressionSyntax parenthesized:
                return ContainsInterpolatedValue(parenthesized.Expression, semanticModel, cancellationToken);
            case CastExpressionSyntax cast:
                return ContainsInterpolatedValue(cast.Expression, semanticModel, cancellationToken);
            case ConditionalExpressionSyntax conditional:
                return ContainsInterpolatedValue(conditional.WhenTrue, semanticModel, cancellationToken) ||
                       ContainsInterpolatedValue(conditional.WhenFalse, semanticModel, cancellationToken);
            case InvocationExpressionSyntax invocation when IsStringConcat(invocation, semanticModel, cancellationToken):
                return invocation.ArgumentList.Arguments.Any(argument =>
                    ContainsInterpolatedValue(argument.Expression, semanticModel, cancellationToken));
            case IdentifierNameSyntax identifier when semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol is ILocalSymbol local:
                return local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) is VariableDeclaratorSyntax
                {
                    Initializer.Value: var initializer
                } && ContainsInterpolatedValue(initializer, semanticModel, cancellationToken);
            default:
                return false;
        }
    }

    /// <summary>
    /// 判断调用是否为 System.String.Concat。
    /// </summary>
    /// <param name="invocation">调用表达式。</param>
    /// <param name="semanticModel">语义模型。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>是 String.Concat 时返回 true。</returns>
    private static bool IsStringConcat(InvocationExpressionSyntax invocation, SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol
               {
                   Name: "Concat",
                   ContainingType.SpecialType: SpecialType.System_String
               };
    }
}