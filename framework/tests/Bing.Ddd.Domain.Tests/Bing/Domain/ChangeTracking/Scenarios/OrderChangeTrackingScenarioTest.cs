using System;
using System.Collections.Generic;
using System.ComponentModel;
using Bing.Auditing;
using Bing.Domain.Entities;
using Xunit;
using static Bing.Domain.ChangeTracking.Scenarios.ChangeScenarioAssertions;

namespace Bing.Domain.ChangeTracking.Scenarios;

/// <summary>
/// 订单和订单明细的变更跟踪业务场景。
/// </summary>
public class OrderChangeTrackingScenarioTest
{
    private static ScenarioOrder CreateOrder() => new()
    {
        Id = 1,
        Status = "待处理",
        ShippingAddress = new ScenarioAddress { City = "上海", Street = "中山路" },
        Lines = new List<ScenarioOrderLine>
        {
            new() { Id = 1001, ProductId = 55, Quantity = 2, UnitPrice = 10m, Discounts = new() { new() { Id = 5, Amount = 2m } } },
            new() { Id = 1002, ProductId = 55, Quantity = 3, UnitPrice = 20m }
        },
        CreationTime = new DateTime(2024, 1, 1),
        LastModificationTime = new DateTime(2024, 1, 1),
        InternalNote = "内部备注"
    };

    /// <summary>ORD-01：不同实例的数据相同时没有变化。</summary>
    [Fact]
    public void GetChanges_EqualIndependentOrders_ReturnsEmpty()
    {
        var before = CreateOrder();
        var after = CreateOrder();
        Assert.NotSame(before.Lines[0], after.Lines[0]);
        AssertChanges(before.GetChanges(after));
    }

    /// <summary>ORD-02：按订单明细身份定位数量变化。</summary>
    [Fact]
    public void GetChanges_OrderLineQuantityChanged_ReturnsSingleQuantityDifference()
    {
        var before = CreateOrder();
        var after = CreateOrder();
        after.Lines[0].Quantity = 5;

        AssertChanges(before.GetChanges(after), ("Lines[1001].Quantity", "数量", "2", "5"));
    }

    /// <summary>ORD-03：同次比较正确汇总不同明细的增删改。</summary>
    [Fact]
    public void GetChanges_OrderLinesAddedRemovedAndChanged_ReportsEachIdentity()
    {
        var before = CreateOrder();
        var after = CreateOrder();
        after.Lines.RemoveAt(0);
        after.Lines[0].Quantity = 4;
        after.Lines.Add(new ScenarioOrderLine { Id = 1003, ProductId = 77, Quantity = 1 });

        AssertChanges(before.GetChanges(after),
            ("Lines[1001]", "订单明细", "<object>", "<missing>"),
            ("Lines[1002].Quantity", "数量", "3", "4"),
            ("Lines[1003]", "订单明细", "<missing>", "<object>"));
    }

    /// <summary>ORD-04：重排不改变匹配身份，也不掩盖真实字段变化。</summary>
    [Fact]
    public void GetChanges_OrderLinesReordered_MatchesByLineIdentity()
    {
        var before = CreateOrder();
        var after = CreateOrder();
        after.Lines.Reverse();

        AssertChanges(before.GetChanges(after));
        after.Lines[0].Quantity = 8;
        AssertChanges(before.GetChanges(after), ("Lines[1002].Quantity", "数量", "3", "8"));
    }

    /// <summary>ORD-05：同一商品的多条明细不能按商品身份合并。</summary>
    [Fact]
    public void GetChanges_SameProductOnDifferentLines_UsesLineIdentity()
    {
        var before = CreateOrder();
        var after = CreateOrder();
        after.Lines[1].Quantity = 9;

        Assert.Equal(before.Lines[0].ProductId, before.Lines[1].ProductId);
        AssertChanges(before.GetChanges(after), ("Lines[1002].Quantity", "数量", "3", "9"));
    }

    /// <summary>ORD-06：根、地址与明细折扣的变化分别汇总。</summary>
    [Fact]
    public void GetChanges_OrderAddressAndDiscountChanged_ReportsAllThreePaths()
    {
        var before = CreateOrder();
        var after = CreateOrder();
        after.Status = "已支付";
        after.ShippingAddress.City = "杭州";
        after.Lines[0].Discounts[0].Amount = 4m;

        AssertChanges(before.GetChanges(after),
            ("Status", "订单状态", "\"待处理\"", "\"已支付\""),
            ("ShippingAddress.City", "城市", "\"上海\"", "\"杭州\""),
            ("Lines[1001].Discounts[5].Amount", "折扣金额", "2", "4"));
    }

    /// <summary>ORD-07：地址新增、删除和内部字段修改使用不同关系结果。</summary>
    [Fact]
    public void GetChanges_AddressAddedRemovedAndModified_ReportsCorrectRelationship()
    {
        var oldOrder = CreateOrder();
        var noAddress = CreateOrder();
        noAddress.ShippingAddress = null;
        var changedAddress = CreateOrder();
        changedAddress.ShippingAddress.City = "苏州";

        AssertChanges(oldOrder.GetChanges(noAddress), ("ShippingAddress", "收货地址", "<object>", "<missing>"));
        AssertChanges(noAddress.GetChanges(oldOrder), ("ShippingAddress", "收货地址", "<missing>", "<object>"));
        AssertChanges(oldOrder.GetChanges(changedAddress), ("ShippingAddress.City", "城市", "\"上海\"", "\"苏州\""));
    }

    /// <summary>ORD-08：父对象在直接关系、列表和字典中均尊重子实体手写规则。</summary>
    [Fact]
    public void GetChanges_ManualLineRule_IsPreservedInEveryParentRelationship()
    {
        static ScenarioManualLine Line(int quantity, decimal price) =>
            new() { Id = 1001, Quantity = quantity, UnitPrice = price };

        var oldLine = Line(2, 10m);
        var newLine = Line(5, 20m);
        var before = new ScenarioManualOrder
        {
            Direct = oldLine, Lines = new() { oldLine }, LineMap = new() { ["main"] = oldLine }
        };
        var after = new ScenarioManualOrder
        {
            Direct = newLine, Lines = new() { newLine }, LineMap = new() { ["main"] = newLine }
        };

        AssertChanges(oldLine.GetChanges(newLine), ("Quantity", "数量", "2", "5"));
        AssertChanges(before.GetChanges(after),
            ("Direct.Quantity", "数量", "2", "5"),
            ("Lines[1001].Quantity", "数量", "2", "5"),
            ("LineMap{main}.Quantity", "数量", "2", "5"));
    }

    /// <summary>ORD-09：审计时间与显式忽略成员不采集，其他字段仍可采集。</summary>
    [Fact]
    public void GetChanges_AuditAndIgnoredMembersChanged_DoesNotMaskTrackedStatus()
    {
        var before = CreateOrder();
        var after = CreateOrder();
        after.CreationTime = before.CreationTime.Value.AddDays(1);
        after.LastModificationTime = before.LastModificationTime.Value.AddDays(1);
        after.InternalNote = "修改后的内部备注";
        AssertChanges(before.GetChanges(after));

        after.Status = "已支付";
        AssertChanges(before.GetChanges(after), ("Status", "订单状态", "\"待处理\"", "\"已支付\""));
    }

    /// <summary>ORD-09：未实现审计接口的同名业务时间仍应被跟踪。</summary>
    [Fact]
    public void GetChanges_OrdinaryBusinessTimesWithAuditNames_AreStillTracked()
    {
        var before = new ScenarioBusinessTime
        {
            CreationTime = new DateTime(2024, 1, 1),
            LastModificationTime = new DateTime(2024, 2, 1)
        };
        var after = new ScenarioBusinessTime
        {
            CreationTime = new DateTime(2024, 1, 2),
            LastModificationTime = new DateTime(2024, 2, 2)
        };

        AssertChanges(before.GetChanges(after),
            ("CreationTime", "业务创建时间", "2024-01-01T00:00:00.0000000", "2024-01-02T00:00:00.0000000"),
            ("LastModificationTime", "业务修改时间", "2024-02-01T00:00:00.0000000", "2024-02-02T00:00:00.0000000"));
    }

    /// <summary>ORD-10：新旧任一侧明细身份重复都必须明确报错。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetChanges_DuplicateOrderLineIdentity_ThrowsWithRelationshipPath(bool duplicateInBefore)
    {
        var before = CreateOrder();
        var after = CreateOrder();
        (duplicateInBefore ? before : after).Lines.Add(new ScenarioOrderLine { Id = 1001 });

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Lines", error.Message);
    }
}

/// <summary>测试用订单；审计接口用于核对默认排除规则。</summary>
internal partial class ScenarioOrder : DomainObjectBase<ScenarioOrder>, IHasCreationTime, IHasModificationTime
{
    public int Id { get; set; }
    [Description("订单状态")] public string Status { get; set; }
    [Description("收货地址")] public ScenarioAddress ShippingAddress { get; set; }
    [Description("订单明细")] public List<ScenarioOrderLine> Lines { get; set; } = new();
    [ChangeIgnore] public string InternalNote { get; set; }
    public DateTime? CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
}

/// <summary>属性名与审计接口相同，但它们只是普通业务字段。</summary>
internal partial class ScenarioBusinessTime : DomainObjectBase<ScenarioBusinessTime>
{
    [Description("业务创建时间")] public DateTime? CreationTime { get; set; }
    [Description("业务修改时间")] public DateTime? LastModificationTime { get; set; }
}

/// <summary>测试用收货地址。</summary>
internal partial class ScenarioAddress : DomainObjectBase<ScenarioAddress>
{
    [Description("城市")] public string City { get; set; }
    [Description("街道")] public string Street { get; set; }
}

/// <summary>测试用订单明细，以明细自身身份匹配。</summary>
internal partial class ScenarioOrderLine : DomainObjectBase<ScenarioOrderLine>, IEntity
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    [Description("数量")] public int Quantity { get; set; }
    [Description("单价")] public decimal UnitPrice { get; set; }
    [Description("明细折扣")] public List<ScenarioDiscount> Discounts { get; set; } = new();
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>测试用订单明细折扣。</summary>
internal partial class ScenarioDiscount : DomainObjectBase<ScenarioDiscount>, IEntity
{
    public int Id { get; set; }
    [Description("折扣金额")] public decimal Amount { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>测试用手写明细规则，不自动采集单价。</summary>
internal partial class ScenarioManualLine : DomainObjectBase<ScenarioManualLine>, IEntity
{
    public int Id { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    protected override void AddChanges(ScenarioManualLine other) =>
        AddChange(nameof(Quantity), "数量", Quantity, other.Quantity);

    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>包含三种明细引用方式的订单。</summary>
internal partial class ScenarioManualOrder : DomainObjectBase<ScenarioManualOrder>
{
    public ScenarioManualLine Direct { get; set; }
    public List<ScenarioManualLine> Lines { get; set; } = new();
    public Dictionary<string, ScenarioManualLine> LineMap { get; set; } = new();
}
