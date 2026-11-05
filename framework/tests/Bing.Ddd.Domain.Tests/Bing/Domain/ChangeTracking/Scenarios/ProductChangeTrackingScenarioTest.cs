using System;
using System.Collections.Generic;
using System.ComponentModel;
using Bing.Domain.Entities;
using Xunit;
using static Bing.Domain.ChangeTracking.Scenarios.ChangeScenarioAssertions;

namespace Bing.Domain.ChangeTracking.Scenarios;

/// <summary>
/// 商品、SKU、规格与价格的变更跟踪业务场景。
/// </summary>
public class ProductChangeTrackingScenarioTest
{
    private static ScenarioProduct CreateProduct() => new()
    {
        Id = 1,
        Name = "衬衫",
        Details = new()
        {
            new()
            {
                Id = 2001, SkuCode = "SKU-A", Price = 99.5m,
                Specifications = new(StringComparer.OrdinalIgnoreCase) { ["Color"] = "红", ["Material"] = "棉" }
            }
        },
        Tags = new(StringComparer.OrdinalIgnoreCase) { "red", "Blue" }
    };

    /// <summary>PRD-01：记录具体 SKU 的价格变化。</summary>
    [Fact]
    public void GetChanges_ProductSkuPriceChanged_ReportsSkuPath()
    {
        var before = CreateProduct();
        var after = CreateProduct();
        after.Details[0].Price = 89.5m;

        AssertChanges(before.GetChanges(after), ("Details[2001].Price", "价格", "99.5", "89.5"));
    }

    /// <summary>PRD-02：相等的 decimal 值不因小数位展示不同而产生变化。</summary>
    [Fact]
    public void GetChanges_EquivalentDecimalPrices_ReturnsEmpty()
    {
        var before = CreateProduct();
        var after = CreateProduct();
        before.Details[0].Price = 1.0m;
        after.Details[0].Price = 1.00m;

        AssertChanges(before.GetChanges(after));
    }

    /// <summary>PRD-03：SKU 身份替换按旧项删除和新项新增报告。</summary>
    [Fact]
    public void GetChanges_ProductSkuIdentityReplaced_ReportsRemoveAndAdd()
    {
        var before = CreateProduct();
        var after = CreateProduct();
        after.Details[0].Id = 2002;

        AssertChanges(before.GetChanges(after),
            ("Details[2001]", "产品明细", "<object>", "<missing>"),
            ("Details[2002]", "产品明细", "<missing>", "<object>"));
    }

    /// <summary>PRD-04：同次比较准确报告规格项增删改及新旧值。</summary>
    [Fact]
    public void GetChanges_SpecificationsAddedRemovedAndModified_ReportsCompleteDifferences()
    {
        var before = CreateProduct();
        var after = CreateProduct();
        after.Details[0].Specifications["Color"] = "蓝";
        after.Details[0].Specifications.Remove("Material");
        after.Details[0].Specifications["Size"] = "M";

        AssertChanges(before.GetChanges(after),
            ("Details[2001].Specifications{Color}", "规格", "\"红\"", "\"蓝\""),
            ("Details[2001].Specifications{Material}", "规格", "\"棉\"", "<missing>"),
            ("Details[2001].Specifications{Size}", "规格", "<missing>", "\"M\""));
    }

    /// <summary>PRD-05：规格字典使用其自身的大小写比较器。</summary>
    [Fact]
    public void GetChanges_SpecificationKeysDifferOnlyByCase_UsesDictionaryComparer()
    {
        var before = CreateProduct();
        var after = CreateProduct();
        after.Details[0].Specifications = new(StringComparer.OrdinalIgnoreCase)
        {
            ["color"] = "红", ["Material"] = "棉"
        };
        AssertChanges(before.GetChanges(after));

        after.Details[0].Specifications["color"] = "蓝";
        AssertChanges(before.GetChanges(after), ("Details[2001].Specifications{Color}", "规格", "\"红\"", "\"蓝\""));
    }

    /// <summary>PRD-06：标签按 Set 比较器处理，纯顺序变化无差异。</summary>
    [Fact]
    public void GetChanges_ProductTags_UsesSetComparerAndIgnoresInsertionOrder()
    {
        var before = CreateProduct();
        var after = CreateProduct();
        after.Tags = new(StringComparer.OrdinalIgnoreCase) { "blue", "red" };
        AssertChanges(before.GetChanges(after));

        after.Tags.Remove("red");
        after.Tags.Add("green");
        AssertChanges(before.GetChanges(after),
            ("Tags[\"red\"]", "标签", "\"red\"", "<missing>"),
            ("Tags[\"green\"]", "标签", "<missing>", "\"green\""));
    }

    /// <summary>PRD-07：多级领域对象中的字典值继续使用金额对象自身规则。</summary>
    [Fact]
    public void GetChanges_ProductPriceGroupCurrency_UsesMoneyOwnChanges()
    {
        var before = CreateProduct();
        var after = CreateProduct();
        before.Details[0].PriceGroups.Add(new ScenarioPriceGroup
        {
            Id = 7, Currencies = new(StringComparer.OrdinalIgnoreCase) { ["USD"] = new() { Amount = 10m } }
        });
        after.Details[0].PriceGroups.Add(new ScenarioPriceGroup
        {
            Id = 7, Currencies = new(StringComparer.OrdinalIgnoreCase) { ["usd"] = new() { Amount = 12m } }
        });

        AssertChanges(before.Details[0].PriceGroups[0].Currencies["USD"].GetChanges(
                after.Details[0].PriceGroups[0].Currencies["usd"]),
            ("Amount", "金额", "10", "12"));
        AssertChanges(before.GetChanges(after),
            ("Details[2001].PriceGroups[7].Currencies{USD}.Amount", "金额", "10", "12"));
    }

    /// <summary>PRD-07：没有领域对象包裹的内层字典也应维持原有比较器。</summary>
    [Fact]
    public void GetChanges_ProductNestedCurrencyDictionary_PreservesInnerComparer()
    {
        var before = CreateProduct();
        var after = CreateProduct();
        before.Details[0].NestedPrices["retail"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = new ScenarioMoney { Amount = 10m }
        };
        after.Details[0].NestedPrices["retail"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["usd"] = new ScenarioMoney { Amount = 12m }
        };

        AssertChanges(before.GetChanges(after),
            ("Details[2001].NestedPrices{retail}{USD}.Amount", "金额", "10", "12"));
    }

    /// <summary>PRD-08：SKU 手写大小写规则在三种关系中保持一致。</summary>
    [Fact]
    public void GetChanges_ManualSkuRule_IsPreservedInObjectListAndDictionary()
    {
        var oldSku = new ScenarioManualSku { Id = 1, SkuCode = "ABC", Price = 10m };
        var newSku = new ScenarioManualSku { Id = 1, SkuCode = "abc", Price = 20m };
        var before = new ScenarioSkuReferences
        {
            Direct = oldSku, Items = new() { oldSku }, Map = new() { ["current"] = oldSku }
        };
        var after = new ScenarioSkuReferences
        {
            Direct = newSku, Items = new() { newSku }, Map = new() { ["current"] = newSku }
        };

        AssertChanges(oldSku.GetChanges(newSku));
        AssertChanges(before.GetChanges(after));
        newSku.SkuCode = "XYZ";
        AssertChanges(oldSku.GetChanges(newSku), ("SkuCode", "SKU编码", "\"ABC\"", "\"XYZ\""));
        AssertChanges(before.GetChanges(after),
            ("Direct.SkuCode", "SKU编码", "\"ABC\"", "\"XYZ\""),
            ("Items[1].SkuCode", "SKU编码", "\"ABC\"", "\"XYZ\""),
            ("Map{current}.SkuCode", "SKU编码", "\"ABC\"", "\"XYZ\""));
    }

    /// <summary>PRD-09：建模为领域对象的元数据必须实际报告子字段变化。</summary>
    [Fact]
    public void GetChanges_ProductDomainMetadata_ReportsNestedField()
    {
        var before = CreateProduct();
        var after = CreateProduct();
        before.Details[0].Metadata = new ScenarioProductMetadata { Origin = "中国" };
        after.Details[0].Metadata = new ScenarioProductMetadata { Origin = "日本" };

        AssertChanges(before.GetChanges(after), ("Details[2001].Metadata.Origin", "产地", "\"中国\"", "\"日本\""));
    }
}

/// <summary>测试用商品聚合。</summary>
internal partial class ScenarioProduct : DomainObjectBase<ScenarioProduct>
{
    public int Id { get; set; }
    public string Name { get; set; }
    [Description("产品明细")] public List<ScenarioSku> Details { get; set; } = new();
    [Description("标签")] public HashSet<string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>测试用 SKU，保存规格和多层价格。</summary>
internal partial class ScenarioSku : DomainObjectBase<ScenarioSku>, IEntity
{
    public int Id { get; set; }
    public string SkuCode { get; set; }
    [Description("价格")] public decimal Price { get; set; }
    [Description("规格")] public Dictionary<string, string> Specifications { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ScenarioPriceGroup> PriceGroups { get; set; } = new();
    public Dictionary<string, Dictionary<string, ScenarioMoney>> NestedPrices { get; set; } = new();
    public ScenarioProductMetadata Metadata { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>价格分组拥有自己的身份和币种字典。</summary>
internal partial class ScenarioPriceGroup : DomainObjectBase<ScenarioPriceGroup>, IEntity
{
    public int Id { get; set; }
    public Dictionary<string, ScenarioMoney> Currencies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>作为领域对象参与比较的金额。</summary>
internal partial class ScenarioMoney : DomainObjectBase<ScenarioMoney>
{
    [Description("金额")] public decimal Amount { get; set; }
}

/// <summary>领域化的商品元数据。</summary>
internal partial class ScenarioProductMetadata : DomainObjectBase<ScenarioProductMetadata>
{
    [Description("产地")] public string Origin { get; set; }
}

/// <summary>手写 SKU 编码比较规则，不采集价格。</summary>
internal partial class ScenarioManualSku : DomainObjectBase<ScenarioManualSku>, IEntity
{
    public int Id { get; set; }
    public string SkuCode { get; set; }
    public decimal Price { get; set; }

    protected override void AddChanges(ScenarioManualSku other)
    {
        if (!string.Equals(SkuCode, other.SkuCode, StringComparison.OrdinalIgnoreCase))
            AddChange(nameof(SkuCode), "SKU编码", SkuCode, other.SkuCode);
    }

    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>同时用对象、列表和字典引用 SKU 的测试聚合。</summary>
internal partial class ScenarioSkuReferences : DomainObjectBase<ScenarioSkuReferences>
{
    public ScenarioManualSku Direct { get; set; }
    public List<ScenarioManualSku> Items { get; set; } = new();
    public Dictionary<string, ScenarioManualSku> Map { get; set; } = new();
}
