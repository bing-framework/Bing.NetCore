using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bing.Domain.Entities;
using Xunit;
using static Bing.Domain.ChangeTracking.Scenarios.ChangeScenarioAssertions;

namespace Bing.Domain.ChangeTracking.Scenarios;

/// <summary>
/// 一次比较结果、异常恢复、并发与格式化的生命周期测试。
/// </summary>
public class ChangeTrackingLifecycleScenarioTest
{
    /// <summary>LIF-01：后续比较不得更改先前返回的结果集合。</summary>
    [Fact]
    public void GetChanges_CalledRepeatedly_ReturnsIndependentResults()
    {
        var before = new ScenarioLifecycleValue { Text = "旧" };
        var first = before.GetChanges(new ScenarioLifecycleValue { Text = "新一" });
        var second = before.GetChanges(new ScenarioLifecycleValue { Text = "新二" });

        Assert.NotSame(first, second);
        AssertChanges(first, ("Text", "文本", "\"旧\"", "\"新一\""));
        AssertChanges(second, ("Text", "文本", "\"旧\"", "\"新二\""));
    }

    /// <summary>LIF-02：子对象抛异常后清理 frame，再次比较仍准确。</summary>
    [Fact]
    public void GetChanges_ChildHookThrows_CleansUpBeforeNextComparison()
    {
        var before = new ScenarioLifecycleParent { Child = new() { Text = "旧", ThrowOnCompare = true } };
        var after = new ScenarioLifecycleParent { Child = new() { Text = "新" } };

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Equal("子对象比较失败", error.Message);
        before.Child.ThrowOnCompare = false;
        AssertChanges(before.GetChanges(after), ("Child.Text", "文本", "\"旧\"", "\"新\""));
    }

    /// <summary>LIF-03：共享同一只读旧对象并发比较时结果相互隔离。</summary>
    [Fact]
    public void GetChanges_SameReadOnlyBeforeComparedConcurrently_IsolatesResults()
    {
        var before = new ScenarioLifecycleValue { Text = "原值" };
        var results = new ChangedValueDescriptorCollection[64];

        Parallel.For(0, results.Length, index =>
            results[index] = before.GetChanges(new ScenarioLifecycleValue { Text = "新值" + index }));

        for (var index = 0; index < results.Length; index++)
            AssertChanges(results[index], ("Text", "文本", "\"原值\"", "\"新值" + index + "\""));
    }

    /// <summary>LIF-04：深拷贝相等、自身比较和浅拷贝丢失旧值的边界。</summary>
    [Fact]
    public void GetChanges_SelfAndIndependentCopies_AreEqualWhileSharedMutationLosesOldValue()
    {
        var before = new ScenarioLifecycleParent { Child = new() { Text = "旧" } };
        var independent = new ScenarioLifecycleParent { Child = new() { Text = "旧" } };
        Assert.NotSame(before.Child, independent.Child);
        AssertChanges(before.GetChanges(before));
        AssertChanges(before.GetChanges(independent));

        var shallow = new ScenarioLifecycleParent { Child = before.Child };
        shallow.Child.Text = "新";
        Assert.Same(before.Child, shallow.Child);
        AssertChanges(before.GetChanges(shallow)); // 两侧已经指向同一个被修改的子对象，旧值不可恢复。
    }

    /// <summary>LIF-05：比较与展示保留字符串原值，并与当前区域设置无关。</summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("zh-CN")]
    public void GetChanges_TextAndNumberFormatting_IsCultureIndependent(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            AssertChanges(
                new ScenarioLifecycleValue { Text = null }.GetChanges(new ScenarioLifecycleValue { Text = "" }),
                ("Text", "文本", "null", "\"\""));
            AssertChanges(
                new ScenarioLifecycleValue { Text = " A " }.GetChanges(new ScenarioLifecycleValue { Text = "a" }),
                ("Text", "文本", "\" A \"", "\"a\""));
            AssertChanges(
                new ScenarioLifecycleValue { Text = "行\n\"旧\"" }.GetChanges(new ScenarioLifecycleValue { Text = "行\t新" }),
                ("Text", "文本", "\"行\\n\\\"旧\\\"\"", "\"行\\t新\""));
            AssertChanges(
                new ScenarioLifecycleValue { Amount = 1.5m }.GetChanges(new ScenarioLifecycleValue { Amount = 2.5m }),
                ("Amount", "金额", "1.5", "2.5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    /// <summary>LIF-06：区分根 null 入口、子对象删除与 null/空集合行为。</summary>
    [Fact]
    public void GetChanges_RootNullAndNullRelationships_AreHandledSeparately()
    {
        var root = new ScenarioLifecycleParent
        {
            Child = new() { Text = "旧", ThrowOnCompare = true, ThrowOnTextRead = true }
        };
        AssertChanges(root.GetChanges(null));
        AssertChanges(root.GetChanges(new ScenarioLifecycleParent { Child = null }),
            ("Child", "子对象", "<object>", "<missing>"));

        var nullCollection = new ScenarioLifecycleList { Items = null };
        var emptyCollection = new ScenarioLifecycleList { Items = new() };
        AssertChanges(nullCollection.GetChanges(emptyCollection));

        var withChild = new ScenarioLifecycleList
        {
            Items = new() { new() { Text = "旧", ThrowOnCompare = true, ThrowOnTextRead = true } }
        };
        var withNull = new ScenarioLifecycleList { Items = new() { null } };
        AssertChanges(withChild.GetChanges(withNull), ("Items[0]", "明细", "<object>", "<missing>"));
    }

    /// <summary>LIF-07：千条只枚举一次的明细中只报告预设的一处变化。</summary>
    [Fact]
    public void GetChanges_SingleUseEnumerableWithThousandLines_EnumeratesEachSideOnce()
    {
        var beforeItems = Enumerable.Range(1, 1000)
            .Select(id => new ScenarioOrderLine { Id = id, ProductId = id, Quantity = 1 })
            .ToArray();
        var afterItems = Enumerable.Range(1, 1000)
            .Select(id => new ScenarioOrderLine { Id = id, ProductId = id, Quantity = id == 333 ? 2 : 1 })
            .ToArray();
        var beforeSource = new SingleUseEnumerable<ScenarioOrderLine>(beforeItems);
        var afterSource = new SingleUseEnumerable<ScenarioOrderLine>(afterItems);
        var before = new ScenarioEnumerableOrder { Lines = beforeSource };
        var after = new ScenarioEnumerableOrder { Lines = afterSource };

        AssertChanges(before.GetChanges(after), ("Lines[333].Quantity", "数量", "1", "2"));
        Assert.Equal(1, beforeSource.EnumerationCount);
        Assert.Equal(1, afterSource.EnumerationCount);
    }

    /// <summary>LIF-07：手写关系辅助可验证运行时一次性枚举，与生成器支持范围分开。</summary>
    [Fact]
    public void GetChanges_ManualEnumerableHook_EnumeratesEachSideOnce()
    {
        var beforeItems = Enumerable.Range(1, 1000)
            .Select(id => new ScenarioOrderLine { Id = id, ProductId = id, Quantity = 1 })
            .ToArray();
        var afterItems = Enumerable.Range(1, 1000)
            .Select(id => new ScenarioOrderLine { Id = id, ProductId = id, Quantity = id == 333 ? 2 : 1 })
            .ToArray();
        var beforeSource = new SingleUseEnumerable<ScenarioOrderLine>(beforeItems);
        var afterSource = new SingleUseEnumerable<ScenarioOrderLine>(afterItems);

        var before = new ScenarioManualEnumerableOrder { Lines = beforeSource };
        var after = new ScenarioManualEnumerableOrder { Lines = afterSource };
        AssertChanges(before.GetChanges(after), ("Lines[333].Quantity", "数量", "1", "2"));
        Assert.Equal(1, beforeSource.EnumerationCount);
        Assert.Equal(1, afterSource.EnumerationCount);
    }

    /// <summary>LIF-08：交换比较方向时更新值和结构增删方向反转。</summary>
    [Fact]
    public void GetChanges_ReversedDirection_ReversesOldNewAndAddedRemoved()
    {
        var before = new ScenarioDirectionOrder
        {
            Status = "旧",
            Lines = new() { new() { Id = 1, Quantity = 2 }, new() { Id = 2, Quantity = 1 } }
        };
        var after = new ScenarioDirectionOrder
        {
            Status = "新",
            Lines = new() { new() { Id = 1, Quantity = 3 }, new() { Id = 3, Quantity = 1 } }
        };

        AssertChanges(before.GetChanges(after),
            ("Status", "状态", "\"旧\"", "\"新\""),
            ("Lines[1].Quantity", "数量", "2", "3"),
            ("Lines[2]", "明细", "<object>", "<missing>"),
            ("Lines[3]", "明细", "<missing>", "<object>"));
        AssertChanges(after.GetChanges(before),
            ("Status", "状态", "\"新\"", "\"旧\""),
            ("Lines[1].Quantity", "数量", "3", "2"),
            ("Lines[2]", "明细", "<missing>", "<object>"),
            ("Lines[3]", "明细", "<object>", "<missing>"));
    }
}

/// <summary>生命周期测试使用的标量领域对象。</summary>
internal partial class ScenarioLifecycleValue : DomainObjectBase<ScenarioLifecycleValue>
{
    [Description("文本")] public string Text { get; set; }
    [Description("金额")] public decimal Amount { get; set; }
}

/// <summary>生命周期测试使用的子对象容器。</summary>
internal partial class ScenarioLifecycleParent : DomainObjectBase<ScenarioLifecycleParent>
{
    [Description("子对象")] public ScenarioLifecycleChild Child { get; set; }
}

/// <summary>可验证删除比较不会读取属性或调用手写 hook 的子对象。</summary>
internal partial class ScenarioLifecycleChild : DomainObjectBase<ScenarioLifecycleChild>
{
    private string _text;

    public string Text
    {
        get => ThrowOnTextRead ? throw new InvalidOperationException("已删除子对象不应读取属性") : _text;
        set => _text = value;
    }

    public bool ThrowOnCompare { get; set; }
    public bool ThrowOnTextRead { get; set; }
    protected override void AddChanges(ScenarioLifecycleChild other)
    {
        if (ThrowOnCompare)
            throw new InvalidOperationException("子对象比较失败");
        AddChange(nameof(Text), "文本", Text, other.Text);
    }
}

/// <summary>用于记录集合不同 null 位置行为的父对象。</summary>
internal partial class ScenarioLifecycleList : DomainObjectBase<ScenarioLifecycleList>
{
    [Description("明细")] public List<ScenarioLifecycleChild> Items { get; set; }
}

/// <summary>允许测试一次性枚举源的订单。</summary>
internal partial class ScenarioEnumerableOrder : DomainObjectBase<ScenarioEnumerableOrder>
{
    [Description("明细")] public IEnumerable<ScenarioOrderLine> Lines { get; set; }
}

/// <summary>手写调用集合辅助方法，单独覆盖运行时对一次性枚举源的处理。</summary>
internal partial class ScenarioManualEnumerableOrder : DomainObjectBase<ScenarioManualEnumerableOrder>
{
    public IEnumerable<ScenarioOrderLine> Lines { get; set; }

    protected override void AddChanges(ScenarioManualEnumerableOrder other) =>
        AddCollectionChanges(nameof(Lines), "明细", Lines, other.Lines);
}

/// <summary>用于验证比较方向的订单。</summary>
internal partial class ScenarioDirectionOrder : DomainObjectBase<ScenarioDirectionOrder>
{
    [Description("状态")] public string Status { get; set; }
    [Description("明细")] public List<ScenarioOrderLine> Lines { get; set; } = new();
}

/// <summary>禁止第二次枚举的测试数据源。</summary>
internal sealed class SingleUseEnumerable<T> : IEnumerable<T>
{
    private readonly IEnumerable<T> _values;
    public int EnumerationCount { get; private set; }

    public SingleUseEnumerable(IEnumerable<T> values) => _values = values;

    public IEnumerator<T> GetEnumerator()
    {
        EnumerationCount++;
        if (EnumerationCount > 1)
            throw new InvalidOperationException("数据源不允许重复枚举");
        return _values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
