using System.Collections.Generic;
using System.ComponentModel;
using Bing.Domain.ChangeTracking;
using Bing.Domain.Entities;
using Xunit;
using static Bing.Domain.ChangeTracking.Scenarios.ChangeScenarioAssertions;

namespace Bing.Domain.ChangeTracking.Scenarios;

/// <summary>
/// 角色树变更跟踪业务场景。
/// </summary>
public class RoleTreeChangeTrackingScenarioTest
{
    private static ScenarioRole CreateRole(int id, string name, bool enabled = true, int? parentId = null) =>
        new()
        {
            Id = id,
            Name = name,
            Enabled = enabled,
            ParentId = parentId
        };

    private static ScenarioRole CreateRoleTree()
    {
        var root = CreateRole(1, "根角色");
        var parent = CreateRole(10, "角色10", parentId: 1);
        parent.Children.Add(CreateRole(101, "角色101", parentId: 10));
        root.Children.Add(parent);
        return root;
    }

    /// <summary>ROL-01：修改孙角色时保留从根到叶的身份路径。</summary>
    [Fact]
    public void GetChanges_ROL01_GrandchildNameChanged_UsesIdentityPath()
    {
        var before = CreateRoleTree();
        var after = CreateRoleTree();
        after.Children[0].Children[0].Name = "更新后的角色101";

        AssertChanges(before.GetChanges(after),
            ("Children[10].Children[101].Name", "角色名称", "\"角色101\"", "\"更新后的角色101\""));
    }

    /// <summary>ROL-02：新增和删除子树都只报告子树根结构事件。</summary>
    [Fact]
    public void GetChanges_ROL02_SubtreeAddedOrRemoved_ReportsOnlySubtreeRoot()
    {
        var before = CreateRole(1, "根角色");
        before.Children.Add(CreateRole(10, "角色10", parentId: 1));
        var after = CreateRole(1, "根角色");
        var afterParent = CreateRole(10, "角色10", parentId: 1);
        var addedRoot = CreateRole(101, "新增子树根", parentId: 10);
        addedRoot.Children.Add(CreateRole(1011, "新增叶角色", parentId: 101));
        afterParent.Children.Add(addedRoot);
        after.Children.Add(afterParent);

        AssertChanges(before.GetChanges(after),
            ("Children[10].Children[101]", "子角色", "<missing>", "<object>"));
        AssertChanges(after.GetChanges(before),
            ("Children[10].Children[101]", "子角色", "<object>", "<missing>"));
    }

    /// <summary>ROL-03：根字段和孙角色字段分别产生一条变化。</summary>
    [Fact]
    public void GetChanges_ROL03_RootAndGrandchildChanged_ReportsTwoIndependentPaths()
    {
        var before = CreateRoleTree();
        var after = CreateRoleTree();
        after.Name = "更新后的根角色";
        after.Children[0].Children[0].Enabled = false;

        AssertChanges(before.GetChanges(after),
            ("Name", "角色名称", "\"根角色\"", "\"更新后的根角色\""),
            ("Children[10].Children[101].Enabled", "已启用", "True", "False"));
    }

    /// <summary>ROL-04：将角色移到另一个父节点时报告旧、新两个结构路径。</summary>
    [Fact]
    public void GetChanges_ROL04_RoleMovedBetweenParents_ReportsRemovedAndAddedPaths()
    {
        var before = CreateRole(1, "根角色");
        var beforeParent10 = CreateRole(10, "角色10", parentId: 1);
        beforeParent10.Children.Add(CreateRole(101, "角色101", parentId: 10));
        before.Children.Add(beforeParent10);
        before.Children.Add(CreateRole(20, "角色20", parentId: 1));

        var after = CreateRole(1, "根角色");
        after.Children.Add(CreateRole(10, "角色10", parentId: 1));
        var afterParent20 = CreateRole(20, "角色20", parentId: 1);
        afterParent20.Children.Add(CreateRole(101, "角色101", parentId: 20));
        after.Children.Add(afterParent20);

        AssertChanges(before.GetChanges(after),
            ("Children[10].Children[101]", "子角色", "<object>", "<missing>"),
            ("Children[20].Children[101]", "子角色", "<missing>", "<object>"));
    }

    /// <summary>ROL-05：权限集合只报告成员增删，插入顺序变化为空。</summary>
    [Fact]
    public void GetChanges_ROL05_PermissionSetChanged_ReportsMembershipOnly()
    {
        var before = CreateRole(1, "角色");
        before.PermissionCodes = new HashSet<string> { "read", "write" };
        var after = CreateRole(1, "角色");
        after.PermissionCodes = new HashSet<string> { "write", "admin" };

        AssertChanges(before.GetChanges(after),
            ("PermissionCodes[\"admin\"]", "权限编码", "<missing>", "\"admin\""),
            ("PermissionCodes[\"read\"]", "权限编码", "\"read\"", "<missing>"));

        var insertionOrderBefore = CreateRole(1, "角色");
        insertionOrderBefore.PermissionCodes = new HashSet<string> { "read", "write" };
        var insertionOrderAfter = CreateRole(1, "角色");
        insertionOrderAfter.PermissionCodes = new HashSet<string> { "write", "read" };

        AssertChanges(insertionOrderBefore.GetChanges(insertionOrderAfter));
    }

    /// <summary>ROL-06：保留父导航形成回环时仍只报告可达叶字段。</summary>
    [Fact]
    public void GetChanges_ROL06_ParentNavigationCycle_ReportsFiniteLeafChange()
    {
        var beforeRoot = new ScenarioRoleWithParent { Id = 1, Name = "根角色" };
        var beforeParent = new ScenarioRoleWithParent { Id = 10, Name = "角色10", Parent = beforeRoot };
        var beforeLeaf = new ScenarioRoleWithParent { Id = 101, Name = "角色101", Parent = beforeParent };
        beforeRoot.Children.Add(beforeParent);
        beforeParent.Children.Add(beforeLeaf);

        var afterRoot = new ScenarioRoleWithParent { Id = 1, Name = "根角色" };
        var afterParent = new ScenarioRoleWithParent { Id = 10, Name = "角色10", Parent = afterRoot };
        var afterLeaf = new ScenarioRoleWithParent { Id = 101, Name = "更新后的角色101", Parent = afterParent };
        afterRoot.Children.Add(afterParent);
        afterParent.Children.Add(afterLeaf);

        AssertChanges(beforeRoot.GetChanges(afterRoot),
            ("Children[10].Children[101].Name", "角色名称", "\"角色101\"", "\"更新后的角色101\""));
    }

    /// <summary>ROL-07：自引用与三节点环均有限终止并保留叶字段差异。</summary>
    [Fact]
    public void GetChanges_ROL07_SelfAndThreeNodeCycles_KeepReachableChanges()
    {
        var beforeSelf = new ScenarioRoleCycleNode { Id = 1, Name = "旧自关联" };
        beforeSelf.Self = beforeSelf;
        var afterSelf = new ScenarioRoleCycleNode { Id = 1, Name = "新自关联" };
        afterSelf.Self = afterSelf;
        AssertChanges(beforeSelf.GetChanges(afterSelf),
            ("Name", "角色名称", "\"旧自关联\"", "\"新自关联\""));

        var beforeA = new ScenarioRoleCycleNode { Id = 1, Name = "A" };
        var beforeB = new ScenarioRoleCycleNode { Id = 2, Name = "B" };
        var beforeC = new ScenarioRoleCycleNode { Id = 3, Name = "旧C" };
        beforeA.Next = beforeB;
        beforeB.Next = beforeC;
        beforeC.Next = beforeA;

        var afterA = new ScenarioRoleCycleNode { Id = 1, Name = "A" };
        var afterB = new ScenarioRoleCycleNode { Id = 2, Name = "B" };
        var afterC = new ScenarioRoleCycleNode { Id = 3, Name = "新C" };
        afterA.Next = afterB;
        afterB.Next = afterC;
        afterC.Next = afterA;

        AssertChanges(beforeA.GetChanges(afterA),
            ("Next.Next.Name", "角色名称", "\"旧C\"", "\"新C\""));
    }

    /// <summary>ROL-08：从子角色观察时，Parent 忽略与保留分别限定观察范围。</summary>
    [Fact]
    public void GetChanges_ROL08_ParentIgnoredOrPreserved_UsesExpectedObservationBoundary()
    {
        var ignoredBeforeParent = new ScenarioRoleIgnoredParent { Id = 10, Name = "父角色" };
        var ignoredBeforeChild = new ScenarioRoleIgnoredParent { Id = 101, Name = "子角色", Parent = ignoredBeforeParent };
        ignoredBeforeParent.Children.Add(ignoredBeforeChild);
        ignoredBeforeParent.Children.Add(new ScenarioRoleIgnoredParent { Id = 102, Name = "旧兄弟" });
        var ignoredAfterParent = new ScenarioRoleIgnoredParent { Id = 10, Name = "父角色" };
        var ignoredAfterChild = new ScenarioRoleIgnoredParent { Id = 101, Name = "子角色", Parent = ignoredAfterParent };
        ignoredAfterParent.Children.Add(ignoredAfterChild);
        ignoredAfterParent.Children.Add(new ScenarioRoleIgnoredParent { Id = 102, Name = "新兄弟" });
        AssertChanges(ignoredBeforeChild.GetChanges(ignoredAfterChild));

        var preservedBeforeParent = new ScenarioRoleWithParent { Id = 10, Name = "父角色" };
        var preservedBeforeChild = new ScenarioRoleWithParent { Id = 101, Name = "子角色", Parent = preservedBeforeParent };
        preservedBeforeParent.Children.Add(preservedBeforeChild);
        preservedBeforeParent.Children.Add(new ScenarioRoleWithParent { Id = 102, Name = "旧兄弟", Parent = preservedBeforeParent });
        var preservedAfterParent = new ScenarioRoleWithParent { Id = 10, Name = "父角色" };
        var preservedAfterChild = new ScenarioRoleWithParent { Id = 101, Name = "子角色", Parent = preservedAfterParent };
        preservedAfterParent.Children.Add(preservedAfterChild);
        preservedAfterParent.Children.Add(new ScenarioRoleWithParent { Id = 102, Name = "新兄弟", Parent = preservedAfterParent });

        AssertChanges(preservedBeforeChild.GetChanges(preservedAfterChild),
            ("Parent.Children[102].Name", "角色名称", "\"旧兄弟\"", "\"新兄弟\""));
    }
}

/// <summary>主角色模型；父导航按业务规则忽略，权限编码只保存显式集合。</summary>
internal partial class ScenarioRole : DomainObjectBase<ScenarioRole>, IEntity
{
    public int Id { get; set; }
    [Description("角色名称")] public string Name { get; set; }
    [Description("已启用")] public bool Enabled { get; set; }
    [Description("上级角色编号")] public int? ParentId { get; set; }
    [Description("子角色")] public List<ScenarioRole> Children { get; set; } = new();
    [Description("上级角色")]
    [ChangeIgnore]
    public ScenarioRole Parent { get; set; }
    [Description("权限编码")] public HashSet<string> PermissionCodes { get; set; } = new();
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>保留父子导航的角色节点，用于验证回环以及向上观察兄弟节点。</summary>
internal partial class ScenarioRoleWithParent : DomainObjectBase<ScenarioRoleWithParent>, IEntity
{
    public int Id { get; set; }
    [Description("角色名称")] public string Name { get; set; }
    [Description("子角色")] public List<ScenarioRoleWithParent> Children { get; set; } = new();
    [Description("上级角色")] public ScenarioRoleWithParent Parent { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>忽略父导航的角色节点，用于固定从子节点开始比较的观察边界。</summary>
internal partial class ScenarioRoleIgnoredParent : DomainObjectBase<ScenarioRoleIgnoredParent>, IEntity
{
    public int Id { get; set; }
    [Description("角色名称")] public string Name { get; set; }
    [Description("子角色")] public List<ScenarioRoleIgnoredParent> Children { get; set; } = new();
    [Description("上级角色")]
    [ChangeIgnore]
    public ScenarioRoleIgnoredParent Parent { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}

/// <summary>可形成自引用或多节点环的角色节点。</summary>
internal partial class ScenarioRoleCycleNode : DomainObjectBase<ScenarioRoleCycleNode>, IEntity
{
    public int Id { get; set; }
    [Description("角色名称")] public string Name { get; set; }
    [Description("自关联")] public ScenarioRoleCycleNode Self { get; set; }
    [Description("下一个角色")] public ScenarioRoleCycleNode Next { get; set; }
    public void Init() { }
    public object[] GetKeys() => new object[] { Id };
}
