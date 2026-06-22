using Bing.Auditing;
using Bing.Test.Shared.Identity;
using Bing.Test.Shared.Timing;
using Shouldly;
using Xunit;

namespace Bing.Auditing.Tests;

// ─── 测试用实体模型 ───────────────────────────────────────────────

/// <summary>简单实体：仅含创建时间</summary>
internal class CreationOnlyEntity : IHasCreationTime
{
    public DateTime? CreationTime { get; set; }
}

/// <summary>实体：Guid 创建人 + 创建时间</summary>
internal class GuidCreationEntity : ICreationAuditedObject<Guid>
{
    public DateTime? CreationTime { get; set; }
    public Guid CreatorId { get; set; }
}

/// <summary>实体：可空 Guid 创建人</summary>
internal class NullableGuidCreationEntity : ICreationAuditedObject<Guid?>
{
    public DateTime? CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
}

/// <summary>实体：int 创建人</summary>
internal class IntCreationEntity : ICreationAuditedObject<int>
{
    public DateTime? CreationTime { get; set; }
    public int CreatorId { get; set; }
}

/// <summary>实体：string 创建人</summary>
internal class StringCreationEntity : ICreationAuditedObject<string>
{
    public DateTime? CreationTime { get; set; }
    public string CreatorId { get; set; }
}

/// <summary>实体：long 创建人</summary>
internal class LongCreationEntity : ICreationAuditedObject<long>
{
    public DateTime? CreationTime { get; set; }
    public long CreatorId { get; set; }
}

/// <summary>实体：含创建人名</summary>
internal class NamedCreationEntity : ICreationAuditedObject<Guid?>, IHasCreator
{
    public DateTime? CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public string Creator { get; set; }
}

/// <summary>实体：含修改时间</summary>
internal class ModificationEntity : IModificationAuditedObject<Guid?>
{
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }
}

/// <summary>实体：含修改人名</summary>
internal class NamedModificationEntity : IModificationAuditedObject<Guid?>, IHasModifier
{
    public DateTime? LastModificationTime { get; set; }
    public Guid? LastModifierId { get; set; }
    public string LastModifier { get; set; }
}

/// <summary>实体：软删除</summary>
internal class DeletionEntity : IDeletionAuditedObject<Guid?>
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletionTime { get; set; }
    public Guid? DeleterId { get; set; }
}

/// <summary>实体：含删除人名</summary>
internal class NamedDeletionEntity : IDeletionAuditedObject<Guid?>, IHasDeleter
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletionTime { get; set; }
    public Guid? DeleterId { get; set; }
    public string Deleter { get; set; }
}

// ─── 测试类 ──────────────────────────────────────────────────────

/// <summary>
/// <see cref="AuditPropertySetter"/> 单元测试
/// </summary>
public class AuditPropertySetterTest
{
    /// <summary>固定时间：2025-06-01 12:00:00</summary>
    private static readonly DateTime FixedTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Local);

    private static AuditPropertySetter CreateSetter(
        string userId = "user-001",
        string userName = "张三",
        bool isAuthenticated = true)
    {
        var clock = new FakeClock(FixedTime);
        var user = isAuthenticated
            ? FakeCurrentUser.AsAuthenticated(userId, userName)
            : FakeCurrentUser.AsAnonymous();
        return new AuditPropertySetter(user, clock);
    }

    // ── SetCreationProperties ─────────────────────────────────────

    /// <summary>
    /// 测试目的：SetCreationProperties 应将 CreationTime 设置为时钟的当前时间。
    /// </summary>
    [Fact]
    public void SetCreationProperties_WhenCreationTimeIsNull_ShouldSetToClockNow()
    {
        // Arrange
        var setter = CreateSetter();
        var entity = new CreationOnlyEntity();

        // Act
        setter.SetCreationProperties(entity);

        // Assert
        entity.CreationTime.ShouldBe(FixedTime);
    }

    /// <summary>
    /// 测试目的：CreationTime 已存在时，SetCreationProperties 不应覆盖原有值（幂等性）。
    /// </summary>
    [Fact]
    public void SetCreationProperties_WhenCreationTimeAlreadySet_ShouldNotOverwrite()
    {
        // Arrange
        var existingTime = new DateTime(2020, 1, 1);
        var setter = CreateSetter();
        var entity = new CreationOnlyEntity { CreationTime = existingTime };

        // Act
        setter.SetCreationProperties(entity);

        // Assert
        entity.CreationTime.ShouldBe(existingTime);
    }

    /// <summary>
    /// 测试目的：SetCreationProperties 应填充 Guid 类型的 CreatorId。
    /// </summary>
    [Fact]
    public void SetCreationProperties_WithAuthenticatedUser_ShouldSetGuidCreatorId()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var setter = CreateSetter(userId: userId);
        var entity = new GuidCreationEntity();

        // Act
        setter.SetCreationProperties(entity);

        // Assert
        entity.CreatorId.ShouldBe(Guid.Parse(userId));
    }

    /// <summary>
    /// 测试目的：CreatorId（Guid）已有值时不应被覆盖。
    /// </summary>
    [Fact]
    public void SetCreationProperties_WhenGuidCreatorIdAlreadySet_ShouldNotOverwrite()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var setter = CreateSetter(userId: Guid.NewGuid().ToString());
        var entity = new GuidCreationEntity { CreatorId = existingId };

        // Act
        setter.SetCreationProperties(entity);

        // Assert
        entity.CreatorId.ShouldBe(existingId);
    }

    /// <summary>
    /// 测试目的：SetCreationProperties 应填充 int 类型的 CreatorId。
    /// </summary>
    [Fact]
    public void SetCreationProperties_WithAuthenticatedUser_ShouldSetIntCreatorId()
    {
        // Arrange
        var setter = CreateSetter(userId: "42");
        var entity = new IntCreationEntity();

        // Act
        setter.SetCreationProperties(entity);

        // Assert
        entity.CreatorId.ShouldBe(42);
    }

    /// <summary>
    /// 测试目的：SetCreationProperties 应填充 string 类型的 CreatorId。
    /// </summary>
    [Fact]
    public void SetCreationProperties_WithAuthenticatedUser_ShouldSetStringCreatorId()
    {
        // Arrange
        var setter = CreateSetter(userId: "user-abc");
        var entity = new StringCreationEntity();

        // Act
        setter.SetCreationProperties(entity);

        // Assert
        entity.CreatorId.ShouldBe("user-abc");
    }

    /// <summary>
    /// 测试目的：SetCreationProperties 应填充 long 类型的 CreatorId。
    /// </summary>
    [Fact]
    public void SetCreationProperties_WithAuthenticatedUser_ShouldSetLongCreatorId()
    {
        // Arrange
        var setter = CreateSetter(userId: "9876543210");
        var entity = new LongCreationEntity();

        // Act
        setter.SetCreationProperties(entity);

        // Assert
        entity.CreatorId.ShouldBe(9876543210L);
    }

    /// <summary>
    /// 测试目的：未认证用户时，CreatorId 不应被填充（保持默认值）。
    /// </summary>
    [Fact]
    public void SetCreationProperties_WhenUserNotAuthenticated_ShouldNotSetCreatorId()
    {
        // Arrange
        var setter = CreateSetter(isAuthenticated: false);
        var entity = new GuidCreationEntity();

        // Act
        setter.SetCreationProperties(entity);

        // Assert
        entity.CreatorId.ShouldBe(Guid.Empty);
    }

    /// <summary>
    /// 测试目的：未认证用户时，CreationTime 仍应被填充（创建时间不依赖用户认证）。
    /// </summary>
    [Fact]
    public void SetCreationProperties_WhenUserNotAuthenticated_ShouldStillSetCreationTime()
    {
        // Arrange
        var setter = CreateSetter(isAuthenticated: false);
        var entity = new CreationOnlyEntity();

        // Act
        setter.SetCreationProperties(entity);

        // Assert
        entity.CreationTime.ShouldBe(FixedTime);
    }

    /// <summary>
    /// 测试目的：SetCreationProperties 应填充 Creator（用户名）字段。
    /// </summary>
    [Fact]
    public void SetCreationProperties_WithAuthenticatedUser_ShouldSetCreatorName()
    {
        // Arrange
        var setter = CreateSetter(userId: "user-001", userName: "李四");
        var entity = new NamedCreationEntity();

        // Act
        setter.SetCreationProperties(entity);

        // Assert
        entity.Creator.ShouldBe("李四");
    }

    /// <summary>
    /// 测试目的：传入 null 时，SetCreationProperties 应不抛异常，静默处理。
    /// </summary>
    [Fact]
    public void SetCreationProperties_WhenTargetIsNull_ShouldNotThrow()
    {
        // Arrange
        var setter = CreateSetter();

        // Act & Assert
        Should.NotThrow(() => setter.SetCreationProperties(null));
    }

    // ── SetModificationProperties ─────────────────────────────────

    /// <summary>
    /// 测试目的：SetModificationProperties 应始终更新 LastModificationTime（包括有旧值的情况）。
    /// </summary>
    [Fact]
    public void SetModificationProperties_ShouldAlwaysOverwriteLastModificationTime()
    {
        // Arrange
        var setter = CreateSetter();
        var entity = new ModificationEntity { LastModificationTime = new DateTime(2010, 1, 1) };

        // Act
        setter.SetModificationProperties(entity);

        // Assert
        entity.LastModificationTime.ShouldBe(FixedTime);
    }

    /// <summary>
    /// 测试目的：SetModificationProperties 应填充 LastModifierId（Guid?）。
    /// </summary>
    [Fact]
    public void SetModificationProperties_WithAuthenticatedUser_ShouldSetLastModifierId()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var setter = CreateSetter(userId: userId);
        var entity = new ModificationEntity();

        // Act
        setter.SetModificationProperties(entity);

        // Assert
        entity.LastModifierId.ShouldBe(Guid.Parse(userId));
    }

    /// <summary>
    /// 测试目的：SetModificationProperties 应始终覆盖 LastModifierId（不做幂等保护）。
    /// </summary>
    [Fact]
    public void SetModificationProperties_ShouldOverwriteExistingLastModifierId()
    {
        // Arrange
        var newUserId = Guid.NewGuid().ToString();
        var setter = CreateSetter(userId: newUserId);
        var entity = new ModificationEntity { LastModifierId = Guid.NewGuid() };

        // Act
        setter.SetModificationProperties(entity);

        // Assert
        entity.LastModifierId.ShouldBe(Guid.Parse(newUserId));
    }

    /// <summary>
    /// 测试目的：SetModificationProperties 应填充 LastModifier（用户名）字段。
    /// </summary>
    [Fact]
    public void SetModificationProperties_WithAuthenticatedUser_ShouldSetLastModifierName()
    {
        // Arrange
        var setter = CreateSetter(userId: "user-001", userName: "王五");
        var entity = new NamedModificationEntity();

        // Act
        setter.SetModificationProperties(entity);

        // Assert
        entity.LastModifier.ShouldBe("王五");
    }

    /// <summary>
    /// 测试目的：传入 null 时，SetModificationProperties 应不抛异常。
    /// </summary>
    [Fact]
    public void SetModificationProperties_WhenTargetIsNull_ShouldNotThrow()
    {
        // Arrange
        var setter = CreateSetter();

        // Act & Assert
        Should.NotThrow(() => setter.SetModificationProperties(null));
    }

    // ── SetDeletionProperties ─────────────────────────────────────

    /// <summary>
    /// 测试目的：SetDeletionProperties 应将 DeletionTime 设置为时钟当前时间。
    /// </summary>
    [Fact]
    public void SetDeletionProperties_ShouldSetDeletionTimeToClockNow()
    {
        // Arrange
        var setter = CreateSetter();
        var entity = new DeletionEntity();

        // Act
        setter.SetDeletionProperties(entity);

        // Assert
        entity.DeletionTime.ShouldBe(FixedTime);
    }

    /// <summary>
    /// 测试目的：SetDeletionProperties 应填充 DeleterId（Guid?）。
    /// </summary>
    [Fact]
    public void SetDeletionProperties_WithAuthenticatedUser_ShouldSetDeleterId()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var setter = CreateSetter(userId: userId);
        var entity = new DeletionEntity();

        // Act
        setter.SetDeletionProperties(entity);

        // Assert
        entity.DeleterId.ShouldBe(Guid.Parse(userId));
    }

    /// <summary>
    /// 测试目的：DeleterId 已有值时，SetDeletionProperties 不应覆盖（幂等性）。
    /// </summary>
    [Fact]
    public void SetDeletionProperties_WhenDeleterIdAlreadySet_ShouldNotOverwrite()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var setter = CreateSetter(userId: Guid.NewGuid().ToString());
        var entity = new DeletionEntity { DeleterId = existingId };

        // Act
        setter.SetDeletionProperties(entity);

        // Assert
        entity.DeleterId.ShouldBe(existingId);
    }

    /// <summary>
    /// 测试目的：未认证用户删除时，DeleterId 不应被填充。
    /// </summary>
    [Fact]
    public void SetDeletionProperties_WhenUserNotAuthenticated_ShouldNotSetDeleterId()
    {
        // Arrange
        var setter = CreateSetter(isAuthenticated: false);
        var entity = new DeletionEntity();

        // Act
        setter.SetDeletionProperties(entity);

        // Assert
        entity.DeleterId.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：SetDeletionProperties 应填充 Deleter（用户名）字段。
    /// </summary>
    [Fact]
    public void SetDeletionProperties_WithAuthenticatedUser_ShouldSetDeleterName()
    {
        // Arrange
        var setter = CreateSetter(userId: "user-001", userName: "赵六");
        var entity = new NamedDeletionEntity();

        // Act
        setter.SetDeletionProperties(entity);

        // Assert
        entity.Deleter.ShouldBe("赵六");
    }

    /// <summary>
    /// 测试目的：传入 null 时，SetDeletionProperties 应不抛异常。
    /// </summary>
    [Fact]
    public void SetDeletionProperties_WhenTargetIsNull_ShouldNotThrow()
    {
        // Arrange
        var setter = CreateSetter();

        // Act & Assert
        Should.NotThrow(() => setter.SetDeletionProperties(null));
    }

    // ── FakeClock 时间推进场景 ────────────────────────────────────

    /// <summary>
    /// 测试目的：使用 FakeClock.Advance 推进时间后，后续调用应获得新的时间戳。
    /// 验证 FakeClock 可以模拟时序相关的审计场景（如创建后修改）。
    /// </summary>
    [Fact]
    public void SetModificationProperties_AfterClockAdvance_ShouldUseNewTime()
    {
        // Arrange
        var clock = new FakeClock(FixedTime);
        var user = FakeCurrentUser.AsAuthenticated("user-001", "张三");
        var setter = new AuditPropertySetter(user, clock);
        var entity = new NamedModificationEntity();

        // 先用初始时间设置一次创建属性（模拟创建行为）
        clock.Advance(TimeSpan.FromHours(2));
        var expectedModTime = FixedTime.AddHours(2);

        // Act
        setter.SetModificationProperties(entity);

        // Assert
        entity.LastModificationTime.ShouldBe(expectedModTime);
    }
}
