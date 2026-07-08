using Bing.Auditing;
using Bing.Data;
using Bing.Tests.Samples;

namespace Bing.Data;

// ─────────────────────────────────────────────────────────────────────────────
// ISoftDelete 测试辅助类型
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>简单的逻辑删除实体（仅实现 ISoftDelete）</summary>
internal class SoftDeleteEntity : ISoftDelete
{
    public bool IsDeleted { get; set; }
}

/// <summary>带删除审计的逻辑删除实体（同时实现 ISoftDelete + IDeletionAuditedObject）</summary>
internal class AuditedSoftDeleteEntity : ISoftDelete, IDeletionAuditedObject
{
    public bool IsDeleted { get; set; }
    public Guid? DeleterId { get; set; }
    public DateTime? DeletionTime { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 测试目的：验证 <see cref="SoftDeleteExtensions"/> 的 IsNullOrDeleted 和 UnDelete 行为。
/// </summary>
public class SoftDeleteExtensionsTest
{
    // ── IsNullOrDeleted ───────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：entity 为 null 时，IsNullOrDeleted 应返回 true。
    /// </summary>
    [Fact]
    public void IsNullOrDeleted_WhenNull_ShouldReturnTrue()
    {
        // Act
        var result = ((ISoftDelete)null).IsNullOrDeleted();

        // Assert
        result.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：entity.IsDeleted=true 时，IsNullOrDeleted 应返回 true。
    /// </summary>
    [Fact]
    public void IsNullOrDeleted_WhenDeleted_ShouldReturnTrue()
    {
        // Arrange
        var entity = new SoftDeleteEntity { IsDeleted = true };

        // Act
        var result = entity.IsNullOrDeleted();

        // Assert
        result.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：entity.IsDeleted=false 时，IsNullOrDeleted 应返回 false。
    /// </summary>
    [Fact]
    public void IsNullOrDeleted_WhenNotDeleted_ShouldReturnFalse()
    {
        // Arrange
        var entity = new SoftDeleteEntity { IsDeleted = false };

        // Act
        var result = entity.IsNullOrDeleted();

        // Assert
        result.ShouldBeFalse();
    }

    // ── UnDelete（仅 ISoftDelete） ─────────────────────────────────────────

    /// <summary>
    /// 测试目的：对已删除实体调用 UnDelete，IsDeleted 应变为 false。
    /// </summary>
    [Fact]
    public void UnDelete_WhenDeleted_ShouldSetIsDeletedFalse()
    {
        // Arrange
        var entity = new SoftDeleteEntity { IsDeleted = true };

        // Act
        entity.UnDelete();

        // Assert
        entity.IsDeleted.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：对未删除实体调用 UnDelete，IsDeleted 应仍为 false（幂等）。
    /// </summary>
    [Fact]
    public void UnDelete_WhenAlreadyNotDeleted_ShouldRemainFalse()
    {
        // Arrange
        var entity = new SoftDeleteEntity { IsDeleted = false };

        // Act
        entity.UnDelete();

        // Assert
        entity.IsDeleted.ShouldBeFalse();
    }

    // ── UnDelete（ISoftDelete + IDeletionAuditedObject） ──────────────────

    /// <summary>
    /// 测试目的：实体同时实现 IDeletionAuditedObject 时，UnDelete 应将 DeletionTime 和 DeleterId 都置 null。
    /// </summary>
    [Fact]
    public void UnDelete_WithAuditedEntity_ShouldClearAuditFields()
    {
        // Arrange
        var entity = new AuditedSoftDeleteEntity
        {
            IsDeleted = true,
            DeleterId = Guid.NewGuid(),
            DeletionTime = DateTime.UtcNow
        };

        // Act
        entity.UnDelete();

        // Assert
        entity.IsDeleted.ShouldBeFalse();
        entity.DeleterId.ShouldBeNull();
        entity.DeletionTime.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：IDeletionAuditedObject 字段本身已为 null 时，UnDelete 不抛异常（边界场景）。
    /// </summary>
    [Fact]
    public void UnDelete_WithAuditedEntity_WhenFieldsAlreadyNull_ShouldNotThrow()
    {
        // Arrange
        var entity = new AuditedSoftDeleteEntity { IsDeleted = true, DeleterId = null, DeletionTime = null };

        // Act & Assert
        Should.NotThrow(() => entity.UnDelete());
        entity.IsDeleted.ShouldBeFalse();
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 测试目的：验证 <see cref="DomainExtensions.Compare"/> 扩展方法对各类型 Key 列表的委托行为。
/// </summary>
public class DomainExtensionsCompareTest
{
    // ── Compare IEnumerable<Guid> ────────────────────────────────────────

    /// <summary>
    /// 测试目的：新集合新增一个 Guid 时，CreateList 应包含该 Guid，其余为空。
    /// </summary>
    [Fact]
    public void Compare_Guids_NewItem_ShouldBeInCreateList()
    {
        // Arrange
        var id = Guid.NewGuid();
        var newList = new List<Guid> { id };
        var oldList = new List<Guid>();

        // Act
        var result = newList.Compare(oldList);

        // Assert
        result.CreateList.ShouldContain(id);
        result.UpdateList.ShouldBeEmpty();
        result.DeleteList.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：旧集合有而新集合无的 Guid，应在 DeleteList 中。
    /// </summary>
    [Fact]
    public void Compare_Guids_RemovedItem_ShouldBeInDeleteList()
    {
        // Arrange
        var id = Guid.NewGuid();
        var newList = new List<Guid>();
        var oldList = new List<Guid> { id };

        // Act
        var result = newList.Compare(oldList);

        // Assert
        result.DeleteList.ShouldContain(id);
        result.CreateList.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：新旧集合均包含的 Guid，应在 UpdateList 中。
    /// </summary>
    [Fact]
    public void Compare_Guids_CommonItem_ShouldBeInUpdateList()
    {
        // Arrange
        var id = Guid.NewGuid();
        var newList = new List<Guid> { id };
        var oldList = new List<Guid> { id };

        // Act
        var result = newList.Compare(oldList);

        // Assert
        result.UpdateList.ShouldContain(id);
    }

    // ── Compare IEnumerable<string> ───────────────────────────────────────

    /// <summary>
    /// 测试目的：字符串集合 Compare：新增项应进 CreateList。
    /// </summary>
    [Fact]
    public void Compare_Strings_NewItem_ShouldBeInCreateList()
    {
        // Arrange
        var newList = new List<string> { "a", "b" };
        var oldList = new List<string> { "a" };

        // Act
        var result = newList.Compare(oldList);

        // Assert
        result.CreateList.ShouldContain("b");
        result.UpdateList.ShouldContain("a");
        result.DeleteList.ShouldBeEmpty();
    }

    // ── Compare IEnumerable<int> ──────────────────────────────────────────

    /// <summary>
    /// 测试目的：int 集合 Compare：删除项应进 DeleteList。
    /// </summary>
    [Fact]
    public void Compare_Ints_RemovedItem_ShouldBeInDeleteList()
    {
        // Arrange
        var newList = new List<int> { 1 };
        var oldList = new List<int> { 1, 2, 3 };

        // Act
        var result = newList.Compare(oldList);

        // Assert
        result.DeleteList.ShouldContain(2);
        result.DeleteList.ShouldContain(3);
    }

    // ── Compare IEnumerable<long> ─────────────────────────────────────────

    /// <summary>
    /// 测试目的：long 集合 Compare：新旧均为空时三个列表均为空。
    /// </summary>
    [Fact]
    public void Compare_Longs_BothEmpty_ShouldReturnAllEmpty()
    {
        // Arrange
        var newList = new List<long>();
        var oldList = new List<long>();

        // Act
        var result = newList.Compare(oldList);

        // Assert
        result.CreateList.ShouldBeEmpty();
        result.UpdateList.ShouldBeEmpty();
        result.DeleteList.ShouldBeEmpty();
    }

    // ── Compare IEnumerable<TEntity> (IKey<Guid>) ─────────────────────────

    /// <summary>
    /// 测试目的：实体集合 Compare：新增实体应进 CreateList（通过默认 Guid Key 泛型重载）。
    /// </summary>
    [Fact]
    public void Compare_Entities_NewEntity_ShouldBeInCreateList()
    {
        // Arrange
        var id = Guid.NewGuid();
        var newEntity = new AggregateRootSample(id) { Name = "new" };
        var newList = new List<AggregateRootSample> { newEntity };
        var oldList = new List<AggregateRootSample>();

        // Act
        var result = newList.Compare(oldList);

        // Assert
        result.CreateList.ShouldContain(newEntity);
        result.UpdateList.ShouldBeEmpty();
        result.DeleteList.ShouldBeEmpty();
    }
}
