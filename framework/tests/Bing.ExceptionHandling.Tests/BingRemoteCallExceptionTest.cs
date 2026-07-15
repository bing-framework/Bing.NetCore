using Bing.Http;
using Bing.Http.Clients;
using Shouldly;
using Xunit;

namespace Bing.ExceptionHandling.Tests;

/// <summary>
/// <see cref="BingRemoteCallException"/> 单元测试
/// </summary>
public class BingRemoteCallExceptionTest
{
    // ═══════════════════════════════════════════════════════════
    // 继承结构
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：BingRemoteCallException 应实现 IHasHttpStatusCode，允许携带 HTTP 状态码。
    /// </summary>
    [Fact]
    public void BingRemoteCallException_ShouldImplementIHasHttpStatusCode()
    {
        // Arrange & Act
        var ex = new BingRemoteCallException("远程调用失败");

        // Assert
        ex.ShouldBeAssignableTo<IHasHttpStatusCode>();
    }

    // ═══════════════════════════════════════════════════════════
    // 构造函数：(string message)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：使用 message 构造时，Message 应等于传入文本。
    /// </summary>
    [Fact]
    public void Ctor_WithMessage_ShouldSetMessage()
    {
        // Arrange & Act
        var ex = new BingRemoteCallException("调用 OrderService 失败");

        // Assert
        ex.Message.ShouldBe("调用 OrderService 失败");
    }

    /// <summary>
    /// 测试目的：使用 message + innerException 构造时，InnerException 应被保存。
    /// </summary>
    [Fact]
    public void Ctor_WithMessageAndInner_ShouldSetInnerException()
    {
        // Arrange
        var inner = new HttpRequestException("网络超时");

        // Act
        var ex = new BingRemoteCallException("远程调用失败", inner);

        // Assert
        ex.InnerException.ShouldBe(inner);
    }

    /// <summary>
    /// 测试目的：无内部异常时 InnerException 应为 null，不抛异常。
    /// </summary>
    [Fact]
    public void Ctor_WithMessageOnly_ShouldHaveNullInnerException()
    {
        // Arrange & Act
        var ex = new BingRemoteCallException("简单错误");

        // Assert
        ex.InnerException.ShouldBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // 构造函数：(RemoteServiceErrorInfo error)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：使用 RemoteServiceErrorInfo 构造时，Error 属性应指向该错误信息。
    /// </summary>
    [Fact]
    public void Ctor_WithErrorInfo_ShouldSetErrorProperty()
    {
        // Arrange
        var errorInfo = new RemoteServiceErrorInfo("用户不存在", code: "USER_NOT_FOUND");

        // Act
        var ex = new BingRemoteCallException(errorInfo);

        // Assert
        ex.Error.ShouldBe(errorInfo);
    }

    /// <summary>
    /// 测试目的：ErrorInfo.Message 应作为异常 Message，确保错误描述可读。
    /// </summary>
    [Fact]
    public void Ctor_WithErrorInfo_ShouldUseErrorInfoMessageAsExceptionMessage()
    {
        // Arrange
        var errorInfo = new RemoteServiceErrorInfo("服务不可用");

        // Act
        var ex = new BingRemoteCallException(errorInfo);

        // Assert
        ex.Message.ShouldBe("服务不可用");
    }

    /// <summary>
    /// 测试目的：ErrorInfo.Code 应可通过 Code 属性访问。
    /// </summary>
    [Fact]
    public void Ctor_WithErrorInfo_CodeShouldBeAccessibleViaProperty()
    {
        // Arrange
        var errorInfo = new RemoteServiceErrorInfo("权限不足", code: "AUTH_403");

        // Act
        var ex = new BingRemoteCallException(errorInfo);

        // Assert
        ex.Code.ShouldBe("AUTH_403");
    }

    /// <summary>
    /// 测试目的：ErrorInfo.Details 应可通过 Details 属性访问。
    /// </summary>
    [Fact]
    public void Ctor_WithErrorInfo_DetailsShouldBeAccessibleViaProperty()
    {
        // Arrange
        var errorInfo = new RemoteServiceErrorInfo("参数错误", details: "字段 Name 不能为空");

        // Act
        var ex = new BingRemoteCallException(errorInfo);

        // Assert
        ex.Details.ShouldBe("字段 Name 不能为空");
    }

    /// <summary>
    /// 测试目的：当 ErrorInfo.Data 有键值对时，应被复制到异常的 Data 字典。
    /// </summary>
    [Fact]
    public void Ctor_WithErrorInfoData_ShouldCopyDataToException()
    {
        // Arrange
        var data = new System.Collections.Hashtable { ["requestId"] = "req-001", ["service"] = "UserService" };
        var errorInfo = new RemoteServiceErrorInfo("错误", data: data);

        // Act
        var ex = new BingRemoteCallException(errorInfo);

        // Assert
        ex.Data["requestId"].ShouldBe("req-001");
        ex.Data["service"].ShouldBe("UserService");
    }

    // ═══════════════════════════════════════════════════════════
    // HttpStatusCode
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：HttpStatusCode 默认为 0（未设置），允许调用方后续赋值。
    /// </summary>
    [Fact]
    public void HttpStatusCode_Default_ShouldBeZero()
    {
        // Arrange & Act
        var ex = new BingRemoteCallException("错误");

        // Assert
        ex.HttpStatusCode.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：HttpStatusCode 可被设置为具体 HTTP 状态码，方便外层框架处理。
    /// </summary>
    [Fact]
    public void HttpStatusCode_WhenSet_ShouldReflectNewValue()
    {
        // Arrange
        var ex = new BingRemoteCallException("未授权") { HttpStatusCode = 401 };

        // Assert
        ex.HttpStatusCode.ShouldBe(401);
    }

    // ═══════════════════════════════════════════════════════════
    // RemoteServiceErrorInfo 构造函数
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：RemoteServiceErrorInfo 默认构造后各字段均为 null，允许按需填充。
    /// </summary>
    [Fact]
    public void RemoteServiceErrorInfo_Default_AllFieldsShouldBeNull()
    {
        // Arrange & Act
        var info = new RemoteServiceErrorInfo();

        // Assert
        info.Code.ShouldBeNull();
        info.Message.ShouldBeNull();
        info.Details.ShouldBeNull();
        info.Data.ShouldBeNull();
        info.ValidationErrors.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：使用带参数构造时，各字段应被正确赋值。
    /// </summary>
    [Fact]
    public void RemoteServiceErrorInfo_Ctor_ShouldSetAllProvidedFields()
    {
        // Arrange & Act
        var info = new RemoteServiceErrorInfo("错误消息", "详细说明", "ERR_001");

        // Assert
        info.Message.ShouldBe("错误消息");
        info.Details.ShouldBe("详细说明");
        info.Code.ShouldBe("ERR_001");
    }
}
