using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Bing.Analyzers;

/// <summary>
/// 生成旧版领域对象变更入口时使用的编译期元数据帮助方法。
/// </summary>
public sealed partial class ChangeTrackableSourceGenerator
{
    /// <summary>无法静态读取的本地化资源必须由用户修正，禁止静默把资源键当作描述。</summary>
    private static readonly DiagnosticDescriptor InvalidDisplayResource = new DiagnosticDescriptor(
        "CG017", "显示资源无法静态读取", "属性“{0}”的显示资源“{1}”必须是可访问的静态字符串属性", "ChangeGraph", DiagnosticSeverity.Error, true);
    /// <summary>无法在编译期还原的 DescriptionAttribute 派生特性必须显式修正。</summary>
    private static readonly DiagnosticDescriptor UnsupportedDescriptionAttribute = new DiagnosticDescriptor(
        "CG018", "描述特性无法静态读取", "属性“{0}”使用派生特性“{1}”；请改用标准 DescriptionAttribute 常量", "ChangeGraph", DiagnosticSeverity.Error, true);
    /// <summary>
    /// 获取旧版变更入口使用的静态描述表达式。
    /// </summary>
    /// <param name="property">当前生成的领域属性。</param>
    /// <param name="owner">包含生成代码的领域类型。</param>
    /// <param name="context">用于检查静态访问权限并报告诊断的生成上下文。</param>
    /// <returns>描述字面量或可访问的本地化资源属性表达式。</returns>
    private static string GetLegacyDescriptionExpression(IPropertySymbol property, INamedTypeSymbol owner,
        GeneratorExecutionContext context)
    {
        var displayAttribute = FindAttribute(property, DisplayAttributeName);
        var displayName = displayAttribute?.NamedArguments
            .FirstOrDefault(argument => argument.Key == "Name").Value.Value as string;
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var resourceType = displayAttribute.NamedArguments
                .FirstOrDefault(argument => argument.Key == "ResourceType").Value.Value as INamedTypeSymbol;
            if (resourceType != null)
            {
                if (TryGetDisplayResourceProperty(resourceType, displayName, out var resourceProperty) &&
                    context.Compilation.IsSymbolAccessibleWithin(resourceType, owner) &&
                    context.Compilation.IsSymbolAccessibleWithin(resourceProperty.GetMethod, owner))
                    return TypeName(resourceType) + "." + EscapeLegacyIdentifier(resourceProperty.Name);
                context.ReportDiagnostic(Diagnostic.Create(InvalidDisplayResource, property.Locations.FirstOrDefault(),
                    property.ToDisplayString(), displayName));
            }
            return ToLegacyLiteral(displayName);
        }

        var descriptionAttribute = FindDescriptionAttribute(property, out var unsupportedDescriptionAttribute);
        if (unsupportedDescriptionAttribute != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(UnsupportedDescriptionAttribute,
                property.Locations.FirstOrDefault(), property.ToDisplayString(),
                unsupportedDescriptionAttribute.AttributeClass.ToDisplayString()));
            return ToLegacyLiteral(property.Name);
        }
        var description = descriptionAttribute?.ConstructorArguments.FirstOrDefault().Value as string;
        return ToLegacyLiteral(string.IsNullOrWhiteSpace(description) ? property.Name : description);
    }

    /// <summary>
    /// 按属性 override 链解析标准描述特性，或定位无法静态求值的派生描述特性。
    /// </summary>
    private static AttributeData FindDescriptionAttribute(IPropertySymbol property,
        out AttributeData unsupportedDescriptionAttribute)
    {
        unsupportedDescriptionAttribute = null;
        for (var current = property; current != null; current = current.OverriddenProperty)
        {
            var attributes = current.GetAttributes();
            var standard = attributes.FirstOrDefault(attribute =>
                attribute.AttributeClass?.ToDisplayString() == DescriptionAttributeName);
            if (standard != null)
                return standard;

            var derived = attributes.FirstOrDefault(attribute => IsDescriptionAttribute(attribute.AttributeClass));
            if (derived != null)
            {
                unsupportedDescriptionAttribute = derived;
                return null;
            }
        }
        return null;
    }

    /// <summary>
    /// 判断特性是否继承自标准 DescriptionAttribute。
    /// </summary>
    private static bool IsDescriptionAttribute(INamedTypeSymbol attributeType)
    {
        for (var current = attributeType?.BaseType; current != null; current = current.BaseType)
        {
            if (current.ToDisplayString() == DescriptionAttributeName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 查找可从生成代码静态读取的显示资源属性。
    /// </summary>
    /// <param name="resourceType">显示特性声明的资源类型。</param>
    /// <param name="resourceName">显示特性中的资源属性名。</param>
    /// <param name="resourceProperty">找到的可访问资源属性。</param>
    /// <returns>属性是静态字符串属性且对生成代码可访问时为 true。</returns>
    private static bool TryGetDisplayResourceProperty(INamedTypeSymbol resourceType,
        string resourceName, out IPropertySymbol resourceProperty)
    {
        resourceProperty = null;
        for (var current = resourceType; current != null; current = current.BaseType)
        {
            var declaredMembers = current.GetMembers(resourceName);
            if (declaredMembers.Length == 0)
                continue;

            // 派生类型声明同名成员时会隐藏基类成员，即使该成员本身不符合资源属性契约。
            resourceProperty = declaredMembers.OfType<IPropertySymbol>()
                .FirstOrDefault(property => property.IsStatic && property.Type.SpecialType == SpecialType.System_String &&
                                            property.GetMethod != null);
            return resourceProperty != null;
        }
        return resourceProperty != null;
    }

    /// <summary>
    /// 将文本编码为可安全嵌入生成 C# 的字符串字面量。
    /// </summary>
    /// <param name="value">需要编码的文本。</param>
    /// <returns>包含完整转义序列的字符串字面量。</returns>
    private static string ToLegacyLiteral(string value) => SymbolDisplay.FormatLiteral(value ?? string.Empty, true);

    /// <summary>
    /// 仅为 C# 关键字添加逐字标识符前缀。
    /// </summary>
    /// <param name="identifier">需要写入生成代码的成员标识符。</param>
    /// <returns>普通标识符原文或带 @ 前缀的关键字。</returns>
    private static string EscapeLegacyIdentifier(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None &&
        SyntaxFacts.GetContextualKeywordKind(identifier) == SyntaxKind.None
            ? identifier
            : "@" + identifier;
}
