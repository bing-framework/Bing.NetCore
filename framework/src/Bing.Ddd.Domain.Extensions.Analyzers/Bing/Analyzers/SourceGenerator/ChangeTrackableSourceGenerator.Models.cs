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
/// 构建变更图模型闭包与规则视图的源生成器。
/// </summary>
/// <remarks>
/// ChangeGraph 类型闭包、规则视图和模型构建。
/// </remarks>
public sealed partial class ChangeTrackableSourceGenerator
{
    /// <summary>
    /// 收集源生成器识别到的图模型并检查展开边界。
    /// </summary>
    private sealed class GraphBuilder
    {
        /// <summary>
        /// 保存源生成器执行上下文，用于报告诊断和输出源码。
        /// </summary>
        private readonly GeneratorExecutionContext _context;
        /// <summary>
        /// 保存正在生成变更模型的根类型符号。
        /// </summary>
        private readonly INamedTypeSymbol _root;
        /// <summary>
        /// 保存真实领域对象基类符号，用于语义继承判断。
        /// </summary>
        private readonly INamedTypeSymbol _domainBaseSymbol;
        /// <summary>
        /// 保存已生成模型按类型名索引的映射。
        /// </summary>
        private readonly Dictionary<string, GraphModelInfo> _modelsByType = new Dictionary<string, GraphModelInfo>(StringComparer.Ordinal);
        /// <summary>
        /// 保存当前待展开的模型类型栈。
        /// </summary>
        private readonly List<INamedTypeSymbol> _activeModelTypes = new List<INamedTypeSymbol>();
        /// <summary>
        /// 记录已报告的无界泛型展开，避免重复诊断。
        /// </summary>
        private readonly HashSet<string> _reportedGenericExpansions = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>
        /// 记录已报告的适配器需求。
        /// </summary>
        private readonly HashSet<string> _reportedAdapterRequirements = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 初始化一个 <see cref="GraphBuilder" /> 类型的实例。
        /// </summary>
        /// <param name="context">当前操作关联的上下文或工作单元。</param>
        /// <param name="root">当前对象图、工作单元或文件系统的根对象或根路径。</param>
        internal GraphBuilder(GeneratorExecutionContext context, INamedTypeSymbol root)
        {
            _context = context;
            _root = root;
            _domainBaseSymbol = context.Compilation.GetTypeByMetadataName(DomainObjectBaseName);
            Models = new List<GraphModelInfo>();
        }

        /// <summary>
        /// 获取分析过程中发现的模型集合。
        /// </summary>
        internal List<GraphModelInfo> Models { get; }
        /// <summary>
        /// 获取泛型模型展开是否无法确定边界。
        /// </summary>
        internal bool HasUnboundedGenericExpansion { get; private set; }

        /// <summary>
        /// 创建或复用对象及可空对象的模型。
        /// </summary>
        /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
        /// <returns>对应模型；类型不支持或检测到无界泛型展开时返回 null。</returns>
        internal GraphModelInfo EnsureModel(ITypeSymbol type) => EnsureObjectModelOrNullable(type);

        /// <summary>
        /// 创建或复用容器及对象形状模型。
        /// </summary>
        /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
        /// <returns>对应形状模型；类型不支持或检测到无界泛型展开时返回 null。</returns>
        internal GraphModelInfo EnsureShapeModel(ITypeSymbol type)
        {
            if (!IsSupportedShape(type))
                return null;
            if (!TryEnterModelType(type))
                return null;
            try
            {
                if (TryGetDictionary(type, out var dictionaryKey, out var dictionaryValue))
                {
                    var cacheKey = "dictionary:" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (_modelsByType.TryGetValue(cacheKey, out var existing))
                        return existing;
                    var shape = IsScalarType(dictionaryValue) ? GraphModelShape.ScalarDictionary : GraphModelShape.Dictionary;
                    var model = new GraphModelInfo(type, Models.Count, shape, null, dictionaryKey, dictionaryValue, GetKeySpec(type),
                        GetModelId(type));
                    _modelsByType.Add(cacheKey, model);
                    Models.Add(model);
                    if (shape == GraphModelShape.Dictionary)
                        model.ValueModel = EnsureShapeModel(dictionaryValue);
                    return model;
                }
                if (TryGetSet(type, out var setElement))
                {
                    var cacheKey = "set:" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (_modelsByType.TryGetValue(cacheKey, out var existing))
                        return existing;
                    var shape = IsScalarType(setElement) ? GraphModelShape.ScalarSet : GraphModelShape.Set;
                    var model = new GraphModelInfo(type, Models.Count, shape, setElement, null, null, GetKeySpec(type),
                        GetModelId(type));
                    _modelsByType.Add(cacheKey, model);
                    Models.Add(model);
                    if (shape == GraphModelShape.Set)
                    {
                        model.ElementKey = GetKeySpec(setElement);
                        model.ElementModel = EnsureShapeModel(setElement);
                    }
                    return model;
                }
                if (TryGetSequence(type, out var sequenceElement))
                {
                    var cacheKey = "sequence:" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (_modelsByType.TryGetValue(cacheKey, out var existing))
                        return existing;
                    var shape = IsScalarType(sequenceElement) ? GraphModelShape.ScalarSequence : GraphModelShape.Sequence;
                    var model = new GraphModelInfo(type, Models.Count, shape, sequenceElement, null, null, GetKeySpec(type),
                        GetModelId(type));
                    _modelsByType.Add(cacheKey, model);
                    Models.Add(model);
                    if (shape == GraphModelShape.Sequence)
                    {
                        model.ElementKey = GetKeySpec(sequenceElement);
                        model.ElementModel = EnsureShapeModel(sequenceElement);
                    }
                    return model;
                }
                if (IsAdapterType(type))
                {
                    var cacheKey = "adapter:" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (_modelsByType.TryGetValue(cacheKey, out var existing))
                        return existing;
                    var model = new GraphModelInfo(type, Models.Count, GraphModelShape.Adapter, null, null, null, null,
                        GetModelId(type));
                    _modelsByType.Add(cacheKey, model);
                    Models.Add(model);
                    return model;
                }
                return EnsureObjectModelOrNullable(type);
            }
            finally
            {
                ExitModelType(type);
            }
        }

        /// <summary>
        /// 创建或复用具体对象模型。
        /// </summary>
        /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
        /// <returns>对应对象模型；类型不支持或检测到无界泛型展开时返回 null。</returns>
        private GraphModelInfo EnsureObjectModel(ITypeSymbol type)
        {
            if (!(type is INamedTypeSymbol named) || named.TypeKind == TypeKind.Error || named.TypeKind == TypeKind.Interface ||
                named.SpecialType == SpecialType.System_Object || named.IsUnboundGenericType)
                return null;
            var key = "object:" + named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (_modelsByType.TryGetValue(key, out var existing))
                return existing;
            // 独立实体拥有自己的生成模型。这里只记录关系引用，不遍历或复制它的成员和键。
            if (IsDomainObjectBase(named, _domainBaseSymbol) && !SymbolEqualityComparer.Default.Equals(named, _root))
            {
                if (!TryEnterModelType(named))
                    return null;
                try
                {
                    ValidateEntityDependencies(named, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
                }
                finally { ExitModelType(named); }
                var external = new GraphModelInfo(named, Models.Count, GraphModelShape.Object, null, null, null, null,
                    named.ToDisplayString()) { IsExternal = true };
                _modelsByType.Add(key, external);
                Models.Add(external);
                return external;
            }
            if (!TryEnterModelType(named))
                return null;
            try
            {
                var model = new GraphModelInfo(named, Models.Count, GraphModelShape.Object, null, null, null, GetKeySpec(named),
                    GetModelId(named));
                _modelsByType.Add(key, model);
                Models.Add(model);
                BuildMembers(model);
                return model;
            }
            finally
            {
                ExitModelType(named);
            }
        }

        /// <summary>
        /// 创建或复用对象及可空对象的模型。
        /// </summary>
        /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
        /// <returns>对应模型；非可空对象类型不支持或检测到无界泛型展开时返回 null。</returns>
        private GraphModelInfo EnsureObjectModelOrNullable(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol nullable &&
                nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && nullable.TypeArguments.Length == 1 &&
                !IsScalarType(nullable))
            {
                var key = "nullable:" + nullable.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (_modelsByType.TryGetValue(key, out var existing))
                    return existing;
                var model = new GraphModelInfo(nullable, Models.Count, GraphModelShape.Nullable,
                    nullable.TypeArguments[0], null, null, null, GetModelId(nullable));
                _modelsByType.Add(key, model);
                Models.Add(model);
                model.ElementModel = EnsureModel(nullable.TypeArguments[0]);
                return model;
            }
            return EnsureObjectModel(type);
        }

        /// <summary>
        /// 沿实体依赖检查泛型闭包，不改变各实体的生成归属。
        /// </summary>
        /// <param name="owner">当前实体或结构值的类型。</param>
        /// <param name="visited">本次依赖检查已访问的类型。</param>
        private void ValidateEntityDependencies(INamedTypeSymbol owner, HashSet<ITypeSymbol> visited)
        {
            if (!visited.Add(owner) || HasUnboundedGenericExpansion)
                return;
            foreach (var property in GetPublicInstanceProperties(owner))
            {
                if (ShouldIgnore(owner, property) || property.Name == "ChangeGraphModel")
                    continue;
                ValidateDependency(property.Type, visited);
            }
        }

        /// <summary>验证关系依赖而不复制子实体的模型或成员代码。</summary>
        private void ValidateDependency(ITypeSymbol type, HashSet<ITypeSymbol> visited)
        {
            if (IsScalarType(type) || IsAdapterType(type) || !IsSupportedShape(type))
                return;
            if (TryGetDictionary(type, out var value)) { ValidateDependency(value, visited); return; }
            if (TryGetSequence(type, out var element)) { ValidateDependency(element, visited); return; }
            if (!(type is INamedTypeSymbol named) || visited.Contains(named))
                return;
            if (!TryEnterModelType(named))
                return;
            try { ValidateEntityDependencies(named, visited); }
            finally { ExitModelType(named); }
        }

        /// <summary>构建当前实体自身的成员模型。</summary>
        private void BuildMembers(GraphModelInfo model)
        {
            if (!(model.Type is INamedTypeSymbol owner))
                return;
            foreach (var property in GetPublicInstanceProperties(owner))
            {
                // 引用程序集中的生成入口是基础设施，不属于业务属性。
                if (property.Name == "ChangeGraphModel" && property.Type.ToDisplayString() == ChangeGraphModelName)
                    continue;
                if (ShouldIgnore(owner, property))
                    continue;
                var member = BuildMember(property);
                if (member != null)
                    model.Members.Add(member);
            }
        }

        /// <summary>
        /// 按属性形状构建成员模型信息。
        /// </summary>
        /// <param name="property">待分析或报告诊断的属性符号。</param>
        /// <returns>属性对应的成员信息；不支持的形状以 Unsupported 成员表示。</returns>
        private GraphMemberInfo BuildMember(IPropertySymbol property)
        {
            var type = property.Type;
            if (!IsSupportedShape(type))
            {
                ReportUnsupported(property);
                return new GraphMemberInfo(property, GraphMemberKind.Unsupported, null, null);
            }
            // 递归校验所有层级的无序对象 Set 必须具备领域身份。
            if (!ValidateSetIdentities(type, property))
            {
                return new GraphMemberInfo(property, GraphMemberKind.Unsupported, null, null);
            }
            if (ContainsAdapterLeaf(type))
                ReportAdapterRequired(property);
            if (IsAdapterType(type))
            {
                return new GraphMemberInfo(property, GraphMemberKind.Adapted, null, null);
            }
            if (TryGetDictionary(type, out var dictionaryValue))
            {
                if (IsScalarType(dictionaryValue))
                    return new GraphMemberInfo(property, GraphMemberKind.DictionaryScalar, null, null);
                var child = EnsureShapeModel(dictionaryValue);
                if (child == null)
                {
                    ReportUnsupported(property);
                    return new GraphMemberInfo(property, GraphMemberKind.Unsupported, null, null);
                }
                return new GraphMemberInfo(property, GraphMemberKind.DictionaryObject, child, GetElementKeySpec(dictionaryValue));
            }

            if (TryGetSet(type, out var setElement))
            {
                if (IsScalarType(setElement))
                    return new GraphMemberInfo(property, GraphMemberKind.SetScalar, null, null);
                var elementKey = GetElementKeySpec(setElement);
                if (elementKey == null)
                {
                    _context.ReportDiagnostic(Diagnostic.Create(SetElementNeedsIdentity,
                        property.Locations.FirstOrDefault(), property.ToDisplayString(), setElement.ToDisplayString()));
                    return new GraphMemberInfo(property, GraphMemberKind.Unsupported, null, null);
                }
                var child = EnsureShapeModel(setElement);
                if (child == null)
                {
                    ReportUnsupported(property);
                    return new GraphMemberInfo(property, GraphMemberKind.Unsupported, null, null);
                }
                return new GraphMemberInfo(property, GraphMemberKind.SetObject, child, elementKey);
            }

            if (TryGetSequence(type, out var sequenceElement))
            {
                if (IsScalarType(sequenceElement))
                    return new GraphMemberInfo(property, GraphMemberKind.SequenceScalar, null, null);
                var child = EnsureShapeModel(sequenceElement);
                if (child == null)
                {
                    ReportUnsupported(property);
                    return new GraphMemberInfo(property, GraphMemberKind.Unsupported, null, null);
                }
                return new GraphMemberInfo(property, GraphMemberKind.SequenceObject, child, GetKeySpec(sequenceElement));
            }

            if (IsScalarType(type))
                return new GraphMemberInfo(property, GraphMemberKind.Scalar, null, null);
            if (type is INamedTypeSymbol named && (named.TypeKind == TypeKind.Class || named.TypeKind == TypeKind.Struct || named.TypeKind == TypeKind.Interface))
            {
                var child = EnsureModel(named);
                if (child != null)
                    return new GraphMemberInfo(property, GraphMemberKind.Object, child, null);
            }
            ReportUnsupported(property);
            return new GraphMemberInfo(property, GraphMemberKind.Unsupported, null, null);
        }

        /// <summary>
        /// 递归校验容器形状中的无序对象 Set 是否具备领域身份。
        /// </summary>
        /// <param name="type">待检查的成员或容器类型。</param>
        /// <param name="origin">报告诊断时定位的拥有者属性。</param>
        /// <returns>所有层级均合法时为 true；任一对象 Set 缺少身份时为 false。</returns>
        private bool ValidateSetIdentities(ITypeSymbol type, IPropertySymbol origin)
        {
            if (type == null || IsScalarType(type))
                return true;
            if (TryGetDictionary(type, out var dictionaryValue))
                return ValidateSetIdentities(dictionaryValue, origin);
            // Set 也实现 IEnumerable，必须先于序列判断，否则嵌套 Set 会被当成有序集合。
            if (TryGetSet(type, out var setElement))
            {
                if (!IsScalarType(setElement) && GetElementKeySpec(setElement) == null)
                {
                    _context.ReportDiagnostic(Diagnostic.Create(SetElementNeedsIdentity,
                        origin.Locations.FirstOrDefault(), origin.ToDisplayString(), setElement.ToDisplayString()));
                    return false;
                }
                return ValidateSetIdentities(setElement, origin);
            }
            if (TryGetSequence(type, out var sequenceElement))
                return ValidateSetIdentities(sequenceElement, origin);
            return true;
        }

        /// <summary>
        /// 判断成员是否应从模型中忽略。
        /// </summary>
        /// <param name="owner">待检查属性所处的模型类型。</param>
        /// <param name="property">待分析或报告诊断的属性符号。</param>
        /// <returns>显式忽略或默认审计预设生效时为 true，否则为 false。</returns>
        private static bool ShouldIgnore(INamedTypeSymbol owner, IPropertySymbol property)
        {
            if (FindAttribute(property, ChangeIgnoreAttributeName) != null)
                return true;
            if (property.Name == "Version" || property.Name == "IsDeleted")
                return true;
            return IsAuditProperty(owner, property);
        }

        /// <summary>
        /// 报告不支持的模型成员诊断。
        /// </summary>
        /// <param name="property">待分析或报告诊断的属性符号。</param>
        private void ReportUnsupported(IPropertySymbol property) =>
            _context.ReportDiagnostic(Diagnostic.Create(UnsupportedMember, property.Locations.FirstOrDefault(), property.ToDisplayString()));

        /// <summary>
        /// 报告成员需要显式适配器的诊断。
        /// </summary>
        /// <param name="property">待分析或报告诊断的属性符号。</param>
        /// <remarks>同一属性只报告一次适配器需求。</remarks>
        private void ReportAdapterRequired(IPropertySymbol property)
        {
            var propertyKey = property.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (_reportedAdapterRequirements.Add(propertyKey))
                _context.ReportDiagnostic(Diagnostic.Create(AdapterRequired, property.Locations.FirstOrDefault(), property.ToDisplayString()));
        }

        /// <summary>
        /// 判断类型及嵌套容器形状是否受支持。
        /// </summary>
        /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
        /// <returns>形状可处理且未发现无界泛型展开时为 true，否则为 false。</returns>
        private bool IsSupportedShape(ITypeSymbol type) => IsSupportedShape(type, new List<INamedTypeSymbol>());

        /// <summary>
        /// 判断类型及嵌套容器形状是否受支持。
        /// </summary>
        /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
        /// <param name="activeTypes">当前形状遍历中已进入的泛型类型栈。</param>
        /// <returns>形状可处理且未发现无界泛型展开时为 true，否则为 false。</returns>
        private bool IsSupportedShape(ITypeSymbol type, List<INamedTypeSymbol> activeTypes)
        {
            if (type == null || IsUnsupportedContainer(type))
                return false;

            var named = type as INamedTypeSymbol;
            var entered = false;
            if (named != null && named.Arity > 0)
            {
                var active = activeTypes.FirstOrDefault(item =>
                    SymbolEqualityComparer.Default.Equals(item.OriginalDefinition, named.OriginalDefinition));
                if (active != null)
                {
                    if (SymbolEqualityComparer.Default.Equals(active, named))
                        return true;
                    if (IsExpandingGenericInstantiation(active, named))
                    {
                        ReportGenericExpansion(active, named);
                        return false;
                    }
                }
                activeTypes.Add(named);
                entered = true;
            }

            try
            {
                if (TryGetDictionary(type, out var dictionaryValue))
                    return IsScalarType(dictionaryValue) || IsSupportedShape(dictionaryValue, activeTypes);
                if (TryGetSet(type, out var setElement))
                    return IsScalarType(setElement) || IsSupportedShape(setElement, activeTypes);
                if (TryGetSequence(type, out var sequenceElement))
                    return IsScalarType(sequenceElement) || IsSupportedShape(sequenceElement, activeTypes);
                if (IsScalarType(type))
                    return true;
                if (type.TypeKind == TypeKind.TypeParameter)
                    return true;
                if (type is IArrayTypeSymbol array)
                    return array.Rank == 1 && IsSupportedShape(array.ElementType, activeTypes);
                if (named == null || named.TypeKind == TypeKind.Error || named.IsUnboundGenericType)
                    return false;
                return named.TypeKind == TypeKind.Class || named.TypeKind == TypeKind.Struct || named.TypeKind == TypeKind.Interface;
            }
            finally
            {
                if (entered)
                    activeTypes.RemoveAt(activeTypes.Count - 1);
            }
        }

        /// <summary>
        /// 登记待构建的模型类型并检测泛型展开。
        /// </summary>
        /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
        /// <returns>类型可以进入构建时为 true，检测到无界泛型展开时为 false。</returns>
        private bool TryEnterModelType(ITypeSymbol type)
        {
            if (!(type is INamedTypeSymbol named) || named.Arity == 0)
                return true;
            var active = _activeModelTypes.FirstOrDefault(item =>
                SymbolEqualityComparer.Default.Equals(item.OriginalDefinition, named.OriginalDefinition));
            if (active != null && !SymbolEqualityComparer.Default.Equals(active, named) &&
                IsExpandingGenericInstantiation(active, named))
            {
                ReportGenericExpansion(active, named);
                return false;
            }
            _activeModelTypes.Add(named);
            return true;
        }

        /// <summary>
        /// 移除当前构建栈中的泛型模型类型。
        /// </summary>
        /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
        private void ExitModelType(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named && named.Arity > 0 && _activeModelTypes.Count != 0)
                _activeModelTypes.RemoveAt(_activeModelTypes.Count - 1);
        }

        /// <summary>
        /// 报告无界泛型展开并标记生成失败。
        /// </summary>
        /// <param name="previous">先前进入的泛型实例。</param>
        /// <param name="next">需要检测是否扩展类型参数的后继泛型实例。</param>
        private void ReportGenericExpansion(INamedTypeSymbol previous, INamedTypeSymbol next)
        {
            HasUnboundedGenericExpansion = true;
            var key = next.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (_reportedGenericExpansions.Add(key))
                _context.ReportDiagnostic(Diagnostic.Create(UnboundedGenericExpansion, next.Locations.FirstOrDefault(),
                    previous.ToDisplayString(), next.ToDisplayString()));
        }

        /// <summary>
        /// 判断后继泛型实例是否包裹先前类型参数。
        /// </summary>
        /// <param name="previous">先前进入的泛型实例。</param>
        /// <param name="next">需要检测是否扩展类型参数的后继泛型实例。</param>
        /// <returns>同位置的新参数包含不同的旧参数时为 true，否则为 false。</returns>
        private static bool IsExpandingGenericInstantiation(INamedTypeSymbol previous, INamedTypeSymbol next)
        {
            if (previous.TypeArguments.Length != next.TypeArguments.Length)
                return false;
            for (var index = 0; index < previous.TypeArguments.Length; index++)
            {
                if (!SymbolEqualityComparer.Default.Equals(previous.TypeArguments[index], next.TypeArguments[index]) &&
                    ContainsType(next.TypeArguments[index], previous.TypeArguments[index]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断类型结构是否包含目标类型。
        /// </summary>
        /// <param name="container">需要递归检查的数组或泛型类型结构。</param>
        /// <param name="candidate">需要在类型结构中查找的类型符号。</param>
        /// <returns>类型自身、数组元素或泛型参数包含目标符号时为 true，否则为 false。</returns>
        private static bool ContainsType(ITypeSymbol container, ITypeSymbol candidate)
        {
            if (SymbolEqualityComparer.Default.Equals(container, candidate))
                return true;
            if (container is IArrayTypeSymbol array)
                return ContainsType(array.ElementType, candidate);
            if (container is INamedTypeSymbol named)
                return named.TypeArguments.Any(argument => ContainsType(argument, candidate));
            return false;
        }

        /// <summary>
        /// 判断类型是否需要运行时适配器。
        /// </summary>
        /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
        /// <returns>类型为 object、接口或类型参数时为 true，否则为 false。</returns>
        private static bool IsAdapterType(ITypeSymbol type) =>
            type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Interface || type.TypeKind == TypeKind.TypeParameter;

        /// <summary>
        /// 判断容器是否包含需要适配器的叶类型。
        /// </summary>
        /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
        /// <returns>自身或容器、可空类型内部存在适配叶类型时为 true，否则为 false。</returns>
        private static bool ContainsAdapterLeaf(ITypeSymbol type)
        {
            if (IsAdapterType(type))
                return true;
            if (TryGetDictionary(type, out var dictionaryValue))
                return ContainsAdapterLeaf(dictionaryValue);
            if (TryGetSet(type, out var setElement))
                return ContainsAdapterLeaf(setElement);
            if (TryGetSequence(type, out var sequenceElement))
                return ContainsAdapterLeaf(sequenceElement);
            if (type is INamedTypeSymbol nullable && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                return ContainsAdapterLeaf(nullable.TypeArguments[0]);
            return false;
        }

        /// <summary>
        /// 解析领域身份键契约。
        /// </summary>
        /// <param name="type">需要检查、构建模型或格式化名称的类型符号。</param>
        /// <returns>有效键定义；没有可用键或键被忽略时返回 null。</returns>
        private KeySpec GetKeySpec(ITypeSymbol type)
        {
            if (!(type is INamedTypeSymbol named))
                return null;
            // 独立实体的身份由它自己的模型决定，父实体不借用子实体键。
            if (IsDomainObjectBase(named, _domainBaseSymbol) && !SymbolEqualityComparer.Default.Equals(named, _root))
                return null;
            return ResolveKeySpec(named);
        }

        /// <summary>
        /// 解析容器元素自身的领域身份键。
        /// </summary>
        /// <param name="type">集合元素类型符号。</param>
        /// <returns>元素实现 IKey/IEntity 时的键定义；否则为 null。</returns>
        private static KeySpec GetElementKeySpec(ITypeSymbol type) =>
            type is INamedTypeSymbol named ? ResolveKeySpec(named) : null;

        /// <summary>
        /// 按领域身份契约解析键定义。
        /// </summary>
        /// <param name="named">需要解析身份的实体类型符号。</param>
        /// <returns>有效键定义；没有可用键或键被忽略时返回 null。</returns>
        private static KeySpec ResolveKeySpec(INamedTypeSymbol named)
        {
            // DEC-03：实现 IEntity 时以 GetKeys() 为身份真源，即使同时实现 IKey 也优先。
            if (ImplementsEntity(named))
                return new KeySpec(new List<KeyMemberSpec>(), false) { UsesGetKeys = true };

            // 未实现 IEntity、仅实现唯一 IKey<TKey> 时使用 Id。
            var keyInterfaces = named.AllInterfaces.Where(IsIKeyInterface).ToList();
            var keyInterface = keyInterfaces.FirstOrDefault();
            if (keyInterface != null)
            {
                if (keyInterfaces.Count > 1)
                    return null;
                var idContract = keyInterface.GetMembers("Id").OfType<IPropertySymbol>().FirstOrDefault();
                var implementation = idContract == null ? null : named.FindImplementationForInterfaceMember(idContract) as IPropertySymbol;
                if (implementation != null)
                {
                    if (FindAttribute(implementation, ChangeIgnoreAttributeName) != null)
                        return null;
                    return new KeySpec(new List<KeyMemberSpec>
                    {
                        new KeyMemberSpec(implementation, keyInterface, idContract.Name)
                    }, false);
                }
            }

            return null;
        }

        /// <summary>
        /// 判断类型是否实现领域实体契约 <c>IEntity</c>。
        /// </summary>
        /// <param name="type">需要检查的类型符号。</param>
        /// <returns>实现 IEntity 契约时为 true，否则为 false。</returns>
        private static bool ImplementsEntity(INamedTypeSymbol type) =>
            type.AllInterfaces.Any(item => item.Name == "IEntity" && item.Arity == 0 &&
                                           item.ContainingNamespace.ToDisplayString() == "Bing.Domain.Entities");
    }

    /// <summary>
    /// 对象图生成器分析得到的类型模型。
    /// </summary>
    private sealed class GraphModelInfo
    {
        /// <summary>
        /// 初始化一个 <see cref="GraphModelInfo" /> 类型的实例。
        /// </summary>
        /// <param name="type">用于初始化实例的类型信息。</param>
        /// <param name="index">当前值在所属序列中的索引。</param>
        /// <param name="shape">描述模型结构的类型信息。</param>
        /// <param name="elementType">集合元素的类型信息。</param>
        /// <param name="keyType">键的类型信息。</param>
        /// <param name="valueType">值的类型信息。</param>
        /// <param name="key">对象在容器内的键。</param>
        /// <param name="modelId">用于区分对象图模型定义的标识。</param>
        internal GraphModelInfo(ITypeSymbol type, int index, GraphModelShape shape, ITypeSymbol elementType,
            ITypeSymbol keyType, ITypeSymbol valueType, KeySpec key, string modelId)
        {
            Type = type;
            Index = index;
            Shape = shape;
            ElementType = elementType;
            KeyType = keyType;
            ValueType = valueType;
            Key = key;
            ModelId = modelId;
            Members = new List<GraphMemberInfo>();
        }

        /// <summary>
        /// 获取当前模型的 Roslyn 类型符号。
        /// </summary>
        internal ITypeSymbol Type { get; }
        /// <summary>该模型是否由另一个实体的生成文件提供。</summary>
        internal bool IsExternal { get; set; }
        /// <summary>
        /// 获取生成模型在本轮分析中的序号。
        /// </summary>
        internal int Index { get; }
        /// <summary>
        /// 获取对象图类型的结构形态。
        /// </summary>
        internal GraphModelShape Shape { get; }
        /// <summary>
        /// 获取集合元素的类型。
        /// </summary>
        internal ITypeSymbol ElementType { get; }
        /// <summary>
        /// 获取字典键的类型。
        /// </summary>
        internal ITypeSymbol KeyType { get; }
        /// <summary>
        /// 获取字典值的类型。
        /// </summary>
        internal ITypeSymbol ValueType { get; }
        /// <summary>
        /// 获取当前模型的稳定身份键定义。
        /// </summary>
        internal KeySpec Key { get; }
        /// <summary>
        /// 获取对象图模型的稳定标识。
        /// </summary>
        internal string ModelId { get; }
        /// <summary>
        /// 获取模型包含的成员描述。
        /// </summary>
        internal List<GraphMemberInfo> Members { get; }
        /// <summary>
        /// 获取或设置集合元素使用的对象图模型。
        /// </summary>
        internal GraphModelInfo ElementModel { get; set; }
        /// <summary>
        /// 获取或设置字典值使用的对象图模型。
        /// </summary>
        internal GraphModelInfo ValueModel { get; set; }
        /// <summary>
        /// 获取或设置集合元素的稳定身份键定义。
        /// </summary>
        internal KeySpec ElementKey { get; set; }
    }

    /// <summary>
    /// 标识变更图中类型模型的结构形态。
    /// </summary>
    private enum GraphModelShape
    {
        /// <summary>
        /// 按公开属性展开的对象模型。
        /// </summary>
        Object,
        /// <summary>
        /// 由自定义适配器负责处理的模型。
        /// </summary>
        Adapter,
        /// <summary>
        /// 包装可空值类型的模型。
        /// </summary>
        Nullable,
        /// <summary>
        /// 元素为对象模型的有序集合。
        /// </summary>
        Sequence,
        /// <summary>
        /// 元素为标量值的有序集合。
        /// </summary>
        ScalarSequence,
        /// <summary>
        /// 值为对象模型的键值映射。
        /// </summary>
        Dictionary,
        /// <summary>
        /// 值为标量值的键值映射。
        /// </summary>
        ScalarDictionary,
        /// <summary>
        /// 按元素键识别对象模型的集合。
        /// </summary>
        Set,
        /// <summary>
        /// 元素为标量值的集合。
        /// </summary>
        ScalarSet
    }

    /// <summary>
    /// 对象图生成器分析得到的属性模型。
    /// </summary>
    private sealed class GraphMemberInfo
    {
        /// <summary>
        /// 初始化一个 <see cref="GraphMemberInfo" /> 类型的实例。
        /// </summary>
        /// <param name="property">对应的实体属性信息。</param>
        /// <param name="kind">当前对象或操作的种类。</param>
        /// <param name="childModel">子节点对应的对象图模型。</param>
        /// <param name="elementKey">集合元素的键定义。</param>
        internal GraphMemberInfo(IPropertySymbol property, GraphMemberKind kind, GraphModelInfo childModel, KeySpec elementKey)
        {
            Property = property;
            Kind = kind;
            ChildModel = childModel;
            ElementKey = elementKey;
        }

        /// <summary>
        /// 获取需要生成访问代码的 Roslyn 属性符号。
        /// </summary>
        internal IPropertySymbol Property { get; }
        /// <summary>
        /// 获取属性使用的对象图跟踪方式。
        /// </summary>
        internal GraphMemberKind Kind { get; }
        /// <summary>
        /// 获取成员值使用的子对象图模型。
        /// </summary>
        internal GraphModelInfo ChildModel { get; }
        /// <summary>
        /// 获取集合元素的稳定身份键定义。
        /// </summary>
        internal KeySpec ElementKey { get; }
    }

    /// <summary>
    /// 对象图稳定身份键的成员定义。
    /// </summary>
    private sealed class KeySpec
    {
        /// <summary>
        /// 初始化一个 <see cref="KeySpec" /> 类型的实例。
        /// </summary>
        /// <param name="members">当前类型中参与键生成的成员规格集合。</param>
        /// <param name="isExplicit">指示成员是否通过显式声明纳入生成结果。</param>
        internal KeySpec(List<KeyMemberSpec> members, bool isExplicit)
        {
            Members = members;
            IsExplicit = isExplicit;
        }

        /// <summary>
        /// 获取模型包含的成员描述。
        /// </summary>
        internal List<KeyMemberSpec> Members { get; }
        /// <summary>
        /// 获取身份键是否由显式配置指定。
        /// </summary>
        internal bool IsExplicit { get; }
        /// <summary>
        /// 获取或设置身份键是否来自 <c>IEntity.GetKeys()</c> 复合契约。
        /// </summary>
        internal bool UsesGetKeys { get; set; }
    }

    /// <summary>
    /// 对象图身份键的属性访问定义。
    /// </summary>
    private sealed class KeyMemberSpec
    {
        /// <summary>
        /// 初始化一个 <see cref="KeyMemberSpec" /> 类型的实例。
        /// </summary>
        /// <param name="property">对应的实体属性信息。</param>
        /// <param name="interfaceType">当前实现对应的接口类型。</param>
        /// <param name="memberName">当前成员的名称。</param>
        internal KeyMemberSpec(IPropertySymbol property, INamedTypeSymbol interfaceType, string memberName)
        {
            Property = property;
            InterfaceType = interfaceType;
            MemberName = memberName;
        }

        /// <summary>
        /// 获取组成身份键的 Roslyn 属性符号。
        /// </summary>
        internal IPropertySymbol Property { get; }
        /// <summary>
        /// 获取通过接口读取键属性时使用的接口类型。
        /// </summary>
        internal INamedTypeSymbol InterfaceType { get; }
        /// <summary>
        /// 获取CLR 成员名称。
        /// </summary>
        internal string MemberName { get; }

        /// <summary>
        /// 构造直接属性或接口属性的访问表达式。
        /// </summary>
        /// <param name="valueName">生成属性访问所使用的对象变量名称。</param>
        /// <returns>以指定变量名及必要接口转换构造的属性访问源码。</returns>
        internal string BuildAccess(string valueName)
        {
            var target = InterfaceType == null ? valueName : "((" + TypeName(InterfaceType) + ")" + valueName + ")";
            return target + "." + EscapeIdentifier(MemberName);
        }
    }

    /// <summary>
    /// 标识变更图中属性成员的值形态。
    /// </summary>
    private enum GraphMemberKind
    {
        /// <summary>
        /// 属性值为标量。
        /// </summary>
        Scalar,
        /// <summary>
        /// 属性值展开为对象模型。
        /// </summary>
        Object,
        /// <summary>
        /// 属性值为标量元素序列。
        /// </summary>
        SequenceScalar,
        /// <summary>
        /// 属性值为对象元素序列。
        /// </summary>
        SequenceObject,
        /// <summary>
        /// 属性值为标量值字典。
        /// </summary>
        DictionaryScalar,
        /// <summary>
        /// 属性值为对象值字典。
        /// </summary>
        DictionaryObject,
        /// <summary>
        /// 属性值为标量元素集合。
        /// </summary>
        SetScalar,
        /// <summary>
        /// 属性值为按键识别对象元素的集合。
        /// </summary>
        SetObject,
        /// <summary>
        /// 属性类型不受当前变更图建模支持。
        /// </summary>
        Unsupported,
        /// <summary>
        /// 属性类型由已登记的自定义适配器处理。
        /// </summary>
        Adapted
    }

}
