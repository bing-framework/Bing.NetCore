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
/// 为领域对象生成传统 AddChanges 以及类型化 ChangeGraph 模型。
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
    /// 定义变更图节点接口的完全限定元数据名称。
    /// </summary>
    private const string ChangeGraphNodeInterfaceName = "Bing.Domain.ChangeTracking.IChangeGraphNode";
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
    /// 报告生成模型属性与已有成员冲突。
    /// </summary>
    private static readonly DiagnosticDescriptor ExistingModelProperty = new DiagnosticDescriptor(
        "CG005", "ChangeGraph 模型属性冲突", "类型“{0}”已经声明 ChangeGraphModel 属性，生成器无法安全添加模型入口", "ChangeGraph", DiagnosticSeverity.Error, true);
    /// <summary>
    /// 报告成员类型无法生成对象图采集代码。
    /// </summary>
    private static readonly DiagnosticDescriptor UnsupportedMember = new DiagnosticDescriptor(
        "CG006", "ChangeGraph 成员不受支持", "属性“{0}”的类型无法生成对象图采集代码，捕获时将按路径失败", "ChangeGraph", DiagnosticSeverity.Warning, true);
    /// <summary>
    /// 报告泛型闭包无界而无法生成模型。
    /// </summary>
    private static readonly DiagnosticDescriptor UnboundedGenericExpansion = new DiagnosticDescriptor(
        "CG010", "ChangeGraph 泛型闭包无界", "类型“{0}”递归展开为“{1}”；无法安全生成无界泛型模型闭包", "ChangeGraph", DiagnosticSeverity.Error, true);
    /// <summary>
    /// 报告开放类型成员需要显式适配器。
    /// </summary>
    private static readonly DiagnosticDescriptor AdapterRequired = new DiagnosticDescriptor(
        "CG011", "ChangeGraph 成员需要显式适配器", "属性“{0}”的开放类型需要通过 ChangeGraphOptions.WithAdapter 显式注册模型", "ChangeGraph", DiagnosticSeverity.Warning, true);
    /// <summary>
    /// 报告无序集合元素缺少领域身份契约。
    /// </summary>
    private static readonly DiagnosticDescriptor SetElementNeedsIdentity = new DiagnosticDescriptor(
        "CG019", "ChangeGraph Set 元素缺少领域身份", "属性“{0}”的 Set 元素类型“{1}”未实现 IKey<TKey> 或 IEntity；无序集合必须有稳定身份键", "ChangeGraph", DiagnosticSeverity.Error, true);
    /// <summary>
    /// 提示属性规则不会改写手写变更记录。
    /// </summary>
    private static readonly DiagnosticDescriptor ManualChangeTrackingRule = new DiagnosticDescriptor(
        "CG015", "属性规则不会改写手写 AddChanges", "类型“{0}”的 ChangeIgnore 只影响自动生成代码，不会改写手写 AddChanges；请自行维护旧入口逻辑", "ChangeGraph", DiagnosticSeverity.Info, true);
    /// <summary>
    /// 报告类型声明了与生成 hook 冲突的方法。
    /// </summary>
    private static readonly DiagnosticDescriptor HookSignatureConflict = new DiagnosticDescriptor(
        "CG020", "领域对象 hook 签名冲突", "类型“{0}”声明了方法“{1}”；AddChanges hook 必须使用 override 或重命名，生成器不能重复声明", "ChangeGraph", DiagnosticSeverity.Error, true);
    /// <summary>
    /// 报告类型已声明生成器保留的辅助成员。
    /// </summary>
    private static readonly DiagnosticDescriptor CaptureChangesConflict = new DiagnosticDescriptor(
        "CG021", "领域对象辅助成员冲突", "类型“{0}”已经声明 CaptureChanges，生成器无法安全添加采集入口", "ChangeGraph", DiagnosticSeverity.Error, true);

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

            // 生成器保留 CaptureChanges；用户声明同名成员时必须定位到源声明而不是让 CS0111 落在生成文件。
            if (classSymbol.GetMembers("CaptureChanges").Length != 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(CaptureChangesConflict, classSymbol.Locations.FirstOrDefault(),
                    classSymbol.ToDisplayString()));
                continue;
            }

            var hookParameter = GetChangeParameterType(classSymbol);
            var addChangesMembers = classSymbol.GetMembers("AddChanges").OfType<IMethodSymbol>()
                .Where(item => !item.IsStatic).ToList();
            var manualMethod = addChangesMembers.FirstOrDefault(item => item.IsOverride);
            var conflictingHook = addChangesMembers.FirstOrDefault(item =>
                !item.IsOverride && HasHookSignature(item, hookParameter));
            if (conflictingHook != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(HookSignatureConflict,
                    conflictingHook.Locations.FirstOrDefault(), classSymbol.ToDisplayString(), conflictingHook.Name));
                continue;
            }

            // 手写入口保持原样，但必须在跳过生成前说明属性规则的适用范围。
            if (manualMethod != null && GetPublicInstanceProperties(classSymbol).Any(property =>
                    FindAttribute(property, ChangeIgnoreAttributeName) != null))
                context.ReportDiagnostic(Diagnostic.Create(ManualChangeTrackingRule,
                    manualMethod.Locations.FirstOrDefault(), classSymbol.ToDisplayString()));

            GenerateEntityChangeTracking(context, classSymbol, manualMethod == null);
        }
    }

    /// <summary>
    /// 判断方法是否与 AddChanges hook 签名一致。
    /// </summary>
    /// <param name="method">待检查的方法符号。</param>
    /// <param name="parameterType">真实 hook 参数类型。</param>
    /// <returns>单个参数、参数类型一致且返回 void 时为 true。</returns>
    private static bool HasHookSignature(IMethodSymbol method, ITypeSymbol parameterType) =>
        method.ReturnsVoid && method.Parameters.Length == 1 &&
        SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, parameterType);

    /// <summary>
    /// 校验领域对象并生成其对象图模型与变更跟踪入口。
    /// </summary>
    /// <param name="context">用于读取语义模型、输出源码及报告诊断的生成上下文。</param>
    /// <param name="root">需要生成变更跟踪的领域对象类型。</param>
    /// <param name="generateAddChanges">是否生成 AddChanges 重写；手写 override 时为 false。</param>
    private static void GenerateEntityChangeTracking(GeneratorExecutionContext context, INamedTypeSymbol root,
        bool generateAddChanges)
    {
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
        if (root.GetMembers("ChangeGraphModel").Length != 0 ||
            root.GetMembers("GeneratedChangeGraphModel").Length != 0 ||
            root.GetMembers("CaptureChanges").Length != 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(ExistingModelProperty, root.Locations.FirstOrDefault(), root.ToDisplayString()));
            return;
        }

        var builder = new GraphBuilder(context, root);
        var rootModel = builder.EnsureModel(root);
        if (rootModel == null || builder.HasUnboundedGenericExpansion)
            return;

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable disable");
        var namespaceName = root.ContainingNamespace.IsGlobalNamespace ? null : root.ContainingNamespace.ToDisplayString();
        if (!string.IsNullOrWhiteSpace(namespaceName))
        {
            source.Append("namespace ").Append(namespaceName).AppendLine(";");
            source.AppendLine();
        }

        var typeChain = GetTypeChain(root);
        foreach (var type in typeChain)
        {
            AppendPartialTypeDeclaration(source, type, context, SymbolEqualityComparer.Default.Equals(type, root));
            source.AppendLine("{");
        }
        source.AppendLine("    /// <summary>获取源生成的对象图模型。</summary>");
        source.Append("    public ");
        if (HasBaseGraphModel(root, context))
            source.Append("new ");
        source.Append("global::").Append(ChangeGraphModelName).AppendLine(" ChangeGraphModel => __ChangeGraph.Model0;");
        source.AppendLine("    /// <summary>获取当前类型的独立生成模型，供其他实体直接调用。</summary>");
        source.Append("    public ");
        if (HasBaseGraphModel(root, context))
            source.Append("new ");
        source.Append("static global::").Append(ChangeGraphModelName).Append("<").Append(TypeName(root))
            .AppendLine("> GeneratedChangeGraphModel => __ChangeGraph.Model0;");
        source.AppendLine();
        source.AppendLine("    /// <summary>采集自身成员；关系成员调用对应实体模型并共享当前会话。</summary>");
        source.Append("    private void CaptureChanges(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<")
            .Append(TypeName(root)).AppendLine("> writer)");
        source.AppendLine("    {");
        foreach (var member in rootModel.Members)
            EmitMember(source, member, root, context, "this");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    private static class __ChangeGraph");
        source.AppendLine("    {");
        foreach (var model in builder.Models.Where(model => !model.IsExternal))
            EmitModel(source, model, root, context, model == rootModel);
        source.AppendLine("    }");
        for (var index = typeChain.Count - 1; index >= 0; index--)
            source.AppendLine("}");

        context.AddSource(GetHintName(root, "ChangeGraph"), SourceText.From(source.ToString(), Encoding.UTF8));

        if (!generateAddChanges)
            return;
        // 所有实体使用同一逐成员 hook：标量直接比较，领域对象调用子 GetChanges，集合按身份或索引匹配。
        GenerateMemberAddChanges(context, root, rootModel);
    }

    /// <summary>
    /// 判断基类是否提供变更图模型。
    /// </summary>
    /// <param name="root">需要生成变更跟踪的领域类型。</param>
    /// <param name="context">用于读取领域基类符号的生成上下文。</param>
    /// <returns>基类已有可访问模型成员或同为源码内生成的领域对象时为 true，否则为 false。</returns>
    private static bool HasBaseGraphModel(INamedTypeSymbol root, GeneratorExecutionContext context)
    {
        var domainBaseSymbol = context.Compilation.GetTypeByMetadataName(DomainObjectBaseName);
        // 基类的模型可能仍待本轮生成，不能只查询已有成员符号；源码内声明的领域对象视为会生成。
        for (var current = root.BaseType; current != null; current = current.BaseType)
        {
            if (current.GetMembers("ChangeGraphModel").Any(member => member.DeclaredAccessibility != Accessibility.Private))
                return true;
            if (current.DeclaringSyntaxReferences.Length != 0 && IsDomainObjectBase(current, domainBaseSymbol))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 输出对象或容器的变更图模型字段。
    /// </summary>
    /// <param name="source">接收生成源码的文本缓冲区。</param>
    /// <param name="model">需要构建或输出的变更图模型信息。</param>
    private static void EmitModel(StringBuilder source, GraphModelInfo model, INamedTypeSymbol owner,
        GeneratorExecutionContext context, bool ownsCapture = false)
    {
        if (model.Shape != GraphModelShape.Object)
        {
            EmitCollectionModel(source, model);
            return;
        }
        source.Append("        internal static readonly global::").Append(ChangeGraphModelName).Append("<")
            .Append(TypeName(model.Type)).Append("> Model").Append(model.Index).Append(" = new global::")
            .Append(ChangeGraphModelName).Append("<").Append(TypeName(model.Type)).Append(">(");
        // 独立泛型模型必须区分 Box<int>/Box<string>，并区分不同程序集中的同名实体。
        // typeof 是静态类型引用；不扫描运行时成员，也不动态构造泛型。
        source.Append("typeof(").Append(TypeOfTypeName(model.Type)).Append(").AssemblyQualifiedName + \":\" + ")
            .Append(ToLiteral(model.ModelId)).Append(", ");
        if (ownsCapture)
            source.Append("static (writer, value) => value.CaptureChanges(writer), ");
        else
        {
            source.AppendLine("static (writer, value) =>");
            source.AppendLine("        {");
            foreach (var member in model.Members)
                EmitMember(source, member, owner, context);
            source.Append("        }, ");
        }
        source.Append(BuildKeySelector(model.Key));
        source.Append(", new string[] { ")
            .Append(string.Join(", ", model.Members.Where(member => member.Kind != GraphMemberKind.Unsupported)
                .Select(member => ToLiteral(member.Property.Name))))
            .Append(" }, ");
        var collectionMembers = model.Members.Where(IsSequenceOrSetMember).ToList();
        if (collectionMembers.Count == 0)
            source.Append("null");
        else
        {
            source.Append("new global::System.Collections.Generic.Dictionary<string, global::System.Type> { ");
            foreach (var member in collectionMembers)
            {
                var elementType = GetCollectionElementType(member);
                source.Append("{ ").Append(ToLiteral(member.Property.Name)).Append(", typeof(")
                    .Append(TypeOfTypeName(elementType)).Append(") }, ");
            }
            source.Append("}");
        }
        source.AppendLine(");");
        source.AppendLine();
    }

    /// <summary>
    /// 输出容器形状的变更图模型字段。
    /// </summary>
    /// <param name="source">接收生成源码的文本缓冲区。</param>
    /// <param name="model">需要构建或输出的变更图模型信息。</param>
    private static void EmitCollectionModel(StringBuilder source, GraphModelInfo model)
    {
        var modelType = model.Shape == GraphModelShape.Nullable ? NullableValueTypeName(model.Type) : TypeName(model.Type);
        var id = ToLiteral(model.ModelId);
        source.Append("        internal static readonly global::").Append(ChangeGraphModelName).Append("<")
            .Append(modelType).Append("> Model").Append(model.Index).Append(" = ");
        switch (model.Shape)
        {
            case GraphModelShape.Sequence:
                source.Append("global::").Append(ChangeGraphModelName).Append("<").Append(modelType).Append(">.CreateSequenceModel<")
                    .Append(modelType).Append(", ").Append(TypeName(model.ElementType)).Append(">( ");
                source.Append(id).Append(", static value => value, static () => ").Append(ModelReference(model.ElementModel))
                    .Append(", ").Append(ElementKeyResolver(model.ElementModel, model.ElementKey)).AppendLine(" );");
                break;
            case GraphModelShape.ScalarSequence:
                source.Append("global::").Append(ChangeGraphModelName).Append("<").Append(modelType).Append(">.CreateScalarSequenceModel<")
                    .Append(modelType).Append(", ").Append(TypeName(model.ElementType)).Append(">( ")
                    .Append(id).AppendLine(", static value => value );");
                break;
            case GraphModelShape.Dictionary:
                source.Append("global::").Append(ChangeGraphModelName).Append("<").Append(modelType).Append(">.CreateDictionaryModel<")
                    .Append(modelType).Append(", ").Append(TypeName(model.KeyType)).Append(", ").Append(TypeName(model.ValueType)).Append(">( ")
                    .Append(id).Append(", static value => value, static () => ").Append(ModelReference(model.ValueModel)).AppendLine(" );");
                break;
            case GraphModelShape.ScalarDictionary:
                source.Append("global::").Append(ChangeGraphModelName).Append("<").Append(modelType).Append(">.CreateScalarDictionaryModel<")
                    .Append(modelType).Append(", ").Append(TypeName(model.KeyType)).Append(", ").Append(TypeName(model.ValueType)).Append(">( ")
                    .Append(id).AppendLine(", static value => value );");
                break;
            case GraphModelShape.Set:
                source.Append("global::").Append(ChangeGraphModelName).Append("<").Append(modelType).Append(">.CreateSetModel<")
                    .Append(modelType).Append(", ").Append(TypeName(model.ElementType)).Append(">( ")
                    .Append(id).Append(", static value => value, static () => ").Append(ModelReference(model.ElementModel))
                    .Append(", ").Append(ElementKeyResolver(model.ElementModel, model.ElementKey)).AppendLine(" );");
                break;
            case GraphModelShape.ScalarSet:
                source.Append("global::").Append(ChangeGraphModelName).Append("<").Append(modelType).Append(">.CreateScalarSetModel<")
                    .Append(modelType).Append(", ").Append(TypeName(model.ElementType)).Append(">( ")
                    .Append(id).AppendLine(", static value => value );");
                break;
            case GraphModelShape.Adapter:
                source.Append("global::").Append(ChangeGraphModelName).Append("<").Append(modelType)
                    .Append(">.CreateAdapterModel<").Append(modelType).Append(">( ").Append(id).AppendLine(" );");
                break;
            case GraphModelShape.Nullable:
                source.Append("global::").Append(ChangeGraphModelName).Append("<").Append(NullableValueTypeName(model.Type))
                    .Append(">.CreateNullableModel<").Append(TypeName(model.ElementType)).Append(">( ")
                    .Append(id).Append(", static () => ").Append(ModelReference(model.ElementModel)).AppendLine(" );");
                break;
        }
        source.AppendLine();
    }

    /// <summary>
    /// 判断成员是否为序列或集合形状。
    /// </summary>
    /// <param name="member">包含属性符号及捕获形状的成员信息。</param>
    /// <returns>成员为标量或对象序列、集合时为 true，否则为 false。</returns>
    private static bool IsSequenceOrSetMember(GraphMemberInfo member) =>
        member.Kind == GraphMemberKind.SequenceScalar || member.Kind == GraphMemberKind.SequenceObject ||
        member.Kind == GraphMemberKind.SetScalar || member.Kind == GraphMemberKind.SetObject;

    /// <summary>生成对实体自身模型或本地容器模型的直接引用。</summary>
    private static string ModelReference(GraphModelInfo model) => model.IsExternal
        ? TypeName(model.Type) + ".GeneratedChangeGraphModel"
        : "__ChangeGraph.Model" + model.Index;

    /// <summary>实体的集合身份规则由该实体自己的模型提供。</summary>
    private static string ElementKeyReference(GraphModelInfo model, KeySpec key) => model.IsExternal
        ? ModelReference(model) + ".KeySelector"
        : BuildKeySelector(key);

    /// <summary>容器初始化时延迟读取实体键，避免双向关系的静态初始化环。</summary>
    private static string ElementKeyResolver(GraphModelInfo model, KeySpec key) => model.IsExternal
        ? "keySelectorResolver: static () => " + ModelReference(model) + ".KeySelector"
        : BuildKeySelector(key);

    /// <summary>
    /// 解析序列或集合成员的元素类型。
    /// </summary>
    /// <param name="member">包含属性符号及捕获形状的成员信息。</param>
    /// <returns>成员的集合元素类型；成员形状不匹配时抛出异常。</returns>
    private static ITypeSymbol GetCollectionElementType(GraphMemberInfo member)
    {
        if (TryGetSet(member.Property.Type, out var setElement))
            return setElement;
        if (TryGetSequence(member.Property.Type, out var sequenceElement))
            return sequenceElement;
        throw new InvalidOperationException("集合成员元数据与生成的成员形状不一致。");
    }

    /// <summary>
    /// 输出成员捕获及异常包装代码。
    /// </summary>
    /// <param name="source">接收生成源码的文本缓冲区。</param>
    /// <param name="member">包含属性符号及捕获形状的成员信息。</param>
    /// <param name="owner">报告诊断与解析静态资源访问所用的拥有者类型。</param>
    /// <param name="context">用于读取语义模型、输出源码及报告诊断的生成上下文。</param>
    /// <param name="ownerVar">生成捕获代码时访问成员所用的对象变量名。</param>
    private static void EmitMember(StringBuilder source, GraphMemberInfo member, INamedTypeSymbol owner,
        GeneratorExecutionContext context, string ownerVar = "value")
    {
        var name = member.Property.Name;
        var value = ownerVar + "." + EscapeIdentifier(name);
        var literalName = ToLiteral(name);
        var literalDescription = GetLegacyDescriptionExpression(member.Property, owner, context);
        source.Append("            if (!writer.IsIgnored(").Append(literalName).AppendLine("))");
        source.AppendLine("            {");
        source.AppendLine("                try");
        source.AppendLine("                {");
        switch (member.Kind)
        {
            case GraphMemberKind.Scalar:
                source.Append("                    writer.Scalar(").Append(literalName).Append(", ").Append(value)
                    .Append(", ").Append(literalDescription).AppendLine(");");
                break;
            case GraphMemberKind.Object:
                source.Append("                    writer.Object(").Append(literalName).Append(", ").Append(value)
                    .Append(", ").Append(ModelReference(member.ChildModel)).Append(", ").Append(literalDescription).AppendLine(");");
                break;
            case GraphMemberKind.SequenceScalar:
                source.Append("                    writer.SequenceScalar(").Append(literalName).Append(", ").Append(value)
                    .Append(", ").Append(literalDescription).AppendLine(");");
                break;
            case GraphMemberKind.SequenceObject:
                source.Append("                    writer.Sequence(").Append(literalName).Append(", ").Append(value)
                    .Append(", ").Append(ModelReference(member.ChildModel)).Append(", ")
                    .Append(ElementKeyReference(member.ChildModel, member.ElementKey)).Append(", ").Append(literalDescription).AppendLine(");");
                break;
            case GraphMemberKind.DictionaryScalar:
                source.Append("                    writer.DictionaryScalar(").Append(literalName).Append(", ").Append(value)
                    .Append(", ").Append(literalDescription).AppendLine(");");
                break;
            case GraphMemberKind.DictionaryObject:
                source.Append("                    writer.Dictionary(").Append(literalName).Append(", ").Append(value)
                    .Append(", ").Append(ModelReference(member.ChildModel)).Append(", ").Append(literalDescription).AppendLine(");");
                break;
            case GraphMemberKind.SetScalar:
                source.Append("                    writer.SetScalar(").Append(literalName).Append(", ").Append(value)
                    .Append(", ").Append(literalDescription).AppendLine(");");
                break;
            case GraphMemberKind.SetObject:
                source.Append("                    writer.Set(").Append(literalName).Append(", ").Append(value)
                    .Append(", ").Append(ModelReference(member.ChildModel)).Append(", ")
                    .Append(ElementKeyReference(member.ChildModel, member.ElementKey)).Append(", ").Append(literalDescription).AppendLine(");");
                break;
            case GraphMemberKind.Unsupported:
                source.Append("                    writer.Unsupported(").Append(literalName).AppendLine(");");
                break;
            case GraphMemberKind.Adapted:
                source.Append("                    writer.Adapted(").Append(literalName).Append(", ").Append(value)
                    .Append(", ").Append(literalDescription).AppendLine(");");
                break;
        }
        source.AppendLine("                }");
        source.AppendLine("                catch (global::System.Exception exception)");
        source.AppendLine("                {");
        source.Append("                    throw writer.CreateCaptureException(").Append(literalName).AppendLine(", exception);");
        source.AppendLine("                }");
        source.AppendLine("            }");
    }

    /// <summary>
    /// 构造变更标识键的选择器源码。
    /// </summary>
    /// <param name="key">元素标识键定义；可为空以表示无键。</param>
    /// <returns>键选择器的 Lambda 源码；未定义键成员时为文本 null。</returns>
    private static string BuildKeySelector(KeySpec key)
    {
        if (key == null)
            return "null";
        if (key.UsesGetKeys)
            return "static value => { if (value == null) return null; " +
                   "var keys = ((global::Bing.Domain.Entities.IEntity)value).GetKeys(); " +
                   "return keys.Length == 1 ? keys[0] : (object)global::Bing.Domain.ChangeTracking.ChangeGraphKey.Create(keys); }";
        if (key.Members.Count == 0)
            return "null";
        var guard = key.Members[0].Property.ContainingType.IsReferenceType ? "value == null ? null : " : string.Empty;
        var accesses = key.Members.Select(member => "(object)" + member.BuildAccess("value")).ToList();
        if (!key.IsExplicit && key.Members.Count == 1)
            return "static value => " + guard + accesses[0];
        var arguments = string.Join(", ", accesses);
        return "static value => " + guard + "global::Bing.Domain.ChangeTracking.ChangeGraphKey.Create(" + arguments + ")";
    }

    /// <summary>
    /// 为实体生成逐成员变更比较方法；关系成员调用子实体 GetChanges 并汇总结果。
    /// </summary>
    /// <param name="context">用于读取语义模型、输出源码及报告诊断的生成上下文。</param>
    /// <param name="classSymbol">需要生成变更跟踪代码的领域类型。</param>
    /// <param name="model">当前实体的成员模型。</param>
    private static void GenerateMemberAddChanges(GeneratorExecutionContext context, INamedTypeSymbol classSymbol,
        GraphModelInfo model)
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
            AppendPartialTypeDeclaration(source, type, context, false);
            source.AppendLine("{");
        }
        var parameterType = GetChangeParameterType(classSymbol);
        source.Append("    protected override void AddChanges(").Append(TypeName(parameterType)).AppendLine(" other)");
        source.AppendLine("    {");
        var direct = SymbolEqualityComparer.Default.Equals(parameterType, classSymbol);
        if (!direct)
            AppendComparedObject(source, classSymbol, parameterType);
        var otherVar = direct ? "other" : "typedOther";
        foreach (var member in model.Members)
        {
            if (member.Kind == GraphMemberKind.Unsupported)
                continue;
            EmitSimplifiedMemberChange(source, member, classSymbol, context, otherVar);
        }
        source.AppendLine("    }");
        for (var index = typeChain.Count - 1; index >= 0; index--)
            source.AppendLine("}");
        context.AddSource(GetHintName(classSymbol, "AddChanges"), SourceText.From(source.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// 输出单个成员的简化变更调用：直接属性读取 + 固定辅助方法。
    /// </summary>
    private static void EmitSimplifiedMemberChange(StringBuilder source, GraphMemberInfo member,
        INamedTypeSymbol owner, GeneratorExecutionContext context, string otherVar)
    {
        var property = member.Property;
        var name = EscapeLegacyIdentifier(property.Name);
        var nameOf = "nameof(" + name + ")";
        var description = GetLegacyDescriptionExpression(property, owner, context);
        var oldValue = "this." + name;
        var newValue = otherVar + "." + name;
        switch (member.Kind)
        {
            case GraphMemberKind.Scalar:
                source.Append("        AddChange(").Append(nameOf).Append(", ").Append(description).Append(", ")
                    .Append(oldValue).Append(", ").Append(newValue).AppendLine(");");
                return;
            case GraphMemberKind.Object:
                if (!IsChangeTrackableType(member.Property.Type))
                {
                    ReportSimplifiedUnsupported(context, property);
                    return;
                }
                source.Append("        AddObjectChanges(").Append(nameOf).Append(", ").Append(description).Append(", ")
                    .Append(oldValue).Append(", ").Append(newValue).AppendLine(");");
                return;
            case GraphMemberKind.SequenceScalar:
            case GraphMemberKind.SetScalar:
                source.Append("        AddCollectionChanges(").Append(nameOf).Append(", ").Append(description).Append(", ")
                    .Append(oldValue).Append(", ").Append(newValue).AppendLine(");");
                return;
            case GraphMemberKind.SequenceObject:
            case GraphMemberKind.SetObject:
                if (!IsSimplifiedElementSupported(member.ChildModel))
                {
                    ReportSimplifiedUnsupported(context, property);
                    return;
                }
                source.Append("        AddCollectionChanges(").Append(nameOf).Append(", ").Append(description).Append(", ")
                    .Append(oldValue).Append(", ").Append(newValue).AppendLine(");");
                return;
            case GraphMemberKind.DictionaryScalar:
            case GraphMemberKind.DictionaryObject:
                if (member.Kind == GraphMemberKind.DictionaryObject && !IsSimplifiedElementSupported(member.ChildModel))
                {
                    ReportSimplifiedUnsupported(context, property);
                    return;
                }
                source.Append("        AddDictionaryChanges(").Append(nameOf).Append(", ").Append(description).Append(", ")
                    .Append(oldValue).Append(", ").Append(newValue).AppendLine(");");
                return;
            case GraphMemberKind.Adapted:
            default:
                ReportSimplifiedUnsupported(context, property);
                return;
        }
    }

    /// <summary>
    /// 判断集合元素模型是否可由简化主链处理。
    /// </summary>
    private static bool IsSimplifiedElementSupported(GraphModelInfo model)
    {
        if (model == null)
            return false;
        if (model.IsExternal || IsChangeTrackableType(model.Type))
            return true;
        switch (model.Shape)
        {
            case GraphModelShape.ScalarSequence:
            case GraphModelShape.ScalarSet:
            case GraphModelShape.ScalarDictionary:
                return true;
            case GraphModelShape.Sequence:
            case GraphModelShape.Set:
                return IsSimplifiedElementSupported(model.ElementModel);
            case GraphModelShape.Dictionary:
                return IsSimplifiedElementSupported(model.ValueModel);
            default:
                return false;
        }
    }

    /// <summary>
    /// 报告简化主链不支持的成员类型。
    /// </summary>
    private static void ReportSimplifiedUnsupported(GeneratorExecutionContext context, IPropertySymbol property) =>
        context.ReportDiagnostic(Diagnostic.Create(UnsupportedMember, property.Locations.FirstOrDefault(),
            property.ToDisplayString()));

    /// <summary>输出结构模型引用；没有模型时输出 null。</summary>
    private static string ModelOrNull(GraphModelInfo model) => model == null ? "null" : ModelReference(model);

    /// <summary>输出集合元素身份选择器；没有身份时为 null。</summary>
    private static string KeyOrNull(GraphMemberInfo member) =>
        member.ChildModel == null ? "null" : ElementKeyReference(member.ChildModel, member.ElementKey);

    /// <summary>
    /// 判断类型是否实现变更跟踪契约（领域实体）。
    /// </summary>
    /// <param name="type">待判断的类型符号。</param>
    /// <returns>实现 IChangeTrackable 时为 true。</returns>
    private static bool IsChangeTrackableType(ITypeSymbol type) =>
        type != null && type.AllInterfaces.Any(item =>
            item.ToDisplayString() == "Bing.Domain.ChangeTracking.IChangeTrackable");

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
    /// <param name="implementsGraphNode">是否为当前分部声明补充变更图节点接口。</param>
    private static void AppendPartialTypeDeclaration(StringBuilder source, INamedTypeSymbol type,
        GeneratorExecutionContext context, bool implementsGraphNode)
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
        if (implementsGraphNode)
            source.Append(" : global::").Append(ChangeGraphNodeInterfaceName);
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
    /// 判断接口是否为领域实体键契约。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <returns>名称、泛型元数及命名空间均匹配领域 IKey 契约时为 true，否则为 false。</returns>
    private static bool IsIKeyInterface(INamedTypeSymbol type) =>
        type.Name == "IKey" && type.Arity == 1 && type.ContainingNamespace.ToDisplayString() == "Bing.Domain.Entities";

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
    /// 构造对象图模型的稳定标识。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <returns>类型全名作为模型标识。</returns>
    private static string GetModelId(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

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
    /// 生成显式保留可空值类型的类型名称。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <returns>可空值类型使用全限定 Nullable 写法，其他类型使用普通生成名称。</returns>
    private static string NullableValueTypeName(ITypeSymbol type) =>
        type is INamedTypeSymbol nullable && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? "global::System.Nullable<" + TypeName(nullable.TypeArguments[0]) + ">"
            : TypeName(type);

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
    /// 生成 typeof 表达式使用的类型名称。
    /// </summary>
    /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
    /// <returns>移除可空引用修饰符后的全限定类型名称。</returns>
    private static string TypeOfTypeName(ITypeSymbol type) => TypeName(type);

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
