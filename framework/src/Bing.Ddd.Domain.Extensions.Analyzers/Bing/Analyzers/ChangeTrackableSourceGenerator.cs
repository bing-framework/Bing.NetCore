using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.Analyzers.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Bing.Analyzers;

/// <summary>
/// 为领域对象生成逐成员变更跟踪方法。
/// </summary>
[Generator]
public sealed partial class ChangeTrackableSourceGenerator : ISourceGenerator
{
    /// <summary>
    /// 定义变更忽略特性的完全限定元数据名称。
    /// </summary>
    private const string ChangeIgnoreAttributeName = "Bing.Domain.ChangeTracking.ChangeIgnoreAttribute";
    /// <summary>
    /// 定义描述特性的完全限定元数据名称。
    /// </summary>
    private const string DescriptionAttributeName = "System.ComponentModel.DescriptionAttribute";
    /// <summary>
    /// 定义显示特性的完全限定元数据名称。
    /// </summary>
    private const string DisplayAttributeName = "System.ComponentModel.DataAnnotations.DisplayAttribute";
    /// <summary>
    /// 定义变更图模型类型的完全限定元数据名称。
    /// </summary>
    private const string ChangeGraphModelName = "Bing.Domain.ChangeTracking.ChangeGraphModel";
    /// <summary>
    /// 定义领域对象基类的完全限定元数据名称。
    /// </summary>
    private const string DomainObjectBaseName = "Bing.Domain.Entities.DomainObjectBase`1";

    /// <summary>
    /// 报告领域对象未声明为 partial，源生成器无法合并分部声明。
    /// </summary>
    private static readonly DiagnosticDescriptor RootMustBePartial = new DiagnosticDescriptor(
        "CG001", "领域对象必须为 partial", "类型“{0}”继承 DomainObjectBase<TObject> 时必须声明为 partial", "ChangeGraph", DiagnosticSeverity.Error, true);
    /// <summary>
    /// 报告变更图根类型使用了不受支持的静态形状。
    /// </summary>
    private static readonly DiagnosticDescriptor RootShapeUnsupported = new DiagnosticDescriptor(
        "CG002", "ChangeGraph 根类型形状不受支持", "类型“{0}”不能作为 ChangeGraph 根类型（静态类型不受支持）", "ChangeGraph", DiagnosticSeverity.Error, true);
    /// <summary>
    /// 报告成员类型无法生成对象图采集代码。
    /// </summary>
    private static readonly DiagnosticDescriptor UnsupportedMember = new DiagnosticDescriptor(
        "CG006", "AddChanges 成员不受支持", "属性“{0}”的类型无法安全生成自动 AddChanges 调用，生成器将跳过该属性", "ChangeGraph", DiagnosticSeverity.Warning, true);
    /// <summary>
    /// 报告无序集合元素缺少领域身份契约。
    /// </summary>
    private static readonly DiagnosticDescriptor SetElementNeedsIdentity = new DiagnosticDescriptor(
        "CG019", "AddChanges Set 元素缺少领域身份", "属性“{0}”的 Set 元素类型“{1}”未实现 IEntity；无序集合必须由 GetKeys() 提供稳定身份键", "ChangeGraph", DiagnosticSeverity.Error, true);
    /// <summary>
    /// 提示属性规则不会改写手写变更记录。
    /// </summary>
    private static readonly DiagnosticDescriptor ManualChangeTrackingRule = new DiagnosticDescriptor(
        "CG015", "属性规则不会改写手写 AddChanges", "类型“{0}”的 ChangeIgnore 只影响自动生成代码，不会改写手写 AddChanges；请自行维护旧入口逻辑", "ChangeGraph", DiagnosticSeverity.Info, true);
    /// <summary>
    /// 报告类型声明了与生成 hook 冲突的方法。
    /// </summary>
    private static readonly DiagnosticDescriptor HookSignatureConflict = new DiagnosticDescriptor(
        "CG020", "领域对象 hook 签名冲突", "类型“{0}”的方法“{1}”与领域对象 AddChanges hook 冲突；请重写领域基类 hook 或重命名冲突方法", "ChangeGraph", DiagnosticSeverity.Error, true);

    /// <inheritdoc />
    public void Initialize(GeneratorInitializationContext context) =>
        context.RegisterForSyntaxNotifications(() => new DomainObjectBaseSyntaxReceiver());

    /// <inheritdoc />
    public void Execute(GeneratorExecutionContext context)
    {
        if (!(context.SyntaxReceiver is DomainObjectBaseSyntaxReceiver receiver))
            return;

        var domainBaseSymbol = context.Compilation.GetTypeByMetadataName(DomainObjectBaseName);
        var processed = new List<INamedTypeSymbol>();
        foreach (var classDeclaration in receiver.CandidateClasses)
        {
            var semanticModel = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            if (!(ModelExtensions.GetDeclaredSymbol(semanticModel, classDeclaration) is INamedTypeSymbol classSymbol))
                continue;
            if (processed.Any(item => SymbolEqualityComparer.Default.Equals(item, classSymbol)))
                continue;
            processed.Add(classSymbol);

            // 继承 DomainObjectBase<TObject> 是唯一的生成入口，不再依赖类级标记。
            if (!IsDomainObjectBase(classSymbol, domainBaseSymbol))
                continue;

            var hookParameter = GetChangeParameterType(classSymbol);
            var addChangesMembers = classSymbol.GetMembers("AddChanges").OfType<IMethodSymbol>().ToList();
            var manualMethod = addChangesMembers.FirstOrDefault(item =>
                HasHookSignature(item, hookParameter) && IsDomainHookOverride(item, domainBaseSymbol));
            var conflictingHook = addChangesMembers.FirstOrDefault(item =>
                HasHookSignature(item, hookParameter) && !IsDomainHookOverride(item, domainBaseSymbol));
            var inheritedConflict = manualMethod == null
                ? FindInheritedHookConflict(context.Compilation, classSymbol, domainBaseSymbol, hookParameter)
                : null;
            var conflict = conflictingHook ?? inheritedConflict;
            if (conflict != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(HookSignatureConflict,
                    conflictingHook?.Locations.FirstOrDefault() ?? classSymbol.Locations.FirstOrDefault(),
                    classSymbol.ToDisplayString(), conflict.Name));
                continue;
            }

            // 手写入口保持原样，但必须在跳过生成前说明属性规则的适用范围。
            if (manualMethod != null && GetPublicInstanceProperties(classSymbol).Any(property =>
                    FindAttribute(property, ChangeIgnoreAttributeName) != null))
                context.ReportDiagnostic(Diagnostic.Create(ManualChangeTrackingRule,
                    manualMethod.Locations.FirstOrDefault(), classSymbol.ToDisplayString()));

            // 手写 override 完全拥有其变更规则；自动成员形状和身份诊断仅适用于生成 hook。
            GenerateEntityChangeTracking(context, classSymbol, manualMethod == null);
        }
    }

    /// <summary>
    /// 判断方法是否与 AddChanges hook 签名一致。
    /// </summary>
    /// <param name="method">待检查的方法符号。</param>
    /// <param name="parameterType">真实 hook 参数类型。</param>
    /// <returns>非泛型方法具有相同的按值参数时为 true；返回类型不区分 C# 方法签名。</returns>
    private static bool HasHookSignature(IMethodSymbol method, ITypeSymbol parameterType) =>
        method.Arity == 0 && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
        SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, parameterType);

    /// <summary>沿实际重写链确认方法覆盖的是领域基类的虚方法，而不是同名重载或隐藏方法。</summary>
    private static bool IsDomainHookOverride(IMethodSymbol method, INamedTypeSymbol domainBaseSymbol)
    {
        if (!method.IsOverride || !method.ReturnsVoid)
            return false;
        for (var current = method.OverriddenMethod; current != null; current = current.OverriddenMethod)
            if (SymbolEqualityComparer.Default.Equals(current.ContainingType.OriginalDefinition, domainBaseSymbol))
                return true;
        return false;
    }

    /// <summary>发现跨继承层级阻断生成重写的同签名方法。</summary>
    private static IMethodSymbol FindInheritedHookConflict(Compilation compilation, INamedTypeSymbol type,
        INamedTypeSymbol domainBaseSymbol, ITypeSymbol parameterType)
    {
        for (var current = type.BaseType; current != null &&
             !SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, domainBaseSymbol);
             current = current.BaseType)
        {
            var method = current.GetMembers("AddChanges").OfType<IMethodSymbol>().FirstOrDefault(item =>
                HasHookSignature(item, parameterType) && compilation.IsSymbolAccessibleWithin(item, type));
            if (method != null)
                return IsDomainHookOverride(method, domainBaseSymbol) && !method.IsSealed ? null : method;
        }
        return null;
    }

    /// <summary>
    /// 为领域对象生成直接读取属性的 AddChanges 重写。
    /// </summary>
    /// <param name="context">用于读取语义模型、输出源码及报告诊断的生成上下文。</param>
    /// <param name="root">需要生成变更跟踪的领域对象类型。</param>
    /// <param name="generateAddChanges">是否生成 AddChanges 重写；手写 override 时为 false。</param>
    private static void GenerateEntityChangeTracking(GeneratorExecutionContext context, INamedTypeSymbol root,
        bool generateAddChanges)
    {
        if (!generateAddChanges)
            return;
        if (!ArePartialTypes(root))
        {
            context.ReportDiagnostic(Diagnostic.Create(RootMustBePartial, root.Locations.FirstOrDefault(), root.ToDisplayString()));
            return;
        }
        if (root.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(RootShapeUnsupported, root.Locations.FirstOrDefault(), root.ToDisplayString()));
            return;
        }
        var members = BuildAddChangesMembers(context, root);
        GenerateMemberAddChanges(context, root, members);
    }

    /// <summary>
    /// 按属性类型建立直接 AddChanges 所需的有限静态形状。
    /// </summary>
    private static List<AddChangesMemberInfo> BuildAddChangesMembers(GeneratorExecutionContext context,
        INamedTypeSymbol owner)
    {
        var members = new List<AddChangesMemberInfo>();
        foreach (var property in GetPublicInstanceProperties(owner))
        {
            if (property.Name == "ChangeGraphModel" && property.Type.ToDisplayString() == ChangeGraphModelName)
                continue;
            if (ShouldIgnoreChangeProperty(owner, property))
                continue;

            if (!ValidateSetIdentities(context, property, property.Type,
                    new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), 0))
                continue;

            var shape = BuildAddChangesTypeShape(property.Type,
                new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), 0);
            if (shape.Kind == AddChangesShapeKind.Unsupported)
            {
                ReportSimplifiedUnsupported(context, property);
                continue;
            }
            members.Add(new AddChangesMemberInfo(property, shape));
        }
        return members;
    }

    /// <summary>
    /// 递归识别标量、领域关系和同步容器，不展开普通对象属性。
    /// </summary>
    private static AddChangesTypeShape BuildAddChangesTypeShape(ITypeSymbol type,
        HashSet<ITypeSymbol> activeTypes, int depth)
    {
        // 有限深度上限也能挡住逐层改写类型参数的无限容器闭包。
        if (type == null || type.TypeKind == TypeKind.Error || depth >= 64 || IsUnsupportedContainer(type))
            return new AddChangesTypeShape(type, AddChangesShapeKind.Unsupported);
        if (IsScalarType(type))
            return new AddChangesTypeShape(type, AddChangesShapeKind.Scalar);

        if (TryGetDictionary(type, out var keyType, out var valueType))
        {
            if (!activeTypes.Add(type))
                return new AddChangesTypeShape(type, AddChangesShapeKind.Unsupported);
            try
            {
                var child = BuildAddChangesTypeShape(valueType, activeTypes, depth + 1);
                return child.Kind == AddChangesShapeKind.Unsupported
                    ? new AddChangesTypeShape(type, AddChangesShapeKind.Unsupported)
                    : new AddChangesTypeShape(type, AddChangesShapeKind.Dictionary, keyType, child);
            }
            finally { activeTypes.Remove(type); }
        }

        // Set 同样实现 IEnumerable<T>，因此必须在普通序列之前识别。
        if (TryGetSet(type, out var setElementType))
        {
            if (!activeTypes.Add(type))
                return new AddChangesTypeShape(type, AddChangesShapeKind.Unsupported);
            try
            {
                var child = BuildAddChangesTypeShape(setElementType, activeTypes, depth + 1);
                return child.Kind == AddChangesShapeKind.Unsupported
                    ? new AddChangesTypeShape(type, AddChangesShapeKind.Unsupported)
                    : new AddChangesTypeShape(type, AddChangesShapeKind.Set, null, child);
            }
            finally { activeTypes.Remove(type); }
        }

        if (TryGetSequence(type, out var elementType))
        {
            if (!activeTypes.Add(type))
                return new AddChangesTypeShape(type, AddChangesShapeKind.Unsupported);
            try
            {
                var child = BuildAddChangesTypeShape(elementType, activeTypes, depth + 1);
                return child.Kind == AddChangesShapeKind.Unsupported
                    ? new AddChangesTypeShape(type, AddChangesShapeKind.Unsupported)
                    : new AddChangesTypeShape(type, AddChangesShapeKind.Sequence, null, child);
            }
            finally { activeTypes.Remove(type); }
        }

        if (IsChangeTrackableType(type))
            return new AddChangesTypeShape(type, AddChangesShapeKind.Object);
        return new AddChangesTypeShape(type, AddChangesShapeKind.Unsupported);
    }

    /// <summary>判断领域对象的属性是否排除在自动比较之外。</summary>
    private static bool ShouldIgnoreChangeProperty(INamedTypeSymbol owner, IPropertySymbol property) =>
        FindAttribute(property, ChangeIgnoreAttributeName) != null || property.Name == "Version" ||
        property.Name == "IsDeleted" || IsAuditProperty(owner, property);

    /// <summary>递归检查无序集合每一层的复杂元素是否声明稳定身份。</summary>
    private static bool ValidateSetIdentities(GeneratorExecutionContext context, IPropertySymbol property,
        ITypeSymbol type, HashSet<ITypeSymbol> activeTypes, int depth)
    {
        if (type == null || depth >= 64 || IsScalarType(type) || IsUnsupportedContainer(type))
            return true;

        if (TryGetDictionary(type, out var dictionaryValueType))
        {
            if (!activeTypes.Add(type))
                return true;
            try { return ValidateSetIdentities(context, property, dictionaryValueType, activeTypes, depth + 1); }
            finally { activeTypes.Remove(type); }
        }

        // Set 同样实现 IEnumerable<T>，先检查元素身份再递归其子容器。
        if (TryGetSet(type, out var setElementType))
        {
            if (!IsScalarType(setElementType) && !ImplementsEntityContract(setElementType))
            {
                context.ReportDiagnostic(Diagnostic.Create(SetElementNeedsIdentity, property.Locations.FirstOrDefault(),
                    property.ToDisplayString(), setElementType.ToDisplayString()));
                return false;
            }
            if (!activeTypes.Add(type))
                return true;
            try { return ValidateSetIdentities(context, property, setElementType, activeTypes, depth + 1); }
            finally { activeTypes.Remove(type); }
        }

        if (TryGetSequence(type, out var elementType))
        {
            if (!activeTypes.Add(type))
                return true;
            try { return ValidateSetIdentities(context, property, elementType, activeTypes, depth + 1); }
            finally { activeTypes.Remove(type); }
        }
        return true;
    }

    /// <summary>判断类型是否通过 IEntity 明确提供自身身份。</summary>
    private static bool ImplementsEntityContract(ITypeSymbol type)
    {
        if (type == null)
            return false;
        if (type.ToDisplayString() == "Bing.Domain.Entities.IEntity")
            return true;
        return type.AllInterfaces.Any(item => item.Name == "IEntity" && item.Arity == 0 &&
                                               item.ContainingNamespace.ToDisplayString() == "Bing.Domain.Entities");
    }

    /// <summary>
    /// 为每个领域对象生成逐成员比较方法与必要的嵌套容器静态辅助方法。
    /// </summary>
    private static void GenerateMemberAddChanges(GeneratorExecutionContext context, INamedTypeSymbol classSymbol,
        List<AddChangesMemberInfo> members)
    {
        var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace ? null : classSymbol.ContainingNamespace.ToDisplayString();
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable disable");
        if (!string.IsNullOrWhiteSpace(namespaceName))
        {
            source.Append("namespace ").Append(namespaceName).AppendLine(";");
            source.AppendLine();
        }
        var typeChain = GetTypeChain(classSymbol);
        foreach (var type in typeChain)
        {
            AppendPartialTypeDeclaration(source, type, context);
            source.AppendLine("{");
        }

        var helperIndex = 0;
        var helperNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            if (member.Shape.HasNestedContainer)
                member.NestedHelper = CreateNestedChangeHelper(member.Shape, classSymbol, ref helperIndex, helperNames);
        }

        var parameterType = GetChangeParameterType(classSymbol);
        source.Append("    protected override void AddChanges(").Append(TypeName(parameterType)).AppendLine(" other)");
        source.AppendLine("    {");
        var direct = SymbolEqualityComparer.Default.Equals(parameterType, classSymbol);
        if (!direct)
            AppendComparedObject(source, classSymbol, parameterType);
        var otherVar = direct ? "other" : "typedOther";
        foreach (var member in members)
            EmitAddChangesMember(source, member, classSymbol, context, otherVar);
        source.AppendLine("    }");

        foreach (var member in members)
            EmitNestedChangeHelpers(source, classSymbol, member.NestedHelper);

        for (var index = typeChain.Count - 1; index >= 0; index--)
            source.AppendLine("}");
        context.AddSource(GetHintName(classSymbol, "AddChanges"), SourceText.From(source.ToString(), Encoding.UTF8));
    }

    /// <summary>创建当前成员递归所需的静态容器辅助方法链。</summary>
    private static NestedChangeHelperInfo CreateNestedChangeHelper(AddChangesTypeShape shape,
        INamedTypeSymbol owner, ref int index, HashSet<string> usedNames)
    {
        if (!shape.HasNestedContainer)
            return null;
        var childHelper = CreateNestedChangeHelper(shape.ChildShape, owner, ref index, usedNames);
        string methodName;
        do { methodName = "__BingCompareNestedChanges" + index++; }
        while (usedNames.Contains(methodName) || owner.GetMembers(methodName).Length != 0);
        usedNames.Add(methodName);
        return new NestedChangeHelperInfo(shape, methodName, childHelper);
    }

    /// <summary>输出一个嵌套容器辅助方法及其静态子方法调用。</summary>
    private static void EmitNestedChangeHelpers(StringBuilder source, INamedTypeSymbol owner,
        NestedChangeHelperInfo helper)
    {
        if (helper == null)
            return;
        EmitNestedChangeHelper(source, owner, helper);
        EmitNestedChangeHelpers(source, owner, helper.ChildHelper);
    }

    /// <summary>输出单个 typed pair 遍历，路径和描述由运行时 helper 维护。</summary>
    private static void EmitNestedChangeHelper(StringBuilder source, INamedTypeSymbol owner,
        NestedChangeHelperInfo helper)
    {
        var shape = helper.Shape;
        source.Append("    private static void ").Append(helper.MethodName).Append("(").Append(TypeName(owner))
            .Append(" owner, string path, string description, ").Append(TypeName(shape.Type))
            .AppendLine(" oldValue, " + TypeName(shape.Type) + " newValue)");
        source.AppendLine("    {");
        if (shape.Kind == AddChangesShapeKind.Dictionary)
        {
            source.Append("        foreach (var pair in owner.GetNestedDictionaryPairs<")
                .Append(TypeName(shape.KeyType)).Append(", ").Append(TypeName(shape.ChildShape.Type))
                .AppendLine(">(path, description, oldValue, newValue))");
        }
        else
        {
            source.Append("        foreach (var pair in owner.GetNestedCollectionPairs<")
                .Append(TypeName(shape.ChildShape.Type)).Append(">(path, description, oldValue, newValue");
            if (shape.Kind == AddChangesShapeKind.Set)
                source.Append(", true");
            source.AppendLine("))");
        }
        source.AppendLine("        {");
        if (helper.ChildHelper != null)
        {
            source.Append("            ").Append(helper.ChildHelper.MethodName)
                .AppendLine("(owner, pair.Path, description, pair.OldValue, pair.NewValue);");
        }
        else
        {
            EmitContainerChangeCall(source, shape.ChildShape, "pair.Path", "description",
                "pair.OldValue", "pair.NewValue", "            ", "owner.");
        }
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine();
    }

    /// <summary>输出单个成员的一行静态调用，嵌套容器入口调用 typed helper。</summary>
    private static void EmitAddChangesMember(StringBuilder source, AddChangesMemberInfo member,
        INamedTypeSymbol owner, GeneratorExecutionContext context, string otherVar)
    {
        var property = member.Property;
        var name = EscapeLegacyIdentifier(property.Name);
        var nameOf = "nameof(" + name + ")";
        var description = GetLegacyDescriptionExpression(property, owner, context);
        var oldValue = "this." + name;
        var newValue = otherVar + "." + name;
        if (member.NestedHelper != null)
        {
            source.Append("        ").Append(member.NestedHelper.MethodName).Append("(this, ")
                .Append(nameOf).Append(", ").Append(description).Append(", ")
                .Append(oldValue).Append(", ").Append(newValue).AppendLine(");");
            return;
        }
        EmitLeafChangeCall(source, member.Shape, nameOf, description, oldValue, newValue);
    }

    /// <summary>按静态叶类型选择运行时现有的固定比较方法。</summary>
    private static void EmitLeafChangeCall(StringBuilder source, AddChangesTypeShape shape, string propertyName,
        string description, string oldValue, string newValue)
    {
        switch (shape.Kind)
        {
            case AddChangesShapeKind.Scalar:
                source.Append("        AddChange(").Append(propertyName).Append(", ").Append(description).Append(", ")
                    .Append(oldValue).Append(", ").Append(newValue).AppendLine(");");
                return;
            case AddChangesShapeKind.Object:
                source.Append("        AddObjectChanges(").Append(propertyName).Append(", ").Append(description).Append(", ")
                    .Append(oldValue).Append(", ").Append(newValue).AppendLine(");");
                return;
            case AddChangesShapeKind.Sequence:
            case AddChangesShapeKind.Set:
                EmitContainerChangeCall(source, shape, propertyName, description, oldValue, newValue, "        ");
                return;
            case AddChangesShapeKind.Dictionary:
                EmitContainerChangeCall(source, shape, propertyName, description, oldValue, newValue, "        ");
                return;
            default:
                return;
        }
    }

    /// <summary>输出一个不再嵌套容器的 AddCollectionChanges/AddDictionaryChanges 调用。</summary>
    private static void EmitContainerChangeCall(StringBuilder source, AddChangesTypeShape shape,
        string propertyName, string description, string oldValue, string newValue, string indentation,
        string receiver = null)
    {
        var method = shape.Kind == AddChangesShapeKind.Dictionary ? "AddDictionaryChanges" : "AddCollectionChanges";
        source.Append(indentation).Append(receiver).Append(method).Append("(").Append(propertyName).Append(", ")
            .Append(description).Append(", ").Append(oldValue).Append(", ").Append(newValue);
        if (shape.Kind == AddChangesShapeKind.Set)
            source.Append(", true");
        source.AppendLine(");");
    }

    /// <summary>报告当前直接 AddChanges 不支持的静态成员。</summary>
    private static void ReportSimplifiedUnsupported(GeneratorExecutionContext context, IPropertySymbol property) =>
        context.ReportDiagnostic(Diagnostic.Create(UnsupportedMember, property.Locations.FirstOrDefault(),
            property.ToDisplayString()));

    /// <summary>
    /// 判断类型是否实现变更跟踪契约（领域实体）。
    /// </summary>
    /// <param name="type">待判断的类型符号。</param>
    /// <returns>实现 IChangeTrackable 时为 true。</returns>
    private static bool IsChangeTrackableType(ITypeSymbol type) =>
        type != null && (type.ToDisplayString() == "Bing.Domain.ChangeTracking.IChangeTrackable" ||
                         type.AllInterfaces.Any(item =>
                             item.ToDisplayString() == "Bing.Domain.ChangeTracking.IChangeTrackable"));

    /// <summary>
    /// 判断类型是否继承真实领域对象基类。
    /// </summary>
    /// <param name="classSymbol">需要检查或生成跟踪代码的领域类型。</param>
    /// <param name="domainBaseSymbol">领域对象基类的真实符号定义；为 null 时不生成。</param>
    /// <returns>基类链包含真实领域基类符号时为 true，否则为 false。</returns>
    /// <remarks>不对同名基类做字符串兜底，避免误识别外部程序集中的无关类型。</remarks>
    private static bool IsDomainObjectBase(INamedTypeSymbol classSymbol, INamedTypeSymbol domainBaseSymbol)
    {
        if (domainBaseSymbol == null)
            return false;
        for (var baseType = classSymbol.BaseType; baseType != null; baseType = baseType.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, domainBaseSymbol))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 判断类型是否声明为分部类型。
    /// </summary>
    /// <param name="symbol">需要检查的类型或成员符号。</param>
    /// <returns>任一类型声明包含 partial 修饰符时为 true，否则为 false。</returns>
    private static bool IsPartial(INamedTypeSymbol symbol) =>
        symbol.DeclaringSyntaxReferences.Any(reference =>
        {
            var declaration = reference.GetSyntax() as TypeDeclarationSyntax;
            return declaration != null && declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword));
        });

    /// <summary>
    /// 判断自身及包含类型是否均为分部类型。
    /// </summary>
    /// <param name="symbol">需要检查的类型或成员符号。</param>
    /// <returns>从当前类型到最外层类型均包含 partial 声明时为 true，否则为 false。</returns>
    private static bool ArePartialTypes(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current != null; current = current.ContainingType)
        {
            if (!IsPartial(current))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 收集从最外层到当前类型的嵌套声明链。
    /// </summary>
    /// <param name="symbol">需要检查的类型或成员符号。</param>
    /// <returns>按外层到内层顺序排列的类型符号。</returns>
    private static List<INamedTypeSymbol> GetTypeChain(INamedTypeSymbol symbol)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var current = symbol; current != null; current = current.ContainingType)
            chain.Add(current);
        chain.Reverse();
        return chain;
    }

    /// <summary>
    /// 输出保留类型形态及约束的分部声明。
    /// </summary>
    /// <param name="source">接收生成源码的文本缓冲区。</param>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <param name="context">用于读取语义模型、输出源码及报告诊断的生成上下文。</param>
    private static void AppendPartialTypeDeclaration(StringBuilder source, INamedTypeSymbol type,
        GeneratorExecutionContext context)
    {
        source.Append(GetAccessibility(type));
        if (type.IsStatic)
            source.Append(" static");
        else
        {
            if (type.IsAbstract)
                source.Append(" abstract");
            if (type.IsSealed)
                source.Append(" sealed");
        }

        var declarations = type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax() as TypeDeclarationSyntax)
            .Where(item => item != null)
            .ToList();
        var declaration = declarations.FirstOrDefault(item => item.ConstraintClauses.Count != 0) ?? declarations.FirstOrDefault();
        if (declaration is RecordDeclarationSyntax record)
        {
            source.Append(" partial record");
            if (type.TypeKind == TypeKind.Struct)
                source.Append(" struct");
        }
        else if (type.TypeKind == TypeKind.Struct)
        {
            if (declaration != null && declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.RefKeyword)))
                source.Append(" ref");
            if (declaration != null && declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword)))
                source.Append(" readonly");
            source.Append(" partial struct");
        }
        else if (type.TypeKind == TypeKind.Interface)
            source.Append(" partial interface");
        else
            source.Append(" partial class");

        source.Append(" ").Append(EscapeIdentifier(type.Name));
        if (type.TypeParameters.Length != 0)
            source.Append("<").Append(string.Join(", ", type.TypeParameters.Select(parameter => EscapeIdentifier(parameter.Name)))).Append(">");
        if (type.TypeParameters.Length != 0)
            AppendConstraintClauses(source, declaration, context);
        source.AppendLine();
    }

    /// <summary>
    /// 输出语义解析后的泛型约束子句。
    /// </summary>
    /// <param name="source">接收生成源码的文本缓冲区。</param>
    /// <param name="declaration">提供泛型约束的类型声明；为空时不输出约束。</param>
    /// <param name="context">用于读取语义模型、输出源码及报告诊断的生成上下文。</param>
    private static void AppendConstraintClauses(StringBuilder source, TypeDeclarationSyntax declaration,
        GeneratorExecutionContext context)
    {
        if (declaration == null)
            return;
        var semanticModel = context.Compilation.GetSemanticModel(declaration.SyntaxTree);
        foreach (var clause in declaration.ConstraintClauses)
        {
            source.Append(" where ").Append(EscapeIdentifier(clause.Name.Identifier.ValueText)).Append(" : ");
            var constraints = new List<string>();
            foreach (var constraint in clause.Constraints)
            {
                if (constraint is TypeConstraintSyntax typeConstraint)
                {
                    var constraintType = semanticModel.GetTypeInfo(typeConstraint.Type).Type;
                    constraints.Add(constraintType == null ? constraint.WithoutTrivia().ToFullString() : ConstraintTypeName(constraintType));
                }
                else
                    constraints.Add(constraint.WithoutTrivia().ToFullString());
            }
            source.Append(string.Join(", ", constraints));
        }
    }

    /// <summary>
    /// 获取类型可访问性的 C# 修饰符文本。
    /// </summary>
    /// <param name="symbol">需要检查的类型或成员符号。</param>
    /// <returns>对应的访问修饰符；没有匹配项时为空字符串。</returns>
    private static string GetAccessibility(INamedTypeSymbol symbol)
    {
        switch (symbol.DeclaredAccessibility)
        {
            case Accessibility.Public:
                return "public";
            case Accessibility.Internal:
                return "internal";
            case Accessibility.Private:
                return "private";
            case Accessibility.Protected:
                return "protected";
            case Accessibility.ProtectedAndInternal:
                return "private protected";
            case Accessibility.ProtectedOrInternal:
                return "protected internal";
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// 收集类型及基类的可读公共实例属性。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <returns>按名称排序且按派生声明优先去重的属性集合。</returns>
    private static List<IPropertySymbol> GetPublicInstanceProperties(INamedTypeSymbol type)
    {
        var properties = new List<IPropertySymbol>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (!seen.Add(property.Name))
                    continue;
                if (property.IsStatic || property.IsIndexer || property.GetMethod == null ||
                    (property.GetMethod.DeclaredAccessibility != Accessibility.Public && current.TypeKind != TypeKind.Interface) ||
                    (property.DeclaredAccessibility != Accessibility.Public && current.TypeKind != TypeKind.Interface))
                    continue;
                properties.Add(property);
            }
        }
        return properties.OrderBy(property => property.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// 判断类型是否按标量捕获。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <returns>类型属于支持的标量、枚举或可空标量，或符号为空及错误类型时为 true，否则为 false。</returns>
    private static bool IsScalarType(ITypeSymbol type)
    {
        if (type == null || type.TypeKind == TypeKind.Error)
            return true;
        if (type.TypeKind == TypeKind.TypeParameter)
            return false;
        if ((type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Object) ||
            type.TypeKind == TypeKind.Enum)
            return true;
        if (type is INamedTypeSymbol nullable && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return IsScalarType(nullable.TypeArguments[0]);
        var fullName = type.OriginalDefinition.ToDisplayString();
        return fullName == "System.DateTime" || fullName == "System.DateTimeOffset" || fullName == "System.TimeSpan" ||
               fullName == "System.Guid" || fullName == "System.Uri" || fullName == "System.Version";
    }

    /// <summary>
    /// 判断类型是否属于不支持的容器形状。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <returns>类型为多维数组、非泛型枚举、查询、异步枚举或未绑定泛型容器时为 true，否则为 false。</returns>
    private static bool IsUnsupportedContainer(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
            return type is IArrayTypeSymbol array && array.Rank != 1;
        var fullName = named.OriginalDefinition.ToDisplayString();
        if (fullName == "System.Collections.IEnumerable" ||
            fullName == "System.Linq.IQueryable" ||
            fullName == "System.Linq.IQueryable<T>" ||
            fullName == "System.Collections.Generic.IAsyncEnumerable<T>")
            return true;
        return named.Name == "IAsyncEnumerable" || named.Name == "IQueryable" || named.IsUnboundGenericType;
    }

    /// <summary>
    /// 尝试解析字典的类型参数。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <param name="valueType">输出字典值类型；未找到字典形状时为 null。</param>
    /// <returns>找到支持的字典形状时为 true，否则为 false。</returns>
    private static bool TryGetDictionary(ITypeSymbol type, out ITypeSymbol valueType)
    {
        return TryGetDictionary(type, out _, out valueType);
    }

    /// <summary>
    /// 尝试解析字典的类型参数。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <param name="keyType">输出字典键类型；未找到字典形状时为 null。</param>
    /// <param name="valueType">输出字典值类型；未找到字典形状时为 null。</param>
    /// <returns>找到支持的字典形状时为 true，否则为 false。</returns>
    private static bool TryGetDictionary(ITypeSymbol type, out ITypeSymbol keyType, out ITypeSymbol valueType)
    {
        keyType = null;
        valueType = null;
        if (type is not INamedTypeSymbol named || type.SpecialType == SpecialType.System_String)
            return false;
        var candidate = new[] { named }.Concat(named.AllInterfaces).FirstOrDefault(item =>
            (item.Name == "IDictionary" || item.Name == "IReadOnlyDictionary" || item.Name == "ImmutableDictionary") && item.TypeArguments.Length == 2);
        if (candidate == null)
            return false;
        keyType = candidate.TypeArguments[0];
        valueType = candidate.TypeArguments[1];
        return true;
    }

    /// <summary>
    /// 尝试解析集合的元素类型。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <param name="elementType">输出序列或集合的元素类型；未找到支持形状时为 null。</param>
    /// <returns>找到支持的集合形状时为 true，否则为 false。</returns>
    private static bool TryGetSet(ITypeSymbol type, out ITypeSymbol elementType)
    {
        elementType = null;
        if (type is not INamedTypeSymbol named || type.SpecialType == SpecialType.System_String)
            return false;
        var candidate = new[] { named }.Concat(named.AllInterfaces).FirstOrDefault(item =>
            (item.Name == "ISet" || item.Name == "IReadOnlySet" || item.Name == "HashSet") && item.TypeArguments.Length == 1);
        if (candidate == null)
            return false;
        elementType = candidate.TypeArguments[0];
        return true;
    }

    /// <summary>
    /// 尝试解析序列的元素类型。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <param name="elementType">输出序列或集合的元素类型；未找到支持形状时为 null。</param>
    /// <returns>找到一维数组或泛型可枚举形状时为 true，否则为 false。</returns>
    private static bool TryGetSequence(ITypeSymbol type, out ITypeSymbol elementType)
    {
        elementType = null;
        if (type is IArrayTypeSymbol array)
        {
            if (array.Rank != 1)
                return false;
            elementType = array.ElementType;
            return true;
        }
        if (type is not INamedTypeSymbol named || type.SpecialType == SpecialType.System_String)
            return false;
        var candidate = new[] { named }.Concat(named.AllInterfaces).FirstOrDefault(item =>
            item.Name == "IEnumerable" && item.TypeArguments.Length == 1);
        if (candidate == null)
            return false;
        elementType = candidate.TypeArguments[0];
        return true;
    }

    /// <summary>
    /// 判断属性是否实现审计时间契约。
    /// </summary>
    /// <param name="owner">待检查属性所处的模型类型。</param>
    /// <param name="property">待分析或报告诊断的属性符号。</param>
    /// <returns>属性符号实际实现对应审计时间接口成员时为 true，否则为 false。</returns>
    private static bool IsAuditProperty(INamedTypeSymbol owner, IPropertySymbol property)
    {
        var contractName = property.Name switch
        {
            "CreationTime" => "Bing.Auditing.IHasCreationTime",
            "LastModificationTime" => "Bing.Auditing.IHasModificationTime",
            "DeletionTime" => "Bing.Auditing.IHasDeletionTime",
            _ => null
        };
        if (contractName == null)
            return false;

        foreach (var contract in owner.AllInterfaces)
        {
            if (contract.ToDisplayString() != contractName)
                continue;
            var contractProperty = contract.GetMembers(property.Name).OfType<IPropertySymbol>().FirstOrDefault();
            if (contractProperty == null)
                continue;
            var implementation = owner.FindImplementationForInterfaceMember(contractProperty) as IPropertySymbol;
            if (implementation != null && SymbolEqualityComparer.Default.Equals(
                    implementation.OriginalDefinition, property.OriginalDefinition))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 沿重写链查找指定特性。
    /// </summary>
    /// <param name="symbol">需要检查的类型或成员符号。</param>
    /// <param name="metadataName">待查找特性的完整元数据名称。</param>
    /// <returns>找到的最近特性；未找到时返回 null。</returns>
    private static AttributeData FindAttribute(ISymbol symbol, string metadataName)
    {
        for (var current = symbol; current != null; current = GetOverriddenSymbol(current))
        {
            var attribute = current.GetAttributes().FirstOrDefault(item => item.AttributeClass != null &&
                item.AttributeClass.ToDisplayString() == metadataName);
            if (attribute != null)
                return attribute;
        }
        return null;
    }

    /// <summary>
    /// 获取成员直接重写的基类符号。
    /// </summary>
    /// <param name="symbol">需要检查的类型或成员符号。</param>
    /// <returns>被重写的属性或方法；没有对应重写成员时返回 null。</returns>
    private static ISymbol GetOverriddenSymbol(ISymbol symbol)
    {
        if (symbol is IPropertySymbol property)
            return property.OverriddenProperty;
        if (symbol is IMethodSymbol method)
            return method.OverriddenMethod;
        return null;
    }

    /// <summary>
    /// 生成不包含可空引用修饰符的类型名称。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <returns>适合生成成员访问代码的全限定类型名称。</returns>
    private static string TypeName(ITypeSymbol type)
    {
        var options = SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions &
                      ~SymbolDisplayMiscellaneousOptions.ExpandNullable &
                      ~SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier;
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(options));
    }

    /// <summary>
    /// 生成保留可空信息的泛型约束类型名称。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <returns>展开可空值类型且保留可空引用修饰符的全限定名称。</returns>
    private static string ConstraintTypeName(ITypeSymbol type)
    {
        var options = SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
                      SymbolDisplayMiscellaneousOptions.ExpandNullable |
                      SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier;
        return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(options));
    }

    /// <summary>
    /// 为标识符添加逐字标识前缀。
    /// </summary>
    /// <param name="identifier">需要写入生成代码的标识符名称。</param>
    /// <returns>以 @ 开头的 C# 标识符文本。</returns>
    private static string EscapeIdentifier(string identifier) => "@" + identifier;

    /// <summary>
    /// 构造转义反斜杠及引号的字符串字面量。
    /// </summary>
    /// <param name="value">需要编码为 C# 字符串字面量的文本；允许为 null。</param>
    /// <returns>带双引号的字符串源码；输入为 null 时编码为空字符串。</returns>
    private static string ToLiteral(string value) => "\"" + (value ?? string.Empty)
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n")
        .Replace("\t", "\\t")
        .Replace("\0", "\\0") + "\"";

    /// <summary>
    /// 构造生成源文件的提示名称。
    /// </summary>
    /// <param name="symbol">需要检查的类型或成员符号。</param>
    /// <param name="suffix">区分生成内容的文件名后缀。</param>
    /// <returns>由规范化类型名、指定后缀及 .g.cs 扩展名组成的提示名称。</returns>
    private static string GetHintName(INamedTypeSymbol symbol, string suffix)
    {
        var name = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", string.Empty)
            .Replace("<", "_").Replace(">", "_").Replace('.', '_').Replace(':', '_');
        return name + "." + suffix + ".g.cs";
    }
}
