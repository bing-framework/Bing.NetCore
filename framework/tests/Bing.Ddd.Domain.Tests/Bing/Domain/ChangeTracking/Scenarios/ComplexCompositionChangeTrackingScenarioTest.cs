using System;
using System.Collections.Generic;
using System.ComponentModel;
using Bing.Domain.Entities;
using Xunit;
using static Bing.Domain.ChangeTracking.Scenarios.ChangeScenarioAssertions;

namespace Bing.Domain.ChangeTracking.Scenarios;

/// <summary>
/// 跨聚合引用、共享对象、多态与嵌套容器场景。
/// </summary>
public class ComplexCompositionChangeTrackingScenarioTest
{
    /// <summary>CMP-01：BOM 中共享的零件应在两个组件路径均被报告。</summary>
    [Fact]
    public void GetChanges_BomSharedPartChanged_ReportsBothComponentPaths()
    {
        var oldPart = new ScenarioBomPart { Name = "旧零件" };
        var newPart = new ScenarioBomPart { Name = "新零件" };
        var before = new ScenarioBom
        {
            Components = new() { new() { Id = 1, Part = oldPart }, new() { Id = 2, Part = oldPart } }
        };
        var after = new ScenarioBom
        {
            Components = new() { new() { Id = 2, Part = newPart }, new() { Id = 1, Part = newPart } }
        };

        AssertChanges(before.GetChanges(after),
            ("Components[1].Part.Name", "零件名称", "\"旧零件\"", "\"新零件\""),
            ("Components[2].Part.Name", "零件名称", "\"旧零件\"", "\"新零件\""));
    }

    /// <summary>CMP-02：工作流多个入口共享节点时，祖先回边防环不能全局去重。</summary>
    [Fact]
    public void GetChanges_WorkflowSharedNodesWithCycle_StopsBackEdgesButKeepsBothPaths()
    {
        var oldA = new ScenarioWorkflowNode { Name = "A" };
        var oldB = new ScenarioWorkflowNode { Name = "旧B" };
        oldA.Next = oldB;
        oldB.Next = oldA;
        var newA = new ScenarioWorkflowNode { Name = "A" };
        var newB = new ScenarioWorkflowNode { Name = "新B" };
        newA.Next = newB;
        newB.Next = newA;
        var before = new ScenarioWorkflow { Primary = oldA, Secondary = oldB };
        var after = new ScenarioWorkflow { Primary = newA, Secondary = newB };

        AssertChanges(before.GetChanges(after),
            ("Primary.Next.Name", "节点名称", "\"旧B\"", "\"新B\""),
            ("Secondary.Name", "节点名称", "\"旧B\"", "\"新B\""));
    }

    /// <summary>CMP-03：同一订单行拆到多个包裹时按包裹明细身份匹配。</summary>
    [Fact]
    public void GetChanges_SplitShipmentLineChanged_DoesNotMixShipmentLines()
    {
        static ScenarioShipmentOrder Create() => new()
        {
            Shipments = new()
            {
                new() { Id = 1, Lines = new() { new() { Id = 11, OrderLineId = 1001, Quantity = 2 } } },
                new() { Id = 2, Lines = new() { new() { Id = 22, OrderLineId = 1001, Quantity = 3 } } }
            }
        };
        var before = Create();
        var after = Create();
        after.Shipments.Reverse();
        after.Shipments[0].Lines[0].Quantity = 4;

        AssertChanges(before.GetChanges(after), ("Shipments[2].Lines[22].Quantity", "发货数量", "3", "4"));
    }

    /// <summary>CMP-04：忽略原订单聚合引用，仍采集退货行自己的引用编号。</summary>
    [Fact]
    public void GetChanges_ReturnLine_IgnoresOriginalOrderGraphButTracksReference()
    {
        var before = new ScenarioReturnOrder
        {
            Lines = new() { new() { Id = 1, OriginalOrderLineId = 1001, OriginalOrder = new() { Name = "旧订单" } } }
        };
        var after = new ScenarioReturnOrder
        {
            Lines = new() { new() { Id = 1, OriginalOrderLineId = 1001, OriginalOrder = new() { Name = "新订单" } } }
        };
        AssertChanges(before.GetChanges(after));

        after.Lines[0].OriginalOrderLineId = 1002;
        AssertChanges(before.GetChanges(after), ("Lines[1].OriginalOrderLineId", "原订单行", "1001", "1002"));
    }

    /// <summary>CMP-05：币种字典内的阶梯价格保持外层比较器和内部实体身份。</summary>
    [Fact]
    public void GetChanges_MultiCurrencyPriceTiers_MatchesCurrencyAndTierIdentity()
    {
        var before = new ScenarioCurrencyTiers
        {
            Tiers = new(StringComparer.OrdinalIgnoreCase)
            {
                ["CNY"] = new() { new() { Id = 1, Amount = 10m }, new() { Id = 2, Amount = 20m } }
            }
        };
        var after = new ScenarioCurrencyTiers
        {
            Tiers = new(StringComparer.OrdinalIgnoreCase)
            {
                ["cny"] = new() { new() { Id = 2, Amount = 21m }, new() { Id = 1, Amount = 10m } }
            }
        };

        AssertChanges(before.GetChanges(after), ("Tiers{CNY}[2].Amount", "阶梯价格", "20", "21"));
    }

    /// <summary>CMP-06：用显式关系辅助验证付款子对象的多态比较。</summary>
    [Fact]
    public void GetChanges_PolymorphicPayment_ReportsOwnFieldsAndTypeReplacement()
    {
        var before = new ScenarioPaymentOrder { Payment = new ScenarioCardPayment { LastFour = "1234" } };
        var sameType = new ScenarioPaymentOrder { Payment = new ScenarioCardPayment { LastFour = "5678" } };
        var otherType = new ScenarioPaymentOrder { Payment = new ScenarioCashPayment { Currency = "CNY" } };

        AssertChanges(before.GetChanges(sameType), ("Payment.LastFour", "卡号后四位", "\"1234\"", "\"5678\""));
        AssertChanges(before.GetChanges(otherType), ("Payment", "付款方式", "<object>", "<object>"));
    }

    /// <summary>CMP-06：基类声明的付款关系由生成 hook 调用具体付款方式。</summary>
    [Fact]
    public void GetChanges_GeneratedAbstractPaymentRelationship_DispatchesToDerivedRules()
    {
        var before = new ScenarioAbstractPaymentOrder { Payment = new ScenarioCardPaymentDerived { LastFour = "1234" } };
        var sameType = new ScenarioAbstractPaymentOrder { Payment = new ScenarioCardPaymentDerived { LastFour = "5678" } };
        var otherType = new ScenarioAbstractPaymentOrder { Payment = new ScenarioCashPaymentDerived { Currency = "CNY" } };

        AssertChanges(before.GetChanges(sameType), ("Payment.LastFour", "卡号后四位", "\"1234\"", "\"5678\""));
        AssertChanges(before.GetChanges(otherType), ("Payment", "付款方式", "<object>", "<object>"));
    }

    /// <summary>CMP-07：无身份多层值对象直接调用与嵌套调用只差关系前缀。</summary>
    [Fact]
    public void GetChanges_NestedValueObject_UsesItsOwnRuleAndPrefixesPath()
    {
        var oldGeo = new ScenarioGeoPoint { Latitude = 30m, Longitude = 120m };
        var newGeo = new ScenarioGeoPoint { Latitude = 31m, Longitude = 120m };
        var before = new ScenarioDelivery { Address = new() { Geo = oldGeo } };
        var after = new ScenarioDelivery { Address = new() { Geo = newGeo } };

        AssertChanges(oldGeo.GetChanges(newGeo), ("Latitude", "纬度", "30", "31"));
        AssertChanges(before.GetChanges(after), ("Address.Geo.Latitude", "纬度", "30", "31"));
    }

    /// <summary>CMP-08：列表中的字典必须保留内层容器的键比较器。</summary>
    [Fact]
    public void GetChanges_ListOfDictionaries_PreservesNestedDictionaryComparer()
    {
        var before = new ScenarioNestedContainers
        {
            Groups = new() { new(StringComparer.OrdinalIgnoreCase) { ["SKU"] = new() { Name = "旧" } } }
        };
        var after = new ScenarioNestedContainers
        {
            Groups = new() { new(StringComparer.OrdinalIgnoreCase) { ["sku"] = new() { Name = "新" } } }
        };

        AssertChanges(before.GetChanges(after), ("Groups[0]{SKU}.Name", "名称", "\"旧\"", "\"新\""));
    }

    /// <summary>CMP-08：字典中的 Set 不应因插入顺序或大小写比较器而产生假变化。</summary>
    [Fact]
    public void GetChanges_DictionaryOfSets_PreservesNestedSetSemantics()
    {
        var before = new ScenarioNestedContainers
        {
            TagsByGroup = new() { ["default"] = new(StringComparer.OrdinalIgnoreCase) { "A", "B" } }
        };
        var after = new ScenarioNestedContainers
        {
            TagsByGroup = new() { ["default"] = new(StringComparer.OrdinalIgnoreCase) { "b", "a" } }
        };

        AssertChanges(before.GetChanges(after));
    }

    /// <summary>CMP-09：临时身份稳定时比较字段，替换为正式身份时报告增删。</summary>
    [Fact]
    public void GetChanges_TemporaryIdentity_TracksModificationAndAssignedIdentity()
    {
        var before = new ScenarioTemporaryItems { Items = new() { new() { Id = "temp-1", Name = "旧" } } };
        var changed = new ScenarioTemporaryItems { Items = new() { new() { Id = "temp-1", Name = "新" } } };
        var assigned = new ScenarioTemporaryItems { Items = new() { new() { Id = "real-1", Name = "旧" } } };

        AssertChanges(before.GetChanges(changed), ("Items[temp-1].Name", "名称", "\"旧\"", "\"新\""));
        AssertChanges(before.GetChanges(assigned),
            ("Items[temp-1]", "明细", "<object>", "<missing>"),
            ("Items[real-1]", "明细", "<missing>", "<object>"));
        before.Items.Add(new ScenarioTemporaryItem { Id = "temp-1", Name = "重复" });
        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(changed));
        Assert.Contains("Items", error.Message);
    }

    /// <summary>CMP-10：IEntity 显式声明的 decimal 键支持重排并报告字段修改。</summary>
    [Fact]
    public void GetChanges_DecimalEntityIdentity_MatchesReorderedItemsAndReportsUpdate()
    {
        var before = new ScenarioDecimalItems
        {
            Items = new() { new() { Id = 1.5m, Name = "一" }, new() { Id = 2.5m, Name = "二" } }
        };
        var after = new ScenarioDecimalItems
        {
            Items = new() { new() { Id = 2.5m, Name = "二" }, new() { Id = 1.5m, Name = "更新" } }
        };

        AssertChanges(before.GetChanges(after),
            ("Items[@(\"System.Decimal\",\"1.5\")].Name", "Name", "\"一\"", "\"更新\""));
    }

    /// <summary>CMP-11：带键列表夹杂 null 时，不得静默把带键元素退化成按索引比较。</summary>
    [Fact]
    public void GetChanges_KeyedListContainingNull_DoesNotSilentlyCompareByIndex()
    {
        var before = new ScenarioNullableKeyedItems
        {
            Items = new() { new() { Id = 1, Name = "一" }, null, new() { Id = 2, Name = "二" } }
        };
        var after = new ScenarioNullableKeyedItems
        {
            Items = new() { new() { Id = 2, Name = "二" }, null, new() { Id = 1, Name = "一" } }
        };

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Items[1]", error.Message);
    }

    /// <summary>CMP-12：null 集合按空集合处理，旧集合成员均作为删除报告。</summary>
    [Fact]
    public void GetChanges_NullAndEmptyCollection_RemoveExistingItemsEqually()
    {
        var before = new ScenarioDirectionOrder { Lines = new() { new() { Id = 1, Quantity = 2 } } };
        var nullCollection = new ScenarioDirectionOrder { Lines = null };
        var empty = new ScenarioDirectionOrder { Lines = new() };

        AssertChanges(before.GetChanges(nullCollection), ("Lines[1]", "明细", "<object>", "<missing>"));
        AssertChanges(before.GetChanges(empty), ("Lines[1]", "明细", "<object>", "<missing>"));
    }
}

/// <summary>BOM 聚合。</summary>
internal partial class ScenarioBom : DomainObjectBase<ScenarioBom>
{
    public List<ScenarioBomComponent> Components { get; set; } = new();
}

/// <summary>有独立身份的 BOM 组件。</summary>
internal partial class ScenarioBomComponent : DomainObjectBase<ScenarioBomComponent>, IEntity
{
    public int Id { get; set; }
    public ScenarioBomPart Part { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>可被多个组件共享的零件。</summary>
internal partial class ScenarioBomPart : DomainObjectBase<ScenarioBomPart>
{
    [Description("零件名称")] public string Name { get; set; }
}

/// <summary>有两个入口的工作流。</summary>
internal partial class ScenarioWorkflow : DomainObjectBase<ScenarioWorkflow>
{
    public ScenarioWorkflowNode Primary { get; set; }
    public ScenarioWorkflowNode Secondary { get; set; }
}

/// <summary>可以循环引用的工作流节点。</summary>
internal partial class ScenarioWorkflowNode : DomainObjectBase<ScenarioWorkflowNode>
{
    [Description("节点名称")] public string Name { get; set; }
    public ScenarioWorkflowNode Next { get; set; }
}

/// <summary>包含多个发货单的订单。</summary>
internal partial class ScenarioShipmentOrder : DomainObjectBase<ScenarioShipmentOrder>
{
    public List<ScenarioShipment> Shipments { get; set; } = new();
}

/// <summary>有独立身份的发货单。</summary>
internal partial class ScenarioShipment : DomainObjectBase<ScenarioShipment>, IEntity
{
    public int Id { get; set; }
    public List<ScenarioShipmentLine> Lines { get; set; } = new();
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>指向同一订单行也应独立匹配的发货明细。</summary>
internal partial class ScenarioShipmentLine : DomainObjectBase<ScenarioShipmentLine>, IEntity
{
    public int Id { get; set; }
    public int OrderLineId { get; set; }
    [Description("发货数量")] public int Quantity { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>退货单。</summary>
internal partial class ScenarioReturnOrder : DomainObjectBase<ScenarioReturnOrder>
{
    public List<ScenarioReturnLine> Lines { get; set; } = new();
}

/// <summary>退货行忽略指向原聚合的导航，保留引用编号。</summary>
internal partial class ScenarioReturnLine : DomainObjectBase<ScenarioReturnLine>, IEntity
{
    public int Id { get; set; }
    [Description("原订单行")] public int OriginalOrderLineId { get; set; }
    [ChangeIgnore] public ScenarioOriginalOrder OriginalOrder { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>被退货单引用的原订单。</summary>
internal partial class ScenarioOriginalOrder : DomainObjectBase<ScenarioOriginalOrder>
{
    public string Name { get; set; }
}

/// <summary>币种对应的阶梯价格集合。</summary>
internal partial class ScenarioCurrencyTiers : DomainObjectBase<ScenarioCurrencyTiers>
{
    public Dictionary<string, List<ScenarioPriceTier>> Tiers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>有身份的价格阶梯。</summary>
internal partial class ScenarioPriceTier : DomainObjectBase<ScenarioPriceTier>, IEntity
{
    public int Id { get; set; }
    [Description("阶梯价格")] public decimal Amount { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>付款方式公共契约。</summary>
internal interface IScenarioPayment : IChangeTrackable { }

/// <summary>手写关系调用使多态测试聚焦运行时，不假设生成器支持接口属性。</summary>
internal partial class ScenarioPaymentOrder : DomainObjectBase<ScenarioPaymentOrder>
{
    public IScenarioPayment Payment { get; set; }
    protected override void AddChanges(ScenarioPaymentOrder other) =>
        AddObjectChanges(nameof(Payment), "付款方式", Payment, other.Payment);
}

/// <summary>银行卡付款方式。</summary>
internal partial class ScenarioCardPayment : DomainObjectBase<ScenarioCardPayment>, IScenarioPayment
{
    [Description("卡号后四位")] public string LastFour { get; set; }
}

/// <summary>现金付款方式。</summary>
internal partial class ScenarioCashPayment : DomainObjectBase<ScenarioCashPayment>, IScenarioPayment
{
    public string Currency { get; set; }
}

/// <summary>付款属性静态类型为抽象基类，关系调用由生成器提供。</summary>
internal partial class ScenarioAbstractPaymentOrder : DomainObjectBase<ScenarioAbstractPaymentOrder>
{
    [Description("付款方式")] public ScenarioPaymentBase Payment { get; set; }
}

/// <summary>可被具体付款方式继承的领域对象。</summary>
internal abstract partial class ScenarioPaymentBase : DomainObjectBase<ScenarioPaymentBase> { }

/// <summary>继承付款基类的银行卡方式。</summary>
internal partial class ScenarioCardPaymentDerived : ScenarioPaymentBase
{
    [Description("卡号后四位")] public string LastFour { get; set; }
}

/// <summary>继承付款基类的现金方式。</summary>
internal partial class ScenarioCashPaymentDerived : ScenarioPaymentBase
{
    public string Currency { get; set; }
}

/// <summary>包含无身份地址值对象的配送单。</summary>
internal partial class ScenarioDelivery : DomainObjectBase<ScenarioDelivery>
{
    public ScenarioDeliveryAddress Address { get; set; }
}

/// <summary>包含地理坐标的地址值对象。</summary>
internal partial class ScenarioDeliveryAddress : DomainObjectBase<ScenarioDeliveryAddress>
{
    public ScenarioGeoPoint Geo { get; set; }
}

/// <summary>不带身份的坐标值对象。</summary>
internal partial class ScenarioGeoPoint : DomainObjectBase<ScenarioGeoPoint>
{
    [Description("纬度")] public decimal Latitude { get; set; }
    [Description("经度")] public decimal Longitude { get; set; }
}

/// <summary>用于验证容器内部比较规则的测试聚合。</summary>
internal partial class ScenarioNestedContainers : DomainObjectBase<ScenarioNestedContainers>
{
    public List<Dictionary<string, ScenarioNestedLeaf>> Groups { get; set; } = new();
    public Dictionary<string, HashSet<string>> TagsByGroup { get; set; } = new();
}

/// <summary>嵌套容器中的领域对象。</summary>
internal partial class ScenarioNestedLeaf : DomainObjectBase<ScenarioNestedLeaf>
{
    [Description("名称")] public string Name { get; set; }
}

/// <summary>用临时字符串身份匹配的集合。</summary>
internal partial class ScenarioTemporaryItems : DomainObjectBase<ScenarioTemporaryItems>
{
    [Description("明细")] public List<ScenarioTemporaryItem> Items { get; set; } = new();
}

/// <summary>可能使用临时身份的明细。</summary>
internal partial class ScenarioTemporaryItem : DomainObjectBase<ScenarioTemporaryItem>, IEntity
{
    public string Id { get; set; }
    [Description("名称")] public string Name { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>用于测试非常见泛型身份类型的集合。</summary>
internal partial class ScenarioDecimalItems : DomainObjectBase<ScenarioDecimalItems>
{
    public List<ScenarioDecimalItem> Items { get; set; } = new();
}

/// <summary>通过 IEntity 明确声明 decimal 身份的领域对象。</summary>
internal partial class ScenarioDecimalItem : DomainObjectBase<ScenarioDecimalItem>, IEntity
{
    public decimal Id { get; set; }
    public string Name { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>允许测试元素为 null 的带键集合。</summary>
internal partial class ScenarioNullableKeyedItems : DomainObjectBase<ScenarioNullableKeyedItems>
{
    public List<ScenarioNullableKeyedItem> Items { get; set; } = new();
}

/// <summary>测试 null 元素周围的带键对象。</summary>
internal partial class ScenarioNullableKeyedItem : DomainObjectBase<ScenarioNullableKeyedItem>, IEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}
