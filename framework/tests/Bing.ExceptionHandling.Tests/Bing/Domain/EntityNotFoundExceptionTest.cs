using Bing.Domain.Entities;
using Bing.Exceptions;
using Shouldly;
using Xunit;

namespace Bing.ExceptionHandling.Tests;

/// <summary>
/// <see cref="EntityNotFoundException"/> 单元测试
/// 直接 new 被测类，不依赖任何外部服务。
/// </summary>
public class EntityNotFoundExceptionTest
{
    // ── Default constructor ────────────────────────────────────────

    /// <summary>
    /// 测试目的：默认构造后，Message 应包含通用的"找不到实体"语义，
    /// Flag 应为 "__ENTITY_NOT_FOUND_FLG"，Code 应为 "1010"。
    /// </summary>
    [Fact]
    public void DefaultConstructor_ShouldSetDefaultMessageFlagAndCode()
    {
        // Act
        var ex = new EntityNotFoundException();

        // Assert
        ex.Message.ShouldContain("entity");
        ex.Flag.ShouldBe("__ENTITY_NOT_FOUND_FLG");
        ex.Code.ShouldBe("1010");
    }

    // ── Constructor(Type entityType) ──────────────────────────────

    /// <summary>
    /// 测试目的：通过实体类型构造时，EntityType 应正确赋值，
    /// Message 应包含该类型的完全限定名。
    /// </summary>
    [Fact]
    public void Constructor_WithEntityType_ShouldSetEntityTypeInMessage()
    {
        // Act
        var ex = new EntityNotFoundException(typeof(SampleEntity));

        // Assert
        ex.EntityType.ShouldBe(typeof(SampleEntity));
        ex.Id.ShouldBeNull();
        ex.Message.ShouldContain(typeof(SampleEntity).FullName!);
    }

    // ── Constructor(Type entityType, object id) ───────────────────

    /// <summary>
    /// 测试目的：通过实体类型和 Id 构造时，EntityType 和 Id 均应被设置，
    /// Message 应同时包含类型名和 Id 值。
    /// </summary>
    [Fact]
    public void Constructor_WithEntityTypeAndId_ShouldSetBothAndIncludeInMessage()
    {
        // Arrange
        var id = 42;

        // Act
        var ex = new EntityNotFoundException(typeof(SampleEntity), id);

        // Assert
        ex.EntityType.ShouldBe(typeof(SampleEntity));
        ex.Id.ShouldBe(id);
        ex.Message.ShouldContain(typeof(SampleEntity).FullName!);
        ex.Message.ShouldContain("42");
    }

    /// <summary>
    /// 测试目的：Guid 类型的 Id 也应被正确包含在 Message 中。
    /// </summary>
    [Fact]
    public void Constructor_WithEntityTypeAndGuidId_ShouldIncludeGuidInMessage()
    {
        // Arrange
        var id = Guid.Parse("12345678-0000-0000-0000-000000000001");

        // Act
        var ex = new EntityNotFoundException(typeof(SampleEntity), id);

        // Assert
        ex.Id.ShouldBe(id);
        ex.Message.ShouldContain(id.ToString());
    }

    /// <summary>
    /// 测试目的：通过实体类型和 null Id 构造时，Message 应包含"given id"相关提示。
    /// </summary>
    [Fact]
    public void Constructor_WithEntityTypeAndNullId_ShouldUseGivenIdMessage()
    {
        // Act
        var ex = new EntityNotFoundException(typeof(SampleEntity), (object)null);

        // Assert
        ex.EntityType.ShouldBe(typeof(SampleEntity));
        ex.Id.ShouldBeNull();
        ex.Message.ShouldContain("given id");
    }

    // ── Constructor(string message) ───────────────────────────────

    /// <summary>
    /// 测试目的：通过自定义消息构造时，Message 应与传入值一致，
    /// EntityType 和 Id 均为 null。
    /// </summary>
    [Fact]
    public void Constructor_WithCustomMessage_ShouldSetMessage()
    {
        // Act
        var ex = new EntityNotFoundException("找不到目标记录");

        // Assert
        ex.Message.ShouldBe("找不到目标记录");
        ex.EntityType.ShouldBeNull();
        ex.Id.ShouldBeNull();
    }

    // ── Constructor(string message, Exception innerException) ─────

    /// <summary>
    /// 测试目的：带内部异常的构造应将 InnerException 正确链接。
    /// </summary>
    [Fact]
    public void Constructor_WithInnerException_ShouldLinkInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("数据库查询失败");

        // Act
        var ex = new EntityNotFoundException("找不到用户", inner);

        // Assert
        ex.Message.ShouldBe("找不到用户");
        ex.InnerException.ShouldBe(inner);
        ex.Flag.ShouldBe("__ENTITY_NOT_FOUND_FLG");
    }

    // ── Is-a hierarchy ────────────────────────────────────────────

    /// <summary>
    /// 测试目的：EntityNotFoundException 应继承自 BingException，
    /// 满足框架统一异常类型层次约定。
    /// </summary>
    [Fact]
    public void EntityNotFoundException_ShouldInheritFromBingException()
    {
        // Arrange
        var ex = new EntityNotFoundException("test");

        // Assert
        ex.ShouldBeAssignableTo<BingException>();
    }

    // ── 辅助实体类型 ──────────────────────────────────────────────

    private class SampleEntity { }
}
