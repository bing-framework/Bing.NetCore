using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

/// <summary>
/// R7 验收回归：普通结构根的键 comparer 不得覆盖内部领域实体的集合/字典身份语义。
/// </summary>
public sealed partial class ChangeGraphSourceGeneratorTest
{
    /// <summary>FIX-011：结构根 WithKeyComparer 不得影响内部领域列表身份。</summary>
    [Fact]
    public void Generate_WhenStructureRootKeyComparerWrapsDomainList_ShouldKeepDomainIdentity()
    {
        var result = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Leaf : DomainObjectBase<Leaf>, IEntity
            {
                public string Id { get; set; }
                public string Name { get; set; }
                public void Init() { }
                public object[] GetKeys() => new object[] { Id };
            }
            public class Envelope { public List<Leaf> Children { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var model = new ChangeGraphModel<Envelope>("envelope", (writer, value) =>
                        writer.Sequence(nameof(Envelope.Children), value.Children,
                            Leaf.GeneratedChangeGraphModel, Leaf.GeneratedChangeGraphModel.KeySelector));
                    var options = ChangeGraphOptions.Default
                        .WithAdapter(model)
                        .WithKeyComparer<string>(StringComparer.OrdinalIgnoreCase);
                    var tracker = new ChangeGraphTracker(options);
                    var before = new Envelope
                    {
                        Children = new List<Leaf>
                        {
                            new Leaf { Id = "ABC", Name = "old" },
                            new Leaf { Id = "abc", Name = "old2" }
                        }
                    };
                    var after = new Envelope
                    {
                        Children = new List<Leaf>
                        {
                            new Leaf { Id = "ABC", Name = "old" },
                            new Leaf { Id = "abc", Name = "old2" }
                        }
                    };
                    return tracker.Compare(before, after).Changes.Count.ToString();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("0", RunCheck(result));
    }

    /// <summary>FIX-011：结构根 WithKeyComparer 不得影响嵌套领域列表身份。</summary>
    [Fact]
    public void Generate_WhenStructureRootKeyComparerWrapsNestedDomainList_ShouldKeepDomainIdentity()
    {
        var result = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Leaf : DomainObjectBase<Leaf>, IEntity
            {
                public string Id { get; set; }
                public void Init() { }
                public object[] GetKeys() => new object[] { Id };
            }
            public class Envelope { public List<List<Leaf>> Children { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var inner = ChangeGraphModel<List<Leaf>>.CreateSequenceModel<List<Leaf>, Leaf>(
                        "inner", value => value, () => Leaf.GeneratedChangeGraphModel, Leaf.GeneratedChangeGraphModel.KeySelector);
                    var model = new ChangeGraphModel<Envelope>("envelope", (writer, value) =>
                        writer.Sequence(nameof(Envelope.Children), value.Children, inner));
                    var options = ChangeGraphOptions.Default
                        .WithAdapter(model)
                        .WithKeyComparer<string>(StringComparer.OrdinalIgnoreCase);
                    var tracker = new ChangeGraphTracker(options);
                    return tracker.Compare(
                        new Envelope { Children = new() { new() { new Leaf { Id = "ABC" }, new Leaf { Id = "abc" } } } },
                        new Envelope { Children = new() { new() { new Leaf { Id = "ABC" }, new Leaf { Id = "abc" } } } })
                        .Changes.Count.ToString();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("0", RunCheck(result));
    }

    /// <summary>FIX-011：快照路径与 Compare 使用相同的身份作用域。</summary>
    [Fact]
    public void Generate_WhenStructureRootKeyComparerSnapshotWrapsDomainList_ShouldKeepDomainIdentity()
    {
        var result = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Leaf : DomainObjectBase<Leaf>, IEntity
            {
                public string Id { get; set; }
                public void Init() { }
                public object[] GetKeys() => new object[] { Id };
            }
            public class Envelope { public List<Leaf> Children { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var model = new ChangeGraphModel<Envelope>("envelope", (writer, value) =>
                        writer.Sequence(nameof(Envelope.Children), value.Children,
                            Leaf.GeneratedChangeGraphModel, Leaf.GeneratedChangeGraphModel.KeySelector));
                    var options = ChangeGraphOptions.Default
                        .WithAdapter(model)
                        .WithKeyComparer<string>(StringComparer.OrdinalIgnoreCase);
                    var tracker = new ChangeGraphTracker(options);
                    var before = new Envelope
                    {
                        Children = new List<Leaf> { new Leaf { Id = "ABC" }, new Leaf { Id = "abc" } }
                    };
                    var snapshot = tracker.Capture(before);
                    return tracker.CompareSnapshot(snapshot, before).Changes.Count.ToString();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("0", RunCheck(result));
    }

    /// <summary>FIX-011：结构根键 comparer 不影响字典值领域身份。</summary>
    [Fact]
    public void Generate_WhenStructureRootKeyComparerWrapsDomainDictionaryValues_ShouldKeepDomainIdentity()
    {
        var result = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Leaf : DomainObjectBase<Leaf>, IEntity
            {
                public string Id { get; set; }
                public void Init() { }
                public object[] GetKeys() => new object[] { Id };
            }
            public class Envelope { public Dictionary<string, Leaf> Children { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var model = new ChangeGraphModel<Envelope>("envelope", (writer, value) =>
                        writer.Dictionary(nameof(Envelope.Children), value.Children, Leaf.GeneratedChangeGraphModel));
                    var options = ChangeGraphOptions.Default
                        .WithAdapter(model)
                        .WithKeyComparer<string>(StringComparer.OrdinalIgnoreCase);
                    return new ChangeGraphTracker(options).Compare(
                        new Envelope { Children = new() { ["a"] = new Leaf { Id = "ABC" }, ["b"] = new Leaf { Id = "abc" } } },
                        new Envelope { Children = new() { ["a"] = new Leaf { Id = "ABC" }, ["b"] = new Leaf { Id = "abc" } } })
                        .Changes.Count.ToString();
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("0", RunCheck(result));
    }
}
