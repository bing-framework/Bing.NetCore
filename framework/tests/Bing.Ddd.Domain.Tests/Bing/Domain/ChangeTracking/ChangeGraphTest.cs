using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Auditing;
using Bing.Domain.Entities;
using Shouldly;
using Xunit;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 验证源生成对象图对嵌套对象、集合、字典、Set 和忽略规则的支持。
/// </summary>
public class ChangeGraphTest
{
    /// <summary>
    /// 验证比较发现嵌套对象及集合元素变更。
    /// </summary>
    [Fact]
    public void Compare_ShouldFindNestedObjectAndCollectionChanges()
    {
        var before = CreateGraph();
        var after = CreateGraph();
        after.Customer.Name = "新客户";
        after.Lines.Single(line => line.Id == 10).Amount = 15m;
        after.LineMap["main"].Product.Name = "新产品";
        after.Tags[1] = "changed";

        var result = new ChangeGraphTracker().Compare(before, after);

        result.HasChanges.ShouldBeTrue();
        result.Changes.Select(change => change.Path.Value).ShouldBe(new[]
        {
            "Customer.Name", "LineMap{main}.Product.Name", "Lines[10].Amount", "Tags[1]"
        });
        result.Changes.Select(change => change.Kind).ShouldBe(Enumerable.Repeat(ChangeKind.Modified, 4));
        result.Changes.Select(change => change.OldValue.Value).ShouldBe(new object[] { "客户", "映射产品", 10m, "two" });
        result.Changes.Select(change => change.NewValue.Value).ShouldBe(new object[] { "新客户", "新产品", 15m, "changed" });
    }

    /// <summary>
    /// 验证集合顺序改变时仍按实体标识匹配元素。
    /// </summary>
    [Fact]
    public void Compare_ShouldMatchEntityCollectionById_WhenOrderChanges()
    {
        var before = CreateGraph();
        var after = CreateGraph();
        after.Lines.Reverse();
        after.Lines[1].Amount = 22m;

        var result = new ChangeGraphTracker(ChangeGraphOptions.Default.WithTrackMoves()).Compare(before, after);

        result.Changes.Select(change => change.Path.Value).ShouldContain("Lines[10].Amount");
        result.Changes.Any(change => change.Path.Value == "Lines[0].Amount").ShouldBeFalse();
        result.Changes.Any(change => change.Kind == ChangeKind.Moved).ShouldBeTrue();
    }

    /// <summary>
    /// 验证默认比较忽略集合顺序。
    /// </summary>
    [Fact]
    public void Compare_ShouldIgnoreCollectionOrderByDefault()
    {
        var before = CreateGraph();
        var after = CreateGraph();
        after.Lines.Reverse();

        var result = new ChangeGraphTracker().Compare(before, after);

        result.Changes.ShouldBeEmpty();
    }

    /// <summary>
    /// 验证嵌套集合使用复合变更键匹配元素。
    /// </summary>
    [Fact]
    public void Compare_ShouldUseCompositeChangeKeyForNestedCollection()
    {
        var before = CreateGraph();
        var after = CreateGraph();
        after.CompositeLines.Single(line => line.Sku == "A" && line.Lot == 1).Value = "changed";
        after.CompositeLines.Reverse();

        var result = new ChangeGraphTracker().Compare(before, after);

        result.Changes.Count.ShouldBe(1);
        result.Changes[0].Path.Value.ShouldBe("CompositeLines[A,1].Value");
        result.Changes[0].Kind.ShouldBe(ChangeKind.Modified);
        result.Changes[0].OldValue.GetScalar<string>().ShouldBe("A1");
        result.Changes[0].NewValue.GetScalar<string>().ShouldBe("changed");
    }

    /// <summary>
    /// 验证比较忽略标记属性及审计时间。
    /// </summary>
    [Fact]
    public void Compare_ShouldIgnoreMarkedPropertiesAndAuditTime()
    {
        var before = CreateGraph();
        var after = CreateGraph();
        after.CreationTime = before.CreationTime.Value.AddDays(1);
        after.Customer.Secret = "changed";

        var result = new ChangeGraphTracker().Compare(before, after);

        result.HasChanges.ShouldBeFalse();
    }

    /// <summary>
    /// 验证快照保留对象修改前的状态。
    /// </summary>
    [Fact]
    public void CaptureAndCompareSnapshot_ShouldKeepBeforeState()
    {
        var source = CreateGraph();
        var tracker = new ChangeGraphTracker();
        var snapshot = tracker.Capture(source);
        source.Customer.Name = "changed after capture";

        var result = tracker.CompareSnapshot(snapshot, source);

        result.Changes.Select(change => change.Path.Value).ShouldContain("Customer.Name");
    }

    /// <summary>
    /// 验证关系级忽略在读取子对象与集合前短路。
    /// </summary>
    [Fact]
    public void Compare_ShouldSkipIgnoredRelationBeforeReadingNestedGetter()
    {
        var before = new PathGraph();
        var after = new PathGraph();

        var result = new ChangeGraphTracker().Compare(before, after);

        result.HasChanges.ShouldBeFalse();
    }

    /// <summary>
    /// 验证标量集合按无序值比较。
    /// </summary>
    [Fact]
    public void Compare_ShouldTreatScalarSetAsUnorderedValues()
    {
        var before = new OrderGraph { TagSet = new HashSet<string> { "one", "two" } };
        var after = new OrderGraph { TagSet = new HashSet<string> { "two", "three" } };

        var result = new ChangeGraphTracker().Compare(before, after);

        result.Changes.Count.ShouldBe(2);
        result.Changes.Any(change => change.Kind == ChangeKind.Removed).ShouldBeTrue();
        result.Changes.Any(change => change.Kind == ChangeKind.Added).ShouldBeTrue();
    }

    /// <summary>
    /// 验证递归子对象图比较可终止。
    /// </summary>
    [Fact]
    public void Compare_ShouldTerminateRecursiveChildGraphs()
    {
        var before = new RecursiveGraph { Root = new RecursiveNode { Name = "old" } };
        before.Root.Next = before.Root;
        var after = new RecursiveGraph { Root = new RecursiveNode { Name = "new" } };
        after.Root.Next = after.Root;

        var result = new ChangeGraphTracker().Compare(before, after);

        result.Changes.Select(change => change.Path.Value).ShouldContain("Root.Name");
    }

    /// <summary>
    /// 验证未初始化的实体键被拒绝并报告集合路径。
    /// </summary>
    [Fact]
    public void Capture_ShouldRejectUninitializedEntityKeysWithCollectionPath()
    {
        var graph = new OrderGraph
        {
            Lines = new List<OrderLine> { new OrderLine { Id = 0, Amount = 1m } }
        };

        Should.Throw<InvalidOperationException>(() => new ChangeGraphTracker().Capture(graph))
            .Message.ShouldContain("Lines[0]");
    }

    /// <summary>
    /// 验证属性读取失败被包装并携带成员路径。
    /// </summary>
    [Fact]
    public void Capture_ShouldWrapGetterFailureWithMemberPath()
    {
        var exception = Should.Throw<ChangeGraphCaptureException>(() =>
            new ChangeGraphTracker().Capture(new ThrowingGraph()));

        exception.Path.ShouldBe("Value");
    }

    /// <summary>
    /// 验证空根对象比较可明确表示新增及删除。
    /// </summary>
    [Fact]
    public void Compare_ShouldHandleNullRootWithoutOverloadAmbiguity()
    {
        var graph = CreateGraph();

        var added = new ChangeGraphTracker().Compare<OrderGraph>(null, graph);
        var removed = new ChangeGraphTracker().Compare<OrderGraph>(graph, null);

        added.Changes.Single().Kind.ShouldBe(ChangeKind.Added);
        removed.Changes.Single().Kind.ShouldBe(ChangeKind.Removed);
    }

    /// <summary>
    /// 验证调用方集合键覆盖入口已删除，元素身份由实体自身 IKey 决定。
    /// </summary>
    [Fact]
    public void Compare_ShouldUseEntityOwnedIdentityInsteadOfCallerOverride()
    {
        // DEC-04：WithCollectionKey/WithIdentity 已被删除，父级不能为元素另选键。
        Assert.Null(typeof(ChangeGraphOptions).GetMethod("WithCollectionKey"));
        Assert.Null(typeof(ChangeGraphOptions).GetMethod("WithIdentity"));

        var before = new CustomKeyGraph
        {
            Lines = new List<CustomKeyLine> { new CustomKeyLine { Id = 1, ClientKey = "A", Name = "old" } }
        };
        var after = new CustomKeyGraph
        {
            Lines = new List<CustomKeyLine> { new CustomKeyLine { Id = 2, ClientKey = "A", Name = "new" } }
        };

        var result = new ChangeGraphTracker().Compare(before, after);

        result.Changes.Select(change => change.Path.Value).ShouldBe(new[] { "Lines[1]", "Lines[2]" });
    }

    /// <summary>
    /// 验证新增对象提供冻结的结构化载荷。
    /// </summary>
    [Fact]
    public void Compare_ShouldExposeFrozenStructuredPayloadForAddedObject()
    {
        var before = new CustomKeyGraph { Lines = new List<CustomKeyLine>() };
        var after = new CustomKeyGraph
        {
            Lines = new List<CustomKeyLine>
            {
                new CustomKeyLine { Id = 1, ClientKey = "A", Name = "新增" }
            }
        };

        var result = new ChangeGraphTracker().Compare(before, after);
        var change = result.Changes.Single(item => item.Kind == ChangeKind.Added);

        change.NewValue.Kind.ShouldBe(ChangeValueKind.Object);
        change.NewValue.Members["Name"].GetScalar<string>().ShouldBe("新增");
    }

    /// <summary>
    /// 验证同一 IKey 身份下只比较字段差异，不伪报新增删除。
    /// </summary>
    [Fact]
    public void Compare_ShouldMatchEntityIdentityAndReportOnlyFieldChanges()
    {
        var before = new CustomKeyGraph
        {
            Lines = new List<CustomKeyLine> { new CustomKeyLine { Id = 1, ClientKey = "A", Name = "old" } }
        };
        var after = new CustomKeyGraph
        {
            Lines = new List<CustomKeyLine> { new CustomKeyLine { Id = 1, ClientKey = "B", Name = "new" } }
        };

        var result = new ChangeGraphTracker().Compare(before, after);

        result.Changes.Select(change => change.Path.Value).ShouldBe(new[] { "Lines[1].ClientKey", "Lines[1].Name" });
    }

    /// <summary>
    /// DEC-04：调用方标量 comparer 不得改写领域实体字段的差异判定。
    /// </summary>
    [Fact]
    public void Compare_WhenComparerConfiguredForDomainEntity_ShouldNotOverrideDomainRules()
    {
        var before = new DomainComparerRoot { Name = "old" };
        var after = new DomainComparerRoot { Name = "OLD" };
        var options = ChangeGraphOptions.Default.WithComparer<string>(StringComparer.OrdinalIgnoreCase);

        var result = new ChangeGraphTracker(options).Compare(before, after);

        // 领域实体字段使用自身类型值判等，调用方 comparer 不生效。
        var change = Assert.Single(result.Changes);
        Assert.Equal("Name", change.Path.Value);
        Assert.Equal("old", change.OldValue.GetScalar<string>());
        Assert.Equal("OLD", change.NewValue.GetScalar<string>());
    }

    /// <summary>
    /// 验证标量 comparer 仍可用于非领域结构适配器。
    /// </summary>
    [Fact]
    public void Compare_WhenComparerConfiguredForStructureAdapter_ShouldApplyToStructureScalars()
    {
        var model = new ChangeGraphModel<StructureComparerRoot>("structure", (writer, value) =>
            writer.Scalar(nameof(StructureComparerRoot.Name), value.Name));
        var options = ChangeGraphOptions.Default
            .WithAdapter(model)
            .WithComparer<string>(StringComparer.OrdinalIgnoreCase);

        var result = new ChangeGraphTracker(options).Compare(
            new StructureComparerRoot { Name = "ABC" },
            new StructureComparerRoot { Name = "abc" });

        Assert.Empty(result.Changes);
    }

    /// <summary>
    /// 验证快照不能用于具有不同规则的跟踪器。
    /// </summary>
    [Fact]
    public void CompareSnapshot_ShouldRejectAnotherTrackerRules()
    {
        var source = CreateGraph();
        var snapshot = new ChangeGraphTracker().Capture(source);

        var action = () => new ChangeGraphTracker().CompareSnapshot(snapshot, source);

        Should.Throw<InvalidOperationException>(action);
    }

    /// <summary>
    /// 验证比较遍历嵌套集合及字典值。
    /// </summary>
    [Fact]
    public void Compare_ShouldTraverseNestedCollectionsAndDictionaryValues()
    {
        var before = new NestedCollectionGraph
        {
            Groups = new List<List<OrderProduct>>
            {
                new List<OrderProduct> { new OrderProduct { Name = "old" }, new OrderProduct { Name = "same" } }
            },
            Map = new Dictionary<string, List<OrderProduct>>
            {
                ["main"] = new List<OrderProduct> { new OrderProduct { Name = "map-old" } }
            }
        };
        var after = new NestedCollectionGraph
        {
            Groups = new List<List<OrderProduct>>
            {
                new List<OrderProduct> { new OrderProduct { Name = "new" }, new OrderProduct { Name = "same" } }
            },
            Map = new Dictionary<string, List<OrderProduct>>
            {
                ["main"] = new List<OrderProduct> { new OrderProduct { Name = "map-new" } }
            }
        };

        var result = new ChangeGraphTracker().Compare(before, after);

        result.Changes.Select(change => change.Path.Value).ShouldBe(new[] { "Groups[0][0].Name", "Map{main}[0].Name" });
        result.Changes.Select(change => change.Kind).ShouldBe(new[] { ChangeKind.Modified, ChangeKind.Modified });
        result.Changes.Select(change => change.OldValue.Value).ShouldBe(new object[] { "old", "map-old" });
        result.Changes.Select(change => change.NewValue.Value).ShouldBe(new object[] { "new", "map-new" });
    }

    /// <summary>
    /// 创建嵌套对象及集合变更测试的订单图。
    /// </summary>
    /// <returns>包含嵌套对象、集合及审计属性的订单图。</returns>
    private static OrderGraph CreateGraph()
    {
        return new OrderGraph
        {
            Id = 1,
            CreationTime = new DateTime(2026, 1, 1),
            LastModificationTime = new DateTime(2026, 1, 2),
            Customer = new OrderCustomer { Name = "客户", Secret = "secret" },
            Lines = new List<OrderLine>
            {
                new OrderLine { Id = 10, Amount = 10m, Product = new OrderProduct { Name = "产品" } },
                new OrderLine { Id = 20, Amount = 20m, Product = new OrderProduct { Name = "产品2" } }
            },
            LineMap = new Dictionary<string, OrderLine>
            {
                ["main"] = new OrderLine { Id = 30, Amount = 30m, Product = new OrderProduct { Name = "映射产品" } }
            },
            LineSet = new HashSet<OrderLine>
            {
                new OrderLine { Id = 40, Amount = 40m, Product = new OrderProduct { Name = "Set产品" } }
            },
            CompositeLines = new List<CompositeLine>
            {
                new CompositeLine { Sku = "A", Lot = 1, Value = "A1" },
                new CompositeLine { Sku = "B", Lot = 2, Value = "B2" }
            },
            Tags = new List<string> { "one", "two" }
        };
    }
}

/// <summary>
/// 包含订单节点、集合与审计时间的对象图模型。
/// </summary>
public partial class OrderGraph : DomainObjectBase<OrderGraph>, IHasCreationTime, IHasModificationTime
{
    /// <summary>
    /// 获取或设置主键。
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// 获取或设置客户数据。
    /// </summary>
    public OrderCustomer Customer { get; set; }
    /// <summary>
    /// 获取或设置订单行集合。
    /// </summary>
    public List<OrderLine> Lines { get; set; }
    /// <summary>
    /// 获取或设置按键索引的订单行字典。
    /// </summary>
    public Dictionary<string, OrderLine> LineMap { get; set; }
    /// <summary>
    /// 获取或设置订单行集合。
    /// </summary>
    public HashSet<OrderLine> LineSet { get; set; }
    /// <summary>
    /// 获取或设置按组合键匹配的行集合。
    /// </summary>
    public List<CompositeLine> CompositeLines { get; set; }
    /// <summary>
    /// 获取或设置标签序列。
    /// </summary>
    public List<string> Tags { get; set; }
    /// <summary>
    /// 获取或设置标签集合。
    /// </summary>
    public HashSet<string> TagSet { get; set; }
    /// <inheritdoc />
    public DateTime? CreationTime { get; set; }

    /// <inheritdoc />
    public DateTime? LastModificationTime { get; set; }
}

/// <summary>
/// 验证订单行路径匹配的对象图模型。
/// </summary>
public partial class PathGraph : DomainObjectBase<PathGraph>
{
    /// <summary>
    /// 获取或设置订单行集合；关系级忽略后不读取、不枚举。
    /// </summary>
    [ChangeIgnore]
    public List<OrderLine> Lines
    {
        get => throw new InvalidOperationException("被忽略的关系不应被读取");
        set { }
    }
}

/// <summary>
/// 验证同一节点在不同路径下的忽略规则的对象图模型。
/// </summary>
/// <summary>
/// 验证递归引用的对象图模型。
/// </summary>
public partial class RecursiveGraph : DomainObjectBase<RecursiveGraph>
{
    /// <summary>
    /// 获取或设置根节点。
    /// </summary>
    public RecursiveNode Root { get; set; }
}

/// <summary>
/// 包含后继引用的递归节点。
/// </summary>
public class RecursiveNode
{
    /// <summary>
    /// 获取或设置名称。
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 获取或设置后继节点。
    /// </summary>
    public RecursiveNode Next { get; set; }
}

/// <summary>
/// 验证属性读取失败处理的对象图模型。
/// </summary>
public partial class ThrowingGraph : DomainObjectBase<ThrowingGraph>
{
    /// <summary>
    /// 获取读取时引发异常的测试值。
    /// </summary>
    /// <remarks>用于验证属性读取异常的捕获与报告。</remarks>
    public string Value => throw new InvalidOperationException("测试 getter");
}

/// <summary>
/// 验证自定义集合匹配键的对象图模型。
/// </summary>
public partial class CustomKeyGraph : DomainObjectBase<CustomKeyGraph>
{
    /// <summary>
    /// 获取或设置订单行集合。
    /// </summary>
    public List<CustomKeyLine> Lines { get; set; }
}

/// <summary>
/// 包含默认主键与业务匹配键的测试行。
/// </summary>
public class CustomKeyLine : Bing.Domain.Entities.IKey<int>
{
    /// <inheritdoc />
    public int Id { get; set; }
    /// <summary>
    /// 获取或设置业务匹配键。
    /// </summary>
    public string ClientKey { get; set; }
    /// <summary>
    /// 获取或设置名称。
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// 包含嵌套序列与字典值集合的对象图模型。
/// </summary>
public partial class NestedCollectionGraph : DomainObjectBase<NestedCollectionGraph>
{
    /// <summary>
    /// 获取或设置嵌套节点分组。
    /// </summary>
    public List<List<OrderProduct>> Groups { get; set; }
    /// <summary>
    /// 获取或设置按键索引的产品分组。
    /// </summary>
    public Dictionary<string, List<OrderProduct>> Map { get; set; }
}

/// <summary>
/// 包含无稳定标识对象集合的对象图模型。
/// </summary>
/// <summary>
/// 包含姓名与忽略属性的订单客户测试数据。
/// </summary>

public class OrderCustomer
{
    /// <summary>
    /// 获取或设置客户姓名。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 保存订单客户对象中被忽略的密钥字段。
    /// </summary>
    private string _secret;

    /// <summary>
    /// 获取或设置应被忽略的客户私密值。
    /// </summary>
    /// <remarks>此属性在变更捕获时被忽略；直接读取会引发异常。</remarks>
    [ChangeIgnore]
    public string Secret
    {
        get => throw new InvalidOperationException("被 ChangeIgnore 的 getter 不应执行");
        set => _secret = value;
    }
}

/// <summary>
/// 订单对象图中的产品测试数据。
/// </summary>

public class OrderProduct
{
    /// <summary>
    /// 获取或设置产品名称。
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// 包含金额与产品的带键订单行。
/// </summary>
public class OrderLine : Bing.Domain.Entities.IKey<int>
{
    /// <inheritdoc />
    public int Id { get; set; }
    /// <summary>
    /// 获取或设置金额。
    /// </summary>
    public decimal Amount { get; set; }
    /// <summary>
    /// 获取或设置产品数据。
    /// </summary>
    public OrderProduct Product { get; set; }
}

/// <summary>
/// 领域实体 comparer 隔离测试使用的实体。
/// </summary>
public partial class DomainComparerRoot : Bing.Domain.Entities.DomainObjectBase<DomainComparerRoot>
{
    /// <summary>
    /// 获取或设置名称。
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// 非领域结构适配器测试使用的普通类。
/// </summary>
public class StructureComparerRoot
{
    /// <summary>
    /// 获取或设置名称。
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// 通过商品编码与批次组合匹配的测试行。
/// </summary>

public class CompositeLine : Bing.Domain.Entities.IEntity
{
    /// <summary>
    /// 获取或设置商品编码。
    /// </summary>
    public string Sku { get; set; }

    /// <summary>
    /// 获取或设置批次号。
    /// </summary>
    public int Lot { get; set; }

    /// <summary>
    /// 获取或设置成员值。
    /// </summary>
    public string Value { get; set; }

    /// <inheritdoc />
    public void Init() { }

    /// <inheritdoc />
    public object[] GetKeys() => new object[] { Sku, Lot };

    /// <inheritdoc />
    public Bing.Validation.IValidationResult Validate() => new Bing.Validation.ValidationResultCollection();
}
