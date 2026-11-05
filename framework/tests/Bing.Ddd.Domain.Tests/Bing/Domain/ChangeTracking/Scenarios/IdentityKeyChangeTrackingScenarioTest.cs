using System;
using System.Collections.Generic;
using System.ComponentModel;
using Bing.Domain.Entities;
using Xunit;
using static Bing.Domain.ChangeTracking.Scenarios.ChangeScenarioAssertions;

namespace Bing.Domain.ChangeTracking.Scenarios;

/// <summary>核对常见单一实体身份的匹配和字面路径。</summary>
public class IdentityKeyChangeTrackingScenarioTest
{
    /// <summary>不同类型身份在明细重排后仍按各自身份匹配。</summary>
    [Theory]
    [InlineData("long", "LongLines[@(\"System.Int64\",\"10000000001\")].Quantity")]
    [InlineData("guid", "GuidLines[@(\"System.Guid\",\"00000000-0000-0000-0000-000000000001\")].Quantity")]
    [InlineData("string", "TextLines[order-A].Quantity")]
    public void GetChanges_CommonKeyTypes_MatchReorderedItemsAndKeepLiteralPaths(string keyType, string expectedPath)
    {
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var before = new ScenarioIdentityOrder
        {
            LongLines = new() { new() { Id = 10000000001L, Quantity = 1 }, new() { Id = 10000000002L, Quantity = 2 } },
            GuidLines = new() { new() { Id = id1, Quantity = 1 }, new() { Id = id2, Quantity = 2 } },
            TextLines = new() { new() { Id = "order-A", Quantity = 1 }, new() { Id = "order-B", Quantity = 2 } }
        };
        var after = new ScenarioIdentityOrder
        {
            LongLines = new() { new() { Id = 10000000002L, Quantity = 2 }, new() { Id = 10000000001L, Quantity = 1 } },
            GuidLines = new() { new() { Id = id2, Quantity = 2 }, new() { Id = id1, Quantity = 1 } },
            TextLines = new() { new() { Id = "order-B", Quantity = 2 }, new() { Id = "order-A", Quantity = 1 } }
        };
        AssertChanges(before.GetChanges(after));

        switch (keyType)
        {
            case "long": after.LongLines[1].Quantity = 3; break;
            case "guid": after.GuidLines[1].Quantity = 3; break;
            case "string": after.TextLines[1].Quantity = 3; break;
            default: throw new ArgumentOutOfRangeException(nameof(keyType));
        }

        AssertChanges(before.GetChanges(after), (expectedPath, "数量", "1", "3"));
    }

    /// <summary>CMP-09、INV-14：两个尚未分配的零身份不能被静默合并为一个明细。</summary>
    [Fact]
    public void GetChanges_TwoZeroIdentities_RejectsDuplicates()
    {
        var before = new ScenarioDefaultKeyList
        {
            Items = new() { new() { Id = 0, Value = 1 }, new() { Id = 0, Value = 2 } }
        };
        var after = new ScenarioDefaultKeyList { Items = new() };

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("Items", error.Message);
    }

    /// <summary>CMP-09、INV-14：单个空 Guid 是未分配身份，必须明确拒绝。</summary>
    [Fact]
    public void GetChanges_UnassignedGuidIdentity_ThrowsWithRelationshipPath()
    {
        var before = new ScenarioIdentityOrder { GuidLines = new() { new() { Id = Guid.Empty, Quantity = 1 } } };
        var after = new ScenarioIdentityOrder { GuidLines = new() { new() { Id = Guid.Empty, Quantity = 2 } } };

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("GuidLines", error.Message);
    }

    /// <summary>带身份集合即使只包含 null，也不能退回按索引比较。</summary>
    [Fact]
    public void GetChanges_KeyedCollectionContainingOnlyNull_RejectsWithIndex()
    {
        var before = new ScenarioIdentityOrder { LongLines = new() { null } };
        var after = new ScenarioIdentityOrder { LongLines = new() };

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("LongLines[0]", error.Message);
    }

    /// <summary>单一空字符串身份没有分配，复合键中的空分量则可以由实体定义。</summary>
    [Fact]
    public void GetChanges_UnassignedStringIdentity_RejectsWithIndex()
    {
        var before = new ScenarioIdentityOrder { TextLines = new() { new() { Id = "" } } };
        var after = new ScenarioIdentityOrder();

        var error = Assert.Throws<InvalidOperationException>(() => before.GetChanges(after));
        Assert.Contains("TextLines[0]", error.Message);
    }

    /// <summary>通过 IEntity 明确声明的枚举键可用于重排匹配。</summary>
    [Fact]
    public void GetChanges_EnumEntityIdentity_MatchesReorderedItemsAndReportsUpdate()
    {
        var before = new ScenarioEnumKeyItems
        {
            Items = new() { new() { Id = ScenarioEnumKey.First, Value = 1 }, new() { Id = ScenarioEnumKey.Second, Value = 2 } }
        };
        var after = new ScenarioEnumKeyItems
        {
            Items = new() { new() { Id = ScenarioEnumKey.Second, Value = 2 }, new() { Id = ScenarioEnumKey.First, Value = 3 } }
        };

        AssertChanges(before.GetChanges(after),
            ("Items[@(\"Bing.Domain.ChangeTracking.Scenarios.ScenarioEnumKey\",\"First\")].Value", "值", "1", "3"));
    }

    /// <summary>实体 GetKeys 优先于 IKey，且复合身份允许业务有效的零与空字符串分量。</summary>
    [Fact]
    public void GetChanges_CompositeEntityKeysWithDefaultComponents_MatchByCompleteKey()
    {
        var before = new ScenarioCompositeIdentityItems
        {
            Items = new() { new() { Code = "", Sequence = 0, Value = 1 }, new() { Code = "A", Sequence = 0, Value = 2 } }
        };
        var after = new ScenarioCompositeIdentityItems
        {
            Items = new() { new() { Code = "A", Sequence = 0, Value = 2 }, new() { Code = "", Sequence = 0, Value = 3 } }
        };

        AssertChanges(before.GetChanges(after), ("Items[\"\",0].Value", "值", "1", "3"));
    }

    /// <summary>声明为普通接口的集合，IEntity 实体仍按 GetKeys 身份匹配。</summary>
    [Fact]
    public void GetChanges_InterfaceElements_UseRuntimeEntityIdentity()
    {
        var before = new ScenarioInterfaceItems
        {
            Items = new() { new ScenarioInterfaceItem { Id = 1, Value = 1 }, new ScenarioInterfaceItem { Id = 2, Value = 2 } }
        };
        var after = new ScenarioInterfaceItems
        {
            Items = new() { new ScenarioInterfaceItem { Id = 2, Value = 2 }, new ScenarioInterfaceItem { Id = 1, Value = 3 } }
        };

        AssertChanges(before.GetChanges(after), ("Items[1].Value", "值", "1", "3"));
    }

    /// <summary>多个 IKey 接口不参与领域实体身份解析，GetKeys 是唯一身份来源。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetChanges_InterfaceElementsWithMultipleKeys_UsesEntityGetKeys(bool includesUnsupportedKey)
    {
        var before = new ScenarioInterfaceItems();
        var after = new ScenarioInterfaceItems();
        if (includesUnsupportedKey)
        {
            before.Items.Add(new ScenarioMixedKeyItem { Id = 1, Value = 1 });
            before.Items.Add(new ScenarioMixedKeyItem { Id = 2, Value = 2 });
            after.Items.Add(new ScenarioMixedKeyItem { Id = 2, Value = 2 });
            after.Items.Add(new ScenarioMixedKeyItem { Id = 1, Value = 3 });
        }
        else
        {
            before.Items.Add(new ScenarioMultipleKeyItem { Id = 1, Value = 1 });
            before.Items.Add(new ScenarioMultipleKeyItem { Id = 2, Value = 2 });
            after.Items.Add(new ScenarioMultipleKeyItem { Id = 2, Value = 2 });
            after.Items.Add(new ScenarioMultipleKeyItem { Id = 1, Value = 3 });
        }

        AssertChanges(before.GetChanges(after), ("Items[1].Value", "值", "1", "3"));
    }

    /// <summary>仅实现 IKey 的领域对象没有实体身份契约，集合仍按索引比较。</summary>
    [Fact]
    public void GetChanges_InterfaceElementsWithIKeyOnly_UsesIndexMatching()
    {
        var before = new ScenarioInterfaceItems
        {
            Items = new() { new ScenarioKeyOnlyInterfaceItem(1, 1), new ScenarioKeyOnlyInterfaceItem(2, 2) }
        };
        var after = new ScenarioInterfaceItems
        {
            Items = new() { new ScenarioKeyOnlyInterfaceItem(2, 2), new ScenarioKeyOnlyInterfaceItem(1, 3) }
        };

        AssertChanges(before.GetChanges(after),
            ("Items[0].Value", "值", "1", "2"),
            ("Items[1].Value", "值", "2", "3"));
    }
}

/// <summary>使用三种常见身份类型的订单明细集合。</summary>
internal partial class ScenarioIdentityOrder : DomainObjectBase<ScenarioIdentityOrder>
{
    public List<ScenarioLongKeyLine> LongLines { get; set; } = new();
    public List<ScenarioGuidKeyLine> GuidLines { get; set; } = new();
    public List<ScenarioTextKeyLine> TextLines { get; set; } = new();
}

/// <summary>以 long 作为身份的明细。</summary>
internal partial class ScenarioLongKeyLine : DomainObjectBase<ScenarioLongKeyLine>, IEntity
{
    public long Id { get; set; }
    [Description("数量")] public int Quantity { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>以 Guid 作为身份的明细。</summary>
internal partial class ScenarioGuidKeyLine : DomainObjectBase<ScenarioGuidKeyLine>, IEntity
{
    public Guid Id { get; set; }
    [Description("数量")] public int Quantity { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>以字符串作为身份的明细。</summary>
internal partial class ScenarioTextKeyLine : DomainObjectBase<ScenarioTextKeyLine>, IEntity
{
    public string Id { get; set; }
    [Description("数量")] public int Quantity { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>由 IEntity 明确声明的枚举身份。</summary>
internal enum ScenarioEnumKey { First = 1, Second = 2 }

/// <summary>用于验证 IEntity 可以显式声明枚举身份的集合。</summary>
internal partial class ScenarioEnumKeyItems : DomainObjectBase<ScenarioEnumKeyItems>
{
    public List<ScenarioEnumKeyItem> Items { get; set; } = new();
}

/// <summary>具有枚举身份的领域对象。</summary>
internal partial class ScenarioEnumKeyItem : DomainObjectBase<ScenarioEnumKeyItem>, IEntity
{
    public ScenarioEnumKey Id { get; set; }
    [Description("值")] public int Value { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>复合身份的比较容器。</summary>
internal partial class ScenarioCompositeIdentityItems : DomainObjectBase<ScenarioCompositeIdentityItems>
{
    public List<ScenarioCompositeIdentityItem> Items { get; set; } = new();
}

/// <summary>GetKeys 指定的复合身份优先于未经支持的 IKey。</summary>
internal partial class ScenarioCompositeIdentityItem : DomainObjectBase<ScenarioCompositeIdentityItem>, IEntity, IKey<decimal>, IKey<int>
{
    public decimal Id => 0;
    int IKey<int>.Id => throw new InvalidOperationException("IEntity.GetKeys 应为唯一身份来源。");
    decimal IKey<decimal>.Id => throw new InvalidOperationException("IEntity.GetKeys 应为唯一身份来源。");
    public string Code { get; set; }
    public int Sequence { get; set; }
    [Description("值")] public int Value { get; set; }
    public object[] GetKeys() => new object[] { Code, Sequence };
    public void Init() { }
}

/// <summary>不暴露身份契约的业务接口。</summary>
internal interface IScenarioInterfaceItem { }

/// <summary>通过 IEntity 身份比较业务接口元素。</summary>
internal partial class ScenarioInterfaceItem : DomainObjectBase<ScenarioInterfaceItem>, IScenarioInterfaceItem, IEntity
{
    public int Id { get; set; }
    [Description("值")] public int Value { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>同时实现 IEntity 和多个旧式身份接口的领域对象。</summary>
internal partial class ScenarioMultipleKeyItem : DomainObjectBase<ScenarioMultipleKeyItem>, IScenarioInterfaceItem, IEntity, IKey<int>, IKey<string>
{
    public int Id { get; set; }
    [Description("值")] public int Value { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
    int IKey<int>.Id => throw new InvalidOperationException("IEntity.GetKeys 应为唯一身份来源。");
    string IKey<string>.Id => throw new InvalidOperationException("IEntity.GetKeys 应为唯一身份来源。");
}

/// <summary>同时实现 IEntity、受支持和其他旧式身份接口的领域对象。</summary>
internal partial class ScenarioMixedKeyItem : DomainObjectBase<ScenarioMixedKeyItem>, IScenarioInterfaceItem, IEntity, IKey<int>, IKey<ScenarioEnumKey>
{
    public int Id { get; set; }
    [Description("值")] public int Value { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
    int IKey<int>.Id => throw new InvalidOperationException("IEntity.GetKeys 应为唯一身份来源。");
    ScenarioEnumKey IKey<ScenarioEnumKey>.Id => throw new InvalidOperationException("IEntity.GetKeys 应为唯一身份来源。");
}

/// <summary>只有旧式 IKey 而没有 IEntity 的对象，不能据此启用身份匹配。</summary>
internal partial class ScenarioKeyOnlyInterfaceItem : DomainObjectBase<ScenarioKeyOnlyInterfaceItem>, IScenarioInterfaceItem, IKey<int>
{
    private readonly int _id;

    internal ScenarioKeyOnlyInterfaceItem(int id, int value)
    {
        _id = id;
        Value = value;
    }

    [Description("值")] public int Value { get; set; }
    int IKey<int>.Id => _id;
}

/// <summary>手写集合规则以验证任意业务接口声明的运行时匹配。</summary>
internal partial class ScenarioInterfaceItems : DomainObjectBase<ScenarioInterfaceItems>
{
    public List<IScenarioInterfaceItem> Items { get; set; } = new();
    protected override void AddChanges(ScenarioInterfaceItems other) =>
        AddCollectionChanges(nameof(Items), "项目", Items, other.Items);
}
