using System.Collections;
using Bing.AspNetCore.ExceptionHandling;
using Bing.Http;
using Shouldly;
using Xunit;

namespace Bing.ExceptionHandling.Tests.Http;

/// <summary>
/// 测试目的：验证 <see cref="RemoteServiceErrorInfo"/> 各构造重载及属性读写行为。
/// </summary>
public class RemoteServiceErrorInfoTest
{
    // ── 默认构造 ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：默认构造函数应创建所有属性为 null 的实例，不抛异常。
    /// </summary>
    [Fact]
    public void DefaultCtor_ShouldCreateInstanceWithNullProperties()
    {
        // Act
        var info = new RemoteServiceErrorInfo();

        // Assert
        info.Message.ShouldBeNull();
        info.Details.ShouldBeNull();
        info.Code.ShouldBeNull();
        info.Data.ShouldBeNull();
        info.ValidationErrors.ShouldBeNull();
    }

    // ── 参数化构造 ───────────────────────────────────────────────

    /// <summary>
    /// 测试目的：传入 message 时，Message 属性应正确赋值。
    /// </summary>
    [Fact]
    public void Ctor_WithMessageOnly_ShouldSetMessage()
    {
        // Act
        var info = new RemoteServiceErrorInfo("用户不存在");

        // Assert
        info.Message.ShouldBe("用户不存在");
        info.Details.ShouldBeNull();
        info.Code.ShouldBeNull();
        info.Data.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：传入 message + details 时，两个属性均应正确赋值。
    /// </summary>
    [Fact]
    public void Ctor_WithMessageAndDetails_ShouldSetBothFields()
    {
        // Act
        var info = new RemoteServiceErrorInfo("请求失败", "超出限流配额");

        // Assert
        info.Message.ShouldBe("请求失败");
        info.Details.ShouldBe("超出限流配额");
    }

    /// <summary>
    /// 测试目的：传入 message + details + code 时，三个属性均应正确赋值。
    /// </summary>
    [Fact]
    public void Ctor_WithMessageDetailsAndCode_ShouldSetAllFields()
    {
        // Act
        var info = new RemoteServiceErrorInfo("订单不存在", "订单 #12345 未找到", "ORDER_NOT_FOUND");

        // Assert
        info.Message.ShouldBe("订单不存在");
        info.Details.ShouldBe("订单 #12345 未找到");
        info.Code.ShouldBe("ORDER_NOT_FOUND");
    }

    /// <summary>
    /// 测试目的：传入非 null Data 时，Data 属性应引用同一字典。
    /// </summary>
    [Fact]
    public void Ctor_WithData_ShouldSetDataProperty()
    {
        // Arrange
        IDictionary data = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var info = new RemoteServiceErrorInfo("错误", data: data);

        // Assert
        info.Data.ShouldBeSameAs(data);
    }

    // ── 属性读写 ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：ValidationErrors 属性可读写，应存储验证错误数组。
    /// </summary>
    [Fact]
    public void ValidationErrors_WhenSet_ShouldBeReadable()
    {
        // Arrange
        var errors = new[]
        {
            new RemoteServiceValidationErrorInfo("用户名不能为空", "UserName"),
            new RemoteServiceValidationErrorInfo("邮箱格式不正确", "Email")
        };
        var info = new RemoteServiceErrorInfo("参数验证失败");

        // Act
        info.ValidationErrors = errors;

        // Assert
        info.ValidationErrors.ShouldBe(errors);
        info.ValidationErrors.Length.ShouldBe(2);
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 测试目的：验证 <see cref="RemoteServiceErrorResponse"/> 构造函数及 Error 属性。
/// </summary>
public class RemoteServiceErrorResponseTest
{
    /// <summary>
    /// 测试目的：构造时传入非 null 的 ErrorInfo，Error 属性应引用同一实例。
    /// </summary>
    [Fact]
    public void Ctor_WithErrorInfo_ShouldSetErrorProperty()
    {
        // Arrange
        var errorInfo = new RemoteServiceErrorInfo("服务不可用");

        // Act
        var response = new RemoteServiceErrorResponse(errorInfo);

        // Assert
        response.Error.ShouldBeSameAs(errorInfo);
    }

    /// <summary>
    /// 测试目的：构造时传入 null，Error 属性应为 null，不抛异常。
    /// </summary>
    [Fact]
    public void Ctor_WithNullErrorInfo_ErrorShouldBeNull()
    {
        // Act
        var response = new RemoteServiceErrorResponse(null);

        // Assert
        response.Error.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：通过属性设置 Error 后，应能正确读取新值。
    /// </summary>
    [Fact]
    public void Error_PropertySet_ShouldBeReadable()
    {
        // Arrange
        var response = new RemoteServiceErrorResponse(null);
        var newError = new RemoteServiceErrorInfo("新错误", code: "E001");

        // Act
        response.Error = newError;

        // Assert
        response.Error.ShouldBeSameAs(newError);
        response.Error.Code.ShouldBe("E001");
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 测试目的：验证 <see cref="RemoteServiceValidationErrorInfo"/> 各构造重载的属性赋值行为。
/// </summary>
public class RemoteServiceValidationErrorInfoTest
{
    // ── 默认构造 ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：默认构造应创建 Message=null、Members=null 的实例。
    /// </summary>
    [Fact]
    public void DefaultCtor_ShouldCreateInstanceWithNullFields()
    {
        // Act
        var info = new RemoteServiceValidationErrorInfo();

        // Assert
        info.Message.ShouldBeNull();
        info.Members.ShouldBeNull();
    }

    // ── 单参数构造 ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：传入 message 时，Message 应被正确赋值，Members 保持 null。
    /// </summary>
    [Fact]
    public void Ctor_WithMessage_ShouldSetMessageOnly()
    {
        // Act
        var info = new RemoteServiceValidationErrorInfo("字段不能为空");

        // Assert
        info.Message.ShouldBe("字段不能为空");
        info.Members.ShouldBeNull();
    }

    // ── message + members 数组构造 ─────────────────────────────────

    /// <summary>
    /// 测试目的：传入 message + string[] members 时，两个属性均应正确赋值。
    /// </summary>
    [Fact]
    public void Ctor_WithMessageAndMembersArray_ShouldSetBothFields()
    {
        // Arrange
        var members = new[] { "UserName", "Email" };

        // Act
        var info = new RemoteServiceValidationErrorInfo("验证失败", members);

        // Assert
        info.Message.ShouldBe("验证失败");
        info.Members.ShouldBe(members);
        info.Members.Length.ShouldBe(2);
    }

    // ── message + 单个 member 字符串构造 ──────────────────────────────

    /// <summary>
    /// 测试目的：传入 message + 单个 member 字符串时，Members 应包含这一个元素。
    /// </summary>
    [Fact]
    public void Ctor_WithMessageAndSingleMember_ShouldWrapMemberInArray()
    {
        // Act
        var info = new RemoteServiceValidationErrorInfo("名称不能为空", "Name");

        // Assert
        info.Message.ShouldBe("名称不能为空");
        info.Members.ShouldNotBeNull();
        info.Members.Length.ShouldBe(1);
        info.Members[0].ShouldBe("Name");
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 测试目的：验证 <see cref="BingExceptionHandlingOptions"/> 默认值及属性读写行为。
/// </summary>
public class BingExceptionHandlingOptionsTest
{
    /// <summary>
    /// 测试目的：默认 SendExceptionDetailsToClients 应为 false（不暴露内部错误详情给客户端）。
    /// </summary>
    [Fact]
    public void Default_SendExceptionDetailsToClients_ShouldBeFalse()
    {
        // Act
        var options = new BingExceptionHandlingOptions();

        // Assert
        options.SendExceptionDetailsToClients.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：默认 SendStackTraceToClients 应为 true（默认允许传递堆栈信息）。
    /// </summary>
    [Fact]
    public void Default_SendStackTraceToClients_ShouldBeTrue()
    {
        // Act
        var options = new BingExceptionHandlingOptions();

        // Assert
        options.SendStackTraceToClients.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：可将 SendExceptionDetailsToClients 设置为 true，并正确读取。
    /// </summary>
    [Fact]
    public void SendExceptionDetailsToClients_WhenSetTrue_ShouldReturnTrue()
    {
        // Arrange
        var options = new BingExceptionHandlingOptions
        {
            SendExceptionDetailsToClients = true
        };

        // Assert
        options.SendExceptionDetailsToClients.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：可将 SendStackTraceToClients 设置为 false，并正确读取。
    /// </summary>
    [Fact]
    public void SendStackTraceToClients_WhenSetFalse_ShouldReturnFalse()
    {
        // Arrange
        var options = new BingExceptionHandlingOptions
        {
            SendStackTraceToClients = false
        };

        // Assert
        options.SendStackTraceToClients.ShouldBeFalse();
    }
}
