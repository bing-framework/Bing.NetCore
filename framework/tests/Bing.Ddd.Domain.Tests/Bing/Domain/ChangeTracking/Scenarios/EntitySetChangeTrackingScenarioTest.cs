using System.Collections.Generic;
using Bing.Domain.Entities;
using Xunit;
using static Bing.Domain.ChangeTracking.Scenarios.ChangeScenarioAssertions;

namespace Bing.Domain.ChangeTracking.Scenarios;

/// <summary>验证具有领域身份的无序实体集合走 GetChanges 主链并精确报告增删改。</summary>
public class EntitySetChangeTrackingScenarioTest
{
    /// <summary>忽略 HashSet 的枚举顺序，按 IEntity.GetKeys 匹配并报告一个更新和增删。</summary>
    [Fact]
    public void GetChanges_EntityHashSet_ReportsMatchedUpdateAndAddedRemovedItems()
    {
        var before = new ScenarioEntitySetRoot
        {
            Items = new HashSet<ScenarioEntitySetItem>
            {
                new() { Id = 1, Name = "旧名称" },
                new() { Id = 2, Name = "已删除" }
            }
        };
        var after = new ScenarioEntitySetRoot
        {
            Items = new HashSet<ScenarioEntitySetItem>
            {
                new() { Id = 3, Name = "新增" },
                new() { Id = 1, Name = "新名称" }
            }
        };

        AssertChanges(before.GetChanges(after),
            ("Items[1].Name", "名称", "\"旧名称\"", "\"新名称\""),
            ("Items[2]", "Items", "<object>", "<missing>"),
            ("Items[3]", "Items", "<missing>", "<object>"));
    }
}

/// <summary>以 IEntity.GetKeys 提供复合物之外的单一身份键。</summary>
internal partial class ScenarioEntitySetRoot : DomainObjectBase<ScenarioEntitySetRoot>
{
    public HashSet<ScenarioEntitySetItem> Items { get; set; } = new();
}

/// <summary>可由父实体 Set 按 GetKeys 匹配的领域明细。</summary>
internal partial class ScenarioEntitySetItem : DomainObjectBase<ScenarioEntitySetItem>, IEntity
{
    public int Id { get; set; }

    [System.ComponentModel.Description("名称")]
    public string Name { get; set; }

    public void Init() { }

    public object[] GetKeys() => new object[] { Id };
}
