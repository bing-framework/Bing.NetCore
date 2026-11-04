using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

/// <summary>
/// R5 验收回归：值类型结构自治、接口/开放属性领域规则隔离、object 运行时替换与快照分派。
/// </summary>
public sealed partial class ChangeGraphSourceGeneratorTest
{
    /// <summary>FIX-007：struct 包装内的领域对象必须执行手写 hook。</summary>
    [Fact]
    public void GetChanges_WhenStructWrapperContainsManualHook_ShouldOnlyEmitSelectedMember()
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
            public struct Wrapper { public Leaf Child { get; set; } }
            public partial class Root : DomainObjectBase<Root> { public Wrapper Value { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root
                    {
                        Value = new Wrapper { Child = new Leaf { City = "old", Street = "x" } }
                    }.GetChanges(new Root
                    {
                        Value = new Wrapper { Child = new Leaf { City = "new", Street = "y" } }
                    }).Select(change => change.PropertyName));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Value.Child.City", RunCheck(result));
    }

    /// <summary>FIX-007：可空 struct 包装同样遵守子实体手写 hook。</summary>
    [Fact]
    public void GetChanges_WhenNullableStructWrapperContainsManualHook_ShouldOnlyEmitSelectedMember()
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
            public struct Wrapper { public Leaf Child { get; set; } }
            public partial class Root : DomainObjectBase<Root> { public Wrapper? Value { get; set; } }
            public static class Check
            {
                public static string Run() => string.Join("|", new Root
                    {
                        Value = new Wrapper { Child = new Leaf { City = "old", Street = "x" } }
                    }.GetChanges(new Root
                    {
                        Value = new Wrapper { Child = new Leaf { City = "new", Street = "y" } }
                    }).Select(change => change.PropertyName));
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Value.Child.City", RunCheck(result));
    }

    /// <summary>FIX-007：struct 包装作为列表元素时仍执行手写 hook。</summary>
    [Fact]
    public void GetChanges_WhenStructWrapperInsideList_ShouldOnlyEmitSelectedMember()
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
            public struct Wrapper { public Leaf Child { get; set; } }
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

    /// <summary>FIX-011：接口根不得被空适配器覆盖领域模型。</summary>
    [Fact]
    public void Generate_WhenInterfaceRootUsesAdapter_ShouldNotOverrideDomainModel()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public interface IMarker { }
            public partial class Root : DomainObjectBase<Root>, IMarker { public string Name { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    IMarker before = new Root { Name = "A" };
                    IMarker after = new Root { Name = "B" };
                    var model = new ChangeGraphModel<Root>("empty", (writer, value) => { });
                    return new ChangeGraphTracker(ChangeGraphOptions.Default.WithAdapter(model))
                        .Compare(before, after).Changes.Count.ToString();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("1", RunCheck(result));
    }

    /// <summary>FIX-011：声明为 object 的属性不得被空适配器覆盖领域模型。</summary>
    [Fact]
    public void Generate_WhenOpenChildUsesAdapter_ShouldNotOverrideDomainModel()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Leaf : DomainObjectBase<Leaf> { public string Name { get; set; } }
            public partial class Root : DomainObjectBase<Root> { public object Value { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var model = new ChangeGraphModel<Leaf>("empty", (writer, value) => { });
                    return new ChangeGraphTracker(ChangeGraphOptions.Default.WithAdapter(model))
                        .Compare(new Root { Value = new Leaf { Name = "A" } },
                                 new Root { Value = new Leaf { Name = "B" } }).Changes.Count.ToString();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("1", RunCheck(result));
    }

    /// <summary>FIX-019：object 根不同运行时类型必须报告 Replaced。</summary>
    [Fact]
    public void Generate_WhenObjectRootRuntimeTypesDiffer_ShouldReportReplaced()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Alpha : DomainObjectBase<Alpha> { public int Number { get; set; } }
            public partial class Beta : DomainObjectBase<Beta> { public string Name { get; set; } }
            public static class Check
            {
                public static string Run() => new ChangeGraphTracker()
                    .Compare<object>(new Alpha { Number = 1 }, new Beta { Name = "B" })
                    .Changes.Single().Kind.ToString();
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Replaced", RunCheck(result));
    }

    /// <summary>FIX-019：object 快照后续比较不同运行时类型必须报告 Replaced。</summary>
    [Fact]
    public void Generate_WhenObjectSnapshotRuntimeTypesDiffer_ShouldReportReplaced()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Alpha : DomainObjectBase<Alpha> { public int Number { get; set; } }
            public partial class Beta : DomainObjectBase<Beta> { public string Name { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var tracker = new ChangeGraphTracker();
                    var snapshot = tracker.Capture<object>(new Alpha { Number = 1 });
                    return tracker.CompareSnapshot<object>(snapshot, new Beta { Name = "B" })
                        .Changes.Single().Kind.ToString();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Replaced", RunCheck(result));
    }
}
