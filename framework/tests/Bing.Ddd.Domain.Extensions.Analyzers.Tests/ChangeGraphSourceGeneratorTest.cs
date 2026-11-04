using System.Collections;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bing.Analyzers;
using Bing.Auditing;
using Bing.Domain.ChangeTracking;
using Bing.Domain.Entities;
using Bing.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

/// <summary>
/// 验证继承 <c>DomainObjectBase&lt;TObject&gt;</c> 即可自动生成变更跟踪产物，并覆盖身份、忽略与容器边界。
/// </summary>
public sealed partial class ChangeGraphSourceGeneratorTest
{
    /// <summary>继承领域对象基类的实体无需任何类级标记即可生成 AddChanges 与模型。</summary>
    [Fact]
    public void Generate_WhenEntityInheritsDomainObjectBase_ShouldEmitAddChangesAndModel()
    {
        const string source = """
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public string Name { get; set; }
            }
            """;

        var result = RunGenerator(source);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.Contains("protected override void AddChanges(global::Root other)", result.GeneratedSource);
        Assert.Contains("GeneratedChangeGraphModel", result.GeneratedSource);
        var changes = CompareGenerated(result, "Root",
            new Dictionary<string, object> { ["Name"] = "old" },
            new Dictionary<string, object> { ["Name"] = "new" });
        Assert.Equal("Name", Assert.Single(changes.Changes).Path.Value);
    }

    /// <summary>显示及描述元数据优先且空值回退到成员名。</summary>
    [Fact]
    public void Compare_WhenPropertiesHaveDisplayOrDescription_ShouldUseStaticMetadataAndFallbackForEmptyValues()
    {
        const string source = """
            using System.ComponentModel;
            using System.ComponentModel.DataAnnotations;
            using Bing.Domain.Entities;

            public partial class Root : DomainObjectBase<Root>
            {
                [Display(Name = "显示名称")]
                [Description("属性说明")]
                public string DisplayAndDescription { get; set; }

                [Description("属性说明")]
                public string DescriptionOnly { get; set; }

                [Display(Name = "")]
                [Description("备用说明")]
                public string EmptyDisplay { get; set; }

                [Description("")]
                public string EmptyDescription { get; set; }

                public string Plain { get; set; }
            }
            """;

        var result = RunGenerator(source);

        AssertNoErrors(result.Diagnostics);
        var changes = CompareGenerated(result, "Root",
            new Dictionary<string, object>
            {
                ["DisplayAndDescription"] = "old",
                ["DescriptionOnly"] = "old",
                ["EmptyDisplay"] = "old",
                ["EmptyDescription"] = "old",
                ["Plain"] = "old"
            },
            new Dictionary<string, object>
            {
                ["DisplayAndDescription"] = "new",
                ["DescriptionOnly"] = "new",
                ["EmptyDisplay"] = "new",
                ["EmptyDescription"] = "new",
                ["Plain"] = "new"
            });

        Assert.Equal(
            new[] { "DescriptionOnly", "DisplayAndDescription", "EmptyDescription", "EmptyDisplay", "Plain" },
            changes.Changes.Select(change => change.Path.Value));
        Assert.Equal(
            new[] { "属性说明", "显示名称", "EmptyDescription", "备用说明", "Plain" },
            changes.Changes.Select(change => change.Description));
    }

    /// <summary>结构值对象成员展开到拥有者模型并保留完整路径。</summary>
    [Fact]
    public void Compare_WhenNestedValueObjectHasNoMetadata_ShouldFallbackDescriptionToMemberNameAndKeepFullPath()
    {
        const string source = """
            using Bing.Domain.Entities;

            public partial class Root : DomainObjectBase<Root>
            {
                public Child Child { get; set; }
            }

            public class Child
            {
                public string Name { get; set; }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);

        var assembly = Assembly.Load(result.AssemblyImage);
        var rootType = assembly.GetType("Root", throwOnError: true);
        var childType = assembly.GetType("Child", throwOnError: true);
        var beforeChild = Activator.CreateInstance(childType);
        childType.GetProperty("Name").SetValue(beforeChild, "old");
        var afterChild = Activator.CreateInstance(childType);
        childType.GetProperty("Name").SetValue(afterChild, "new");
        var changes = CompareGenerated(result, rootType,
            new Dictionary<string, object> { ["Child"] = beforeChild },
            new Dictionary<string, object> { ["Child"] = afterChild });

        var change = Assert.Single(changes.Changes);
        Assert.Equal("Child.Name", change.Path.Value);
        Assert.Equal("Name", change.Description);
        Assert.Equal("Child.Name", Assert.Single(changes.ToLegacyDescriptors()).PropertyName);
    }

    /// <summary>非 partial 领域对象必须报告 CG001。</summary>
    [Fact]
    public void Generate_WhenEntityIsNotPartial_ShouldReportCg001()
    {
        const string source = """
            using Bing.Domain.Entities;
            public class Root : DomainObjectBase<Root> { }
            """;

        var result = RunGenerator(source);

        var diagnostic = result.Diagnostics.First(item => item.Id == "CG001");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);    }

    /// <summary>已声明 ChangeGraphModel 的实体必须报告 CG005。</summary>
    [Fact]
    public void Generate_WhenModelPropertyAlreadyExists_ShouldReportCg005()
    {
        const string source = """
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public object ChangeGraphModel => null;
            }
            """;

        var result = RunGenerator(source);

        var diagnostic = result.Diagnostics.First(item => item.Id == "CG005");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    /// <summary>手写 AddChanges 时不得重复生成，并在存在成员规则时报告 CG015。</summary>
    [Fact]
    public void Generate_WhenManualAddChangesAndIgnoreRuleExist_ShouldNotDuplicateAndReportHint()
    {
        const string source = """
            using Bing.Domain.ChangeTracking;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                [ChangeIgnore] public string Name { get; set; }
                protected override void AddChanges(Root other) { }
            }
            """;

        var result = RunGenerator(source);

        Assert.DoesNotContain("protected override void AddChanges", result.GeneratedSource);
        var hint = result.Diagnostics.First(item => item.Id == "CG015");
        Assert.Equal(DiagnosticSeverity.Info, hint.Severity);
    }

    /// <summary>被忽略成员的 getter 不进入生成模型。</summary>
    [Fact]
    public void Generate_WhenMemberIsIgnored_ShouldNotReferenceItsGetter()
    {
        const string source = """
            using System;
            using Bing.Domain.ChangeTracking;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public string Visible { get; set; }
                [ChangeIgnore] public string Secret { get => throw new InvalidOperationException(); set { } }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        Assert.DoesNotContain("\"Secret\"", result.GeneratedSource);

        var changes = CompareGenerated(result, "Root",
            new Dictionary<string, object> { ["Visible"] = "old" },
            new Dictionary<string, object> { ["Visible"] = "new" });
        Assert.Equal("Visible", Assert.Single(changes.Changes).Path.Value);
    }

    /// <summary>父实体忽略整个关系时不读取子对象。</summary>
    [Fact]
    public void Generate_WhenRelationIsIgnored_ShouldShortCircuitBeforeReadingChild()
    {
        const string source = """
            using System;
            using Bing.Domain.ChangeTracking;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public string Visible { get; set; }
                [ChangeIgnore] public Child Child { get => throw new InvalidOperationException(); set { } }
            }
            public class Child { public string Name { get; set; } }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        Assert.DoesNotContain("\"Child\"", result.GeneratedSource);

        var changes = CompareGenerated(result, "Root",
            new Dictionary<string, object> { ["Visible"] = "same" },
            new Dictionary<string, object> { ["Visible"] = "same" });
        Assert.Empty(changes.Changes);
    }

    /// <summary>无领域身份契约的无序集合必须报告 CG019。</summary>
    [Fact]
    public void Generate_WhenSetElementHasNoIdentity_ShouldReportCg019()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public HashSet<Item> Items { get; set; }
            }
            public class Item { public string Name { get; set; } }
            """;

        var result = RunGenerator(source);

        var diagnostic = result.Diagnostics.First(item => item.Id == "CG019");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    /// <summary>实现 IKey&lt;TKey&gt; 的集合元素按实体键匹配。</summary>
    [Fact]
    public void Compare_WhenSequenceElementImplementsIKey_ShouldMatchByKey()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public List<Line> Lines { get; set; }
            }
            public class Line : IKey<int>
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);

        var assembly = Assembly.Load(result.AssemblyImage);
        var rootType = assembly.GetType("Root", throwOnError: true);
        var lineType = assembly.GetType("Line", throwOnError: true);
        object Line(int id, string name)
        {
            var line = Activator.CreateInstance(lineType);
            lineType.GetProperty("Id").SetValue(line, id);
            lineType.GetProperty("Name").SetValue(line, name);
            return line;
        }
        var listType = typeof(List<>).MakeGenericType(lineType);
        object Lines(params object[] items)
        {
            var list = (IList)Activator.CreateInstance(listType);
            foreach (var item in items)
                list.Add(item);
            return list;
        }

        var changes = CompareGenerated(result, rootType,
            new Dictionary<string, object> { ["Lines"] = Lines(Line(1, "old"), Line(2, "keep")) },
            new Dictionary<string, object> { ["Lines"] = Lines(Line(1, "new"), Line(2, "keep")) });

        var change = Assert.Single(changes.Changes);
        Assert.Equal("Lines[1].Name", change.Path.Value);
    }

    /// <summary>仅实现 IEntity 的复合身份使用 GetKeys() 顺序匹配。</summary>
    [Fact]
    public void Compare_WhenSequenceElementImplementsIEntity_ShouldMatchByCompositeKeys()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public List<Line> Lines { get; set; }
            }
            public class Line : IEntity
            {
                public string Sku { get; set; }
                public int Lot { get; set; }
                public string Value { get; set; }
                public void Init() { }
                public object[] GetKeys() => new object[] { Sku, Lot };
                public Bing.Validation.IValidationResult Validate() => new Bing.Validation.ValidationResultCollection();
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);

        var assembly = Assembly.Load(result.AssemblyImage);
        var rootType = assembly.GetType("Root", throwOnError: true);
        var lineType = assembly.GetType("Line", throwOnError: true);
        object Line(string sku, int lot, string value)
        {
            var line = Activator.CreateInstance(lineType);
            lineType.GetProperty("Sku").SetValue(line, sku);
            lineType.GetProperty("Lot").SetValue(line, lot);
            lineType.GetProperty("Value").SetValue(line, value);
            return line;
        }
        var listType = typeof(List<>).MakeGenericType(lineType);
        object Lines(params object[] items)
        {
            var list = (IList)Activator.CreateInstance(listType);
            foreach (var item in items)
                list.Add(item);
            return list;
        }

        var changes = CompareGenerated(result, rootType,
            new Dictionary<string, object> { ["Lines"] = Lines(Line("A", 1, "old")) },
            new Dictionary<string, object> { ["Lines"] = Lines(Line("A", 1, "new")) });

        var change = Assert.Single(changes.Changes);
        Assert.Equal("Lines[A,1].Value", change.Path.Value);
    }

    /// <summary>静态、索引器及不可读成员不进入捕获。</summary>
    [Fact]
    public void Generate_WhenMembersAreStaticIndexedOrNotReadable_ShouldExcludeThemFromCapture()
    {
        const string source = """
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public static string StaticValue { get; set; }
                public string this[int index] => null;
                public string Visible { get; set; }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        Assert.DoesNotContain("\"StaticValue\"", result.GeneratedSource);
        Assert.Contains("\"Visible\"", result.GeneratedSource);
    }

    /// <summary>嵌套数组与集合生成可编译模型。</summary>
    [Fact]
    public void Generate_WhenCollectionsContainNestedArrays_ShouldEmitCompilableModels()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public List<List<Line>> Groups { get; set; }
                public Line[][] Jagged { get; set; }
                public Dictionary<string, List<Line>> Map { get; set; }
            }
            public class Line : IKey<int>
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
    }

    /// <summary>仅实现 IHasCreationTime 时保留同名业务时间属性。</summary>
    [Fact]
    public void Generate_WhenOnlyCreationTimeContractIsImplemented_ShouldRetainBusinessTimestamps()
    {
        const string source = """
            using System;
            using Bing.Auditing;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>, IHasCreationTime
            {
                public DateTime? CreationTime { get; set; }
                public DateTime? LastModificationTime { get; set; }
                public DateTime? DeletionTime { get; set; }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);

        var date = new System.DateTime(2026, 1, 1);
        var changes = CompareGenerated(result, "Root",
            new Dictionary<string, object>
            {
                ["CreationTime"] = date,
                ["LastModificationTime"] = date,
                ["DeletionTime"] = date
            },
            new Dictionary<string, object>
            {
                ["CreationTime"] = date.AddDays(1),
                ["LastModificationTime"] = date.AddDays(1),
                ["DeletionTime"] = date.AddDays(1)
            });

        Assert.Equal(new[] { "DeletionTime", "LastModificationTime" },
            changes.Changes.Select(change => change.Path.Value));
    }

    /// <summary>可空自定义结构体生成对象模型。</summary>
    [Fact]
    public void Generate_WhenNullableCustomStructIsUsed_ShouldGenerateAnObjectModel()
    {
        const string source = """
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public Value? Value { get; set; }
            }
            public struct Value { public string Name { get; set; } }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
    }

    /// <summary>关键字属性名生成带转义的访问表达式。</summary>
    [Fact]
    public void Generate_WhenPropertyNameIsKeyword_ShouldEmitCompilableAccesses()
    {
        const string source = """
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public string @class { get; set; }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        Assert.Contains("@class", result.GeneratedSource);
    }

    /// <summary>递归对象类型生成有限模型。</summary>
    [Fact]
    public void Generate_WhenRecursiveObjectTypeIsFinite_ShouldEmitCompilableModel()
    {
        const string source = """
            using Bing.Domain.Entities;
            public partial class Node : DomainObjectBase<Node>
            {
                public string Name { get; set; }
                public Node Next { get; set; }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
    }

    /// <summary>生成的模型不使用运行时反射或动态派发。</summary>
    [Fact]
    public void Generate_WhenEmittingGraphModels_ShouldAvoidRuntimeReflectionAndDynamicDispatch()
    {
        const string source = """
            using System.Collections.Generic;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public List<Line> Lines { get; set; }
            }
            public class Line : IKey<int>
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        Assert.DoesNotContain("System.Reflection", result.GeneratedSource);
        Assert.DoesNotContain("dynamic ", result.GeneratedSource);
        Assert.DoesNotContain("Expression.Lambda", result.GeneratedSource);
    }

    /// <summary>派生实体显式隐藏基类模型并保留派生成员。</summary>
    [Fact]
    public void DerivedGraphModel_ExplicitlyHidesBaseModelAndKeepsDerivedCapture()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            public partial class Base : DomainObjectBase<Base> { public string Name { get; set; } }
            public partial class Derived : Base { public string Extra { get; set; } }
            """);
        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id is "CS0108" or "CS0109");
        var changes = CompareGenerated(result, "Derived",
            new Dictionary<string, object> { ["Name"] = "old", ["Extra"] = "old" },
            new Dictionary<string, object> { ["Name"] = "new", ["Extra"] = "new" });
        Assert.Equal(new[] { "Extra", "Name" }, changes.Changes.Select(c => c.Path.Value));
    }

    /// <summary>
    /// 运行变更跟踪源生成器并收集编译结果。
    /// </summary>
    /// <param name="source">待生成或分析的 C# 源码。</param>
    /// <param name="additionalReferences">附加的编译元数据引用。</param>
    /// <returns>生成源码、编译诊断及程序集输出的集合。</returns>
    private static GeneratorResult RunGenerator(string source, params MetadataReference[] additionalReferences)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, "ChangeGraphFixture.cs");
        var compilation = CSharpCompilation.Create(
            "ChangeGraphFixture",
            new[] { tree },
            CreateMetadataReferences().AddRange(additionalReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[] { new ChangeTrackableSourceGenerator() },
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var driverDiagnostics);
        var runResult = driver.GetRunResult();
        var generatedSource = string.Join(Environment.NewLine,
            runResult.Results.SelectMany(item => item.GeneratedSources).Select(item => item.SourceText.ToString()));
        var originalTrees = new HashSet<SyntaxTree>(compilation.SyntaxTrees);
        var generatedTrees = updatedCompilation.SyntaxTrees.Where(tree => !originalTrees.Contains(tree)).ToImmutableArray();
        using var output = new MemoryStream();
        var emit = updatedCompilation.Emit(output);
        var diagnostics = driverDiagnostics
            .Concat(runResult.Diagnostics)
            .Concat(updatedCompilation.GetDiagnostics())
            .Concat(emit.Diagnostics)
            .ToImmutableArray();
        return new GeneratorResult(generatedSource, diagnostics, emit.Success, output.ToArray(), updatedCompilation, generatedTrees);
    }

    /// <summary>
    /// 创建变更跟踪生成器测试的元数据引用。
    /// </summary>
    /// <returns>生成器测试编译使用的元数据引用集合。</returns>
    private static ImmutableArray<MetadataReference> CreateMetadataReferences()
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        var paths = trustedAssemblies.Split(Path.PathSeparator).ToList();
        paths.Add(typeof(DomainObjectBase).Assembly.Location);
        paths.Add(typeof(ChangeTrackableSourceGenerator).Assembly.Location);
        paths.Add(typeof(IHasCreationTime).Assembly.Location);
        paths.Add(typeof(IValidationResult).Assembly.Location);
        return paths.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    /// <summary>
    /// 验证诊断集合不包含编译错误。
    /// </summary>
    /// <param name="diagnostics">待检查的编译诊断集合。</param>
    private static void AssertNoErrors(ImmutableArray<Diagnostic> diagnostics)
    {
        var errors = diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(System.Environment.NewLine, errors.Select(error => error.ToString())));
    }

    /// <summary>
    /// 使用生成的模型比较对象变更。
    /// </summary>
    /// <param name="result">生成器运行及编译结果。</param>
    /// <param name="typeName">待生成或实例化的类型全名。</param>
    /// <param name="beforeValues">比较前对象的属性值。</param>
    /// <param name="afterValues">比较后对象的属性值。</param>
    /// <returns>比较生成模型后得到的结构化变更结果。</returns>
    private static ChangeGraphResult CompareGenerated(GeneratorResult result, string typeName,
        IReadOnlyDictionary<string, object> beforeValues, IReadOnlyDictionary<string, object> afterValues)
    {
        var assembly = Assembly.Load(result.AssemblyImage);
        var rootType = assembly.GetType(typeName, throwOnError: true);
        return CompareGenerated(result, rootType, beforeValues, afterValues);
    }

    /// <summary>
    /// 使用生成的模型比较对象变更。
    /// </summary>
    /// <param name="result">生成器运行及编译结果。</param>
    /// <param name="rootType">变更图根对象的运行时类型。</param>
    /// <param name="beforeValues">比较前对象的属性值。</param>
    /// <param name="afterValues">比较后对象的属性值。</param>
    /// <returns>比较生成模型后得到的结构化变更结果。</returns>
    private static ChangeGraphResult CompareGenerated(GeneratorResult result, Type rootType,
        IReadOnlyDictionary<string, object> beforeValues, IReadOnlyDictionary<string, object> afterValues)
    {
        var before = CreateInstance(rootType, beforeValues);
        var after = CreateInstance(rootType, afterValues);
        var compare = typeof(ChangeGraphTracker).GetMethods()
            .Single(method => method.Name == nameof(ChangeGraphTracker.Compare))
            .MakeGenericMethod(rootType);
        return (ChangeGraphResult)compare.Invoke(new ChangeGraphTracker(), new[] { before, after });
    }

    /// <summary>
    /// 创建实例并设置指定属性值。
    /// </summary>
    /// <param name="type">待检查或实例化的运行时类型。</param>
    /// <param name="values">待检查或设置的值集合。</param>
    /// <returns>已设置指定属性的对象实例。</returns>
    private static object CreateInstance(Type type, IReadOnlyDictionary<string, object> values)
    {
        var instance = Activator.CreateInstance(type);
        foreach (var value in values)
            type.GetProperty(value.Key).SetValue(instance, value.Value);
        return instance;
    }

    /// <summary>
    /// 封装变更图源码生成测试的源码、诊断和编译结果。
    /// </summary>
    /// <param name="GeneratedSource">生成的源码文本。</param>
    /// <param name="Diagnostics">源码生成诊断集合。</param>
    /// <param name="EmitSucceeded">是否成功发出程序集。</param>
    /// <param name="AssemblyImage">编译生成的程序集映像。</param>
    /// <param name="UpdatedCompilation">应用生成结果后的编译对象。</param>
    /// <param name="GeneratedTrees">生成器输出的语法树集合。</param>
    private sealed record GeneratorResult(string GeneratedSource, ImmutableArray<Diagnostic> Diagnostics, bool EmitSucceeded,
        byte[] AssemblyImage, Compilation UpdatedCompilation, ImmutableArray<SyntaxTree> GeneratedTrees);
}
