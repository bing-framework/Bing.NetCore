using Bing.AspNetCore.ExceptionHandling;
using Bing.Domain.Entities;
using Bing.Http;
using Shouldly;
using Xunit;

namespace Bing.ExceptionHandling.Tests;

/// <summary>
/// <see cref="EntityNotFoundException"/>、<see cref="RemoteServiceErrorInfo"/>、
/// <see cref="RemoteServiceErrorResponse"/>、<see cref="RemoteServiceValidationErrorInfo"/>、
/// <see cref="BingExceptionHandlingOptions"/> 单元测试
/// </summary>
public class ErrorInfoAndEntityNotFoundTest
{
    // ═══════════════════════════════════════════════════════════
    // EntityNotFoundException
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：无参构造应使用默认错误消息，不抛异常，
    /// 便于快速抛出"实体不存在"而不必传入具体信息。
    /// </summary>
    [Fact]
    public void EntityNotFoundException_DefaultConstructor_ShouldHaveDefaultMessage()
    {
        // Act
        var ex = new EntityNotFoundException();

        // Assert
        ex.Message.ShouldNotBeNullOrEmpty();
        ex.EntityType.ShouldBeNull();
        ex.Id.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：仅传入 entityType 时，EntityType 属性应正确设置，Id 应为 null。
    /// </summary>
    [Fact]
    public void EntityNotFoundException_WithEntityType_ShouldSetEntityType()
    {
        // Act
        var ex = new EntityNotFoundException(typeof(string));

        // Assert
        ex.EntityType.ShouldBe(typeof(string));
        ex.Id.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：传入 entityType + id 时，两个属性均应被正确赋值，
    /// 且消息中应包含类型名和 id 信息。
    /// </summary>
    [Fact]
    public void EntityNotFoundException_WithEntityTypeAndId_ShouldSetBothProperties()
    {
        // Act
        var ex = new EntityNotFoundException(typeof(string), 42);

        // Assert
        ex.EntityType.ShouldBe(typeof(string));
        ex.Id.ShouldBe(42);
        ex.Message.ShouldContain("42");
    }

    /// <summary>
    /// 测试目的：传入自定义消息构造时，Message 应反映自定义文本。
    /// </summary>
    [Fact]
    public void EntityNotFoundException_WithCustomMessage_ShouldUseCustomMessage()
    {
        // Act
        var ex = new EntityNotFoundException("custom entity not found");

        // Assert
        ex.Message.ShouldBe("custom entity not found");
    }

    /// <summary>
    /// 测试目的：传入 innerException 时，InnerException 应正确传递，
    /// 不丢失原始异常上下文。
    /// </summary>
    [Fact]
    public void EntityNotFoundException_WithInnerException_ShouldChainInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var ex = new EntityNotFoundException("outer", inner);

        // Assert
        ex.InnerException.ShouldBeSameAs(inner);
    }

    /// <summary>
    /// 测试目的：EntityNotFoundException 应继承自 BingException，
    /// 确保框架统一异常处理链路能识别并处理此类异常。
    /// </summary>
    [Fact]
    public void EntityNotFoundException_ShouldInheritFromBingException()
    {
        // Assert
        typeof(EntityNotFoundException).BaseType.ShouldBe(typeof(BingException));
    }

    // ═══════════════════════════════════════════════════════════
    // RemoteServiceErrorInfo
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：无参构造后所有属性应为 null，
    /// 确保空错误信息不携带垃圾默认值。
    /// </summary>
    [Fact]
    public void RemoteServiceErrorInfo_DefaultConstructor_AllPropertiesShouldBeNull()
    {
        // Act
        var info = new RemoteServiceErrorInfo();

        // Assert
        info.Code.ShouldBeNull();
        info.Message.ShouldBeNull();
        info.Details.ShouldBeNull();
        info.Data.ShouldBeNull();
        info.ValidationErrors.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：通过有参构造创建时，各参数应正确赋值到对应属性。
    /// </summary>
    [Fact]
    public void RemoteServiceErrorInfo_Constructor_WithAllArgs_ShouldSetProperties()
    {
        // Arrange
        var data = new System.Collections.Hashtable { { "key", "value" } };

        // Act
        var info = new RemoteServiceErrorInfo("msg", "details", "CODE-001", data);

        // Assert
        info.Message.ShouldBe("msg");
        info.Details.ShouldBe("details");
        info.Code.ShouldBe("CODE-001");
        info.Data.ShouldBeSameAs(data);
    }

    /// <summary>
    /// 测试目的：仅传 message 时，Details / Code / Data 应保持 null，
    /// 确保可选参数的默认行为正确。
    /// </summary>
    [Fact]
    public void RemoteServiceErrorInfo_Constructor_OnlyMessage_ShouldLeaveOthersNull()
    {
        // Act
        var info = new RemoteServiceErrorInfo("only message");

        // Assert
        info.Message.ShouldBe("only message");
        info.Details.ShouldBeNull();
        info.Code.ShouldBeNull();
        info.Data.ShouldBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // RemoteServiceErrorResponse
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：RemoteServiceErrorResponse 构造器应将传入的 Error 正确存储，
    /// 确保 HTTP 响应封装后的 Error 属性可正常读取。
    /// </summary>
    [Fact]
    public void RemoteServiceErrorResponse_Constructor_ShouldSetError()
    {
        // Arrange
        var errorInfo = new RemoteServiceErrorInfo("test error");

        // Act
        var response = new RemoteServiceErrorResponse(errorInfo);

        // Assert
        response.Error.ShouldBeSameAs(errorInfo);
        response.Error.Message.ShouldBe("test error");
    }

    // ═══════════════════════════════════════════════════════════
    // RemoteServiceValidationErrorInfo
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：无参构造应不抛异常，Message 和 Members 均为 null。
    /// </summary>
    [Fact]
    public void RemoteServiceValidationErrorInfo_DefaultConstructor_ShouldBeEmpty()
    {
        // Act
        var info = new RemoteServiceValidationErrorInfo();

        // Assert
        info.Message.ShouldBeNull();
        info.Members.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：仅传 message 构造时，Message 应正确设置，Members 保持 null。
    /// </summary>
    [Fact]
    public void RemoteServiceValidationErrorInfo_WithMessage_ShouldSetMessageOnly()
    {
        // Act
        var info = new RemoteServiceValidationErrorInfo("field is required");

        // Assert
        info.Message.ShouldBe("field is required");
        info.Members.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：传 message + members 数组构造时，两个属性均应正确赋值。
    /// </summary>
    [Fact]
    public void RemoteServiceValidationErrorInfo_WithMessageAndMembers_ShouldSetBoth()
    {
        // Act
        var info = new RemoteServiceValidationErrorInfo("required", new[] { "Name", "Email" });

        // Assert
        info.Message.ShouldBe("required");
        info.Members.ShouldContain("Name");
        info.Members.ShouldContain("Email");
        info.Members.Length.ShouldBe(2);
    }

    /// <summary>
    /// 测试目的：传 message + 单个 member 字符串时，Members 数组应只有一个元素。
    /// </summary>
    [Fact]
    public void RemoteServiceValidationErrorInfo_WithSingleMember_ShouldWrapInArray()
    {
        // Act
        var info = new RemoteServiceValidationErrorInfo("too long", "Username");

        // Assert
        info.Message.ShouldBe("too long");
        info.Members.Length.ShouldBe(1);
        info.Members[0].ShouldBe("Username");
    }

    // ═══════════════════════════════════════════════════════════
    // BingExceptionHandlingOptions
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认配置中 SendExceptionDetailsToClients 应为 false（安全默认），
    /// SendStackTraceToClients 应为 true（调试友好）。
    /// </summary>
    [Fact]
    public void BingExceptionHandlingOptions_Defaults_ShouldMatchExpected()
    {
        // Act
        var options = new BingExceptionHandlingOptions();

        // Assert
        options.SendExceptionDetailsToClients.ShouldBeFalse();
        options.SendStackTraceToClients.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：选项属性应可读写，确保调用方能按需覆盖默认值。
    /// </summary>
    [Fact]
    public void BingExceptionHandlingOptions_SetProperties_ShouldBeReadable()
    {
        // Arrange & Act
        var options = new BingExceptionHandlingOptions
        {
            SendExceptionDetailsToClients = true,
            SendStackTraceToClients = false
        };

        // Assert
        options.SendExceptionDetailsToClients.ShouldBeTrue();
        options.SendStackTraceToClients.ShouldBeFalse();
    }
}
