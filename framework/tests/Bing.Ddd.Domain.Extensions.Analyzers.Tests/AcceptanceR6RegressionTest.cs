using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

/// <summary>
/// R6 验收回归：普通结构根 comparer 只在结构自身作用域生效，不得侵入内部领域对象字段。
/// </summary>
public sealed partial class ChangeGraphSourceGeneratorTest
{
    /// <summary>FIX-011：结构根 comparer 可影响结构字段，但不得覆盖内部领域实体字段比较。</summary>
    [Fact]
    public void Generate_WhenStructureRootComparerWrapsDomainChild_ShouldIsolateDomainRules()
    {
        var result = RunGenerator("""
            using System;
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Leaf : DomainObjectBase<Leaf>
            {
                public string Name { get; set; }
                public static ChangeGraphModel<Leaf> ExplicitGraphModel = new("leaf",
                    (writer, value) => writer.Scalar(nameof(Name), value.Name));
            }
            public class Envelope { public string Label { get; set; } public Leaf Child { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var model = new ChangeGraphModel<Envelope>("envelope", (writer, value) =>
                    {
                        writer.Scalar(nameof(Envelope.Label), value.Label);
                        writer.Object(nameof(Envelope.Child), value.Child, Leaf.ExplicitGraphModel);
                    });
                    var options = ChangeGraphOptions.Default
                        .WithAdapter(model)
                        .WithComparer<string>(StringComparer.OrdinalIgnoreCase);
                    return string.Join("|", new ChangeGraphTracker(options).Compare(
                        new Envelope { Label = "ABC", Child = new Leaf { Name = "ABC" } },
                        new Envelope { Label = "abc", Child = new Leaf { Name = "abc" } })
                        .Changes.Select(change => change.Path.Value));
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Child.Name", RunCheck(result));
    }

    /// <summary>对照：未配置 comparer 时结构与领域字段都按自身规则报告。</summary>
    [Fact]
    public void Generate_WhenStructureRootWithoutComparer_ShouldReportBothChanges()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;
            using Bing.Domain.ChangeTracking;
            public partial class Leaf : DomainObjectBase<Leaf>
            {
                public string Name { get; set; }
                public static ChangeGraphModel<Leaf> ExplicitGraphModel = new("leaf",
                    (writer, value) => writer.Scalar(nameof(Name), value.Name));
            }
            public class Envelope { public string Label { get; set; } public Leaf Child { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var model = new ChangeGraphModel<Envelope>("envelope", (writer, value) =>
                    {
                        writer.Scalar(nameof(Envelope.Label), value.Label);
                        writer.Object(nameof(Envelope.Child), value.Child, Leaf.ExplicitGraphModel);
                    });
                    return string.Join("|", new ChangeGraphTracker(ChangeGraphOptions.Default.WithAdapter(model)).Compare(
                        new Envelope { Label = "ABC", Child = new Leaf { Name = "ABC" } },
                        new Envelope { Label = "abc", Child = new Leaf { Name = "abc" } })
                        .Changes.Select(change => change.Path.Value));
                }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Equal("Child.Name|Label", RunCheck(result));
    }
}
