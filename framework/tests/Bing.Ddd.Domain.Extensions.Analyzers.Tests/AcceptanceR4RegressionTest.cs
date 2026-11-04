using System;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

/// <summary>
/// R4 验收反例回归：双 null、递归身份/容器语义、结构循环、开放入口领域规则隔离。
/// </summary>
public sealed partial class ChangeGraphSourceGeneratorTest
{
    /// <summary>FIX-015：对象两侧均为 null 必须无变化。</summary>
    [Fact]
    public void GetChanges_WhenBothSidesNullObject_ShouldBeEmpty()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Leaf : DomainObjectBase<Leaf>, IEntity
            {
                public int Id { get; set; }
                public void Init() { }
                public object[] GetKeys() => new object[] { Id };
            }
            public partial class Root : DomainObjectBase<Root> { public Leaf Child { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root().GetChanges(new Root())
                    .Select(change => change.PropertyName + ":" + change.OldValue + "->" + change.NewValue));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal(string.Empty, RunCheck(result));
    }

    /// <summary>FIX-015：无键列表同位置双 null 必须无变化。</summary>
    [Fact]
    public void GetChanges_WhenUnkeyedListElementBothNull_ShouldBeEmpty()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Leaf : DomainObjectBase<Leaf>
            {
                public string City { get; set; }
                protected override void AddChanges(Leaf other) => AddChange(nameof(City), "City", City, other.City);
            }
            public partial class Root : DomainObjectBase<Root> { public List<Leaf> Items { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root { Items = new() { null } }
                    .GetChanges(new Root { Items = new() { null } })
                    .Select(change => change.PropertyName));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal(string.Empty, RunCheck(result));
    }

    /// <summary>FIX-016：嵌套键控集合换序必须按身份配对且无变化。</summary>
    [Fact]
    public void GetChanges_WhenNestedKeyedCollectionReordered_ShouldBeEmpty()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Leaf : DomainObjectBase<Leaf>, IEntity
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public void Init() { }
                public object[] GetKeys() => new object[] { Id };
            }
            public partial class Root : DomainObjectBase<Root> { public List<List<Leaf>> Items { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root
                    {
                        Items = new() { new() { new Leaf { Id = 1, Name = "one" }, new Leaf { Id = 2, Name = "two" } } }
                    }.GetChanges(new Root
                    {
                        Items = new() { new() { new Leaf { Id = 2, Name = "two" }, new Leaf { Id = 1, Name = "one" } } }
                    }).Select(change => change.PropertyName));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal(string.Empty, RunCheck(result));
    }

    /// <summary>FIX-016：嵌套集合重复身份必须抛出带路径异常。</summary>
    [Fact]
    public void GetChanges_WhenNestedKeyedCollectionHasDuplicateIdentity_ShouldThrowWithPath()
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
            public partial class Root : DomainObjectBase<Root> { public List<List<Leaf>> Items { get; set; } }
            public static class Check
            {
                public static string Run() => new Root
                    {
                        Items = new() { new() { new Leaf { Id = 1 }, new Leaf { Id = 1 } } }
                    }.GetChanges(new Root
                    {
                        Items = new() { new() { new Leaf { Id = 1 }, new Leaf { Id = 1 } } }
                    }).Count().ToString();
            }
            """);

        AssertNoErrors(result.Diagnostics);
        var exception = Assert.Throws<TargetInvocationException>(() => RunCheck(result));
        Assert.Contains("Items", exception.InnerException!.Message);
    }

    /// <summary>FIX-007：列表中的包装结构必须递归到手写 hook。</summary>
    [Fact]
    public void GetChanges_WhenWrapperInsideList_ShouldUseChildHook()
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
            public partial class Root : DomainObjectBase<Root> { public List<Wrapper> Items { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root
                    {
                        Items = new() { new Wrapper { Child = new Leaf { City = "old", Street = "x" } } }
                    }.GetChanges(new Root
                    {
                        Items = new() { new Wrapper { Child = new Leaf { City = "new", Street = "y" } } }
                    }).Select(change => change.PropertyName));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Items[0].Child.City", RunCheck(result));
    }

    /// <summary>FIX-007：嵌套列表中的普通结构相等时不得退化为类型名差异。</summary>
    [Fact]
    public void GetChanges_WhenStructureInsideNestedListEqual_ShouldBeEmpty()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public class Value { public int Number { get; set; } }
            public partial class Root : DomainObjectBase<Root> { public List<List<Value>> Items { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root
                    {
                        Items = new() { new() { new Value { Number = 1 } } }
                    }.GetChanges(new Root
                    {
                        Items = new() { new() { new Value { Number = 1 } } }
                    }).Select(change => change.PropertyName + ":" + change.OldValue + "->" + change.NewValue));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal(string.Empty, RunCheck(result));
    }

    /// <summary>FIX-010：嵌套 HashSet 保留自身 comparer。</summary>
    [Fact]
    public void GetChanges_WhenNestedSetUsesComparer_ShouldBeEmpty()
    {
        var result = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public List<HashSet<string>> Items { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root
                    {
                        Items = new() { new(StringComparer.OrdinalIgnoreCase) { "A" } }
                    }.GetChanges(new Root
                    {
                        Items = new() { new(StringComparer.OrdinalIgnoreCase) { "a" } }
                    }).Select(change => change.PropertyName));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal(string.Empty, RunCheck(result));
    }

    /// <summary>FIX-010：嵌套字典保留自身 comparer，只报告一条键值变更。</summary>
    [Fact]
    public void GetChanges_WhenNestedDictionaryUsesComparer_ShouldReportSingleEntry()
    {
        var result = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public List<Dictionary<string, int>> Items { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root
                    {
                        Items = new() { new(StringComparer.OrdinalIgnoreCase) { ["A"] = 1 } }
                    }.GetChanges(new Root
                    {
                        Items = new() { new(StringComparer.OrdinalIgnoreCase) { ["a"] = 2 } }
                    }).Select(change => change.PropertyName + ":" + change.OldValue + "->" + change.NewValue));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Items[0]{A}:1->2", RunCheck(result));
    }

    /// <summary>FIX-018：单侧 null 容器使用存在侧 comparer，不应误报冲突。</summary>
    [Fact]
    public void GetChanges_WhenNullSideHasCustomComparer_ShouldRecordAddition()
    {
        var result = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public Dictionary<string, int> Map { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root()
                    .GetChanges(new Root { Map = new(StringComparer.OrdinalIgnoreCase) { ["A"] = 1 } })
                    .Select(change => change.PropertyName));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Map{A}", RunCheck(result));
    }

    /// <summary>FIX-017：普通结构自环必须终止且无变化。</summary>
    [Fact]
    public void GetChanges_WhenStructureHasSelfCycle_ShouldTerminate()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;
            public class Wrapper
            {
                public static int Reads;
                public Wrapper Next => this;
            }
            public partial class Root : DomainObjectBase<Root> { public Wrapper Value { get; set; } }
            public static class Check
            {
                public static string Run() => new Root { Value = new Wrapper() }
                    .GetChanges(new Root { Value = new Wrapper() }).Count().ToString();
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("0", RunCheck(result));
    }

    /// <summary>FIX-017：领域对象经容器的回边必须终止且无变化。</summary>
    [Fact]
    public void GetChanges_WhenDomainCycleViaContainer_ShouldBeEmpty()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public List<List<Root>> Items { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var before = new Root();
                    var after = new Root();
                    before.Items = new() { new() { before } };
                    after.Items = new() { new() { after } };
                    return string.Join("|", before.GetChanges(after).Select(change => change.PropertyName));
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal(string.Empty, RunCheck(result));
    }

    /// <summary>FIX-011：object 入口不得绕过领域 comparer 隔离。</summary>
    [Fact]
    public void Generate_WhenObjectRootUsesComparer_ShouldNotOverrideDomainRules()
    {
        var result = RunGenerator("""
            using System;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Root : DomainObjectBase<Root> { public string Name { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    object before = new Root { Name = "ABC" };
                    object after = new Root { Name = "abc" };
                    return new ChangeGraphTracker(ChangeGraphOptions.Default.WithComparer<string>(StringComparer.OrdinalIgnoreCase))
                        .Compare(before, after).Changes.Count.ToString();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("1", RunCheck(result));
    }

    /// <summary>FIX-011：object 入口不得用适配器替换领域模型。</summary>
    [Fact]
    public void Generate_WhenObjectRootUsesAdapter_ShouldNotOverrideDomainModel()
    {
        var result = RunGenerator("""
            using System;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Root : DomainObjectBase<Root> { public string Name { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    object before = new Root { Name = "A" };
                    object after = new Root { Name = "B" };
                    var model = new ChangeGraphModel<Root>("empty", (writer, value) => { });
                    return new ChangeGraphTracker(ChangeGraphOptions.Default.WithAdapter(model))
                        .Compare(before, after).Changes.Count.ToString();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("1", RunCheck(result));
    }
}
