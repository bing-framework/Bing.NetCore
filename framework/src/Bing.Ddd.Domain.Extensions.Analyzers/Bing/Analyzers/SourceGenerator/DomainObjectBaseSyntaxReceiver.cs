using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bing.Analyzers.SourceGenerator;

/// <summary>
/// 领域对象基类语法接收器
/// </summary>
sealed class DomainObjectBaseSyntaxReceiver : ISyntaxReceiver
{
    /// <summary>
    /// 获取候选类集合
    /// </summary>
    public List<ClassDeclarationSyntax> CandidateClasses { get; } = new();

    /// <inheritdoc />
    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is not ClassDeclarationSyntax classDeclarationSyntax)
            return;
        if (classDeclarationSyntax.BaseList is null)
            return;
        // 收集所有的基类
        CandidateClasses.Add(classDeclarationSyntax);
    }
}
