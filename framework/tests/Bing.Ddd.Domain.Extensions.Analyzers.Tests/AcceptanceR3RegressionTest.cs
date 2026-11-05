using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

/// <summary>
/// 验收修订 3 的独立反例回归：关系删除/替换、多层结构、显式身份、容器 comparer、覆盖边界、上下文与路径。
/// </summary>
public sealed partial class ChangeGraphSourceGeneratorTest
{
    /// <summary>FIX-006：子对象从非空变 null 必须记录关系删除而不是抛异常。</summary>
    [Fact]
    public void GetChanges_WhenChildBecomesNull_ShouldRecordRemovalWithoutThrowing()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Leaf : DomainObjectBase<Leaf> { public string City { get; set; } }
            public partial class Root : DomainObjectBase<Root> { public Leaf Child { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var changes = new Root { Child = new Leaf { City = "old" } }
                        .GetChanges(new Root()).ToList();
                    return changes.Count + ":" + changes[0].PropertyName + ":" + changes[0].NewValue;
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("1:Child:<missing>", RunCheck(result));
    }

    /// <summary>FIX-014：null 变对象时关系路径就是关系名，不能出现 "$" 占位。</summary>
    [Fact]
    public void GetChanges_WhenNullBecomesChild_ShouldUseRelationshipPath()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Leaf : DomainObjectBase<Leaf> { public string City { get; set; } }
            public partial class Root : DomainObjectBase<Root> { public Leaf Child { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root()
                    .GetChanges(new Root { Child = new Leaf { City = "new" } })
                    .Select(change => change.PropertyName));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Child", RunCheck(result));
    }

    /// <summary>FIX-008：单个关系对象身份变化必须报告删除+新增，而不是 Id 字段更新。</summary>
    [Fact]
    public void GetChanges_WhenObjectIdentityChanges_ShouldReplaceEntities()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Leaf : DomainObjectBase<Leaf>, IEntity
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public void Init() { }
                public object[] GetKeys() => new object[] { Id };
            }
            public partial class Root : DomainObjectBase<Root> { public Leaf Child { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root { Child = new Leaf { Id = 1, Name = "same" } }
                    .GetChanges(new Root { Child = new Leaf { Id = 2, Name = "same" } })
                    .Select(change => change.PropertyName));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Child", RunCheck(result));
    }

    /// <summary>FIX-007：多层容器到达领域对象时必须回到手写 AddChanges，不得补回被排除字段。</summary>
    [Fact]
    public void GetChanges_WhenNestedContainersWrapManualHook_ShouldOnlyEmitSelectedMember()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Leaf : DomainObjectBase<Leaf>
            {
                public string City { get; set; }
                public string Street { get; set; }
                protected override void AddChanges(Leaf other) => AddChange(nameof(City), "City", City, other.City);
            }
            public partial class Root : DomainObjectBase<Root> { public List<List<Leaf>> Items { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root { Items = new() { new() { new Leaf { City = "old", Street = "x" } } } }
                    .GetChanges(new Root { Items = new() { new() { new Leaf { City = "new", Street = "y" } } } })
                    .Select(change => change.PropertyName));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Items[0][0].City", RunCheck(result));
    }

    /// <summary>修订 4：普通 POCO 结构不参与自动比较，需报告 CG006。</summary>
    [Fact]
    public void Generate_WhenPocoStructureWrapsDomainChild_ShouldReportCg006()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Leaf : DomainObjectBase<Leaf>
            {
                public string City { get; set; }
                public string Street { get; set; }
                protected override void AddChanges(Leaf other) => AddChange(nameof(City), "City", City, other.City);
            }
            public class Wrapper { public Leaf Child { get; set; } }
            public partial class Root : DomainObjectBase<Root> { public Wrapper Wrapper { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root { Wrapper = new() { Child = new Leaf { City = "old", Street = "x" } } }
                    .GetChanges(new Root { Wrapper = new() { Child = new Leaf { City = "new", Street = "y" } } })
                    .Select(change => change.PropertyName));
            }
            """);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CG006");
        Assert.Equal("", RunCheck(result));
    }

    /// <summary>FIX-009：显式 IEntity.GetKeys 实现必须在运行时按身份匹配集合成员。</summary>
    [Fact]
    public void Generate_WhenExplicitIEntityImplementation_ShouldCompile()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Leaf : DomainObjectBase<Leaf>, IEntity
            {
                public int Id { get; set; }
                public string Name { get; set; }
                object[] IEntity.GetKeys() => new object[] { Id };
                public void Init() { }
            }
            public partial class Root : DomainObjectBase<Root> { public List<Leaf> Items { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root
                    { Items = new() { new Leaf { Id = 1, Name = "old" }, new Leaf { Id = 2, Name = "same" } } }
                    .GetChanges(new Root
                    { Items = new() { new Leaf { Id = 2, Name = "same" }, new Leaf { Id = 1, Name = "new" } } })
                    .Select(change => change.PropertyName));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.Contains("AddCollectionChanges(nameof(Items)", result.GeneratedSource);
        Assert.Equal("Items[1].Name", RunCheck(result));
    }

    /// <summary>FIX-010：字典自带的 OrdinalIgnoreCase comparer 不得被忽略为两个键。</summary>
    [Fact]
    public void GetChanges_WhenDictionaryUsesCaseInsensitiveComparer_ShouldNotReportFalseChanges()
    {
        var result = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public Dictionary<string, int> Map { get; set; } }
            public static class Check
            {
                public static string Run() => new Root { Map = new(StringComparer.OrdinalIgnoreCase) { ["A"] = 1 } }
                    .GetChanges(new Root { Map = new(StringComparer.OrdinalIgnoreCase) { ["a"] = 1 } })
                    .Count().ToString();
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("0", RunCheck(result));
    }

    /// <summary>FIX-010：HashSet 自带的 OrdinalIgnoreCase comparer 不得被忽略为增删。</summary>
    [Fact]
    public void GetChanges_WhenScalarSetUsesCaseInsensitiveComparer_ShouldNotReportFalseChanges()
    {
        var result = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public HashSet<string> Items { get; set; } }
            public static class Check
            {
                public static string Run() => new Root { Items = new(StringComparer.OrdinalIgnoreCase) { "A" } }
                    .GetChanges(new Root { Items = new(StringComparer.OrdinalIgnoreCase) { "a" } })
                    .Count().ToString();
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("0", RunCheck(result));
    }

    /// <summary>FIX-011：调用方标量 comparer 不能改写领域实体字段差异。</summary>
    [Fact]
    public void Generate_WhenComparerRegistered_ShouldNotOverrideDomainScalarRules()
    {
        var result = RunGenerator("""
            using System;
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Root : DomainObjectBase<Root>, IChangeGraphNode
            {
                public string Name { get; set; }
                private static readonly ChangeGraphModel<Root> Model = new("explicit-root",
                    static (writer, value) => writer.Scalar(nameof(Name), value.Name));
                ChangeGraphModel IChangeGraphNode.ChangeGraphModel => Model;
            }
            public static class Check
            {
                public static string Run() => "get=" + new Root { Name = "ABC" }.GetChanges(new Root { Name = "abc" }).Count()
                    + ";graph=" + new ChangeGraphTracker(ChangeGraphOptions.Default.WithComparer<string>(StringComparer.OrdinalIgnoreCase))
                        .Compare(new Root { Name = "ABC" }, new Root { Name = "abc" }).Changes.Count;
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("get=1;graph=1", RunCheck(result));
    }

    /// <summary>FIX-011：调用方适配器不能替换已有领域实体的生成模型。</summary>
    [Fact]
    public void Generate_WhenAdapterRegisteredForDomainEntity_ShouldNotOverrideDomainModel()
    {
        var result = RunGenerator("""
            using System;
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Root : DomainObjectBase<Root>, IChangeGraphNode
            {
                public string Name { get; set; }
                private static readonly ChangeGraphModel<Root> Model = new("explicit-root",
                    static (writer, value) => writer.Scalar(nameof(Name), value.Name));
                ChangeGraphModel IChangeGraphNode.ChangeGraphModel => Model;
            }
            public static class Check
            {
                public static string Run()
                {
                    var model = new ChangeGraphModel<Root>("empty", (writer, value) => { });
                    return "get=" + new Root { Name = "A" }.GetChanges(new Root { Name = "B" }).Count()
                        + ";graph=" + new ChangeGraphTracker(ChangeGraphOptions.Default.WithAdapter(model))
                            .Compare(new Root { Name = "A" }, new Root { Name = "B" }).Changes.Count;
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("get=1;graph=1", RunCheck(result));
    }

    /// <summary>FIX-012：hook 内新建的独立 ChangeTrackingContext 不得写入外层实体结果。</summary>
    [Fact]
    public void GetChanges_WhenStandaloneContextUsedInsideHook_ShouldNotPolluteParent()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Root : DomainObjectBase<Root>
            {
                protected override void AddChanges(Root other)
                {
                    var local = new ChangeTrackingContext();
                    local.Add("Unrelated", "Unrelated", 1, 2);
                }
            }
            public static class Check
            {
                public static string Run() => new Root().GetChanges(new Root()).Count().ToString();
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("0", RunCheck(result));
    }

    /// <summary>FIX-013：空描述必须回退到属性名，不能丢弃已经判定为变化的记录。</summary>
    [Fact]
    public void GetChanges_WhenDescriptionIsEmpty_ShouldFallbackToPropertyName()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                protected override void AddChanges(Root other) => AddChange("Name", "", 1, 2);
            }
            public static class Check
            {
                public static string Run()
                {
                    var change = new Root().GetChanges(new Root()).Single();
                    return change.PropertyName + ":" + change.Description;
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Name:Name", RunCheck(result));
    }

    /// <summary>FIX-006：列表元素变 null 必须在元素路径记录删除。</summary>
    [Fact]
    public void GetChanges_WhenListElementBecomesNull_ShouldRecordRemovalAtElementPath()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Leaf : DomainObjectBase<Leaf> { public string City { get; set; } }
            public partial class Root : DomainObjectBase<Root> { public List<Leaf> Items { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root { Items = new() { new Leaf { City = "x" } } }
                    .GetChanges(new Root { Items = new() { (Leaf)null } })
                    .Select(change => change.PropertyName + ":" + change.NewValue));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Items[0]:<missing>", RunCheck(result));
    }

    /// <summary>FIX-008：字典同键不同实体值必须按身份替换。</summary>
    [Fact]
    public void GetChanges_WhenDictionaryValueIdentityChanges_ShouldReplaceValue()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Leaf : DomainObjectBase<Leaf>, IEntity
            {
                public int Id { get; set; }
                public void Init() { }
                public object[] GetKeys() => new object[] { Id };
            }
            public partial class Root : DomainObjectBase<Root> { public Dictionary<string, Leaf> Map { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root { Map = new() { ["k"] = new Leaf { Id = 1 } } }
                    .GetChanges(new Root { Map = new() { ["k"] = new Leaf { Id = 2 } } })
                    .Select(change => change.PropertyName));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Map{k}", RunCheck(result));
    }

    /// <summary>R03：并发独立比较必须各自得到隔离结果。</summary>
    [Fact]
    public void GetChanges_WhenComparedConcurrently_ShouldIsolateResults()
    {
        var result = RunGenerator("""
            using System.Linq;
            using System.Threading.Tasks;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public string Name { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var ok = 0;
                    Parallel.For(0, 20, index =>
                    {
                        var changes = new Root { Name = "old" + index }
                            .GetChanges(new Root { Name = "new" + index }).ToList();
                        if (changes.Count == 1 && changes[0].NewValue == "\"new" + index + "\"")
                            System.Threading.Interlocked.Increment(ref ok);
                    });
                    return ok.ToString();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("20", RunCheck(result));
    }

    /// <summary>执行生成源码中的 Check.Run。</summary>
    private static string RunCheck(GeneratorResult result)
    {
        Assert.True(result.EmitSucceeded);
        var type = Assembly.Load(result.AssemblyImage).GetType("Check", throwOnError: true);
        return type.GetMethod("Run").Invoke(null, null)?.ToString();
    }
}
