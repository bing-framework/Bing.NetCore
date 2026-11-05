using System.ComponentModel;
using Bing.Domain.ChangeTracking;
using Bing.Domain.Entities;

/// <summary>验证独立领域比较链在 JIT 与 NativeAOT 下处理嵌套容器和实体身份。</summary>
internal static class DomainNestedScenarios
{
    /// <summary>运行强类型嵌套比较与无效身份断言。</summary>
    internal static void Run()
    {
        var before = new DomainNestedOrder
        {
            Groups = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Stock"] = new() { new() { Id = 1, Quantity = 2 }, new() { Id = 2, Quantity = 5 } }
            },
            Tags = new() { ["main"] = new(StringComparer.OrdinalIgnoreCase) { "A", "b" } },
            ReadOnlyTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GREEN" }
        };
        var after = new DomainNestedOrder
        {
            Groups = new(StringComparer.OrdinalIgnoreCase)
            {
                ["stock"] = new() { new() { Id = 2, Quantity = 5 }, new() { Id = 1, Quantity = 3 } }
            },
            Tags = new() { ["main"] = new(StringComparer.OrdinalIgnoreCase) { "B", "a" } },
            ReadOnlyTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "green" }
        };

        var change = before.GetChanges(after).Single();
        if (change.PropertyName != "Groups{Stock}[1].Quantity" || change.Description != "数量" ||
            change.OldValue != "2" || change.NewValue != "3")
            throw new InvalidOperationException("嵌套容器、实体身份或描述不符合领域比较契约。");

        var oldChild = new DomainNestedOrder
        {
            Child = new DomainNestedChild { Name = "旧名称", ThrowOnCompare = true, ThrowOnNameRead = true }
        };
        var childDeletion = oldChild.GetChanges(new DomainNestedOrder { Child = null }).Single();
        if (childDeletion.PropertyName != "Child" || childDeletion.Description != "子对象" ||
            childDeletion.OldValue != "<object>" || childDeletion.NewValue != "<missing>")
            throw new InvalidOperationException("子关系为 null 时未仅报告结构删除。");
        Console.WriteLine("PASS | 子对象为 null 时只报告结构删除");

        after.Groups["stock"][1].Id = 0;
        try
        {
            before.GetChanges(after);
            throw new InvalidOperationException("无效身份未被拒绝。");
        }
        catch (InvalidOperationException error) when (error.Message.Contains("Groups{Stock}[1]", StringComparison.Ordinal))
        {
            Console.WriteLine("PASS | 嵌套容器、比较器和无效身份路径");
        }

        var customBefore = new DomainNestedOrder { ReadOnlyTags = new DomainReadOnlySet("A") };
        var customAfter = new DomainNestedOrder { ReadOnlyTags = new DomainReadOnlySet("a") };
        try
        {
            customBefore.GetChanges(customAfter);
            throw new InvalidOperationException("未知只读 Set 的比较器未被拒绝。");
        }
        catch (InvalidOperationException error) when (error.Message.Contains("ReadOnlyTags", StringComparison.Ordinal))
        {
            Console.WriteLine("PASS | 只读 Set 保留静态语义并拒绝未知比较器");
        }

        var versions = new DomainVersionSet { Versions = new() { new Version(1, 0) } }
            .GetChanges(new DomainVersionSet { Versions = new() { new Version(2, 0) } });
        if (versions.Count() != 2 || !versions.Any(change => change.PropertyName == "Versions[1.0]") ||
            !versions.Any(change => change.PropertyName == "Versions[2.0]"))
            throw new InvalidOperationException("Version Set 未按标量成员比较。");
        Console.WriteLine("PASS | Version Set 标量成员比较");

        var multipleKeysBefore = new DomainInterfaceItems
        {
            Items = new() { new DomainMultipleKeyEntityItem { Id = 1, Value = 1 }, new DomainMultipleKeyEntityItem { Id = 2, Value = 2 } }
        };
        var multipleKeysAfter = new DomainInterfaceItems
        {
            Items = new() { new DomainMultipleKeyEntityItem { Id = 2, Value = 2 }, new DomainMultipleKeyEntityItem { Id = 1, Value = 3 } }
        };
        var multipleKeyChange = multipleKeysBefore.GetChanges(multipleKeysAfter).Single();
        if (multipleKeyChange.PropertyName != "Items[1].Value" || multipleKeyChange.Description != "值" ||
            multipleKeyChange.OldValue != "1" || multipleKeyChange.NewValue != "3")
            throw new InvalidOperationException("IEntity.GetKeys 未优先于显式 IKey 接口。");
        Console.WriteLine("PASS | AOT 多态实体仅通过 GetKeys 匹配");

        var keyOnlyBefore = new DomainInterfaceItems
        {
            Items = new() { new DomainKeyOnlyItem(1, 1), new DomainKeyOnlyItem(2, 2) }
        };
        var keyOnlyAfter = new DomainInterfaceItems
        {
            Items = new() { new DomainKeyOnlyItem(2, 2), new DomainKeyOnlyItem(1, 3) }
        };
        var keyOnlyChanges = keyOnlyBefore.GetChanges(keyOnlyAfter).ToArray();
        if (keyOnlyChanges.Length != 2 ||
            !keyOnlyChanges.Any(change => change.PropertyName == "Items[0].Value" && change.OldValue == "1" && change.NewValue == "2") ||
            !keyOnlyChanges.Any(change => change.PropertyName == "Items[1].Value" && change.OldValue == "2" && change.NewValue == "3"))
            throw new InvalidOperationException("仅实现 IKey 的无 IEntity 对象未按索引比较。");
        Console.WriteLine("PASS | 无 IEntity 的 IKey 对象按索引比较");
    }
}

/// <summary>原生 AOT 下的订单分组与标签。</summary>
internal partial class DomainNestedOrder : DomainObjectBase<DomainNestedOrder>
{
    public Dictionary<string, List<DomainNestedItem>> Groups { get; set; } = new();
    public Dictionary<string, HashSet<string>> Tags { get; set; } = new();
    public IReadOnlySet<string>? ReadOnlyTags { get; set; }
    [Description("子对象")] public DomainNestedChild? Child { get; set; }
}

/// <summary>验证子关系删除不会读取旧子对象或执行其比较 hook。</summary>
internal partial class DomainNestedChild : DomainObjectBase<DomainNestedChild>
{
    private string? _name;

    public bool ThrowOnCompare { get; set; }
    public bool ThrowOnNameRead { get; set; }
    public string? Name
    {
        get => ThrowOnNameRead ? throw new InvalidOperationException("已删除子对象不应读取 Name") : _name;
        set => _name = value;
    }

    protected override void AddChanges(DomainNestedChild other)
    {
        if (ThrowOnCompare)
            throw new InvalidOperationException("已删除子对象不应调用 AddChanges");
        AddChange(nameof(Name), "名称", Name, other.Name);
    }
}

/// <summary>原生 AOT 下的有身份订单明细。</summary>
internal partial class DomainNestedItem : DomainObjectBase<DomainNestedItem>, IEntity
{
    public int Id { get; set; }
    [Description("数量")] public int Quantity { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>原生 AOT 下的 Version 标量集合。</summary>
internal partial class DomainVersionSet : DomainObjectBase<DomainVersionSet>
{
    public HashSet<Version> Versions { get; set; } = new();
}

/// <summary>不暴露身份契约的业务接口。</summary>
internal interface IDomainInterfaceItem { }

/// <summary>通过接口属性验证领域实体身份与无身份索引比较。</summary>
internal partial class DomainInterfaceItems : DomainObjectBase<DomainInterfaceItems>
{
    public List<IDomainInterfaceItem> Items { get; set; } = new();

    protected override void AddChanges(DomainInterfaceItems other) =>
        AddCollectionChanges(nameof(Items), nameof(Items), Items, other.Items);
}

/// <summary>多个 IKey 接口存在时仍只使用 IEntity.GetKeys。</summary>
internal partial class DomainMultipleKeyEntityItem : DomainObjectBase<DomainMultipleKeyEntityItem>, IDomainInterfaceItem, IEntity, IKey<int>, IKey<DomainUnknownKey>
{
    public int Id { get; set; }
    [Description("值")] public int Value { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
    int IKey<int>.Id => throw new InvalidOperationException("IEntity.GetKeys 是唯一身份来源。");
    DomainUnknownKey IKey<DomainUnknownKey>.Id => throw new InvalidOperationException("IEntity.GetKeys 是唯一身份来源。");
}

/// <summary>无 IEntity 的 IKey 对象按集合索引比较。</summary>
internal partial class DomainKeyOnlyItem : DomainObjectBase<DomainKeyOnlyItem>, IDomainInterfaceItem, IKey<int>
{
    private readonly int _id;

    internal DomainKeyOnlyItem(int id, int value)
    {
        _id = id;
        Value = value;
    }

    [Description("值")] public int Value { get; set; }
    int IKey<int>.Id => _id;
}

internal enum DomainUnknownKey { First = 1 }

/// <summary>不公开内部比较器的只读 Set。</summary>
internal sealed class DomainReadOnlySet : IReadOnlySet<string>
{
    private readonly HashSet<string> _items;

    internal DomainReadOnlySet(params string[] items) => _items = new(items, StringComparer.OrdinalIgnoreCase);
    public int Count => _items.Count;
    public bool Contains(string item) => _items.Contains(item);
    public bool IsProperSubsetOf(IEnumerable<string> other) => _items.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<string> other) => _items.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<string> other) => _items.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<string> other) => _items.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<string> other) => _items.Overlaps(other);
    public bool SetEquals(IEnumerable<string> other) => _items.SetEquals(other);
    public IEnumerator<string> GetEnumerator() => _items.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
