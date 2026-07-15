using Shouldly;
using Xunit;

namespace Bing.Auditing.Tests;

// =========================================================================
//  CreationAuditedInitializer Tests
// =========================================================================
// 注意：辅助实体类（GuidCreationEntity / NullableGuidCreationEntity 等）
// 已在 AuditPropertySetterTest.cs 中声明（同项目/同命名空间），此处直接复用。
// =========================================================================

/// <summary>
/// 测试目的：验证 CreationAuditedInitializer.Init 的各属性初始化行为。
/// </summary>
public class CreationAuditedInitializerTest
{
    private static readonly string UserId = Guid.NewGuid().ToString();
    private static readonly string UserName = "Alice";
    private static readonly DateTime FixedTime = new(2025, 6, 1, 12, 0, 0);

    // -----------------------------------------------------------------
    //  null 实体不抛异常
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：实体为 null 时，Init 应直接返回，不抛异常。
    /// </summary>
    [Fact]
    public void Init_NullEntity_ShouldNotThrow()
    {
        Should.NotThrow(() => CreationAuditedInitializer.Init(null, UserId, UserName));
    }

    // -----------------------------------------------------------------
    //  IHasCreationTime
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：实体实现 IHasCreationTime 时，Init 应将 CreationTime 设置为 DateTime.Now 附近。
    /// </summary>
    [Fact]
    public void Init_EntityWithCreationTime_ShouldSetCreationTimeToNow()
    {
        var entity = new CreationOnlyEntity();
        var before = DateTime.Now;
        CreationAuditedInitializer.Init(entity, UserId, UserName);
        var after = DateTime.Now;

        entity.CreationTime.ShouldNotBeNull();
        entity.CreationTime.Value.ShouldBeInRange(before, after);
    }

    /// <summary>
    /// 测试目的：传入 dateTime 参数时，Init 应使用指定时间，而非 DateTime.Now。
    /// </summary>
    [Fact]
    public void Init_WithDateTime_ShouldUseProvidedTime()
    {
        var entity = new CreationOnlyEntity();
        CreationAuditedInitializer.Init(entity, UserId, UserName, FixedTime);

        entity.CreationTime.ShouldBe(FixedTime);
    }

    // -----------------------------------------------------------------
    //  IHasCreator
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：实体实现 IHasCreator 时，Init 应设置 Creator 为传入的 userName。
    /// </summary>
    [Fact]
    public void Init_EntityWithCreator_ShouldSetCreatorName()
    {
        var entity = new NamedCreationEntity();
        CreationAuditedInitializer.Init(entity, UserId, "Bob");

        entity.Creator.ShouldBe("Bob");
    }

    /// <summary>
    /// 测试目的：userName 为 null/空时，Creator 不应被赋值（保持 null）。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Init_EmptyUserName_ShouldNotSetCreator(string userName)
    {
        var entity = new NamedCreationEntity();
        CreationAuditedInitializer.Init(entity, UserId, userName);

        entity.Creator.ShouldBeNull();
    }

    // -----------------------------------------------------------------
    //  ICreationAuditedObject<Guid>
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：实体实现 ICreationAuditedObject&lt;Guid&gt; 时，Init 应将 CreatorId 解析为 Guid。
    /// </summary>
    [Fact]
    public void Init_GuidCreationEntity_ShouldSetCreatorId()
    {
        var guidUserId = Guid.NewGuid();
        var entity = new GuidCreationEntity();
        CreationAuditedInitializer.Init(entity, guidUserId.ToString(), UserName);

        entity.CreatorId.ShouldBe(guidUserId);
    }

    // -----------------------------------------------------------------
    //  ICreationAuditedObject<Guid?>
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：实体实现 ICreationAuditedObject&lt;Guid?&gt; 时，可解析 Guid? CreatorId。
    /// </summary>
    [Fact]
    public void Init_NullableGuidCreationEntity_ShouldSetCreatorId()
    {
        var guidUserId = Guid.NewGuid();
        var entity = new NullableGuidCreationEntity();
        CreationAuditedInitializer.Init(entity, guidUserId.ToString(), UserName);

        entity.CreatorId.ShouldNotBeNull();
        entity.CreatorId.Value.ShouldBe(guidUserId);
    }

    /// <summary>
    /// 测试目的：userId 为 null 时，NullableGuid CreatorId 不应被赋值。
    /// </summary>
    [Fact]
    public void Init_NullUserId_ShouldNotSetCreatorId()
    {
        var entity = new NullableGuidCreationEntity();
        CreationAuditedInitializer.Init(entity, null, UserName);

        entity.CreatorId.ShouldBeNull();
    }

    // -----------------------------------------------------------------
    //  ICreationAuditedObject<string>
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：实体实现 ICreationAuditedObject&lt;string&gt; 时，CreatorId 应为传入字符串。
    /// </summary>
    [Fact]
    public void Init_StringCreationEntity_ShouldSetCreatorId()
    {
        var entity = new StringCreationEntity();
        CreationAuditedInitializer.Init(entity, "str-user-001", UserName);

        entity.CreatorId.ShouldBe("str-user-001");
    }

    // -----------------------------------------------------------------
    //  ICreationAuditedObject<int>
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：实体实现 ICreationAuditedObject&lt;int&gt; 时，CreatorId 应被解析为 int。
    /// </summary>
    [Fact]
    public void Init_IntCreationEntity_ShouldSetCreatorId()
    {
        var entity = new IntCreationEntity();
        CreationAuditedInitializer.Init(entity, "42", UserName);

        entity.CreatorId.ShouldBe(42);
    }

    // -----------------------------------------------------------------
    //  ICreationAuditedObject<long>
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：实体实现 ICreationAuditedObject&lt;long&gt; 时，CreatorId 应被解析为 long。
    /// </summary>
    [Fact]
    public void Init_LongCreationEntity_ShouldSetCreatorId()
    {
        var entity = new LongCreationEntity();
        CreationAuditedInitializer.Init(entity, "9999999999", UserName);

        entity.CreatorId.ShouldBe(9999999999L);
    }
}

// =========================================================================
//  DeletionAuditedInitializer Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 DeletionAuditedInitializer.Init 的各属性初始化行为。
/// </summary>
public class DeletionAuditedInitializerTest
{
    private static readonly string UserId = Guid.NewGuid().ToString();
    private static readonly string UserName = "Admin";

    /// <summary>
    /// 测试目的：实体为 null 时不抛异常。
    /// </summary>
    [Fact]
    public void Init_NullEntity_ShouldNotThrow()
    {
        Should.NotThrow(() => DeletionAuditedInitializer.Init(null, UserId, UserName));
    }

    /// <summary>
    /// 测试目的：实体实现 IDeletionAuditedObject 时，DeletionTime 应被设置为 DateTime.Now 附近。
    /// </summary>
    [Fact]
    public void Init_EntityWithDeletionTime_ShouldSetDeletionTimeToNow()
    {
        var entity = new DeletionEntity();
        var before = DateTime.Now;
        DeletionAuditedInitializer.Init(entity, UserId, UserName);
        var after = DateTime.Now;

        entity.DeletionTime.ShouldNotBeNull();
        entity.DeletionTime.Value.ShouldBeInRange(before, after);
    }

    /// <summary>
    /// 测试目的：实体实现 IHasDeleter 时，Deleter 应被设置为传入的 userName。
    /// </summary>
    [Fact]
    public void Init_EntityWithDeleter_ShouldSetDeleterName()
    {
        var entity = new NamedDeletionEntity();
        DeletionAuditedInitializer.Init(entity, UserId, "Carol");

        entity.Deleter.ShouldBe("Carol");
    }

    /// <summary>
    /// 测试目的：userName 为 null/空时，Deleter 不应被赋值。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Init_EmptyUserName_ShouldNotSetDeleter(string userName)
    {
        var entity = new NamedDeletionEntity();
        DeletionAuditedInitializer.Init(entity, UserId, userName);

        entity.Deleter.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：实体实现 IDeletionAuditedObject&lt;Guid?&gt; 时，DeleterId 应被解析并赋值。
    /// </summary>
    [Fact]
    public void Init_NullableGuidDeletionEntity_ShouldSetDeleterId()
    {
        var guidId = Guid.NewGuid();
        var entity = new DeletionEntity();
        DeletionAuditedInitializer.Init(entity, guidId.ToString(), UserName);

        entity.DeleterId.ShouldNotBeNull();
        entity.DeleterId.Value.ShouldBe(guidId);
    }

    /// <summary>
    /// 测试目的：userId 为 null 时，DeleterId 不应被赋值。
    /// </summary>
    [Fact]
    public void Init_NullUserId_ShouldNotSetDeleterId()
    {
        var entity = new DeletionEntity();
        DeletionAuditedInitializer.Init(entity, null, UserName);

        entity.DeleterId.ShouldBeNull();
    }
}

// =========================================================================
//  ModificationAuditedInitializer Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 ModificationAuditedInitializer.Init 的各属性初始化行为。
/// </summary>
public class ModificationAuditedInitializerTest
{
    private static readonly string UserId = Guid.NewGuid().ToString();
    private static readonly string UserName = "Editor";
    private static readonly DateTime FixedTime = new(2025, 9, 1, 8, 0, 0);

    /// <summary>
    /// 测试目的：实体为 null 时不抛异常。
    /// </summary>
    [Fact]
    public void Init_NullEntity_ShouldNotThrow()
    {
        Should.NotThrow(() => ModificationAuditedInitializer.Init(null, UserId, UserName));
    }

    /// <summary>
    /// 测试目的：LastModificationTime 应被设置为 DateTime.Now 附近。
    /// </summary>
    [Fact]
    public void Init_EntityWithModificationTime_ShouldSetLastModificationTime()
    {
        var entity = new ModificationEntity();
        var before = DateTime.Now;
        ModificationAuditedInitializer.Init(entity, UserId, UserName);
        var after = DateTime.Now;

        entity.LastModificationTime.ShouldNotBeNull();
        entity.LastModificationTime.Value.ShouldBeInRange(before, after);
    }

    /// <summary>
    /// 测试目的：传入 dateTime 参数时，LastModificationTime 应使用指定时间。
    /// </summary>
    [Fact]
    public void Init_WithDateTime_ShouldUseProvidedTime()
    {
        var entity = new ModificationEntity();
        ModificationAuditedInitializer.Init(entity, UserId, UserName, FixedTime);

        entity.LastModificationTime.ShouldBe(FixedTime);
    }

    /// <summary>
    /// 测试目的：LastModifier 应被设置为传入的 userName。
    /// </summary>
    [Fact]
    public void Init_EntityWithModifier_ShouldSetLastModifier()
    {
        var entity = new NamedModificationEntity();
        ModificationAuditedInitializer.Init(entity, UserId, "Dave");

        entity.LastModifier.ShouldBe("Dave");
    }

    /// <summary>
    /// 测试目的：userName 为 null/空时，LastModifier 不应被赋值。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Init_EmptyUserName_ShouldNotSetLastModifier(string userName)
    {
        var entity = new NamedModificationEntity();
        ModificationAuditedInitializer.Init(entity, UserId, userName);

        entity.LastModifier.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：实体实现 IModificationAuditedObject&lt;Guid?&gt; 时，LastModifierId 应被解析并赋值。
    /// </summary>
    [Fact]
    public void Init_NullableGuidModificationEntity_ShouldSetLastModifierId()
    {
        var guidId = Guid.NewGuid();
        var entity = new ModificationEntity();
        ModificationAuditedInitializer.Init(entity, guidId.ToString(), UserName);

        entity.LastModifierId.ShouldNotBeNull();
        entity.LastModifierId.Value.ShouldBe(guidId);
    }

    /// <summary>
    /// 测试目的：userId 为 null 时，LastModifierId 不应被赋值。
    /// </summary>
    [Fact]
    public void Init_NullUserId_ShouldNotSetLastModifierId()
    {
        var entity = new ModificationEntity();
        ModificationAuditedInitializer.Init(entity, null, UserName);

        entity.LastModifierId.ShouldBeNull();
    }
}
