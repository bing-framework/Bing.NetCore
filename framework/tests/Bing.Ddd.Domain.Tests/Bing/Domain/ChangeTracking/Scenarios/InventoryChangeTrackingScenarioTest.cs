using System;
using System.Collections.Generic;
using System.ComponentModel;
using Bing.Domain.Entities;
using Xunit;
using static Bing.Domain.ChangeTracking.Scenarios.ChangeScenarioAssertions;

namespace Bing.Domain.ChangeTracking.Scenarios;

/// <summary>
/// 多仓库、库位、商品和批次库存的变更跟踪业务场景。
/// </summary>
public class InventoryChangeTrackingScenarioTest
{
    private const string Warehouse10 = "Warehouses[1,10]";
    private const string Location20 = Warehouse10 + ".Locations[1,10,20]";
    private const string Product30 = Location20 + ".Products[1,10,20,30]";
    private const string BatchB1 = Product30 + ".Batches[1,10,20,30,B1,2,Good]";

    private static ScenarioStockBatch Batch(
        int tenant = 1, int warehouse = 10, int location = 20, int product = 30,
        string batch = "B1", int owner = 2, string status = "Good", int quantity = 100) => new()
    {
        TenantId = tenant, WarehouseId = warehouse, LocationId = location,
        ProductId = product, BatchNo = batch, OwnerId = owner, StockStatus = status,
        Quantity = quantity, ReservedQuantity = 5
    };

    private static ScenarioStockProduct Product(int tenant, int warehouse, int location, int product, params ScenarioStockBatch[] batches) => new()
    {
        TenantId = tenant, WarehouseId = warehouse, LocationId = location, ProductId = product,
        Batches = new(batches)
    };

    private static ScenarioStockLocation Location(int tenant, int warehouse, int location, params ScenarioStockProduct[] products) => new()
    {
        TenantId = tenant, WarehouseId = warehouse, LocationId = location, Products = new(products)
    };

    private static ScenarioStockWarehouse Warehouse(int tenant, int warehouse, params ScenarioStockLocation[] locations) => new()
    {
        TenantId = tenant, WarehouseId = warehouse, Locations = new(locations)
    };

    private static ScenarioInventoryOverview CreateInventory() => new()
    {
        Warehouses = new()
        {
            Warehouse(1, 10, Location(1, 10, 20, Product(1, 10, 20, 30, Batch())))
        },
        WarehouseTotal = 100,
        ProductTotal = 100,
        BatchTotal = 100,
        IgnoredTotal = 100
    };

    /// <summary>INV-01：最深层批次数量变化保留完整的复合身份路径。</summary>
    [Fact]
    public void GetChanges_OneBatchQuantityChanged_ReportsFullInventoryPath()
    {
        var before = CreateInventory();
        var after = CreateInventory();
        after.Warehouses[0].Locations[0].Products[0].Batches[0].Quantity = 80;

        AssertChanges(before.GetChanges(after), (BatchB1 + ".Quantity", "库存数量", "100", "80"));
    }

    /// <summary>INV-02：同一商品位于不同库位时只报告目标库位。</summary>
    [Fact]
    public void GetChanges_SameProductInDifferentLocations_DoesNotMixIdentities()
    {
        var before = CreateInventory();
        var after = CreateInventory();
        before.Warehouses[0].Locations.Add(Location(1, 10, 21, Product(1, 10, 21, 30, Batch(location: 21))));
        after.Warehouses[0].Locations.Add(Location(1, 10, 21, Product(1, 10, 21, 30, Batch(location: 21, quantity: 70))));
        after.Warehouses[0].Locations.Reverse();

        AssertChanges(before.GetChanges(after),
            ("Warehouses[1,10].Locations[1,10,21].Products[1,10,21,30].Batches[1,10,21,30,B1,2,Good].Quantity", "库存数量", "100", "70"));
    }

    /// <summary>INV-03：相同批次号用于不同商品时不能互相匹配。</summary>
    [Fact]
    public void GetChanges_SameBatchNumberForDifferentProducts_DoesNotMixIdentities()
    {
        var before = CreateInventory();
        var after = CreateInventory();
        before.Warehouses[0].Locations[0].Products.Add(Product(1, 10, 20, 31, Batch(product: 31)));
        after.Warehouses[0].Locations[0].Products.Add(Product(1, 10, 20, 31, Batch(product: 31, quantity: 90)));
        after.Warehouses[0].Locations[0].Products.Reverse();

        AssertChanges(before.GetChanges(after),
            ("Warehouses[1,10].Locations[1,10,20].Products[1,10,20,31].Batches[1,10,20,31,B1,2,Good].Quantity", "库存数量", "100", "90"));
    }

    /// <summary>INV-04：同商品批次在不同仓库时变化只属于目标仓库。</summary>
    [Fact]
    public void GetChanges_SameProductBatchInDifferentWarehouses_ReportsTargetWarehouse()
    {
        var before = CreateInventory();
        var after = CreateInventory();
        before.Warehouses.Add(Warehouse(1, 11, Location(1, 11, 20, Product(1, 11, 20, 30, Batch(warehouse: 11)))));
        after.Warehouses.Add(Warehouse(1, 11, Location(1, 11, 20, Product(1, 11, 20, 30, Batch(warehouse: 11, quantity: 90)))));
        after.Warehouses.Reverse();

        AssertChanges(before.GetChanges(after),
            ("Warehouses[1,11].Locations[1,11,20].Products[1,11,20,30].Batches[1,11,20,30,B1,2,Good].Quantity", "库存数量", "100", "90"));
    }

    /// <summary>INV-05：复用仓库和批次编号的不同租户按复合身份区分。</summary>
    [Fact]
    public void GetChanges_SameIdentifiersAcrossTenants_DoNotMatchEachOther()
    {
        var before = CreateInventory();
        var after = CreateInventory();
        before.Warehouses.Add(Warehouse(2, 10, Location(2, 10, 20, Product(2, 10, 20, 30, Batch(tenant: 2)))));
        after.Warehouses.Add(Warehouse(2, 10, Location(2, 10, 20, Product(2, 10, 20, 30, Batch(tenant: 2, quantity: 80)))));
        after.Warehouses.Reverse();

        AssertChanges(before.GetChanges(after),
            ("Warehouses[2,10].Locations[2,10,20].Products[2,10,20,30].Batches[2,10,20,30,B1,2,Good].Quantity", "库存数量", "100", "80"));
    }

    /// <summary>INV-06：批次增删与库位整体删除是关系结构变化。</summary>
    [Fact]
    public void GetChanges_BatchesReplacedAndLocationRemoved_ReportsStructuralChanges()
    {
        var before = CreateInventory();
        var after = CreateInventory();
        after.Warehouses[0].Locations[0].Products[0].Batches = new() { Batch(batch: "B2") };

        AssertChanges(before.GetChanges(after),
            (BatchB1, "商品批次库存", "<object>", "<missing>"),
            (Product30 + ".Batches[1,10,20,30,B2,2,Good]", "商品批次库存", "<missing>", "<object>"));

        after.Warehouses[0].Locations.Clear();
        AssertChanges(before.GetChanges(after), (Location20, "库位库存", "<object>", "<missing>"));
    }

    /// <summary>INV-07：预留量变化不自动采集被忽略的可用量。</summary>
    [Fact]
    public void GetChanges_ReservedQuantityChanged_DoesNotReportIgnoredAvailableQuantity()
    {
        var before = CreateInventory();
        var after = CreateInventory();
        after.Warehouses[0].Locations[0].Products[0].Batches[0].ReservedQuantity = 8;

        AssertChanges(before.GetChanges(after), (BatchB1 + ".ReservedQuantity", "预留数量", "5", "8"));
    }

    /// <summary>INV-08：不同库位之间的调拨按两个父路径分别增删。</summary>
    [Fact]
    public void GetChanges_StockMovedBetweenLocations_ReportsRemoveAndAdd()
    {
        var before = CreateInventory();
        var after = CreateInventory();
        before.Warehouses[0].Locations[0].Products[0].Batches[0].Quantity = 10;
        before.Warehouses[0].Locations.Add(Location(1, 10, 21, Product(1, 10, 21, 30)));
        after.Warehouses[0].Locations.Add(Location(1, 10, 21, Product(1, 10, 21, 30, Batch(location: 21, quantity: 10))));
        after.Warehouses[0].Locations[0].Products[0].Batches.Clear();

        AssertChanges(before.GetChanges(after),
            (BatchB1, "商品批次库存", "<object>", "<missing>"),
            ("Warehouses[1,10].Locations[1,10,21].Products[1,10,21,30].Batches[1,10,21,30,B1,2,Good]", "商品批次库存", "<missing>", "<object>"));
    }

    /// <summary>INV-09：批次拆分和合并分别按前后身份集合报告。</summary>
    [Fact]
    public void GetChanges_BatchSplitAndMerge_ReportsIdentityChanges()
    {
        var before = CreateInventory();
        var split = CreateInventory();
        split.Warehouses[0].Locations[0].Products[0].Batches = new()
        {
            Batch(batch: "B2", quantity: 40), Batch(batch: "B3", quantity: 60)
        };

        AssertChanges(before.GetChanges(split),
            (BatchB1, "商品批次库存", "<object>", "<missing>"),
            (Product30 + ".Batches[1,10,20,30,B2,2,Good]", "商品批次库存", "<missing>", "<object>"),
            (Product30 + ".Batches[1,10,20,30,B3,2,Good]", "商品批次库存", "<missing>", "<object>"));
        AssertChanges(split.GetChanges(before),
            (BatchB1, "商品批次库存", "<missing>", "<object>"),
            (Product30 + ".Batches[1,10,20,30,B2,2,Good]", "商品批次库存", "<object>", "<missing>"),
            (Product30 + ".Batches[1,10,20,30,B3,2,Good]", "商品批次库存", "<object>", "<missing>"));
    }

    /// <summary>INV-10：货主或状态是身份分量时，分量变化意味着实体替换。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetChanges_BatchOwnerOrStatusChanges_ReportsReplacement(bool changeOwner)
    {
        var before = CreateInventory();
        var after = CreateInventory();
        var item = after.Warehouses[0].Locations[0].Products[0].Batches[0];
        if (changeOwner)
            item.OwnerId = 3;
        else
            item.StockStatus = "Frozen";
        var newPath = changeOwner
            ? Product30 + ".Batches[1,10,20,30,B1,3,Good]"
            : Product30 + ".Batches[1,10,20,30,B1,2,Frozen]";

        AssertChanges(before.GetChanges(after),
            (BatchB1, "商品批次库存", "<object>", "<missing>"),
            (newPath, "商品批次库存", "<missing>", "<object>"));
    }

    /// <summary>INV-11：已更新的显式汇总分别采集，忽略的汇总不额外产生差异。</summary>
    [Fact]
    public void GetChanges_TrackedAndIgnoredSummaries_ReportsOnlyConfiguredValues()
    {
        var before = CreateInventory();
        var after = CreateInventory();
        after.WarehouseTotal = 80;
        after.ProductTotal = 80;
        after.BatchTotal = 80;
        after.IgnoredTotal = 80;
        after.Warehouses[0].Locations[0].Products[0].Batches[0].Quantity = 80;

        AssertChanges(before.GetChanges(after),
            ("WarehouseTotal", "仓库汇总", "100", "80"),
            ("ProductTotal", "商品汇总", "100", "80"),
            ("BatchTotal", "批次汇总", "100", "80"),
            (BatchB1 + ".Quantity", "库存数量", "100", "80"));

        var ignoredBefore = new ScenarioIgnoredInventorySummary
        {
            WarehouseTotal = 100, ProductTotal = 100, BatchTotal = 100, Warehouses = before.Warehouses
        };
        var ignoredAfter = new ScenarioIgnoredInventorySummary
        {
            WarehouseTotal = 80, ProductTotal = 80, BatchTotal = 80, Warehouses = after.Warehouses
        };
        AssertChanges(ignoredBefore.GetChanges(ignoredAfter), (BatchB1 + ".Quantity", "库存数量", "100", "80"));
    }

    /// <summary>INV-11：批次库存和商品批次库存是各自独立的汇总视图。</summary>
    [Fact]
    public void GetChanges_BatchAndProductBatchViews_ReportsBothChangedProjections()
    {
        var before = new ScenarioStockProjectionOverview
        {
            BatchStocks = new()
            {
                new() { TenantId = 1, ProductId = 30, BatchNo = "B1", Quantity = 100 },
                new() { TenantId = 1, ProductId = 31, BatchNo = "B1", Quantity = 200 }
            },
            ProductBatchStocks = new()
            {
                new() { TenantId = 1, WarehouseId = 10, ProductId = 30, BatchNo = "B1", Quantity = 100 },
                new() { TenantId = 1, WarehouseId = 10, ProductId = 31, BatchNo = "B1", Quantity = 200 }
            }
        };
        var after = new ScenarioStockProjectionOverview
        {
            BatchStocks = new()
            {
                new() { TenantId = 1, ProductId = 31, BatchNo = "B1", Quantity = 200 },
                new() { TenantId = 1, ProductId = 30, BatchNo = "B1", Quantity = 80 }
            },
            ProductBatchStocks = new()
            {
                new() { TenantId = 1, WarehouseId = 10, ProductId = 31, BatchNo = "B1", Quantity = 200 },
                new() { TenantId = 1, WarehouseId = 10, ProductId = 30, BatchNo = "B1", Quantity = 80 }
            }
        };

        AssertChanges(before.GetChanges(after),
            ("BatchStocks[1,30,B1].Quantity", "批次库存数量", "100", "80"),
            ("ProductBatchStocks[1,10,30,B1].Quantity", "商品批次库存数量", "100", "80"));
    }

    /// <summary>INV-12：位置字典保留 comparer，内部批次列表按复合键而非顺序匹配。</summary>
    [Fact]
    public void GetChanges_LocationDictionaryWithBatchLists_PreservesComparerAndIdentity()
    {
        var before = new ScenarioInventoryByLocation
        {
            Locations = new(StringComparer.OrdinalIgnoreCase) { ["A"] = new() { Batch(), Batch(batch: "B2") } }
        };
        var after = new ScenarioInventoryByLocation
        {
            Locations = new(StringComparer.OrdinalIgnoreCase) { ["a"] = new() { Batch(batch: "B2", quantity: 90), Batch() } }
        };

        AssertChanges(before.GetChanges(after),
            ("Locations{A}[1,10,20,30,B2,2,Good].Quantity", "库存数量", "100", "90"));
    }

    /// <summary>INV-13：复合键的分隔符、括号和空字符串不能使不同路径发生碰撞。</summary>
    [Fact]
    public void GetChanges_CompositeKeysWithSeparatorsAndEmptyText_HaveDistinctPaths()
    {
        var before = new ScenarioCompositeKeys
        {
            Items = new() { new() { PartA = "A,B", PartB = "C", Quantity = 1 },
                new() { PartA = "A", PartB = "B,C", Quantity = 3 },
                new() { PartA = "", PartB = "(X)", Quantity = 5 } }
        };
        var after = new ScenarioCompositeKeys
        {
            Items = new() { new() { PartA = "", PartB = "(X)", Quantity = 6 },
                new() { PartA = "A,B", PartB = "C", Quantity = 2 },
                new() { PartA = "A", PartB = "B,C", Quantity = 4 } }
        };

        AssertChanges(before.GetChanges(after),
            ("Items[\"A,B\",C].Quantity", "数量", "1", "2"),
            ("Items[A,\"B,C\"].Quantity", "数量", "3", "4"),
            ("Items[\"\",\"(X)\"].Quantity", "数量", "5", "6"));
    }

    /// <summary>INV-14：复合身份重复时禁止覆盖，异常后可正常比较。</summary>
    [Fact]
    public void GetChanges_DuplicateCompositeIdentity_ThrowsAndRestoresSession()
    {
        var before = CreateInventory();
        var after = CreateInventory();
        before.Warehouses[0].Locations[0].Products[0].Batches.Add(Batch());
        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Batches", error.Message);

        var validBefore = CreateInventory();
        var validAfter = CreateInventory();
        validAfter.Warehouses[0].Locations[0].Products[0].Batches[0].Quantity = 80;
        AssertChanges(validBefore.GetChanges(validAfter), (BatchB1 + ".Quantity", "库存数量", "100", "80"));
    }

    /// <summary>INV-14：空复合键必须带关系位置失败。</summary>
    [Fact]
    public void GetChanges_EmptyEntityKeys_ThrowsWithRelationshipPath()
    {
        var before = new ScenarioEmptyKeyList { Items = new() { new() { Value = 1 } } };
        var after = new ScenarioEmptyKeyList { Items = new() { new() { Value = 2 } } };

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Items", error.Message);
    }

    /// <summary>INV-14：空身份导致异常后，下一次比较仍应独立。</summary>
    [Fact]
    [Trait("Behavior", "CurrentCharacterization")]
    public void GetChanges_EmptyEntityKeys_ExceptionDoesNotPolluteNextComparison()
    {
        var before = new ScenarioEmptyKeyList { Items = new() { new() { Value = 1 } } };
        var after = new ScenarioEmptyKeyList { Items = new() { new() { Value = 2 } } };
        Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));

        var validBefore = CreateInventory();
        var validAfter = CreateInventory();
        validAfter.Warehouses[0].Locations[0].Products[0].Batches[0].Quantity = 80;
        AssertChanges(validBefore.GetChanges(validAfter), (BatchB1 + ".Quantity", "库存数量", "100", "80"));
    }

    /// <summary>INV-14、CMP-09：未分配的单一身份不能参与实体集合比较。</summary>
    [Fact]
    public void GetChanges_UnassignedIdentity_ThrowsRatherThanMatchingUnrelatedItems()
    {
        var before = new ScenarioDefaultKeyList { Items = new() { new() { Id = 0, Value = 1 } } };
        var after = new ScenarioDefaultKeyList { Items = new() { new() { Id = 0, Value = 2 } } };
        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Items", error.Message);
    }

    /// <summary>INV-14：单分量 null 身份必须带关系位置失败。</summary>
    [Fact]
    public void GetChanges_NullIdentity_ThrowsWithRelationshipPath()
    {
        var before = new ScenarioNullKeyList { Items = new() { new() { Id = null, Value = 1 } } };
        var after = new ScenarioNullKeyList { Items = new() { new() { Id = null, Value = 2 } } };
        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Items", error.Message);
    }
}

/// <summary>同时包含明细和显式汇总的库存总览。</summary>
internal partial class ScenarioInventoryOverview : DomainObjectBase<ScenarioInventoryOverview>
{
    [Description("仓库库存")] public List<ScenarioStockWarehouse> Warehouses { get; set; } = new();
    [Description("仓库汇总")] public int WarehouseTotal { get; set; }
    [Description("商品汇总")] public int ProductTotal { get; set; }
    [Description("批次汇总")] public int BatchTotal { get; set; }
    [ChangeIgnore] public int IgnoredTotal { get; set; }
}

/// <summary>分别展示批次汇总和仓库商品批次明细的库存投影视图。</summary>
internal partial class ScenarioStockProjectionOverview : DomainObjectBase<ScenarioStockProjectionOverview>
{
    [Description("批次库存")] public List<ScenarioBatchStock> BatchStocks { get; set; } = new();
    [Description("商品批次库存")] public List<ScenarioProductBatchStock> ProductBatchStocks { get; set; } = new();
}

/// <summary>示例中批次汇总按租户、商品和批次区分。</summary>
internal partial class ScenarioBatchStock : DomainObjectBase<ScenarioBatchStock>, IEntity
{
    public int TenantId { get; set; }
    public int ProductId { get; set; }
    public string BatchNo { get; set; }
    [Description("批次库存数量")] public int Quantity { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { TenantId, ProductId, BatchNo };
}

/// <summary>示例中商品批次库存按租户、仓库、商品和批次区分。</summary>
internal partial class ScenarioProductBatchStock : DomainObjectBase<ScenarioProductBatchStock>, IEntity
{
    public int TenantId { get; set; }
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public string BatchNo { get; set; }
    [Description("商品批次库存数量")] public int Quantity { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { TenantId, WarehouseId, ProductId, BatchNo };
}

/// <summary>明细相同，但三个汇总均不参与自动采集。</summary>
internal partial class ScenarioIgnoredInventorySummary : DomainObjectBase<ScenarioIgnoredInventorySummary>
{
    [Description("仓库库存")] public List<ScenarioStockWarehouse> Warehouses { get; set; } = new();
    [ChangeIgnore] public int WarehouseTotal { get; set; }
    [ChangeIgnore] public int ProductTotal { get; set; }
    [ChangeIgnore] public int BatchTotal { get; set; }
}

/// <summary>仓库库存，身份由租户和仓库共同决定。</summary>
internal partial class ScenarioStockWarehouse : DomainObjectBase<ScenarioStockWarehouse>, IEntity
{
    public int TenantId { get; set; }
    public int WarehouseId { get; set; }
    [Description("库位库存")] public List<ScenarioStockLocation> Locations { get; set; } = new();
    public void Init() { }
    public object[] GetKeys() => new object[] { TenantId, WarehouseId };
}

/// <summary>库位库存，身份包含所属租户和仓库。</summary>
internal partial class ScenarioStockLocation : DomainObjectBase<ScenarioStockLocation>, IEntity
{
    public int TenantId { get; set; }
    public int WarehouseId { get; set; }
    public int LocationId { get; set; }
    [Description("商品库存")] public List<ScenarioStockProduct> Products { get; set; } = new();
    public void Init() { }
    public object[] GetKeys() => new object[] { TenantId, WarehouseId, LocationId };
}

/// <summary>商品库存，身份包含仓库、库位和商品。</summary>
internal partial class ScenarioStockProduct : DomainObjectBase<ScenarioStockProduct>, IEntity
{
    public int TenantId { get; set; }
    public int WarehouseId { get; set; }
    public int LocationId { get; set; }
    public int ProductId { get; set; }
    [Description("商品批次库存")] public List<ScenarioStockBatch> Batches { get; set; } = new();
    public void Init() { }
    public object[] GetKeys() => new object[] { TenantId, WarehouseId, LocationId, ProductId };
}

/// <summary>批次库存，以商品、批次、货主、状态及位置的组合作为身份。</summary>
internal partial class ScenarioStockBatch : DomainObjectBase<ScenarioStockBatch>, IEntity
{
    public int TenantId { get; set; }
    public int WarehouseId { get; set; }
    public int LocationId { get; set; }
    public int ProductId { get; set; }
    public string BatchNo { get; set; }
    public int OwnerId { get; set; }
    public string StockStatus { get; set; }
    [Description("库存数量")] public int Quantity { get; set; }
    [Description("预留数量")] public int ReservedQuantity { get; set; }
    [ChangeIgnore] public int AvailableQuantity => Quantity - ReservedQuantity;
    public void Init() { }
    public object[] GetKeys() => new object[] { TenantId, WarehouseId, LocationId, ProductId, BatchNo, OwnerId, StockStatus };
}

/// <summary>位置字典及其批次列表。</summary>
internal partial class ScenarioInventoryByLocation : DomainObjectBase<ScenarioInventoryByLocation>
{
    public Dictionary<string, List<ScenarioStockBatch>> Locations { get; set; } = new();
}

/// <summary>用于验证复合键输出转义的集合。</summary>
internal partial class ScenarioCompositeKeys : DomainObjectBase<ScenarioCompositeKeys>
{
    public List<ScenarioCompositeItem> Items { get; set; } = new();
}

/// <summary>用于验证复合键转义的元素。</summary>
internal partial class ScenarioCompositeItem : DomainObjectBase<ScenarioCompositeItem>, IEntity
{
    public string PartA { get; set; }
    public string PartB { get; set; }
    [Description("数量")] public int Quantity { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { PartA, PartB };
}

/// <summary>测试空身份数组的集合。</summary>
internal partial class ScenarioEmptyKeyList : DomainObjectBase<ScenarioEmptyKeyList>
{
    public List<ScenarioEmptyKey> Items { get; set; } = new();
}

/// <summary>故意返回空身份数组的实体。</summary>
internal partial class ScenarioEmptyKey : DomainObjectBase<ScenarioEmptyKey>, IEntity
{
    public int Value { get; set; }
    public void Init() { }
    public object[] GetKeys() => Array.Empty<object>();
}

/// <summary>测试未分配数字身份的集合。</summary>
internal partial class ScenarioDefaultKeyList : DomainObjectBase<ScenarioDefaultKeyList>
{
    public List<ScenarioDefaultKey> Items { get; set; } = new();
}

/// <summary>可能尚未分配数字身份的实体。</summary>
internal partial class ScenarioDefaultKey : DomainObjectBase<ScenarioDefaultKey>, IEntity
{
    public int Id { get; set; }
    public int Value { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>测试空字符串身份的集合。</summary>
internal partial class ScenarioNullKeyList : DomainObjectBase<ScenarioNullKeyList>
{
    public List<ScenarioNullKey> Items { get; set; } = new();
}

/// <summary>可能尚未分配字符串身份的实体。</summary>
internal partial class ScenarioNullKey : DomainObjectBase<ScenarioNullKey>, IEntity
{
    public string Id { get; set; }
    public int Value { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}
