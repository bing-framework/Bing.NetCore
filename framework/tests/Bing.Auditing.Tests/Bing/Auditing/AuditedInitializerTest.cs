using Bing.Auditing;
using Shouldly;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// <see cref="CreationAuditedInitializer"/>、<see cref="ModificationAuditedInitializer"/>、
/// <see cref="DeletionAuditedInitializer"/> 单元测试。
/// 直接调用 static Init 工厂方法，不依赖 DI。
/// </summary>
public class AuditedInitializerTest
{
    // ──── 辅助实体定义 ────────────────────────────────────────────

    private class CreationTimeOnly : IHasCreationTime
    {
        public DateTime? CreationTime { get; set; }
    }

    private class FullCreationGuid : ICreationAuditedObject<Guid?>, IHasCreator
    {
        public DateTime? CreationTime { get; set; }
        public Guid? CreatorId { get; set; }
        public string Creator { get; set; }
    }

    private class FullCreationInt : ICreationAuditedObject<int>, IHasCreator
    {
        public DateTime? CreationTime { get; set; }
        public int CreatorId { get; set; }
        public string Creator { get; set; }
    }

    private class FullCreationString : ICreationAuditedObject<string>, IHasCreator
    {
        public DateTime? CreationTime { get; set; }
        public string CreatorId { get; set; }
        public string Creator { get; set; }
    }

    private class FullModificationGuid : IModificationAuditedObject<Guid?>, IHasModifier
    {
        public DateTime? LastModificationTime { get; set; }
        public Guid? LastModifierId { get; set; }
        public string LastModifier { get; set; }
    }

    private class FullModificationString : IModificationAuditedObject<string>
    {
        public DateTime? LastModificationTime { get; set; }
        public string LastModifierId { get; set; }
    }

    private class FullDeletionGuid : IDeletionAuditedObject<Guid?>, IHasDeleter
    {
        public bool IsDeleted { get; set; }
        public DateTime? DeletionTime { get; set; }
        public Guid? DeleterId { get; set; }
        public string Deleter { get; set; }
    }

    private class FullDeletionString : IDeletionAuditedObject<string>
    {
        public bool IsDeleted { get; set; }
        public DateTime? DeletionTime { get; set; }
        public string DeleterId { get; set; }
    }

    /// <summary>固定时间：用于断言 dateTime 参数覆盖</summary>
    private static readonly DateTime FixedTime = new DateTime(2025, 8, 1, 10, 0, 0, DateTimeKind.Local);

    // ════════════════════════════════════════════════════════════════
    // CreationAuditedInitializer
    // ════════════════════════════════════════════════════════════════

    // ── null 实体 ─────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：传入 null 时，Init 应静默处理，不抛异常。
    /// </summary>
    [Fact]
    public void CreationInit_WhenEntityIsNull_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => CreationAuditedInitializer.Init(null, "u1", "张三"));
    }

    // ── 创建时间 ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：传入显式 dateTime 参数时，CreationTime 应等于该指定时间。
    /// </summary>
    [Fact]
    public void CreationInit_WithExplicitDateTime_ShouldSetCreationTimeToSpecifiedValue()
    {
        // Arrange
        var entity = new CreationTimeOnly();

        // Act
        CreationAuditedInitializer.Init(entity, "u1", "张三", FixedTime);

        // Assert
        entity.CreationTime.ShouldBe(FixedTime);
    }

    /// <summary>
    /// 测试目的：不传 dateTime 时，CreationTime 应被设置为非 null（DateTime.Now 附近）。
    /// </summary>
    [Fact]
    public void CreationInit_WithoutDateTime_ShouldSetCreationTimeToNonNull()
    {
        // Arrange
        var before = DateTime.Now.AddSeconds(-1);
        var entity = new CreationTimeOnly();

        // Act
        CreationAuditedInitializer.Init(entity, "u1", "张三");

        // Assert
        entity.CreationTime.ShouldNotBeNull();
        entity.CreationTime.Value.ShouldBeGreaterThan(before);
    }

    // ── CreatorId 多类型 ──────────────────────────────────────────

    /// <summary>
    /// 测试目的：userId 为有效 Guid 字符串时，应正确填充 Guid? 类型的 CreatorId。
    /// </summary>
    [Fact]
    public void CreationInit_WithGuidUserId_ShouldSetNullableGuidCreatorId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entity = new FullCreationGuid();

        // Act
        CreationAuditedInitializer.Init(entity, userId.ToString(), "张三", FixedTime);

        // Assert
        entity.CreatorId.ShouldBe(userId);
    }

    /// <summary>
    /// 测试目的：userId 为整数字符串时，应正确填充 int 类型的 CreatorId。
    /// </summary>
    [Fact]
    public void CreationInit_WithIntUserId_ShouldSetIntCreatorId()
    {
        // Arrange
        var entity = new FullCreationInt();

        // Act
        CreationAuditedInitializer.Init(entity, "100", "张三", FixedTime);

        // Assert
        entity.CreatorId.ShouldBe(100);
    }

    /// <summary>
    /// 测试目的：userId 为任意字符串时，应正确填充 string 类型的 CreatorId。
    /// </summary>
    [Fact]
    public void CreationInit_WithStringUserId_ShouldSetStringCreatorId()
    {
        // Arrange
        var entity = new FullCreationString();

        // Act
        CreationAuditedInitializer.Init(entity, "user-abc", "张三", FixedTime);

        // Assert
        entity.CreatorId.ShouldBe("user-abc");
    }

    // ── Creator 用户名 ────────────────────────────────────────────

    /// <summary>
    /// 测试目的：userName 不为空时，Creator 字段应被正确填充。
    /// </summary>
    [Fact]
    public void CreationInit_WithUserName_ShouldSetCreatorName()
    {
        // Arrange
        var entity = new FullCreationGuid();

        // Act
        CreationAuditedInitializer.Init(entity, Guid.NewGuid().ToString(), "李四", FixedTime);

        // Assert
        entity.Creator.ShouldBe("李四");
    }

    /// <summary>
    /// 测试目的：userName 为空时，Creator 不应被修改（保持 null/默认）。
    /// </summary>
    [Fact]
    public void CreationInit_WhenUserNameEmpty_ShouldNotSetCreatorName()
    {
        // Arrange
        var entity = new FullCreationGuid();

        // Act
        CreationAuditedInitializer.Init(entity, Guid.NewGuid().ToString(), "", FixedTime);

        // Assert
        entity.Creator.ShouldBeNullOrWhiteSpace();
    }

    /// <summary>
    /// 测试目的：userId 为空/null 时，CreatorId 不应被填充（保持默认值）。
    /// </summary>
    [Fact]
    public void CreationInit_WhenUserIdEmpty_ShouldNotSetCreatorId()
    {
        // Arrange
        var entity = new FullCreationGuid();

        // Act
        CreationAuditedInitializer.Init(entity, "", "张三", FixedTime);

        // Assert
        entity.CreatorId.ShouldBeNull();
    }

    // ════════════════════════════════════════════════════════════════
    // ModificationAuditedInitializer
    // ════════════════════════════════════════════════════════════════

    // ── null 实体 ─────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：传入 null 时，Init 应静默处理，不抛异常。
    /// </summary>
    [Fact]
    public void ModificationInit_WhenEntityIsNull_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => ModificationAuditedInitializer.Init(null, "u1", "张三"));
    }

    // ── 修改时间 ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：传入显式 dateTime 参数时，LastModificationTime 应等于该值。
    /// </summary>
    [Fact]
    public void ModificationInit_WithExplicitDateTime_ShouldSetLastModificationTimeToSpecifiedValue()
    {
        // Arrange
        var entity = new FullModificationGuid();

        // Act
        ModificationAuditedInitializer.Init(entity, Guid.NewGuid().ToString(), "张三", FixedTime);

        // Assert
        entity.LastModificationTime.ShouldBe(FixedTime);
    }

    /// <summary>
    /// 测试目的：不传 dateTime 时，LastModificationTime 应被设置为非 null（DateTime.Now 附近）。
    /// </summary>
    [Fact]
    public void ModificationInit_WithoutDateTime_ShouldSetLastModificationTimeToNonNull()
    {
        // Arrange
        var before = DateTime.Now.AddSeconds(-1);
        var entity = new FullModificationGuid();

        // Act
        ModificationAuditedInitializer.Init(entity, Guid.NewGuid().ToString(), "张三");

        // Assert
        entity.LastModificationTime.ShouldNotBeNull();
        entity.LastModificationTime.Value.ShouldBeGreaterThan(before);
    }

    // ── LastModifierId 多类型 ─────────────────────────────────────

    /// <summary>
    /// 测试目的：userId 为 Guid 字符串时，应正确填充 Guid? 类型的 LastModifierId。
    /// </summary>
    [Fact]
    public void ModificationInit_WithGuidUserId_ShouldSetNullableGuidLastModifierId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entity = new FullModificationGuid();

        // Act
        ModificationAuditedInitializer.Init(entity, userId.ToString(), "张三", FixedTime);

        // Assert
        entity.LastModifierId.ShouldBe(userId);
    }

    /// <summary>
    /// 测试目的：userId 为任意字符串时，应正确填充 string 类型的 LastModifierId。
    /// </summary>
    [Fact]
    public void ModificationInit_WithStringUserId_ShouldSetStringLastModifierId()
    {
        // Arrange
        var entity = new FullModificationString();

        // Act
        ModificationAuditedInitializer.Init(entity, "mod-user", "张三", FixedTime);

        // Assert
        entity.LastModifierId.ShouldBe("mod-user");
    }

    // ── LastModifier 用户名 ───────────────────────────────────────

    /// <summary>
    /// 测试目的：userName 不为空时，LastModifier 字段应被正确填充。
    /// </summary>
    [Fact]
    public void ModificationInit_WithUserName_ShouldSetLastModifierName()
    {
        // Arrange
        var entity = new FullModificationGuid();

        // Act
        ModificationAuditedInitializer.Init(entity, Guid.NewGuid().ToString(), "王五", FixedTime);

        // Assert
        entity.LastModifier.ShouldBe("王五");
    }

    /// <summary>
    /// 测试目的：userId 为空时，LastModifierId 不应被填充（保持默认值）。
    /// </summary>
    [Fact]
    public void ModificationInit_WhenUserIdEmpty_ShouldNotSetLastModifierId()
    {
        // Arrange
        var entity = new FullModificationGuid();

        // Act
        ModificationAuditedInitializer.Init(entity, "", "张三", FixedTime);

        // Assert
        entity.LastModifierId.ShouldBeNull();
    }

    // ════════════════════════════════════════════════════════════════
    // DeletionAuditedInitializer
    // ════════════════════════════════════════════════════════════════

    // ── null 实体 ─────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：传入 null 时，Init 应静默处理，不抛异常。
    /// </summary>
    [Fact]
    public void DeletionInit_WhenEntityIsNull_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => DeletionAuditedInitializer.Init(null, "u1", "张三"));
    }

    // ── 删除时间 ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：Init 后，DeletionTime 应被设置为非 null（内部使用 DateTime.Now）。
    /// </summary>
    [Fact]
    public void DeletionInit_ShouldSetDeletionTimeToNonNull()
    {
        // Arrange
        var before = DateTime.Now.AddSeconds(-1);
        var entity = new FullDeletionGuid();

        // Act
        DeletionAuditedInitializer.Init(entity, Guid.NewGuid().ToString(), "张三");

        // Assert
        entity.DeletionTime.ShouldNotBeNull();
        entity.DeletionTime.Value.ShouldBeGreaterThan(before);
    }

    // ── DeleterId 多类型 ──────────────────────────────────────────

    /// <summary>
    /// 测试目的：userId 为 Guid 字符串时，应正确填充 Guid? 类型的 DeleterId。
    /// </summary>
    [Fact]
    public void DeletionInit_WithGuidUserId_ShouldSetNullableGuidDeleterId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entity = new FullDeletionGuid();

        // Act
        DeletionAuditedInitializer.Init(entity, userId.ToString(), "张三");

        // Assert
        entity.DeleterId.ShouldBe(userId);
    }

    /// <summary>
    /// 测试目的：userId 为任意字符串时，应正确填充 string 类型的 DeleterId。
    /// </summary>
    [Fact]
    public void DeletionInit_WithStringUserId_ShouldSetStringDeleterId()
    {
        // Arrange
        var entity = new FullDeletionString();

        // Act
        DeletionAuditedInitializer.Init(entity, "del-user", "张三");

        // Assert
        entity.DeleterId.ShouldBe("del-user");
    }

    // ── Deleter 用户名 ────────────────────────────────────────────

    /// <summary>
    /// 测试目的：userName 不为空时，Deleter 字段应被正确填充。
    /// </summary>
    [Fact]
    public void DeletionInit_WithUserName_ShouldSetDeleterName()
    {
        // Arrange
        var entity = new FullDeletionGuid();

        // Act
        DeletionAuditedInitializer.Init(entity, Guid.NewGuid().ToString(), "赵六");

        // Assert
        entity.Deleter.ShouldBe("赵六");
    }

    /// <summary>
    /// 测试目的：userId 为空时，DeleterId 不应被填充（保持默认值）。
    /// </summary>
    [Fact]
    public void DeletionInit_WhenUserIdEmpty_ShouldNotSetDeleterId()
    {
        // Arrange
        var entity = new FullDeletionGuid();

        // Act
        DeletionAuditedInitializer.Init(entity, "", "张三");

        // Assert
        entity.DeleterId.ShouldBeNull();
    }
}
