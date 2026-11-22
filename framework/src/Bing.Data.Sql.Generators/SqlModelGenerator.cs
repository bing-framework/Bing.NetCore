using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Bing.Data.Sql.Generators;

/// <summary>
/// 根据显式 SQL 模型上下文生成元数据和静态属性访问器。
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SqlModelGenerator : IIncrementalGenerator
{
    private const string MetadataPrefix = "global::Bing.Data.Sql.Metadata.";
    private const string ProviderType = "global::Bing.Data.Sql.Metadata.GeneratedEntityModelMetadataProvider";
    private const string ModelAttribute = "Bing.Data.Sql.Generated.SqlModelAttribute";
    private const string ResultAttribute = "Bing.Data.Sql.Generated.SqlResultAttribute";
    private const string ParametersAttribute = "Bing.Data.Sql.Generated.SqlParametersAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions &
                                 ~SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly DiagnosticDescriptor InvalidContext = new(
        "BINGSQLM001", "SQL 模型上下文无效",
        "SQL 模型上下文 '{0}' 必须是顶层、非泛型的 partial class",
        "Bing.Data.Sql.Generation", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InaccessibleModel = new(
        "BINGSQLM002", "SQL 模型类型不可访问",
        "SQL 模型类型 '{0}' 无法从生成代码访问",
        "Bing.Data.Sql.Generation", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor GeneratedNameConflict = new(
        "BINGSQLM003", "SQL 模型生成成员冲突",
        "SQL 模型上下文 '{0}' 已声明生成成员 '{1}'",
        "Bing.Data.Sql.Generation", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor PropertyNameConflict = new(
        "BINGSQLM004", "SQL 模型属性名冲突",
        "SQL 模型类型 '{0}' 的属性 '{1}' 与 '{2}' 忽略大小写后重名",
        "Bing.Data.Sql.Generation", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor RuntimeContractMismatch = new(
        "BINGSQLM005", "SQL 模型生成器运行库版本不匹配",
        "SQL 模型上下文 '{0}' 需要支持生成访问器和工厂的 Bing.Data.Sql 运行库；请升级运行库或对齐生成器与运行库版本",
        "Bing.Data.Sql.Generation", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidResultMaterializer = new(
        "BINGSQLM006", "SQL 结果模型无法物化",
        "SQL 结果类型 '{0}' 无法由生成器安全物化：{1}。请使用具体类型、公共无参构造器及可写属性",
        "Bing.Data.Sql.Generation", DiagnosticSeverity.Error, true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var contexts = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                static (syntaxContext, _) => FindContext(syntaxContext))
            .Where(static candidate => candidate != null)
            .Select(static (candidate, _) => candidate!);

        var semanticInputs = contexts.Combine(context.CompilationProvider)
            .Select(static (input, _) => Project(input.Left, input.Right))
            .WithComparer(SemanticContextInputComparer.Instance)
            .WithTrackingName("SqlModelSemanticInput");

        var generated = semanticInputs
            .Select(static (input, _) => Generate(input))
            .WithTrackingName("SqlModelGenerate");

        context.RegisterSourceOutput(generated, static (output, result) =>
        {
            foreach (var diagnostic in result.Diagnostics)
                output.ReportDiagnostic(diagnostic);

            if (result.Source != null)
                output.AddSource(result.HintName!, SourceText.From(result.Source, Encoding.UTF8));
        });
    }

    private static ContextCandidate? FindContext(GeneratorSyntaxContext context)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
            return null;

        var modelAttributes = symbol.GetAttributes()
            .Where(IsModelAttribute)
            .Select(attribute => attribute.ApplicationSyntaxReference?.GetSyntax())
            .Where(syntax => syntax != null)
            .OrderBy(syntax => syntax!.SyntaxTree.FilePath, StringComparer.Ordinal)
            .ThenBy(syntax => syntax!.SpanStart)
            .ToArray();
        if (modelAttributes.Length == 0 || modelAttributes[0] is not AttributeSyntax firstAttribute ||
            firstAttribute.SyntaxTree != declaration.SyntaxTree || !declaration.Span.Contains(firstAttribute.Span))
            return null;

        return new ContextCandidate(symbol, firstAttribute.GetLocation());
    }

    private static bool IsModelAttribute(AttributeData attribute)
    {
        var name = attribute.AttributeClass?.ToDisplayString();
        return name == ModelAttribute || name == ResultAttribute || name == ParametersAttribute;
    }

    private static SemanticContextInput Project(ContextCandidate candidate, Compilation compilation)
    {
        var diagnostics = ImmutableArray.CreateBuilder<SemanticDiagnostic>();
        var context = candidate.Symbol;
        var contextInput = ProjectContext(context);
        if (!IsValidContext(context))
        {
            diagnostics.Add(SemanticDiagnostic.Create(InvalidContext, candidate.Location, context.Name));
            return new SemanticContextInput(contextInput, ImmutableArray<ModelProjection>.Empty,
                diagnostics.ToImmutable(), false, false, false);
        }

        var registrations = ReadModels(context, diagnostics, candidate.Location);
        if (registrations.Count == 0)
            return new SemanticContextInput(contextInput, ImmutableArray<ModelProjection>.Empty,
                diagnostics.ToImmutable(), false, false, false);

        var hasRuntimeContracts = HasRuntimeContracts(compilation);
        var hasGeneratedValueReaderContract = HasGeneratedValueReaderContract(compilation);
        var hasGeneratedMutationSqlContract = compilation.GetTypeByMetadataName(
            "Bing.Data.Sql.Metadata.EntityModelMetadata")?.GetMembers("GeneratedMutationSql")
            .OfType<IPropertySymbol>().Any(property => property.DeclaredAccessibility == Accessibility.Public &&
                property.SetMethod?.DeclaredAccessibility == Accessibility.Public) == true;
        var hasGeneratedKeyInsertSqlContract = compilation.GetTypeByMetadataName(
            "Bing.Data.Sql.Metadata.EntityModelMetadata")?.GetMembers("GeneratedKeyInsertSql")
            .OfType<IPropertySymbol>().Any(property => property.DeclaredAccessibility == Accessibility.Public &&
                property.SetMethod?.DeclaredAccessibility == Accessibility.Public) == true;
        var hasGeneratedMutationShapeSqlContract = compilation.GetTypeByMetadataName(
            "Bing.Data.Sql.Metadata.EntityModelMetadata")?.GetMembers("GeneratedMutationShapeSql")
            .OfType<IPropertySymbol>().Any(property => property.DeclaredAccessibility == Accessibility.Public &&
                property.SetMethod?.DeclaredAccessibility == Accessibility.Public) == true;
        if (!hasRuntimeContracts)
        {
            diagnostics.Add(SemanticDiagnostic.Create(RuntimeContractMismatch, candidate.Location, context.Name));
        }

        var invalid = false;
        var models = ImmutableArray.CreateBuilder<ModelProjection>();
        foreach (var registration in registrations)
        {
            if (!IsAccessibleModel(registration.Symbol))
            {
                diagnostics.Add(SemanticDiagnostic.Create(InaccessibleModel, registration.Location,
                    TypeName(registration.Symbol)));
                invalid = true;
                continue;
            }

            var properties = GetProperties(registration.Symbol)
                .Select(property => ProjectProperty(registration.Symbol, property, registration.Roles))
                .ToImmutableArray();
            var collision = properties.GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(propertiesWithName => propertiesWithName.Count() > 1);
            if (collision != null)
            {
                var conflicting = collision.Take(2).ToArray();
                var symbolProperty = GetProperties(registration.Symbol)
                    .First(property => property.Name == conflicting[1].Name);
                diagnostics.Add(SemanticDiagnostic.Create(PropertyNameConflict,
                    symbolProperty.Locations.FirstOrDefault() ?? registration.Location,
                    TypeName(registration.Symbol), conflicting[0].Name, conflicting[1].Name));
                invalid = true;
            }

            var hasResultRole = (registration.Roles & ModelRoles.SqlResult) != 0;
            var needsFactory = (registration.Roles & (ModelRoles.SqlModel | ModelRoles.SqlResult)) != 0;
            var factoryAvailable = needsFactory && CanGenerateFactory(registration.Symbol);
            var materializationFailure = hasResultRole
                ? ResultMaterializationFailure(registration.Symbol, properties)
                : string.Empty;
            if (materializationFailure.Length != 0)
            {
                diagnostics.Add(SemanticDiagnostic.Create(InvalidResultMaterializer,
                    registration.Location, TypeName(registration.Symbol), materializationFailure));
                invalid = true;
            }

            models.Add(ProjectModel(registration.Symbol, registration.Roles, properties, factoryAvailable));
        }

        foreach (var memberName in new[] { "CreateModels", "Register" })
        {
            if (context.GetMembers(memberName).Length == 0)
                continue;
            diagnostics.Add(SemanticDiagnostic.Create(GeneratedNameConflict,
                context.Locations.FirstOrDefault() ?? candidate.Location, context.Name, memberName));
            invalid = true;
        }

        return new SemanticContextInput(contextInput, models.ToImmutable(), diagnostics.ToImmutable(),
            !invalid && hasRuntimeContracts, hasRuntimeContracts, hasGeneratedValueReaderContract,
            hasGeneratedMutationSqlContract, hasGeneratedKeyInsertSqlContract, hasGeneratedMutationShapeSqlContract);
    }

    private static ContextOutput Generate(SemanticContextInput input)
    {
        var diagnostics = input.Diagnostics.Select(diagnostic => diagnostic.ToDiagnostic()).ToImmutableArray();
        if (!input.IsValid || input.Context == null)
            return new ContextOutput(null, null, diagnostics);

        var identity = input.Context.Identity;
        return new ContextOutput(HintName(identity),
            Generate(input.Context, input.Models, input.HasGeneratedValueReaderContract,
                input.HasGeneratedMutationSqlContract, input.HasGeneratedKeyInsertSqlContract,
                input.HasGeneratedMutationShapeSqlContract), diagnostics);
    }

    private static bool HasRuntimeContracts(Compilation compilation)
    {
        var propertyMetadata = compilation.GetTypeByMetadataName("Bing.Data.Sql.Metadata.EntityPropertyMetadata");
        var modelMetadata = compilation.GetTypeByMetadataName("Bing.Data.Sql.Metadata.EntityModelMetadata");
        var objectType = compilation.GetSpecialType(SpecialType.System_Object);
        var stringType = compilation.GetSpecialType(SpecialType.System_String);
        var boolType = compilation.GetSpecialType(SpecialType.System_Boolean);
        var intType = compilation.GetSpecialType(SpecialType.System_Int32);
        var typeType = compilation.GetTypeByMetadataName("System.Type");
        var funcType = compilation.GetTypeByMetadataName("System.Func`1")?.Construct(objectType);
        var getterType = compilation.GetTypeByMetadataName("System.Func`2")?.Construct(objectType, objectType);
        var setterType = compilation.GetTypeByMetadataName("System.Action`2")?.Construct(objectType, objectType);
        var databaseGeneratedOptionType = compilation.GetTypeByMetadataName(
            "System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption");
        var nullableIntType = compilation.GetTypeByMetadataName("System.Nullable`1")?.Construct(intType);
        var propertyEnumerableType = propertyMetadata == null
            ? null
            : compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1")?.Construct(propertyMetadata);
        if (propertyMetadata == null || modelMetadata == null || typeType == null || funcType == null ||
            getterType == null || setterType == null || databaseGeneratedOptionType == null || nullableIntType == null ||
            propertyEnumerableType == null)
            return false;

        var generatedAccessorParameters = new ITypeSymbol[]
        {
            stringType, typeType, boolType, boolType, stringType, boolType, boolType,
            databaseGeneratedOptionType, boolType, boolType, nullableIntType, stringType, getterType, setterType,
            boolType
        };
        var hasGeneratedAccessors = propertyMetadata.GetMembers("CreateGenerated")
            .OfType<IMethodSymbol>()
            .Any(method => method.IsStatic && method.DeclaredAccessibility == Accessibility.Public &&
                           SymbolEqualityComparer.Default.Equals(method.ReturnType, propertyMetadata) &&
                           ParametersMatch(method, generatedAccessorParameters));
        var hasGeneratedFactory = modelMetadata.GetMembers("GeneratedFactory")
            .OfType<IPropertySymbol>()
            .Any(property => property.DeclaredAccessibility == Accessibility.Public &&
                             property.GetMethod?.DeclaredAccessibility == Accessibility.Public &&
                             SymbolEqualityComparer.Default.Equals(property.Type, funcType));
        var generatedFactoryConstructorParameters = new ITypeSymbol[]
        {
            typeType, stringType, stringType, propertyEnumerableType, funcType
        };
        var hasFactoryConstructor = modelMetadata.InstanceConstructors
            .Any(constructor => constructor.DeclaredAccessibility == Accessibility.Public &&
                                constructor.Parameters.Length == generatedFactoryConstructorParameters.Length &&
                                constructor.Parameters[4].Name == "generatedFactory" &&
                                ParametersMatch(constructor, generatedFactoryConstructorParameters));

        var readerType = compilation.GetTypeByMetadataName("System.Data.Common.DbDataReader");
        var readerSetterType = readerType == null ? null : compilation.GetTypeByMetadataName("System.Func`4")?
            .Construct(objectType, readerType, intType, boolType);
        var hasReaderSetter = readerSetterType != null && propertyMetadata.GetMembers("WithGeneratedReader")
            .OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.DeclaredAccessibility == Accessibility.Public &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, propertyMetadata) &&
                ParametersMatch(method, new[] { readerSetterType }));
        return hasGeneratedAccessors && hasGeneratedFactory && hasFactoryConstructor && hasReaderSetter;
    }

    private static bool HasGeneratedValueReaderContract(Compilation compilation)
    {
        var modelMetadata = compilation.GetTypeByMetadataName("Bing.Data.Sql.Metadata.EntityModelMetadata");
        var objectType = compilation.GetSpecialType(SpecialType.System_Object);
        var stringType = compilation.GetSpecialType(SpecialType.System_String);
        var typeType = compilation.GetTypeByMetadataName("System.Type");
        var funcType = compilation.GetTypeByMetadataName("System.Func`1")?.Construct(objectType);
        var valueReaderType = compilation.GetTypeByMetadataName("System.Func`3")?.Construct(objectType, stringType, objectType);
        var propertyMetadata = compilation.GetTypeByMetadataName("Bing.Data.Sql.Metadata.EntityPropertyMetadata");
        var propertyEnumerableType = propertyMetadata == null
            ? null
            : compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1")?.Construct(propertyMetadata);
        if (modelMetadata == null || typeType == null || funcType == null || valueReaderType == null ||
            propertyEnumerableType == null)
            return false;

        var parameters = new ITypeSymbol[]
        {
            typeType, stringType, stringType, propertyEnumerableType, funcType, valueReaderType
        };
        return modelMetadata.InstanceConstructors.Any(constructor =>
            constructor.DeclaredAccessibility == Accessibility.Public &&
            constructor.Parameters.Length == parameters.Length &&
            constructor.Parameters[4].Name == "generatedFactory" &&
            constructor.Parameters[5].Name == "generatedValueReader" &&
            ParametersMatch(constructor, parameters));
    }

    private static bool ParametersMatch(IMethodSymbol method, IReadOnlyList<ITypeSymbol> expectedTypes)
    {
        if (method.Parameters.Length != expectedTypes.Count)
            return false;

        for (var index = 0; index < expectedTypes.Count; index++)
            if (!SymbolEqualityComparer.Default.Equals(method.Parameters[index].Type, expectedTypes[index]))
                return false;

        return true;
    }

    private static bool IsValidContext(INamedTypeSymbol context)
    {
        if (context.ContainingType != null || context.TypeParameters.Length != 0 ||
            context.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
            return false;

        return context.DeclaringSyntaxReferences.All(reference =>
            reference.GetSyntax() is ClassDeclarationSyntax declaration &&
            declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    private static List<RegisteredModel> ReadModels(INamedTypeSymbol context,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics, Location contextLocation)
    {
        var result = new Dictionary<string, RegisteredModel>(StringComparer.Ordinal);
        foreach (var attribute in context.GetAttributes().Where(IsModelAttribute))
        {
            if (attribute.ConstructorArguments.Length != 1 ||
                attribute.ConstructorArguments[0].Value is not INamedTypeSymbol model)
            {
                diagnostics.Add(SemanticDiagnostic.Create(InaccessibleModel,
                    AttributeLocation(attribute, contextLocation), "<未知类型>"));
                continue;
            }

            var name = TypeName(model);
            var role = RoleOf(attribute);
            if (result.TryGetValue(name, out var existing))
                existing.Roles |= role;
            else
                result.Add(name, new RegisteredModel(model, AttributeLocation(attribute, contextLocation), role));
        }

        return result.Values.OrderBy(entry => TypeName(entry.Symbol), StringComparer.Ordinal).ToList();
    }

    private static ModelRoles RoleOf(AttributeData attribute) => attribute.AttributeClass?.ToDisplayString() switch
    {
        ModelAttribute => ModelRoles.SqlModel,
        ResultAttribute => ModelRoles.SqlResult,
        ParametersAttribute => ModelRoles.SqlParameters,
        _ => ModelRoles.None
    };

    private static ContextProjection ProjectContext(INamedTypeSymbol context) => new(
        TypeName(context), NamespaceName(context.ContainingNamespace), context.Name,
        AccessibilityText(context.DeclaredAccessibility), context.IsStatic, context.IsAbstract, context.IsSealed);

    private static ModelProjection ProjectModel(INamedTypeSymbol model, ModelRoles roles,
        ImmutableArray<ModelPropertyProjection> properties, bool factoryAvailable)
    {
        var table = FindAttribute(model, "System.ComponentModel.DataAnnotations.Schema.TableAttribute");
        return new ModelProjection(TypeName(model), ConstructorString(table) ?? model.Name,
            NamedString(table, "Schema") ?? string.Empty, roles, factoryAvailable, properties);
    }

    private static ModelPropertyProjection ProjectProperty(INamedTypeSymbol model, IPropertySymbol property,
        ModelRoles roles)
    {
        var column = FindAttribute(property, "System.ComponentModel.DataAnnotations.Schema.ColumnAttribute");
        var columnName = ConstructorString(column) ?? NamedString(column, "Name") ?? property.Name;
        var providerTypeName = NamedString(column, "TypeName");
        var ignored = FindAttribute(property, "System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute") != null;
        var key = FindAttribute(property, "System.ComponentModel.DataAnnotations.KeyAttribute") != null ||
                  string.Equals(property.Name, "Id", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(property.Name, model.Name + "Id", StringComparison.OrdinalIgnoreCase);
        var timestamp = FindAttribute(property, "System.ComponentModel.DataAnnotations.TimestampAttribute") != null;
        var generated = timestamp ? 2 : GeneratedOption(property);
        var concurrency = timestamp || FindAttribute(property,
            "System.ComponentModel.DataAnnotations.ConcurrencyCheckAttribute") != null;
        var required = FindAttribute(property, "System.ComponentModel.DataAnnotations.RequiredAttribute") != null;
        var length = MaxLength(property);
        var getter = property.GetMethod;
        var setter = property.SetMethod;
        var canRead = getter != null && getter.DeclaredAccessibility == Accessibility.Public && !getter.IsStatic &&
                      CanBox(property.Type) && !property.ReturnsByRef && !property.ReturnsByRefReadonly;
        var canWrite = setter != null && setter.DeclaredAccessibility == Accessibility.Public && !setter.IsStatic &&
                       CanBox(property.Type);
        var hasNonPublicSetter = setter != null && setter.DeclaredAccessibility != Accessibility.Public;
        var emitsGetter = (roles & (ModelRoles.SqlModel | ModelRoles.SqlParameters)) != 0 && canRead;
        var emitsSetter = (roles & (ModelRoles.SqlModel | ModelRoles.SqlResult)) != 0 && canWrite &&
                          setter is { IsInitOnly: false } && !ignored;
        return new ModelPropertyProjection(property.Name, TypeName(property.Type), ReaderStorageTypeName(property.Type), columnName,
            providerTypeName, ignored, key, generated, concurrency, required, length, canRead, canWrite,
            hasNonPublicSetter, emitsGetter, emitsSetter, (roles & ModelRoles.SqlResult) != 0 && !ignored &&
            setter is { IsInitOnly: true, DeclaredAccessibility: Accessibility.Public });
    }

    private static string? ReaderStorageTypeName(ITypeSymbol propertyType)
    {
        var valueType = propertyType is INamedTypeSymbol nullable &&
            nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? nullable.TypeArguments[0]
            : propertyType;
        if (valueType.SpecialType == SpecialType.System_Char)
            return "global::System.Char";
        return valueType is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType
            ? TypeName(enumType.EnumUnderlyingType!)
            : null;
    }

    private static string ResultMaterializationFailure(INamedTypeSymbol model,
        ImmutableArray<ModelPropertyProjection> properties)
    {
        var reasons = new List<string>();
        if (model.IsAbstract)
            reasons.Add("类型是抽象类型");
        if (!model.InstanceConstructors.Any(constructor => !constructor.IsStatic &&
                constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility == Accessibility.Public))
            reasons.Add("缺少公共无参构造器");

        var requiredMembers = new SortedSet<string>(StringComparer.Ordinal);
        for (var current = model; current != null; current = current.BaseType)
            foreach (var member in current.GetMembers().Where(member => member switch
                     {
                         IPropertySymbol property => property.IsRequired,
                         IFieldSymbol field => field.IsRequired,
                         _ => false
                     }))
                requiredMembers.Add(member.Name);
        if (requiredMembers.Count != 0)
            reasons.Add("包含必需成员 " + string.Join(", ", requiredMembers));

        var initOnly = properties.Where(property => property.IsInitOnly && !property.IsIgnored)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (initOnly.Length != 0)
            reasons.Add("init-only 属性无法由生成的结果写入器赋值 " + string.Join(", ", initOnly));

        var nonPublicSetters = properties.Where(property => property.HasNonPublicSetter && !property.IsIgnored)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (nonPublicSetters.Length != 0)
            reasons.Add("非公开 setter 属性无法由生成的结果写入器赋值 " + string.Join(", ", nonPublicSetters));

        return string.Join("；", reasons);
    }

    private static Location AttributeLocation(AttributeData attribute, Location fallback) =>
        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? fallback;

    private static bool IsAccessibleModel(INamedTypeSymbol model)
    {
        if (model.TypeKind != TypeKind.Class || model.IsAnonymousType || ContainsTypeParameter(model))
            return false;

        for (var current = model; current != null; current = current.ContainingType)
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal) ||
                current.DeclaringSyntaxReferences.Any(reference =>
                    reference.GetSyntax() is TypeDeclarationSyntax declaration &&
                    declaration.Modifiers.Any(SyntaxKind.FileKeyword)))
                return false;

        return true;
    }

    private static bool ContainsTypeParameter(INamedTypeSymbol type)
    {
        if (type.IsUnboundGenericType || type.TypeArguments.Any(ContainsTypeParameter))
            return true;
        return type.ContainingType != null && ContainsTypeParameter(type.ContainingType);
    }

    private static bool ContainsTypeParameter(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
        IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
        INamedTypeSymbol named => ContainsTypeParameter(named),
        _ => false
    };

    private static string Generate(ContextProjection context, ImmutableArray<ModelProjection> models,
        bool hasGeneratedValueReaderContract, bool hasGeneratedMutationSqlContract, bool hasGeneratedKeyInsertSqlContract,
        bool hasGeneratedMutationShapeSqlContract)
    {
        var code = new StringBuilder("// <auto-generated/>\n#nullable disable\n");
        var namespaceName = context.NamespaceName;
        if (namespaceName.Length != 0)
            code.Append("namespace ").Append(namespaceName).AppendLine(" {");

        code.Append(context.Accessibility).Append(' ');
        if (context.IsStatic)
            code.Append("static ");
        else
        {
            if (context.IsAbstract)
                code.Append("abstract ");
            if (context.IsSealed)
                code.Append("sealed ");
        }

        code.Append("partial class ").Append(EscapeIdentifier(context.Name)).AppendLine(" {");
        code.Append("public static ").Append(MetadataPrefix).AppendLine("EntityModelMetadata[] CreateModels() {");
        code.Append("return new ").Append(MetadataPrefix).AppendLine("EntityModelMetadata[] {");
        foreach (var model in models)
            AppendModel(code, model, hasGeneratedValueReaderContract, hasGeneratedMutationSqlContract,
                hasGeneratedKeyInsertSqlContract, hasGeneratedMutationShapeSqlContract);
        code.AppendLine("};");
        code.AppendLine("}");
        code.AppendLine("public static void Register() {");
        code.AppendLine("foreach (var model in CreateModels()) " + ProviderType + ".Register(model);");
        code.AppendLine("}");
        code.AppendLine("}");

        if (namespaceName.Length != 0)
            code.AppendLine("}");
        return code.ToString();
    }

    private static void AppendModel(StringBuilder code, ModelProjection model, bool hasGeneratedValueReaderContract,
        bool hasGeneratedMutationSqlContract, bool hasGeneratedKeyInsertSqlContract, bool hasGeneratedMutationShapeSqlContract)
    {
        code.Append("new ").Append(MetadataPrefix).Append("EntityModelMetadata(typeof(")
            .Append(model.TypeName).Append("), ").Append(Literal(model.TableName)).Append(", ")
            .Append(Literal(model.Schema)).Append(", new ").Append(MetadataPrefix)
            .AppendLine("EntityPropertyMetadata[] {");

        foreach (var property in model.Properties)
            AppendProperty(code, model, property);

        code.Append("}, generatedFactory: ");
        var needsFactory = (model.Roles & (ModelRoles.SqlModel | ModelRoles.SqlResult)) != 0;
        if (needsFactory && model.FactoryAvailable)
            code.Append("static () => new ").Append(model.TypeName).Append("()");
        else
            code.Append("null");

        var generatedProperties = model.Properties.Where(property => property.EmitGetter).ToArray();
        if (hasGeneratedValueReaderContract)
        {
            if (generatedProperties.Length == 0)
                code.Append(", generatedValueReader: null");
            else
            {
                code.Append(", generatedValueReader: static (instance, propertyName) => propertyName switch { ");
                foreach (var property in generatedProperties)
                {
                    code.Append(Literal(property.Name)).Append(" => ((").Append(model.TypeName).Append(")instance).")
                        .Append(EscapeIdentifier(property.Name)).Append(", ");
                }
                code.Append("_ => null }");
            }
        }
        code.Append(')');
        if (hasGeneratedMutationSqlContract || hasGeneratedKeyInsertSqlContract || hasGeneratedMutationShapeSqlContract)
            AppendMutationSql(code, model, hasGeneratedMutationSqlContract, hasGeneratedKeyInsertSqlContract,
                hasGeneratedMutationShapeSqlContract);
        code.AppendLine(",");
    }

    // 默认形状和有限增量形状均为完整 SQL 常量；形状数量随列数线性增长，不枚举列子集。
    private static void AppendMutationSql(StringBuilder code, ModelProjection model, bool mutationSql, bool keyInsertSql, bool shapeSql)
    {
        var properties = model.Properties.Where(property => !property.IsIgnored).ToArray();
        if ((model.Roles & ModelRoles.SqlModel) == 0 || !string.IsNullOrEmpty(model.Schema) ||
            !IsStaticIdentifier(model.TableName) || properties.Any(property => !IsStaticIdentifier(property.ColumnName)))
            return;
        var insert = properties.Where(property => property.GeneratedOption == 0).ToArray();
        var update = insert.Where(property => !property.IsKey).ToArray();
        var keys = properties.Where(property => property.IsKey).ToArray();
        var conditions = keys.Concat(properties.Where(property => property.IsConcurrencyToken)).ToArray();
        keyInsertSql &= keys.Length == 1 && keys[0].GeneratedOption == 1;
        if (!mutationSql && !keyInsertSql && !shapeSql)
            return;
        var statements = new List<(string Provider, string? Insert, string? Update, string? Delete, string? KeyInsert)>();
        foreach (var dialect in new[]
        {
            (Key: "bing.sqlite", Open: "`", Close: "`", Prefix: "@_p_"),
            (Key: "bing.mysql", Open: "`", Close: "`", Prefix: "@_p_"),
            (Key: "bing.postgresql", Open: "\"", Close: "\"", Prefix: "@_p_"),
            (Key: "bing.sqlserver", Open: "[", Close: "]", Prefix: "@_p_"),
            (Key: "bing.oracle", Open: "\"", Close: "\"", Prefix: ":p_")
        })
        {
            var table = dialect.Open + model.TableName + dialect.Close;
            string Column(ModelPropertyProjection property) => dialect.Open + property.ColumnName + dialect.Close;
            string Parameter(int index) => dialect.Prefix + index.ToString(CultureInfo.InvariantCulture);
            string Where(int offset) => " Where " + string.Join(" And ", conditions.Select((property, index) =>
                Column(property) + " = " + Parameter(index + offset)));
            var target = "Insert Into " + table;
            var columns = " (" + string.Join(", ", insert.Select(Column)) + ")";
            var values = " Values (" + string.Join(", ", insert.Select((_, index) => Parameter(index))) + ")";
            var insertSql = insert.Length == 0 ? null : target + columns + values;
            string? keySql = null;
            if (keyInsertSql)
            {
                var key = Column(keys[0]);
                keySql = dialect.Key switch
                {
                    "bing.mysql" => insertSql ?? target + " () VALUES ()",
                    "bing.oracle" => (insertSql ?? target + " (" + key + ") Values (DEFAULT)") +
                        " Returning " + key + " Into :bing_identity_key",
                    "bing.sqlserver" => insertSql == null
                        ? target + " OUTPUT INSERTED." + key + " DEFAULT VALUES"
                        : target + columns + " Output [Inserted]." + key + values,
                    _ => insertSql == null ? target + " DEFAULT VALUES RETURNING " + key
                        : insertSql + " Returning " + key
                };
            }
            statements.Add((dialect.Key, insertSql, keys.Length > 0 && update.Length > 0
                    ? "Update " + table + " Set " + string.Join(", ", update.Select((property, index) =>
                        Column(property) + " = " + Parameter(index))) + Where(update.Length) : null,
                keys.Length > 0 ? "Delete From " + table + Where(0) : null, keySql));
        }
        code.Append(" { ");
        void Add(string provider, string operation, string? sql)
        {
            if (sql != null)
                code.Append('(').Append(Literal(provider)).Append(", global::Bing.Data.Sql.Builders.SqlOperationKind.")
                    .Append(operation).Append(") => ").Append(Literal(sql)).Append(", ");
        }
        if (mutationSql)
        {
            code.Append("GeneratedMutationSql = static (provider, operation) => (provider, operation) switch { ");
            foreach (var statement in statements)
            {
                Add(statement.Provider, "InsertValues", statement.Insert);
                Add(statement.Provider, "Update", statement.Update);
                Add(statement.Provider, "Delete", statement.Delete);
            }
            code.Append("_ => null }, ");
        }
        if (keyInsertSql)
        {
            code.Append("GeneratedKeyInsertSql = static provider => provider switch { ");
            foreach (var statement in statements)
                if (statement.KeyInsert != null)
                    code.Append(Literal(statement.Provider)).Append(" => ").Append(Literal(statement.KeyInsert)).Append(", ");
            code.Append("_ => null }, ");
        }
        if (shapeSql)
        {
            code.Append("GeneratedMutationShapeSql = static (provider, operation, property, rows) => (provider, operation, property, rows) switch { ");
            foreach (var statement in statements)
            {
                var open = statement.Provider == "bing.sqlserver" ? "[" :
                    statement.Provider is "bing.postgresql" or "bing.oracle" ? "\"" : "`";
                var close = statement.Provider == "bing.sqlserver" ? "]" : open;
                var prefix = statement.Provider == "bing.oracle" ? ":p_" : "@_p_";
                string Column(ModelPropertyProjection property) => open + property.ColumnName + close;
                string Parameter(int index) => prefix + index.ToString(CultureInfo.InvariantCulture);
                var table = open + model.TableName + close;
                void AddShape(string operation, string? property, int rows, string sql) => code
                    .Append('(').Append(Literal(statement.Provider)).Append(", global::Bing.Data.Sql.Builders.SqlOperationKind.")
                    .Append(operation).Append(", ").Append(Literal(property)).Append(", ").Append(rows)
                    .Append(") => ").Append(Literal(sql)).Append(", ");
                foreach (var property in insert)
                    AddShape("InsertValues", property.Name, 1,
                        "Insert Into " + table + " (" + Column(property) + ") Values (" + Parameter(0) + ")");
                if (keys.Length > 0)
                    foreach (var property in update)
                        AddShape("Update", property.Name, 1, "Update " + table + " Set " + Column(property) +
                            " = " + Parameter(0) + " Where " + string.Join(" And ", conditions.Select((condition, index) =>
                                Column(condition) + " = " + Parameter(index + 1))));
                if (insert.Length > 0 && statement.Provider != "bing.oracle")
                    AddShape("InsertValues", null, 2, "Insert Into " + table + " (" + string.Join(", ", insert.Select(Column)) +
                        ") Values (" + string.Join(", ", insert.Select((_, index) => Parameter(index))) + "), (" +
                        string.Join(", ", insert.Select((_, index) => Parameter(index + insert.Length))) + ")");
            }
            code.Append("_ => null }, ");
        }
        code.Append('}');
    }

    private static bool IsStaticIdentifier(string name) => !string.IsNullOrWhiteSpace(name) &&
        name.All(character => char.IsLetterOrDigit(character) || character == '_');

    /// <summary>只生成与默认转换等价的类型分支；每行检查 Provider 存储类型。</summary>
    private static string? ReaderAssignment(string modelType, string propertyName, string propertyType,
        string? readerStorageTypeName)
    {
        var type = propertyType.Replace("global::", "").TrimEnd('?');
        if (readerStorageTypeName == null && (type == "object" || type == "System.Object"))
        {
            var objectDestination = "((" + modelType + ")instance)." + propertyName;
            // object 结果保留 Provider 原值；DBNull 由上层结果读取器先跳过。
            return "static (instance, reader, ordinal) => {" + objectDestination + "=reader.GetValue(ordinal);return true;}";
        }
        if (readerStorageTypeName != null)
        {
            var storageType = readerStorageTypeName.Replace("global::", "");
            var generatedDestination = "((" + modelType + ")instance)." + propertyName;
            var storageFieldType = "var fieldType=reader.GetFieldType(ordinal);";
            if (storageType == "System.Char")
                return "static (instance, reader, ordinal) => {" + storageFieldType + "if(fieldType==typeof(long)){" + generatedDestination + "=checked((char)reader.GetInt64(ordinal));return true;}" +
                    "if(fieldType!=typeof(char))return false;" + generatedDestination + "=reader.GetChar(ordinal);return true;}";

            var generatedGetter = storageType switch
            {
                "int" or "System.Int32" => "GetInt32", "long" or "System.Int64" => "GetInt64",
                "short" or "System.Int16" => "GetInt16", "byte" or "System.Byte" => "GetByte",
                // DbDataReader 没有无符号和 SByte 的类型化 Getter，先读取精确底层值再转枚举。
                "sbyte" or "System.SByte" or "ushort" or "System.UInt16" or
                "uint" or "System.UInt32" or "ulong" or "System.UInt64" => "GetValue",
                _ => null
            };
            var int64 = "if(fieldType==typeof(long)){" + generatedDestination + "=checked((" + type + ")reader.GetInt64(ordinal));return true;}";
            if (generatedGetter == null)
                return "static (instance, reader, ordinal) => {" + int64 + "return false;}";
            var storageReadValue = generatedGetter == "GetValue"
                ? type == storageType
                    ? "(" + type + ")reader.GetValue(ordinal)"
                    : "(" + type + ")(" + storageType + ")reader.GetValue(ordinal)"
                : type == storageType
                    ? "reader." + generatedGetter + "(ordinal)"
                    : "(" + type + ")reader." + generatedGetter + "(ordinal)";
            return "static (instance, reader, ordinal) => {" + storageFieldType + int64 +
                "if(fieldType!=typeof(" + storageType + "))return false;" + generatedDestination +
                "=" + storageReadValue + ";return true;}";
        }
        var getter = type switch
        {
            "int" or "System.Int32" => "GetInt32", "long" or "System.Int64" => "GetInt64",
            "string" or "System.String" => "GetString", "double" or "System.Double" => "GetDouble",
            "float" or "System.Single" => "GetFloat", "decimal" or "System.Decimal" => "GetDecimal",
            "bool" or "System.Boolean" => "GetBoolean", "short" or "System.Int16" => "GetInt16",
            "sbyte" or "System.SByte" or "ushort" or "System.UInt16" or
            "uint" or "System.UInt32" or "ulong" or "System.UInt64" => "GetValue",
            "byte" or "System.Byte" => "GetByte", "System.DateTime" => "GetDateTime",
            "System.Guid" => "GetGuid", "System.DateTimeOffset" or "System.TimeSpan" or
            "System.DateOnly" or "System.TimeOnly" or "System.Byte[]" or "byte[]" => "GetValue", _ => null
        };
        var destination = "((" + modelType + ")instance)." + propertyName;
        // SQLite INTEGER 返回 Int64；用受检转换直接赋值，避免 GetValue 和中间数值装箱。
        var fieldType = "var fieldType=reader.GetFieldType(ordinal);";
        var integer = type is "int" or "System.Int32" or "short" or "System.Int16" or
            "byte" or "System.Byte" or "sbyte" or "System.SByte" or "ushort" or "System.UInt16" or
            "uint" or "System.UInt32" or "ulong" or "System.UInt64"
            ? "if(fieldType==typeof(long)){" + destination + "=checked((" + type + ")reader.GetInt64(ordinal));return true;}"
            : "";
        if (getter == null && integer.Length == 0) return null;
        var fallbackReadValue = getter == "GetValue"
            ? "(" + type + ")reader.GetValue(ordinal)"
            : "reader." + getter + "(ordinal)";
        var fallback = getter == null ? "return false;" : "if(fieldType!=typeof(" + type + "))return false;" + destination + "=" + fallbackReadValue + ";return true;";
        return "static (instance, reader, ordinal) => {" + fieldType + integer + fallback + "}";
    }

    private static bool CanGenerateFactory(INamedTypeSymbol model)
    {
        if (model.IsAbstract || !model.InstanceConstructors.Any(constructor =>
                !constructor.IsStatic && constructor.Parameters.Length == 0 &&
                constructor.DeclaredAccessibility == Accessibility.Public))
            return false;

        for (var current = model; current != null; current = current.BaseType)
            if (current.GetMembers().Any(member =>
                    member is IPropertySymbol { IsRequired: true } || member is IFieldSymbol { IsRequired: true }))
                return false;

        return true;
    }

    private static void AppendProperty(StringBuilder code, ModelProjection model,
        ModelPropertyProjection property)
    {
        var propertyName = EscapeIdentifier(property.Name);

        code.Append(MetadataPrefix).Append("EntityPropertyMetadata.CreateGenerated(")
            .Append(Literal(property.Name)).Append(", typeof(").Append(property.PropertyTypeName).Append("), ")
            .Append(Bool(property.CanRead)).Append(", ").Append(Bool(property.CanWrite)).Append(", ")
            .Append(Literal(property.ColumnName)).Append(", ").Append(Bool(property.IsIgnored)).Append(", ")
            .Append(Bool(property.IsKey)).Append(", (global::System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption)")
            .Append(property.GeneratedOption.ToString(CultureInfo.InvariantCulture)).Append(", ")
            .Append(Bool(property.IsConcurrencyToken)).Append(", ").Append(Bool(property.IsRequired)).Append(", ")
            .Append(property.MaxLength.HasValue ? property.MaxLength.Value.ToString(CultureInfo.InvariantCulture) : "null").Append(", ")
            .Append(Literal(property.ProviderTypeName)).Append(", ");

        if (property.EmitGetter)
            code.Append("static instance => ((").Append(model.TypeName).Append(")instance).")
                .Append(propertyName);
        else
            code.Append("null");

        code.Append(", ");
        if (property.EmitSetter)
        {
            code.Append("static (instance, value) => ((").Append(model.TypeName).Append(")instance).")
                .Append(propertyName).Append(" = (").Append(property.PropertyTypeName).Append(")value");
        }
        else
            code.Append("null");

        code.Append(", ").Append(Bool(property.HasNonPublicSetter));
        code.Append(")");
        var typedReader = ReaderAssignment(model.TypeName, propertyName, property.PropertyTypeName,
            property.ReaderStorageTypeName);
        if (property.EmitSetter && typedReader != null)
            code.Append(".WithGeneratedReader(").Append(typedReader).Append(")");
        code.AppendLine(",");
    }

    private static IEnumerable<IPropertySymbol> GetProperties(INamedTypeSymbol model)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = model; current != null; current = current.BaseType)
        {
            var members = current.GetMembers().OfType<IPropertySymbol>()
                .Where(property => !property.IsStatic && !property.IsIndexer &&
                                   property.DeclaredAccessibility == Accessibility.Public)
                .OrderBy(property => property.Locations.FirstOrDefault(location => location.IsInSource)?.SourceSpan.Start ?? int.MaxValue)
                .ThenBy(property => property.Name, StringComparer.Ordinal);

            foreach (var property in members)
                if (seen.Add(property.Name))
                    yield return property;
        }
    }

    private static bool CanBox(ITypeSymbol type) =>
        type.TypeKind is not (TypeKind.Pointer or TypeKind.FunctionPointer) && !type.IsRefLikeType && !ContainsTypeParameter(type);

    private static AttributeData? FindAttribute(ISymbol symbol, string fullName)
    {
        for (ISymbol? current = symbol; current != null; current = current switch
        {
            INamedTypeSymbol type => type.BaseType,
            IPropertySymbol property => property.OverriddenProperty,
            _ => null
        })
        {
            var attribute = current.GetAttributes().FirstOrDefault(item =>
                item.AttributeClass?.ToDisplayString() == fullName);
            if (attribute != null)
                return attribute;
        }

        return null;
    }

    private static int GeneratedOption(IPropertySymbol property)
    {
        var attribute = FindAttribute(property,
            "System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedAttribute");
        if (attribute == null || attribute.ConstructorArguments.Length == 0)
            return 0;
        return Convert.ToInt32(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture);
    }

    private static int? MaxLength(IPropertySymbol property)
    {
        var maxLength = ConstructorInt(FindAttribute(property,
            "System.ComponentModel.DataAnnotations.MaxLengthAttribute"));
        if (maxLength > 0)
            return maxLength;

        var stringLength = ConstructorInt(FindAttribute(property,
            "System.ComponentModel.DataAnnotations.StringLengthAttribute"));
        return stringLength > 0 ? stringLength : null;
    }

    private static int? ConstructorInt(AttributeData? attribute) =>
        attribute?.ConstructorArguments.FirstOrDefault().Value is int value ? value : null;

    private static string? ConstructorString(AttributeData? attribute) =>
        attribute?.ConstructorArguments.FirstOrDefault().Value as string;

    private static string? NamedString(AttributeData? attribute, string name) =>
        attribute?.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value as string;

    private static string TypeName(ITypeSymbol type) => type.ToDisplayString(TypeFormat);

    private static string Literal(string? value) => value == null ? "null" : SymbolDisplay.FormatLiteral(value, true);

    private static string Bool(bool value) => value ? "true" : "false";

    private static string AccessibilityText(Accessibility accessibility) => accessibility == Accessibility.Public
        ? "public" : "internal";

    private static string NamespaceName(INamespaceSymbol symbol)
    {
        if (symbol.IsGlobalNamespace)
            return string.Empty;

        var names = new Stack<string>();
        for (var current = symbol; !current.IsGlobalNamespace; current = current.ContainingNamespace)
            names.Push(EscapeIdentifier(current.Name));
        return string.Join(".", names);
    }

    private static string EscapeIdentifier(string value) =>
        SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value : value;

    private static string HintName(string identity)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(identity))
            .TrimEnd('=')
            .Replace('+', '_')
            .Replace('/', '-');
        return "BingSql.Model." + encoded + ".g.cs";
    }

    private sealed class ContextOutput
    {
        public ContextOutput(string? hintName, string? source, ImmutableArray<Diagnostic> diagnostics)
        {
            HintName = hintName;
            Source = source;
            Diagnostics = diagnostics;
        }

        public string? HintName { get; }

        public string? Source { get; }

        public ImmutableArray<Diagnostic> Diagnostics { get; }
    }

    private sealed class ContextProjection
    {
        public ContextProjection(string identity, string namespaceName, string name, string accessibility,
            bool isStatic, bool isAbstract, bool isSealed)
        {
            Identity = identity;
            NamespaceName = namespaceName;
            Name = name;
            Accessibility = accessibility;
            IsStatic = isStatic;
            IsAbstract = isAbstract;
            IsSealed = isSealed;
        }

        public string Identity { get; }
        public string NamespaceName { get; }
        public string Name { get; }
        public string Accessibility { get; }
        public bool IsStatic { get; }
        public bool IsAbstract { get; }
        public bool IsSealed { get; }
    }

    private sealed class ModelProjection
    {
        public ModelProjection(string typeName, string tableName, string schema, ModelRoles roles,
            bool factoryAvailable, ImmutableArray<ModelPropertyProjection> properties)
        {
            TypeName = typeName;
            TableName = tableName;
            Schema = schema;
            Roles = roles;
            FactoryAvailable = factoryAvailable;
            Properties = properties;
        }

        public string TypeName { get; }
        public string TableName { get; }
        public string Schema { get; }
        public ModelRoles Roles { get; }
        public bool FactoryAvailable { get; }
        public ImmutableArray<ModelPropertyProjection> Properties { get; }
    }

    private sealed class ModelPropertyProjection
    {
        public ModelPropertyProjection(string name, string propertyTypeName, string? readerStorageTypeName, string columnName,
            string? providerTypeName, bool isIgnored, bool isKey, int generatedOption,
            bool isConcurrencyToken, bool isRequired, int? maxLength, bool canRead, bool canWrite,
            bool hasNonPublicSetter, bool emitGetter, bool emitSetter, bool isInitOnly)
        {
            Name = name;
            PropertyTypeName = propertyTypeName;
            ReaderStorageTypeName = readerStorageTypeName;
            ColumnName = columnName;
            ProviderTypeName = providerTypeName;
            IsIgnored = isIgnored;
            IsKey = isKey;
            GeneratedOption = generatedOption;
            IsConcurrencyToken = isConcurrencyToken;
            IsRequired = isRequired;
            MaxLength = maxLength;
            CanRead = canRead;
            CanWrite = canWrite;
            HasNonPublicSetter = hasNonPublicSetter;
            EmitGetter = emitGetter;
            EmitSetter = emitSetter;
            IsInitOnly = isInitOnly;
        }

        public string Name { get; }
        public string PropertyTypeName { get; }
        public string? ReaderStorageTypeName { get; }
        public string ColumnName { get; }
        public string? ProviderTypeName { get; }
        public bool IsIgnored { get; }
        public bool IsKey { get; }
        public int GeneratedOption { get; }
        public bool IsConcurrencyToken { get; }
        public bool IsRequired { get; }
        public int? MaxLength { get; }
        public bool CanRead { get; }
        public bool CanWrite { get; }
        public bool HasNonPublicSetter { get; }
        public bool EmitGetter { get; }
        public bool EmitSetter { get; }
        public bool IsInitOnly { get; }
    }

    private sealed class SemanticContextInput
    {
        public SemanticContextInput(ContextProjection? context, ImmutableArray<ModelProjection> models,
            ImmutableArray<SemanticDiagnostic> diagnostics, bool isValid, bool hasRuntimeContracts,
            bool hasGeneratedValueReaderContract, bool hasGeneratedMutationSqlContract = false,
            bool hasGeneratedKeyInsertSqlContract = false, bool hasGeneratedMutationShapeSqlContract = false)
        {
            Context = context;
            Models = models;
            Diagnostics = diagnostics;
            IsValid = isValid;
            HasRuntimeContracts = hasRuntimeContracts;
            HasGeneratedValueReaderContract = hasGeneratedValueReaderContract;
            HasGeneratedMutationSqlContract = hasGeneratedMutationSqlContract;
            HasGeneratedKeyInsertSqlContract = hasGeneratedKeyInsertSqlContract;
            HasGeneratedMutationShapeSqlContract = hasGeneratedMutationShapeSqlContract;
        }

        public ContextProjection? Context { get; }

        public ImmutableArray<ModelProjection> Models { get; }

        public ImmutableArray<SemanticDiagnostic> Diagnostics { get; }

        public bool IsValid { get; }

        public bool HasRuntimeContracts { get; }

        public bool HasGeneratedValueReaderContract { get; }
        public bool HasGeneratedMutationSqlContract { get; }
        public bool HasGeneratedKeyInsertSqlContract { get; }
        public bool HasGeneratedMutationShapeSqlContract { get; }
    }

    private sealed class SemanticDiagnostic
    {
        private SemanticDiagnostic(DiagnosticDescriptor descriptor, string[] arguments, string? path,
            TextSpan span, LinePositionSpan lineSpan)
        {
            Descriptor = descriptor;
            Arguments = arguments;
            Path = path;
            Span = span;
            LineSpan = lineSpan;
        }

        public DiagnosticDescriptor Descriptor { get; }

        public string[] Arguments { get; }

        public string? Path { get; }

        public TextSpan Span { get; }

        public LinePositionSpan LineSpan { get; }

        public static SemanticDiagnostic Create(DiagnosticDescriptor descriptor, Location location,
            params string[] arguments)
        {
            if (!location.IsInSource)
                return new SemanticDiagnostic(descriptor, arguments, null, default, default);

            var lineSpan = location.GetLineSpan();
            return new SemanticDiagnostic(descriptor, arguments, lineSpan.Path,
                location.SourceSpan, lineSpan.Span);
        }

        public Diagnostic ToDiagnostic()
        {
            var location = Path == null ? Location.None : Location.Create(Path, Span, LineSpan);
            return Diagnostic.Create(Descriptor, location, Arguments.Cast<object>().ToArray());
        }
    }

    private sealed class SemanticContextInputComparer : IEqualityComparer<SemanticContextInput>
    {
        public static SemanticContextInputComparer Instance { get; } = new();

        public bool Equals(SemanticContextInput? left, SemanticContextInput? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || !ContextsEqual(left.Context, right.Context) ||
                left.IsValid != right.IsValid || left.HasRuntimeContracts != right.HasRuntimeContracts ||
                left.HasGeneratedValueReaderContract != right.HasGeneratedValueReaderContract ||
                left.HasGeneratedMutationSqlContract != right.HasGeneratedMutationSqlContract ||
                left.HasGeneratedKeyInsertSqlContract != right.HasGeneratedKeyInsertSqlContract ||
                left.HasGeneratedMutationShapeSqlContract != right.HasGeneratedMutationShapeSqlContract ||
                left.Models.Length != right.Models.Length || left.Diagnostics.Length != right.Diagnostics.Length)
                return false;

            for (var index = 0; index < left.Models.Length; index++)
                if (!ModelsEqual(left.Models[index], right.Models[index]))
                    return false;
            for (var index = 0; index < left.Diagnostics.Length; index++)
                if (!DiagnosticsEqual(left.Diagnostics[index], right.Diagnostics[index]))
                    return false;
            return true;
        }

        public int GetHashCode(SemanticContextInput value)
        {
            unchecked
            {
                var hash = ContextHash(value.Context);
                hash = (hash * 397) ^ value.IsValid.GetHashCode();
                hash = (hash * 397) ^ value.HasRuntimeContracts.GetHashCode();
                hash = (hash * 397) ^ value.HasGeneratedValueReaderContract.GetHashCode();
                hash = (hash * 397) ^ value.HasGeneratedMutationSqlContract.GetHashCode();
                hash = (hash * 397) ^ value.HasGeneratedKeyInsertSqlContract.GetHashCode();
                hash = (hash * 397) ^ value.HasGeneratedMutationShapeSqlContract.GetHashCode();
                foreach (var model in value.Models)
                {
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(model.TypeName);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(model.TableName);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(model.Schema);
                    hash = (hash * 397) ^ (int)model.Roles;
                    hash = (hash * 397) ^ model.FactoryAvailable.GetHashCode();
                    foreach (var property in model.Properties)
                    hash = (hash * 397) ^ PropertyHashCode(property);
                }
                foreach (var diagnostic in value.Diagnostics)
                {
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(diagnostic.Descriptor.Id);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(diagnostic.Path ?? string.Empty);
                    hash = (hash * 397) ^ diagnostic.Span.GetHashCode();
                    foreach (var argument in diagnostic.Arguments)
                        hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(argument);
                }
                return hash;
            }
        }

        private static bool ModelsEqual(ModelProjection left, ModelProjection right) =>
            left.TypeName == right.TypeName && left.TableName == right.TableName && left.Schema == right.Schema &&
            left.Roles == right.Roles && left.FactoryAvailable == right.FactoryAvailable &&
            left.Properties.Length == right.Properties.Length &&
            left.Properties.Zip(right.Properties, PropertiesEqual).All(equal => equal);

        private static bool ContextsEqual(ContextProjection? left, ContextProjection? right) =>
            ReferenceEquals(left, right) || (left != null && right != null && left.Identity == right.Identity &&
                left.NamespaceName == right.NamespaceName && left.Name == right.Name &&
                left.Accessibility == right.Accessibility && left.IsStatic == right.IsStatic &&
                left.IsAbstract == right.IsAbstract && left.IsSealed == right.IsSealed);

        private static int ContextHash(ContextProjection? context)
        {
            if (context == null)
                return 0;
            unchecked
            {
                var hash = StringHash(context.Identity);
                hash = (hash * 397) ^ StringHash(context.NamespaceName);
                hash = (hash * 397) ^ StringHash(context.Name);
                hash = (hash * 397) ^ StringHash(context.Accessibility);
                hash = (hash * 397) ^ context.IsStatic.GetHashCode();
                hash = (hash * 397) ^ context.IsAbstract.GetHashCode();
                hash = (hash * 397) ^ context.IsSealed.GetHashCode();
                return hash;
            }
        }

        private static bool PropertiesEqual(ModelPropertyProjection left, ModelPropertyProjection right) =>
            left.Name == right.Name && left.PropertyTypeName == right.PropertyTypeName &&
            left.ReaderStorageTypeName == right.ReaderStorageTypeName &&
            left.ColumnName == right.ColumnName && left.ProviderTypeName == right.ProviderTypeName &&
            left.IsIgnored == right.IsIgnored && left.IsKey == right.IsKey &&
            left.GeneratedOption == right.GeneratedOption &&
            left.IsConcurrencyToken == right.IsConcurrencyToken && left.IsRequired == right.IsRequired &&
            left.MaxLength == right.MaxLength && left.CanRead == right.CanRead &&
            left.CanWrite == right.CanWrite && left.HasNonPublicSetter == right.HasNonPublicSetter &&
            left.EmitGetter == right.EmitGetter &&
            left.EmitSetter == right.EmitSetter && left.IsInitOnly == right.IsInitOnly;

        private static int PropertyHashCode(ModelPropertyProjection property)
        {
            unchecked
            {
                var hash = StringHash(property.Name);
                hash = (hash * 397) ^ StringHash(property.PropertyTypeName);
                hash = (hash * 397) ^ StringHash(property.ReaderStorageTypeName);
                hash = (hash * 397) ^ StringHash(property.ColumnName);
                hash = (hash * 397) ^ StringHash(property.ProviderTypeName);
                hash = (hash * 397) ^ property.IsIgnored.GetHashCode();
                hash = (hash * 397) ^ property.IsKey.GetHashCode();
                hash = (hash * 397) ^ property.GeneratedOption;
                hash = (hash * 397) ^ property.IsConcurrencyToken.GetHashCode();
                hash = (hash * 397) ^ property.IsRequired.GetHashCode();
                hash = (hash * 397) ^ property.MaxLength.GetHashCode();
                hash = (hash * 397) ^ property.CanRead.GetHashCode();
                hash = (hash * 397) ^ property.CanWrite.GetHashCode();
                hash = (hash * 397) ^ property.HasNonPublicSetter.GetHashCode();
                hash = (hash * 397) ^ property.EmitGetter.GetHashCode();
                hash = (hash * 397) ^ property.EmitSetter.GetHashCode();
                hash = (hash * 397) ^ property.IsInitOnly.GetHashCode();
                return hash;
            }
        }

        private static int StringHash(string? value) => value == null
            ? 0
            : StringComparer.Ordinal.GetHashCode(value);

        private static bool DiagnosticsEqual(SemanticDiagnostic left, SemanticDiagnostic right) =>
            left.Descriptor.Id == right.Descriptor.Id && left.Path == right.Path && left.Span == right.Span &&
            left.LineSpan.Equals(right.LineSpan) &&
            left.Arguments.SequenceEqual(right.Arguments, StringComparer.Ordinal);
    }

    private sealed class ContextCandidate
    {
        public ContextCandidate(INamedTypeSymbol symbol, Location location)
        {
            Symbol = symbol;
            Location = location;
        }

        public INamedTypeSymbol Symbol { get; }

        public Location Location { get; }
    }

    [Flags]
    private enum ModelRoles
    {
        None = 0,
        SqlModel = 1,
        SqlParameters = 2,
        SqlResult = 4
    }

    private sealed class RegisteredModel
    {
        public RegisteredModel(INamedTypeSymbol symbol, Location location, ModelRoles roles)
        {
            Symbol = symbol;
            Location = location;
            Roles = roles;
        }

        public INamedTypeSymbol Symbol { get; }

        public Location Location { get; }

        public ModelRoles Roles { get; set; }
    }
}
