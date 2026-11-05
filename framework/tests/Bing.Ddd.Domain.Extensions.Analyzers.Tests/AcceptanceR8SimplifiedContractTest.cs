using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

/// <summary>
/// 修订 4 验收回归：简化后的 AddChanges 产物只包含直接属性访问与四种固定辅助调用。
/// </summary>
public sealed partial class ChangeGraphSourceGeneratorTest
{
    /// <summary>生成的 AddChanges 源码不得引用图模型、委托或反射成员读取。</summary>
    [Fact]
    public void Generate_AddChangesSource_ShouldOnlyUseSimplifiedHelpers()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.ComponentModel;
            using Bing.Domain.Entities;
            public partial class Leaf : DomainObjectBase<Leaf> { public string Name { get; set; } }
            public partial class Root : DomainObjectBase<Root>
            {
                [Description("名称")] public string Name { get; set; }
                public Leaf Child { get; set; }
                public List<Leaf> Items { get; set; }
                public Dictionary<string, Leaf> Map { get; set; }
                public List<string> Tags { get; set; }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        var addChanges = ExtractAddChangesSource(result.GeneratedSource);
        Assert.Contains("AddChange(nameof(Name), ", addChanges);
        Assert.Contains("AddObjectChanges(nameof(Child), ", addChanges);
        Assert.Contains("AddCollectionChanges(nameof(Items), ", addChanges);
        Assert.Contains("AddDictionaryChanges(nameof(Map), ", addChanges);
        Assert.Contains("AddCollectionChanges(nameof(Tags), ", addChanges);
        foreach (var forbidden in new[]
                 {
                     "GeneratedChangeGraphModel", "CaptureChanges", "ChangeGraphModel", "ChangeGraphTracker",
                     "Func<", "Action<", "dynamic ", "GetProperties", "PropertyInfo", "Expression", ".Compile("
                 })
            Assert.DoesNotContain(forbidden, addChanges);
    }

    /// <summary>普通领域对象按位置匹配，不读取额外声明的 IKey 身份。</summary>
    [Fact]
    public void GetChanges_WhenDomainObjectHasOnlyIKey_ShouldUseIndexWithoutReadingKey()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public List<Line> Lines { get; set; } }
            public partial class Line : DomainObjectBase<Line>, IKey<int>
            {
                int IKey<int>.Id => throw new System.InvalidOperationException("不应读取 IKey 身份");
                public string Name { get; set; }
            }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root
                    {
                        Lines = new List<Line> { new Line { Name = "old" }, new Line { Name = "keep" } }
                    }.GetChanges(new Root
                    {
                        Lines = new List<Line> { new Line { Name = "new" }, new Line { Name = "keep" } }
                    }).Select(change => change.PropertyName + ":" + change.OldValue + "->" + change.NewValue));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Lines[0].Name:\"old\"->\"new\"", RunCheck(result));
    }

    /// <summary>仅有 IKey 的普通对象无法参与领域成员比较，生成器应明确诊断。</summary>
    [Fact]
    public void Generate_WhenCollectionHasOnlyIKeyObjects_ShouldReportUnsupportedMember()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public List<Line> Lines { get; set; } }
            public class Line : IKey<int> { public int Id { get; set; } }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CG006" && diagnostic.Location.IsInSource);
        Assert.DoesNotContain("AddCollectionChanges", result.GeneratedSource);
    }

    /// <summary>无序领域集合必须由 IEntity 明确身份，不能根据 IKey 自动推断。</summary>
    [Fact]
    public void Generate_WhenSetHasOnlyIKeyDomainObjects_ShouldRequireEntityIdentity()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public HashSet<Line> Lines { get; set; } }
            public partial class Line : DomainObjectBase<Line>, IKey<int> { public int Id { get; set; } }
            """);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CG019" &&
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Location.IsInSource);
    }

    /// <summary>项目现有实体基类自动生成 hook，并用基类 GetKeys 保持重排匹配。</summary>
    [Fact]
    public void GetChanges_WhenInheritingEntityBase_ShouldGenerateAndMatchByEntityKey()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public List<Line> Lines { get; set; } }
            public partial class Line : EntityBase<Line, int>
            {
                public Line(int id, string name) : base(id) => Name = name;
                public string Name { get; set; }
            }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root
                {
                    Lines = new() { new Line(1, "old"), new Line(2, "keep") }
                }.GetChanges(new Root
                {
                    Lines = new() { new Line(2, "keep"), new Line(1, "new") }
                }).Select(change => change.PropertyName + ":" + change.OldValue + "->" + change.NewValue));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.Equal("Lines[1].Name:\"old\"->\"new\"", RunCheck(result));
    }

    /// <summary>直接字典保留容器自身 comparer，且值按标量比较。</summary>
    [Fact]
    public void GetChanges_WhenDictionaryHasComparer_ShouldMatchKeysByComparer()
    {
        var result = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public Dictionary<string, int> Map { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root
                    {
                        Map = new(StringComparer.OrdinalIgnoreCase) { ["A"] = 1 }
                    }.GetChanges(new Root
                    {
                        Map = new(StringComparer.OrdinalIgnoreCase) { ["a"] = 2 }
                    }).Select(change => change.PropertyName + ":" + change.OldValue + "->" + change.NewValue));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Map{A}:1->2", RunCheck(result));
    }

    /// <summary>提取生成源码中 AddChanges 分部块。</summary>
    private static string ExtractAddChangesSource(string generated)
    {
        var blocks = generated.Split(new[] { "// <auto-generated />" }, System.StringSplitOptions.RemoveEmptyEntries);
        return blocks.First(block => block.Contains("protected override void AddChanges(") &&
                                     block.Contains("class @Root"));
    }
}
