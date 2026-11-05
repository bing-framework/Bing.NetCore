using System.Collections.Generic;
using System.ComponentModel;
using Bing.Domain.ChangeTracking;
using Bing.Domain.Entities;
using Xunit;
using static Bing.Domain.ChangeTracking.Scenarios.ChangeScenarioAssertions;

namespace Bing.Domain.ChangeTracking.Scenarios;

/// <summary>
/// 菜单树变更跟踪业务场景。
/// </summary>
public class MenuTreeChangeTrackingScenarioTest
{
    private static ScenarioMenu CreateMenu(int id, string name, string route = "/", int sort = 0,
        bool visible = true, int? parentId = null) =>
        new()
        {
            Id = id,
            Name = name,
            Route = route,
            Sort = sort,
            Visible = visible,
            ParentId = parentId
        };

    /// <summary>MNU-01：二级菜单路由变化保留原始斜线和大小写。</summary>
    [Fact]
    public void GetChanges_MNU01_SecondLevelRouteChanged_ReportsExactRoute()
    {
        var before = CreateMenu(1, "根菜单");
        var beforeParent = CreateMenu(10, "订单", "/orders-root", parentId: 1);
        beforeParent.Children.Add(CreateMenu(101, "订单列表", "/orders", parentId: 10));
        before.Children.Add(beforeParent);

        var after = CreateMenu(1, "根菜单");
        var afterParent = CreateMenu(10, "订单", "/orders-root", parentId: 1);
        afterParent.Children.Add(CreateMenu(101, "订单列表", "/sales", parentId: 10));
        after.Children.Add(afterParent);

        AssertChanges(before.GetChanges(after),
            ("Children[10].Children[101].Route", "路由", "\"/orders\"", "\"/sales\""));
    }

    /// <summary>MNU-02：同层按实体身份重排且排序字段未变时结果为空。</summary>
    [Fact]
    public void GetChanges_MNU02_SiblingMenusReorderedWithoutSortChange_ReturnsEmpty()
    {
        var before = CreateMenu(1, "根菜单");
        before.Children.Add(CreateMenu(10, "订单", "/orders", 1, parentId: 1));
        before.Children.Add(CreateMenu(20, "客户", "/customers", 2, parentId: 1));
        var after = CreateMenu(1, "根菜单");
        after.Children.Add(CreateMenu(20, "客户", "/customers", 2, parentId: 1));
        after.Children.Add(CreateMenu(10, "订单", "/orders", 1, parentId: 1));

        AssertChanges(before.GetChanges(after));
    }

    /// <summary>MNU-03：交换排序值只报告两个节点各自的 Sort 字段。</summary>
    [Fact]
    public void GetChanges_MNU03_SiblingSortValuesSwapped_ReportsTwoSortChanges()
    {
        var before = CreateMenu(1, "根菜单");
        before.Children.Add(CreateMenu(10, "订单", "/orders", 1, parentId: 1));
        before.Children.Add(CreateMenu(20, "客户", "/customers", 2, parentId: 1));
        var after = CreateMenu(1, "根菜单");
        after.Children.Add(CreateMenu(10, "订单", "/orders", 2, parentId: 1));
        after.Children.Add(CreateMenu(20, "客户", "/customers", 1, parentId: 1));

        AssertChanges(before.GetChanges(after),
            ("Children[10].Sort", "排序", "1", "2"),
            ("Children[20].Sort", "排序", "2", "1"));
    }

    /// <summary>MNU-04：隐藏父菜单不推导或改写子菜单的可见状态。</summary>
    [Fact]
    public void GetChanges_MNU04_ParentHidden_ReportsOnlyParentVisibility()
    {
        var before = CreateMenu(1, "根菜单");
        var beforeParent = CreateMenu(10, "订单", "/orders", visible: true, parentId: 1);
        beforeParent.Children.Add(CreateMenu(101, "订单列表", "/orders/list", visible: true, parentId: 10));
        before.Children.Add(beforeParent);
        var after = CreateMenu(1, "根菜单");
        var afterParent = CreateMenu(10, "订单", "/orders", visible: true, parentId: 1);
        afterParent.Children.Add(CreateMenu(101, "订单列表", "/orders/list", visible: true, parentId: 10));
        after.Children.Add(afterParent);
        after.Children[0].Visible = false;

        AssertChanges(before.GetChanges(after),
            ("Children[10].Visible", "是否可见", "True", "False"));
    }

    /// <summary>MNU-05：删除含多个后代的菜单分支只报告分支根结构变化。</summary>
    [Fact]
    public void GetChanges_MNU05_MenuBranchRemoved_ReportsOnlyBranchRoot()
    {
        var before = CreateMenu(1, "根菜单");
        var branch = CreateMenu(10, "管理", "/admin", parentId: 1);
        branch.Children.Add(CreateMenu(101, "用户管理", "/admin/users", parentId: 10));
        branch.Children.Add(CreateMenu(102, "角色管理", "/admin/roles", parentId: 10));
        before.Children.Add(branch);
        var after = CreateMenu(1, "根菜单");

        AssertChanges(before.GetChanges(after),
            ("Children[10]", "子菜单", "<object>", "<missing>"));
    }

    /// <summary>MNU-06：字符串控制字符转义准确，未标注描述时回退成员名。</summary>
    [Fact]
    public void GetChanges_MNU06_NameEscapingAndDescriptionFallback_AreExact()
    {
        var before = CreateMenu(1, "旧\n\"菜单\"");
        var after = CreateMenu(1, "新\r\n\"主菜单\"");
        AssertChanges(before.GetChanges(after),
            ("Name", "菜单名称", "\"旧\\n\\\"菜单\\\"\"", "\"新\\r\\n\\\"主菜单\\\"\""));

        var undescribedBefore = new ScenarioMenuWithoutDescription { Id = 2, Name = "旧名称" };
        var undescribedAfter = new ScenarioMenuWithoutDescription { Id = 2, Name = "新名称" };
        AssertChanges(undescribedBefore.GetChanges(undescribedAfter),
            ("Name", "Name", "\"旧名称\"", "\"新名称\""));
    }

    /// <summary>MNU-07：同一菜单实例经收藏和主树两条路径可达时保留两条差异。</summary>
    [Fact]
    public void GetChanges_MNU07_SharedMenuInFavoritesAndChildren_ReportsBothPaths()
    {
        var before = CreateMenu(1, "根菜单");
        var beforeShared = CreateMenu(11, "旧收藏菜单", "/shared");
        before.Favorites.Add(beforeShared);
        before.Children.Add(beforeShared);

        var after = CreateMenu(1, "根菜单");
        var afterShared = CreateMenu(11, "新收藏菜单", "/shared");
        after.Favorites.Add(afterShared);
        after.Children.Add(afterShared);

        AssertChanges(before.GetChanges(after),
            ("Children[11].Name", "菜单名称", "\"旧收藏菜单\"", "\"新收藏菜单\""),
            ("Favorites[11].Name", "菜单名称", "\"旧收藏菜单\"", "\"新收藏菜单\""));
    }

    /// <summary>MNU-08：生成模型默认忽略 IsDeleted，手写 hook 可显式跟踪软删除。</summary>
    [Fact]
    public void GetChanges_MNU08_IsDeletedIgnoredByGeneratedModelAndTrackedByManualHook()
    {
        var generatedBefore = CreateMenu(1, "菜单");
        var generatedAfter = CreateMenu(1, "菜单");
        generatedAfter.IsDeleted = true;
        AssertChanges(generatedBefore.GetChanges(generatedAfter));

        var manualBefore = new ScenarioManualSoftDeleteMenu { Id = 2, IsDeleted = false };
        var manualAfter = new ScenarioManualSoftDeleteMenu { Id = 2, IsDeleted = true };
        AssertChanges(manualBefore.GetChanges(manualAfter),
            ("IsDeleted", "软删除", "False", "True"));
    }
}

/// <summary>菜单树节点，保留树、收藏列表和用于业务展示的标量属性。</summary>
internal partial class ScenarioMenu : DomainObjectBase<ScenarioMenu>, IEntity
{
    public int Id { get; set; }
    [Description("菜单名称")] public string Name { get; set; }
    [Description("路由")] public string Route { get; set; }
    [Description("排序")] public int Sort { get; set; }
    [Description("是否可见")] public bool Visible { get; set; }
    [Description("上级菜单编号")] public int? ParentId { get; set; }
    [Description("子菜单")] public List<ScenarioMenu> Children { get; set; } = new();
    [Description("收藏菜单")] public List<ScenarioMenu> Favorites { get; set; } = new();
    [Description("上级菜单")]
    [ChangeIgnore]
    public ScenarioMenu Parent { get; set; }
    public bool IsDeleted { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>Name 未声明 Description 特性时应以成员名作为显示描述。</summary>
internal partial class ScenarioMenuWithoutDescription : DomainObjectBase<ScenarioMenuWithoutDescription>, IEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>手写变更 hook，显式决定跟踪 IsDeleted。</summary>
internal partial class ScenarioManualSoftDeleteMenu : DomainObjectBase<ScenarioManualSoftDeleteMenu>, IEntity
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }

    protected override void AddChanges(ScenarioManualSoftDeleteMenu other) =>
        AddChange(nameof(IsDeleted), "软删除", IsDeleted, other.IsDeleted);

    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}
