using Bing.Domain.Entities;
using Bing.Tests.Samples;
using Bing.Trees;

namespace Bing.Trees;

/// <summary>
/// 测试目的：验证 <see cref="TreeEntityBase{TEntity}"/> 中 InitPath / GetParentIdsFromPath 核心逻辑。
/// </summary>
public class TreeEntityBaseTest
{
    // ── InitPath（无父节点 / 根节点） ────────────────────────────────

    /// <summary>
    /// 测试目的：根节点（parent=null）初始化后 Level 应为 1，Path 应为 "{Id},"。
    /// </summary>
    [Fact]
    public void InitPath_WithNullParent_ShouldSetLevelOneAndSelfPath()
    {
        // Arrange
        var id = Guid.Parse("11111111-0000-0000-0000-000000000000");
        var entity = new TreeEntitySample(id);

        // Act
        entity.InitPath();   // 调用无参重载（parent=default）

        // Assert
        entity.Level.ShouldBe(1);
        entity.Path.ShouldBe($"{id},");
    }

    /// <summary>
    /// 测试目的：有父节点时，Level 应为父级 +1，Path 应在父路径后追加自身 Id。
    /// </summary>
    [Fact]
    public void InitPath_WithParent_ShouldIncrementLevelAndAppendPath()
    {
        // Arrange
        var parentId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000");
        var childId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000000");

        var parent = new TreeEntitySample(parentId);
        parent.InitPath(); // root → Level=1, Path="aaaaa...,"

        var child = new TreeEntitySample(childId);

        // Act
        child.InitPath(parent);

        // Assert
        child.Level.ShouldBe(2);
        child.Path.ShouldBe($"{parentId},{childId},");
    }

    /// <summary>
    /// 测试目的：三层嵌套时，深孙节点的 Level 应为 3，Path 应包含祖父、父、自身 Id。
    /// </summary>
    [Fact]
    public void InitPath_ThreeLevels_ShouldBuildCorrectPath()
    {
        // Arrange
        var id1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var id2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var id3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var root = new TreeEntitySample(id1);
        root.InitPath();

        var child = new TreeEntitySample(id2);
        child.InitPath(root);

        var grandchild = new TreeEntitySample(id3);
        grandchild.InitPath(child);

        // Assert
        grandchild.Level.ShouldBe(3);
        grandchild.Path.ShouldBe($"{id1},{id2},{id3},");
    }

    // ── GetParentIdsFromPath ────────────────────────────────────────

    /// <summary>
    /// 测试目的：Path 为 null 或空时 GetParentIdsFromPath 应返回空列表，不抛异常。
    /// </summary>
    [Fact]
    public void GetParentIdsFromPath_WhenPathEmpty_ShouldReturnEmptyList()
    {
        // Arrange
        var entity = new TreeEntitySample();
        // Path 默认为 string.Empty（构造中传入）

        // Act
        var result = entity.GetParentIdsFromPath();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：排除自身（默认），只应返回父级的 Id，不含自身。
    /// </summary>
    [Fact]
    public void GetParentIdsFromPath_ExcludeSelf_ShouldReturnOnlyParentIds()
    {
        // Arrange
        var parentId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000");
        var childId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000000");

        var parent = new TreeEntitySample(parentId);
        parent.InitPath();

        var child = new TreeEntitySample(childId);
        child.InitPath(parent);

        // Act
        var parentIds = child.GetParentIdsFromPath();

        // Assert
        parentIds.Count.ShouldBe(1);
        parentIds[0].ShouldBe(parentId);
    }

    /// <summary>
    /// 测试目的：包含自身（excludeSelf=false），应返回路径上所有节点 Id（含自身）。
    /// </summary>
    [Fact]
    public void GetParentIdsFromPath_IncludeSelf_ShouldReturnAllIdsInPath()
    {
        // Arrange
        var parentId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000");
        var childId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000000");

        var parent = new TreeEntitySample(parentId);
        parent.InitPath();

        var child = new TreeEntitySample(childId);
        child.InitPath(parent);

        // Act
        var allIds = child.GetParentIdsFromPath(excludeSelf: false);

        // Assert
        allIds.Count.ShouldBe(2);
        allIds.ShouldContain(parentId);
        allIds.ShouldContain(childId);
    }

    /// <summary>
    /// 测试目的：根节点调用 GetParentIdsFromPath(excludeSelf=true)，应返回空列表（无父级）。
    /// </summary>
    [Fact]
    public void GetParentIdsFromPath_RootNode_ExcludeSelf_ShouldReturnEmpty()
    {
        // Arrange
        var id = Guid.Parse("11111111-0000-0000-0000-000000000000");
        var root = new TreeEntitySample(id);
        root.InitPath();

        // Act
        var parentIds = root.GetParentIdsFromPath();

        // Assert
        parentIds.ShouldBeEmpty();
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 测试目的：验证 <see cref="TreeExtensions.SwapSort"/> 和 <see cref="TreeExtensions.GetMissingParentIds"/> 工具方法。
/// </summary>
public class TreeExtensionsTest
{
    // ── SwapSort ──────────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：SwapSort 应将两个实体的 SortId 互换。
    /// </summary>
    [Fact]
    public void SwapSort_ShouldExchangeSortIds()
    {
        // Arrange
        var a = new TreeEntitySample { SortId = 10 };
        var b = new TreeEntitySample { SortId = 20 };

        // Act
        a.SwapSort(b);

        // Assert
        a.SortId.ShouldBe(20);
        b.SortId.ShouldBe(10);
    }

    /// <summary>
    /// 测试目的：两个实体 SortId 相同时，SwapSort 不改变值（交换结果相同）。
    /// </summary>
    [Fact]
    public void SwapSort_WhenSortIdsEqual_ShouldResultInSameValues()
    {
        // Arrange
        var a = new TreeEntitySample { SortId = 5 };
        var b = new TreeEntitySample { SortId = 5 };

        // Act
        a.SwapSort(b);

        // Assert
        a.SortId.ShouldBe(5);
        b.SortId.ShouldBe(5);
    }

    /// <summary>
    /// 测试目的：SortId 为 null 时，SwapSort 应能处理，不抛异常。
    /// </summary>
    [Fact]
    public void SwapSort_WithNullSortIds_ShouldNotThrow()
    {
        // Arrange
        var a = new TreeEntitySample { SortId = null };
        var b = new TreeEntitySample { SortId = null };

        // Act & Assert
        Should.NotThrow(() => a.SwapSort(b));
        a.SortId.ShouldBeNull();
        b.SortId.ShouldBeNull();
    }

    // ── GetMissingParentIds ───────────────────────────────────────────────

    /// <summary>
    /// 测试目的：所有父级 Id 均存在于实体列表中时，GetMissingParentIds 应返回空列表。
    /// </summary>
    [Fact]
    public void GetMissingParentIds_WhenAllParentsExist_ShouldReturnEmpty()
    {
        // Arrange
        var parentId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000");
        var childId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000000");

        var parent = new TreeEntitySample(parentId);
        parent.InitPath();

        var child = new TreeEntitySample(childId);
        child.InitPath(parent);

        var entities = new List<TreeEntitySample> { parent, child };

        // Act
        var missing = entities.GetMissingParentIds<TreeEntitySample, Guid, Guid?>();

        // Assert
        missing.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：父节点不在列表中时，GetMissingParentIds 应返回缺失的父 Id。
    /// </summary>
    [Fact]
    public void GetMissingParentIds_WhenParentMissing_ShouldReturnMissingId()
    {
        // Arrange
        var parentId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000");
        var childId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000000");

        // 只构建 child，不将 parent 加入列表
        var parent = new TreeEntitySample(parentId);
        parent.InitPath();

        var child = new TreeEntitySample(childId);
        child.InitPath(parent);

        var entities = new List<TreeEntitySample> { child };

        // Act
        var missing = entities.GetMissingParentIds<TreeEntitySample, Guid, Guid?>();

        // Assert
        missing.Count.ShouldBe(1);
        missing[0].ShouldBe(parentId.ToString());
    }

    /// <summary>
    /// 测试目的：传入 null 列表时，GetMissingParentIds 应返回空列表，不抛异常。
    /// </summary>
    [Fact]
    public void GetMissingParentIds_WithNullList_ShouldReturnEmpty()
    {
        // Act
        var missing = ((IEnumerable<TreeEntitySample>)null)
            .GetMissingParentIds<TreeEntitySample, Guid, Guid?>();

        // Assert
        missing.ShouldNotBeNull();
        missing.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：根节点列表（均无父节点）时，GetMissingParentIds 应返回空列表。
    /// </summary>
    [Fact]
    public void GetMissingParentIds_WithRootNodesOnly_ShouldReturnEmpty()
    {
        // Arrange
        var r1 = new TreeEntitySample(Guid.NewGuid());
        r1.InitPath();
        var r2 = new TreeEntitySample(Guid.NewGuid());
        r2.InitPath();

        var entities = new List<TreeEntitySample> { r1, r2 };

        // Act
        var missing = entities.GetMissingParentIds<TreeEntitySample, Guid, Guid?>();

        // Assert
        missing.ShouldBeEmpty();
    }
}
