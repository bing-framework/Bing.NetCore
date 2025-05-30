﻿using System.Text;
using Bing.Analyzers.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Bing.Analyzers;

/// <summary>
/// 源代码生成器：用于生成 ChangTrackable 相关代码
/// </summary>
[Generator]
internal class ChangeTrackableSourceGenerator : ISourceGenerator
{
    /// <summary>
    /// 初始化代码生成器
    /// </summary>
    /// <param name="context">生成器初始化上下文</param>
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new DomainObjectBaseSyntaxReceiver());
    }

    /// <summary>
    /// 执行代码生成
    /// </summary>
    /// <param name="context">生成器执行上下文</param>
    public void Execute(GeneratorExecutionContext context)
    {
        var log = new StringBuilder();
        log.AppendLine($"// <auto-generated time='{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}' />");
        if (context.SyntaxReceiver is not DomainObjectBaseSyntaxReceiver receiver)
        {
            log.AppendLine("// warning: No classes detected for generation.");
        }
        else
        {
            var domainBaseSymbol = context.Compilation.GetTypeByMetadataName("Bing.Domain.Entities.DomainObjectBase`1");
            foreach (var classDecl in receiver.CandidateClasses)
            {
                var semanticModel = context.Compilation.GetSemanticModel(classDecl.SyntaxTree);
                if (ModelExtensions.GetDeclaredSymbol(semanticModel, classDecl) is not INamedTypeSymbol classSymbol)
                    continue;
                if (!IsDomainObjectBase(classSymbol, domainBaseSymbol))
                    continue;
                if (!ShouldGenerateChangeTracking(classDecl, classSymbol))
                    continue;

                var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : classSymbol.ContainingNamespace.ToDisplayString();
                var className = classSymbol.Name;
                var properties = GetPublicInstanceProperties(classSymbol);
                log.AppendLine($"// Class {className}, Properties: {properties.Count}");

                GeneratePartialClassAndAddChangesMethod(context, namespaceName, className, properties);
            }

        }
        context.AddSource("Log.1.g.cs", SourceText.From(log.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// 检查类是否继承自 DomainObjectBase
    /// </summary>
    private bool IsDomainObjectBase(INamedTypeSymbol classSymbol, INamedTypeSymbol domainBaseSymbol)
    {
        for (var baseType = classSymbol.BaseType; baseType != null; baseType = baseType.BaseType)
        {
            if (domainBaseSymbol != null && SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, domainBaseSymbol))
                return true;
            if (baseType.Name == "DomainObjectBase")
                return true;
        }
        return false;
    }

    /// <summary>
    /// 确定是否需要生成 ChangeTracking 代码
    /// </summary>
    private bool ShouldGenerateChangeTracking(ClassDeclarationSyntax classDecl, INamedTypeSymbol classSymbol)
    {
        if (!classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            return false;
        return !classSymbol.GetMembers().OfType<IMethodSymbol>().Any(m => m.Name == "AddChanges" && m.IsOverride);
    }

    /// <summary>
    /// 生成部分类并添加 AddChanges 方法
    /// </summary>
    /// <param name="context">生成器执行上下文</param>
    /// <param name="namespaceName">命名空间</param>
    /// <param name="className">类名</param>
    /// <param name="properties">类属性</param>
    private void GeneratePartialClassAndAddChangesMethod(GeneratorExecutionContext context, string namespaceName, string className, List<BuilderPropertyInfo> properties)
    {
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine($"// <auto-generated time='{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} />");
        sourceBuilder.AppendLine("using System;");
        sourceBuilder.AppendLine();
        if (!string.IsNullOrWhiteSpace(namespaceName))
            sourceBuilder.AppendLine($"namespace {namespaceName};");

        var bodyScript = $$"""
                      public partial class {{className}}
                      {
                          /// <summary>
                          /// 添加变更列表
                          /// </summary>
                          protected override void AddChanges( {{className}} other )
                          {
                              {{properties.Render((p, i) =>
        {
            if (p.IsSkipGenerator()) { return string.Empty; }
            return $$"""
                                           AddChange( t=> t.{{p.Name}}, other.{{p.Name}} );

                                           """;
        }).Ident(2)}}
                          }
                      }
                      """;
        sourceBuilder.AppendLine(bodyScript);
        context.AddSource($"{namespaceName}.{className}.AddChanges.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// 构建属性信息
    /// </summary>
    struct BuilderPropertyInfo
    {
        public BuilderPropertyInfo(IPropertySymbol property) : this()
        {
            Type = property.Type.ToString();
            Name = property.Name;
            ParameterName = $"{Name[0].ToString().ToLower()}{Name.Remove(0, 1)}";
            BackingFieldName = $"_{ParameterName}";
        }

        public string Name { get; set; }
        public string Type { get; set; }
        public string ParameterName { get; set; }
        public string BackingFieldName { get; set; }

        /// <summary>
        /// 判断属性是否应跳过生成器
        /// </summary>
        public bool IsSkipGenerator()
        {
            if (Name == "Version" || Name == "IsDeleted")
                return true;
            return false;
        }
    }

    /// <summary>
    /// 获取类的公共实例属性
    /// </summary>
    /// <param name="classSymbol">类符号</param>
    private List<BuilderPropertyInfo> GetPublicInstanceProperties(INamedTypeSymbol classSymbol)
    {
        var properties = new List<BuilderPropertyInfo>();
        while (classSymbol != null)
        {
            properties.AddRange(classSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic)
                .Select(p => new BuilderPropertyInfo(p)));
            classSymbol = classSymbol.BaseType;
        }
        return properties.OrderBy(x => x.Name).ToList();
    }
}

internal static class Extensions
{
    public static string Ident(this object source, int identLevels)
    {
        var lines = source.ToString().TrimStart(' ').Split('\n');
        var ident = new string(' ', identLevels * 4);
        return string.Join("\n", lines.Select((x, i) => $"""{(i > 0 ? ident : "")}{x}"""));
    }

    public static string Render<T>(this IEnumerable<T> source, Func<T, string> template, string separator = "")
    {
        return string.Join(separator, source.Select(template));
    }

    public static string Render<T>(this IEnumerable<T> source, Func<T, int, string> template, string separator = "")
    {
        return string.Join(separator, source.Select(template));
    }
}
