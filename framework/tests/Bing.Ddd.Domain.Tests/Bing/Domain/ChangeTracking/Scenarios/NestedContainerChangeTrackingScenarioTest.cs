using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Bing.Domain.Entities;
using Xunit;
using static Bing.Domain.ChangeTracking.Scenarios.ChangeScenarioAssertions;

namespace Bing.Domain.ChangeTracking.Scenarios;

/// <summary>核对多层容器对自身比较规则、路径和异常上下文的保留。</summary>
public class NestedContainerChangeTrackingScenarioTest
{
    /// <summary>生成器识别的 Version 标量在 Set 中也必须按成员值匹配。</summary>
    [Fact]
    public void GetChanges_VersionSet_ReportsMembershipChanges()
    {
        var before = new ScenarioVersionSet { Versions = new() { new Version(1, 0), new Version(2, 0) } };
        var after = new ScenarioVersionSet { Versions = new() { new Version(2, 0), new Version(3, 0) } };

        AssertChanges(before.GetChanges(after),
            ("Versions[1.0]", "Versions", "1.0", "<missing>"),
            ("Versions[3.0]", "Versions", "<missing>", "3.0"));
    }

    /// <summary>字典、列表、字典、Set 连续嵌套时，逐层保留各自的键和成员语义。</summary>
    [Fact]
    public void GetChanges_DeepContainers_PreserveComparersAndReportSetMembership()
    {
        var before = new ScenarioDeepContainers
        {
            Groups = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Depot"] = new() { new(StringComparer.OrdinalIgnoreCase) { ["SKU"] = new() { 1, 2 } } }
            }
        };
        var after = new ScenarioDeepContainers
        {
            Groups = new(StringComparer.OrdinalIgnoreCase)
            {
                ["depot"] = new() { new(StringComparer.OrdinalIgnoreCase) { ["sku"] = new() { 2, 3 } } }
            }
        };

        AssertChanges(before.GetChanges(after),
            ("Groups{Depot}[0]{SKU}[1]", "分组", "1", "<missing>"),
            ("Groups{Depot}[0]{SKU}[3]", "分组", "<missing>", "3"));
    }

    /// <summary>不同深度的字典新增和删除分别使用各自的容器路径。</summary>
    [Fact]
    public void GetChanges_NestedDictionary_AddsAndRemovesContainersOnce()
    {
        var before = new ScenarioDeepContainers
        {
            Groups = new() { ["A"] = new() { new() { ["old"] = new() { 1 } } } }
        };
        var after = new ScenarioDeepContainers
        {
            Groups = new() { ["A"] = new() { new() { ["new"] = new() { 2 } } } }
        };

        AssertChanges(before.GetChanges(after),
            ("Groups{A}[0]{old}", "分组", "<collection>", "<missing>"),
            ("Groups{A}[0]{new}", "分组", "<missing>", "<collection>"));
    }

    /// <summary>字典内层一侧为 null 时，按容器替换记录而不是拆成所有元素。</summary>
    [Fact]
    public void GetChanges_NestedContainerNull_ReportsOneStructuralChange()
    {
        var before = new ScenarioDeepContainers { Groups = new() { ["A"] = null } };
        var after = new ScenarioDeepContainers { Groups = new() { ["A"] = new() { new() } } };

        AssertChanges(before.GetChanges(after), ("Groups{A}", "分组", "<missing>", "<collection>"));
    }

    /// <summary>内层字典比较器不同，错误应包含从领域对象根开始的完整路径，且后续比较恢复。</summary>
    [Fact]
    public void GetChanges_NestedDictionaryComparerMismatch_ReportsFullPathAndRestoresSession()
    {
        var before = new ScenarioNestedParent
        {
            Child = new() { Values = new() { ["outer"] = new(StringComparer.OrdinalIgnoreCase) { ["A"] = 1 } } }
        };
        var after = new ScenarioNestedParent
        {
            Child = new() { Values = new() { ["outer"] = new(StringComparer.Ordinal) { ["A"] = 1 } } }
        };

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Child.Values{outer}", error.Message);

        after.Child.Values["outer"] = new(StringComparer.OrdinalIgnoreCase) { ["a"] = 2 };
        AssertChanges(before.GetChanges(after), ("Child.Values{outer}{A}", "属性", "1", "2"));
    }

    /// <summary>内层 Set 的比较器不能被外层字典或集合比较器替换。</summary>
    [Fact]
    public void GetChanges_NestedSetComparerMismatch_ReportsFullPath()
    {
        var before = new ScenarioNestedParent
        {
            Child = new() { Tags = new() { ["A"] = new(StringComparer.OrdinalIgnoreCase) { "one" } } }
        };
        var after = new ScenarioNestedParent
        {
            Child = new() { Tags = new() { ["A"] = new(StringComparer.Ordinal) { "one" } } }
        };

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Child.Tags{A}", error.Message);
    }

    /// <summary>手写 AddChanges 也可使用强类型配对方法保留嵌套 Set 语义。</summary>
    [Fact]
    public void GetChanges_ManualNestedRule_UsesContainerComparer()
    {
        var before = new ScenarioManualNested { Tags = new() { ["A"] = new(StringComparer.OrdinalIgnoreCase) { "X", "Y" } } };
        var after = new ScenarioManualNested { Tags = new() { ["A"] = new(StringComparer.OrdinalIgnoreCase) { "y", "x" } } };

        AssertChanges(before.GetChanges(after));
    }

    /// <summary>嵌套排序字典和排序 Set 使用自身排序比较器判断相等。</summary>
    [Fact]
    public void GetChanges_NestedSortedContainers_PreserveOrderingComparers()
    {
        var before = new ScenarioSortedContainers
        {
            Prices = new() { ["retail"] = new(StringComparer.OrdinalIgnoreCase) { ["SKU"] = 1 } },
            Tags = new() { ["retail"] = new(StringComparer.OrdinalIgnoreCase) { "red", "BLUE" } }
        };
        var after = new ScenarioSortedContainers
        {
            Prices = new() { ["retail"] = new(StringComparer.OrdinalIgnoreCase) { ["sku"] = 2 } },
            Tags = new() { ["retail"] = new(StringComparer.OrdinalIgnoreCase) { "blue", "RED" } }
        };

        AssertChanges(before.GetChanges(after), ("Prices{retail}{SKU}", "价格", "1", "2"));
    }

    /// <summary>排序字典两侧比较器不一致时给出完整的容器路径。</summary>
    [Fact]
    public void GetChanges_NestedSortedComparerMismatch_ThrowsWithPath()
    {
        var before = new ScenarioSortedContainers
        {
            Prices = new() { ["retail"] = new(StringComparer.OrdinalIgnoreCase) { ["SKU"] = 1 } }
        };
        var after = new ScenarioSortedContainers
        {
            Prices = new() { ["retail"] = new(StringComparer.Ordinal) { ["SKU"] = 1 } }
        };

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Prices{retail}", error.Message);
    }

    /// <summary>无法读取内部比较器的字典包装器应明确拒绝，不能按默认比较器误报。</summary>
    [Fact]
    public void GetChanges_UnknownDictionaryComparer_ThrowsInsteadOfUsingDefault()
    {
        var before = new ScenarioWrappedDictionary
        {
            Values = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["A"] = 1 })
        };
        var after = new ScenarioWrappedDictionary
        {
            Values = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["a"] = 1 })
        };

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Values", error.Message);
    }

    /// <summary>只读 Set 接口指向标准 HashSet 时仍使用实例的比较器。</summary>
    [Fact]
    public void GetChanges_ReadOnlySetInterface_UsesConcreteSetComparer()
    {
        var before = new ScenarioReadOnlySetRoot
            { Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A" } };
        var after = new ScenarioReadOnlySetRoot
            { Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a" } };

        AssertChanges(before.GetChanges(after));
    }

    /// <summary>无法读取比较器的只读 Set 不能误按索引比较。</summary>
    [Fact]
    public void GetChanges_CustomReadOnlySet_ThrowsInsteadOfUsingSequenceSemantics()
    {
        var before = new ScenarioReadOnlySetRoot
            { Tags = new ScenarioReadOnlySet<string>(StringComparer.OrdinalIgnoreCase, "A", "B") };
        var after = new ScenarioReadOnlySetRoot
            { Tags = new ScenarioReadOnlySet<string>(StringComparer.OrdinalIgnoreCase, "b", "a") };

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Tags", error.Message);
    }

    /// <summary>嵌套只读 Set 同样拒绝丢失比较器，并指出完整路径。</summary>
    [Fact]
    public void GetChanges_NestedCustomReadOnlySet_ThrowsWithFullPath()
    {
        var before = new ScenarioReadOnlySetRoot
            { Groups = new() { ["stock"] = new ScenarioReadOnlySet<string>(StringComparer.OrdinalIgnoreCase, "A") } };
        var after = new ScenarioReadOnlySetRoot
            { Groups = new() { ["stock"] = new ScenarioReadOnlySet<string>(StringComparer.OrdinalIgnoreCase, "a") } };

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Groups{stock}", error.Message);
    }
}

/// <summary>四层容器的自动生成示例。</summary>
internal partial class ScenarioDeepContainers : DomainObjectBase<ScenarioDeepContainers>
{
    [Description("分组")]
    public Dictionary<string, List<Dictionary<string, HashSet<int>>>> Groups { get; set; } = new();
}

/// <summary>用于验证子领域对象内完整异常路径的父对象。</summary>
internal partial class ScenarioNestedParent : DomainObjectBase<ScenarioNestedParent>
{
    public ScenarioNestedChild Child { get; set; }
}

/// <summary>带有独立嵌套字典和 Set 的子领域对象。</summary>
internal partial class ScenarioNestedChild : DomainObjectBase<ScenarioNestedChild>
{
    [Description("属性")] public Dictionary<string, Dictionary<string, int>> Values { get; set; } = new();
    public Dictionary<string, HashSet<string>> Tags { get; set; } = new();
}

/// <summary>手写嵌套配对的领域对象。</summary>
internal partial class ScenarioManualNested : DomainObjectBase<ScenarioManualNested>
{
    public Dictionary<string, HashSet<string>> Tags { get; set; } = new();

    protected override void AddChanges(ScenarioManualNested other)
    {
        foreach (var pair in GetNestedDictionaryPairs(nameof(Tags), "标签", Tags, other.Tags))
            AddCollectionChanges(pair.Path, "标签", pair.OldValue, pair.NewValue);
    }
}

/// <summary>包含排序字典和排序 Set 的多层容器。</summary>
internal partial class ScenarioSortedContainers : DomainObjectBase<ScenarioSortedContainers>
{
    [Description("价格")] public Dictionary<string, SortedDictionary<string, int>> Prices { get; set; } = new();
    public Dictionary<string, SortedSet<string>> Tags { get; set; } = new();
}

/// <summary>验证不公开比较器的字典不会丢失键语义。</summary>
internal partial class ScenarioWrappedDictionary : DomainObjectBase<ScenarioWrappedDictionary>
{
    public IReadOnlyDictionary<string, int> Values { get; set; }

    protected override void AddChanges(ScenarioWrappedDictionary other) =>
        AddDictionaryChanges(nameof(Values), "属性", Values, other.Values);
}

/// <summary>通过只读接口暴露标准或自定义 Set。</summary>
internal partial class ScenarioReadOnlySetRoot : DomainObjectBase<ScenarioReadOnlySetRoot>
{
    public IReadOnlySet<string> Tags { get; set; }
    public Dictionary<string, IReadOnlySet<string>> Groups { get; set; }
}

/// <summary>覆盖生成器和运行时一致识别的 Version 标量。</summary>
internal partial class ScenarioVersionSet : DomainObjectBase<ScenarioVersionSet>
{
    public HashSet<Version> Versions { get; set; } = new();
}

/// <summary>不公开底层比较器、也不实现 ISet 的只读 Set。</summary>
internal sealed class ScenarioReadOnlySet<T> : IReadOnlySet<T>
{
    private readonly HashSet<T> _items;

    public ScenarioReadOnlySet(IEqualityComparer<T> comparer, params T[] items) =>
        _items = new HashSet<T>(items, comparer);

    public int Count => _items.Count;
    public bool Contains(T item) => _items.Contains(item);
    public bool IsProperSubsetOf(IEnumerable<T> other) => _items.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<T> other) => _items.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<T> other) => _items.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<T> other) => _items.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<T> other) => _items.Overlaps(other);
    public bool SetEquals(IEnumerable<T> other) => _items.SetEquals(other);
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
