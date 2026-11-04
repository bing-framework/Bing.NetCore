using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bing.Analyzers.SourceGenerator;

/// <summary>
/// 收集领域对象及带特性的候选类型，再由语义模型识别 ChangeGraph（包含特性别名）。
/// </summary>
sealed class DomainObjectBaseSyntaxReceiver : ISyntaxReceiver
{
    /// <summary>
    /// 获取候选类集合。
    /// </summary>
    public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();

    /// <inheritdoc />
    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (!(syntaxNode is ClassDeclarationSyntax declaration))
            return;
        if (declaration.BaseList == null && declaration.AttributeLists.Count == 0)
            return;
        // 特性名称可能是 using 别名，不在语法阶段按字符串猜测语义。
        CandidateClasses.Add(declaration);
    }
}
