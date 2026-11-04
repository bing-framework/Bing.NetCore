using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bing.Domain.ChangeTracking;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

/// <summary>
/// 本轮整改的契约回归：描述统一、Set 递归诊断、真实基类、hook 冲突、GetChanges 组合链、身份与会话隔离。
/// </summary>
public sealed partial class ChangeGraphSourceGeneratorTest
{
    /// <summary>FIX-001：含关系实体的 Display 资源描述必须使用静态访问，而非属性名字面量。</summary>
    [Fact]
    public void Generate_WhenDisplayResourceOnEntityWithRelation_ShouldUseStaticResource()
    {
        var result = RunGenerator("""
            using System.Linq;
            using Bing.Domain.Entities;
            using System.ComponentModel.DataAnnotations;
            public static class Labels { public static string Name => "名称文本"; }
            public partial class Root : DomainObjectBase<Root>
            {
                [Display(Name = "Name", ResourceType = typeof(Labels))] public string Name { get; set; }
                public Child Child { get; set; }
            }
            public class Child { public int Value { get; set; } }
            public static class Check
            {
                public static string Run() => new Root { Name = "old", Child = new Child() }
                    .GetChanges(new Root { Name = "new", Child = new Child() }).Single().Description;
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.Contains("global::Labels.Name", result.GeneratedSource);
        var type = Assembly.Load(result.AssemblyImage).GetType("Check", throwOnError: true);
        Assert.Equal("名称文本", type.GetMethod("Run").Invoke(null, null));
    }

    /// <summary>FIX-001：含关系实体上的派生 DescriptionAttribute 必须报告 CG018。</summary>
    [Fact]
    public void Generate_WhenDerivedDescriptionOnEntityWithRelation_ShouldReportCg018()
    {
        var result = RunGenerator("""
            using System.ComponentModel;
            using Bing.Domain.Entities;
            public class LabelAttribute : DescriptionAttribute { public LabelAttribute() : base("label") { } }
            public partial class Root : DomainObjectBase<Root>
            {
                [Label] public string Name { get; set; }
                public Child Child { get; set; }
            }
            public class Child { public int Value { get; set; } }
            """);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CG018");
    }

    /// <summary>FIX-002：直接与任意嵌套层的无键对象 Set 都必须报告 CG019。</summary>
    [Theory]
    [InlineData("HashSet<Child> Items")]
    [InlineData("List<HashSet<Child>> Items")]
    [InlineData("Dictionary<string, HashSet<Child>> Items")]
    [InlineData("List<List<HashSet<Child>>> Items")]
    public void Generate_WhenNestedSetHasNoIdentity_ShouldReportCg019(string member)
    {
        var source = "using System.Collections.Generic;\nusing Bing.Domain.Entities;\n" +
                     "public partial class Root : DomainObjectBase<Root> { public " + member + " { get; set; } }\n" +
                     "public class Child { public string Name { get; set; } }";
        var result = RunGenerator(source);

        var diagnostic = result.Diagnostics.First(item => item.Id == "CG019");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
    }

    /// <summary>FIX-003：外部程序集中的同名 DomainObjectBase&lt;T&gt; 不得被误识别。</summary>
    [Fact]
    public void Generate_WhenForeignSameNameBase_ShouldNotGenerate()
    {
        var result = RunGenerator("""
            namespace Foreign
            {
                public class DomainObjectBase<T> { }
                public partial class Root : DomainObjectBase<Root> { public string Name { get; set; } }
            }
            """);

        AssertNoErrors(result.Diagnostics);
        Assert.True(result.EmitSucceeded);
        Assert.DoesNotContain("AddChanges", result.GeneratedSource);
    }

    /// <summary>FIX-004：new hook 必须在源声明处报告 CG020，且不生成重复成员。</summary>
    [Fact]
    public void Generate_WhenNewHookIsDeclared_ShouldReportCg020AndNotDuplicate()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public string Name { get; set; }
                protected new void AddChanges(Root other) { }
            }
            """);

        var diagnostic = result.Diagnostics.First(item => item.Id == "CG020");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
        Assert.DoesNotContain(result.Diagnostics, item => item.Id == "CS0111");
        Assert.DoesNotContain("protected override void AddChanges", result.GeneratedSource);
    }

    /// <summary>FIX-004：已声明 CaptureChanges 必须独立报告 CG021。</summary>
    [Fact]
    public void Generate_WhenCaptureChangesIsDeclared_ShouldReportCg021()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root>
            {
                public string Name { get; set; }
                private void CaptureChanges(object writer) { }
            }
            """);

        var diagnostic = result.Diagnostics.First(item => item.Id == "CG021");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
    }

    /// <summary>DEC-02/R02：手写子 hook 必须经 GetChanges 在对象、集合与字典中一致生效。</summary>
    [Fact]
    public void GetChanges_WhenChildHasManualHook_ShouldUseItInObjectCollectionAndDictionary()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;

            public partial class Root : DomainObjectBase<Root>
            {
                public Address Address { get; set; }
                public List<Address> Items { get; set; }
                public Dictionary<string, Address> Map { get; set; }
            }

            public partial class Address : DomainObjectBase<Address>
            {
                public string City { get; set; }
                public string Street { get; set; }
                protected override void AddChanges(Address other)
                {
                    AddChange(nameof(City), "城市", City, other.City);
                }
            }

            public static class Check
            {
                public static string Run()
                {
                    var before = new Root
                    {
                        Address = new Address { City = "old", Street = "x" },
                        Items = new List<Address> { new Address { City = "old", Street = "x" } },
                        Map = new Dictionary<string, Address> { ["k"] = new Address { City = "old", Street = "x" } }
                    };
                    var after = new Root
                    {
                        Address = new Address { City = "new", Street = "y" },
                        Items = new List<Address> { new Address { City = "new", Street = "y" } },
                        Map = new Dictionary<string, Address> { ["k"] = new Address { City = "new", Street = "y" } }
                    };
                    return string.Join("|", before.GetChanges(after).Select(d => d.PropertyName));
                }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        var type = Assembly.Load(result.AssemblyImage).GetType("Check", throwOnError: true);
        Assert.Equal("Address.City|Items[0].City|Map{k}.City", type.GetMethod("Run").Invoke(null, null));
    }

    /// <summary>R04：共享子实体在不同关系路径下都要保留路径。</summary>
    [Fact]
    public void GetChanges_WhenSharedChildAppearsOnTwoPaths_ShouldReportBothPaths()
    {
        const string source = """
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Holder : DomainObjectBase<Holder>
            {
                public Leaf Left { get; set; }
                public Leaf Right { get; set; }
            }
            public partial class Leaf : DomainObjectBase<Leaf> { public string Name { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var beforeLeaf = new Leaf { Name = "old" };
                    var afterLeaf = new Leaf { Name = "new" };
                    var before = new Holder { Left = beforeLeaf, Right = beforeLeaf };
                    var after = new Holder { Left = afterLeaf, Right = afterLeaf };
                    return string.Join("|", before.GetChanges(after).Select(d => d.PropertyName));
                }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        var type = Assembly.Load(result.AssemblyImage).GetType("Check", throwOnError: true);
        Assert.Equal("Left.Name|Right.Name", type.GetMethod("Run").Invoke(null, null));
    }

    /// <summary>R04：自循环终止且保留叶节点差异。</summary>
    [Fact]
    public void GetChanges_WhenSelfCycle_ShouldTerminateAndReportLeafChange()
    {
        const string source = """
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Node : DomainObjectBase<Node>
            {
                public string Name { get; set; }
                public Node Next { get; set; }
            }
            public static class Check
            {
                public static string Run()
                {
                    var before = new Node { Name = "old" }; before.Next = before;
                    var after = new Node { Name = "new" }; after.Next = after;
                    return string.Join("|", before.GetChanges(after).Select(d => d.PropertyName));
                }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        var type = Assembly.Load(result.AssemblyImage).GetType("Check", throwOnError: true);
        Assert.Equal("Name", type.GetMethod("Run").Invoke(null, null));
    }

    /// <summary>R03：连续调用返回独立结果，后续调用不能清空第一次返回值。</summary>
    [Fact]
    public void GetChanges_CalledTwice_ShouldReturnIndependentResults()
    {
        const string source = """
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public string Name { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var before = new Root { Name = "old" };
                    var first = before.GetChanges(new Root { Name = "new" }).ToList();
                    var second = before.GetChanges(new Root { Name = "next" }).ToList();
                    return first.Count + ":" + first[0].NewValue + ":" + second[0].NewValue;
                }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        var type = Assembly.Load(result.AssemblyImage).GetType("Check", throwOnError: true);
        Assert.Equal("1:\"new\":\"next\"", type.GetMethod("Run").Invoke(null, null));
    }

    /// <summary>R03：子 hook 抛异常后 frame 必须恢复，后续比较仍正常。</summary>
    [Fact]
    public void GetChanges_WhenChildHookThrows_ShouldCleanupAndAllowNextCompare()
    {
        const string source = """
            using System;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class BadRoot : DomainObjectBase<BadRoot> { public BadRootChild Child { get; set; } }
            public partial class BadRootChild : DomainObjectBase<BadRootChild>
            {
                public string Name { get; set; }
                protected override void AddChanges(BadRootChild other) => throw new InvalidOperationException("boom");
            }
            public partial class Good : DomainObjectBase<Good> { public string Name { get; set; } }
            public static class Check
            {
                public static string Run()
                {
                    var failed = false;
                    try { new BadRoot { Child = new BadRootChild() }.GetChanges(new BadRoot { Child = new BadRootChild() }); }
                    catch (InvalidOperationException) { failed = true; }
                    var changes = new Good { Name = "old" }.GetChanges(new Good { Name = "new" }).ToList();
                    return failed + ":" + changes.Count + ":" + changes[0].PropertyName;
                }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        var type = Assembly.Load(result.AssemblyImage).GetType("Check", throwOnError: true);
        Assert.Equal("True:1:Name", type.GetMethod("Run").Invoke(null, null));
    }

    /// <summary>R05/DEC-03：同时实现 IEntity 与 IKey 时，GetKeys() 决定集合匹配。</summary>
    [Fact]
    public void GetChanges_WhenEntityImplementsIEntityAndIKey_ShouldUseGetKeys()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public List<Dual> Items { get; set; } }
            public partial class Dual : DomainObjectBase<Dual>, IEntity, IKey<int>
            {
                public int Id { get; set; }
                public string Key { get; set; }
                public string Name { get; set; }
                public void Init() { }
                public object[] GetKeys() => new object[] { Key };
            }
            public static class Check
            {
                public static string Run()
                {
                    var before = new Root { Items = new List<Dual> { new Dual { Id = 1, Key = "A", Name = "old" } } };
                    var after = new Root { Items = new List<Dual> { new Dual { Id = 2, Key = "B", Name = "new" } } };
                    return string.Join("|", before.GetChanges(after).Select(d => d.PropertyName));
                }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        var type = Assembly.Load(result.AssemblyImage).GetType("Check", throwOnError: true);
        // IKey.Id 从 1 变 2 但 GetKeys() 由 Key 决定：A→B 是身份替换，不是按 Id 更新。
        Assert.Equal("Items[A]|Items[B]", type.GetMethod("Run").Invoke(null, null));
    }

    /// <summary>R06：有键序列按身份报告增删，而不是只按 Math.Min 截断。</summary>
    [Fact]
    public void GetChanges_WhenKeyedSequenceAddsAndRemoves_ShouldReportStructuralEvents()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            using Bing.Domain.Entities;
            public partial class Root : DomainObjectBase<Root> { public List<Line> Lines { get; set; } }
            public partial class Line : DomainObjectBase<Line>, IKey<int>
            {
                public int Id { get; set; }
                public string Name { get; set; }
            }
            public static class Check
            {
                public static string Run()
                {
                    var before = new Root { Lines = new List<Line> { new Line { Id = 1, Name = "one" }, new Line { Id = 2, Name = "two" } } };
                    var after = new Root { Lines = new List<Line> { new Line { Id = 2, Name = "two" }, new Line { Id = 3, Name = "three" } } };
                    return string.Join("|", before.GetChanges(after).Select(d => d.PropertyName));
                }
            }
            """;

        var result = RunGenerator(source);
        AssertNoErrors(result.Diagnostics);
        var type = Assembly.Load(result.AssemblyImage).GetType("Check", throwOnError: true);
        Assert.Equal("Lines[1]|Lines[3]", type.GetMethod("Run").Invoke(null, null));
    }
}
