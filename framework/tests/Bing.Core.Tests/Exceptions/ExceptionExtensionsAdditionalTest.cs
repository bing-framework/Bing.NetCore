using Bing.Exceptions;
using Bing.Logging;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Bing.Tests.Exceptions;

/// <summary>
/// ExceptionExtensions 扩展方法额外测试（GetHttpStatusCode / GetErrorCode / GetLogLevel）
/// </summary>
public class ExceptionExtensionsAdditionalTest
{
    // ==================== GetHttpStatusCode ====================

    /// <summary>
    /// 测试目的：传入 null 异常，GetHttpStatusCode 应返回 200。
    /// </summary>
    [Fact]
    public void GetHttpStatusCode_NullException_Returns200()
    {
        // Arrange
        Exception? exception = null;

        // Act
        var code = exception.GetHttpStatusCode();

        // Assert
        code.ShouldBe(200);
    }

    /// <summary>
    /// 测试目的：普通系统异常（非 Warning），GetHttpStatusCode 应返回 200。
    /// </summary>
    [Fact]
    public void GetHttpStatusCode_SystemException_Returns200()
    {
        // Arrange
        var exception = new InvalidOperationException("something went wrong");

        // Act
        var code = exception.GetHttpStatusCode();

        // Assert
        code.ShouldBe(200);
    }

    /// <summary>
    /// 测试目的：Warning 异常带有 HttpStatusCode，GetHttpStatusCode 应返回该值。
    /// </summary>
    [Fact]
    public void GetHttpStatusCode_WarningWithStatusCode_ReturnsWarningStatusCode()
    {
        // Arrange
        var warning = new Warning("出错了", httpStatusCode: 400);

        // Act
        var code = warning.GetHttpStatusCode();

        // Assert
        code.ShouldBe(400);
    }

    /// <summary>
    /// 测试目的：Warning 未设置 HttpStatusCode（默认 0），GetHttpStatusCode 应返回 0（default int）。
    /// </summary>
    [Fact]
    public void GetHttpStatusCode_WarningWithoutStatusCode_ReturnsDefaultZero()
    {
        // Arrange
        var warning = new Warning("出错了");

        // Act
        var code = warning.GetHttpStatusCode();

        // Assert
        code.ShouldBe(0); // default(int)
    }

    // ==================== GetErrorCode ====================

    /// <summary>
    /// 测试目的：传入 null 异常，GetErrorCode 应返回 null。
    /// </summary>
    [Fact]
    public void GetErrorCode_NullException_ReturnsNull()
    {
        // Arrange
        Exception? exception = null;

        // Act
        var code = exception.GetErrorCode();

        // Assert
        code.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：普通系统异常（非 Warning），GetErrorCode 应返回 null。
    /// </summary>
    [Fact]
    public void GetErrorCode_SystemException_ReturnsNull()
    {
        // Arrange
        var exception = new Exception("error");

        // Act
        var code = exception.GetErrorCode();

        // Assert
        code.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：Warning 带错误码，GetErrorCode 应返回该错误码。
    /// </summary>
    [Fact]
    public void GetErrorCode_WarningWithCode_ReturnsCode()
    {
        // Arrange
        var warning = new Warning("出错了", code: "ERR-001");

        // Act
        var code = warning.GetErrorCode();

        // Assert
        code.ShouldBe("ERR-001");
    }

    /// <summary>
    /// 测试目的：Warning 未设置 Code，GetErrorCode 应返回 null。
    /// </summary>
    [Fact]
    public void GetErrorCode_WarningWithoutCode_ReturnsNull()
    {
        // Arrange
        var warning = new Warning("出错了");

        // Act
        var code = warning.GetErrorCode();

        // Assert
        code.ShouldBeNull();
    }

    // ==================== GetLogLevel ====================

    /// <summary>
    /// 测试目的：BusinessException 实现 IHasLogLevel，GetLogLevel 应返回其 LogLevel 属性值。
    /// </summary>
    [Fact]
    public void GetLogLevel_IHasLogLevel_ReturnsExceptionLogLevel()
    {
        // Arrange
        var ex = new BusinessException("ERR", "msg", logLevel: LogLevel.Critical);

        // Act
        var level = ex.GetLogLevel();

        // Assert
        level.ShouldBe(LogLevel.Critical);
    }

    /// <summary>
    /// 测试目的：普通异常（非 IHasLogLevel），默认返回 Error。
    /// </summary>
    [Fact]
    public void GetLogLevel_PlainException_ReturnsDefaultError()
    {
        // Arrange
        var ex = new InvalidOperationException("error");

        // Act
        var level = ex.GetLogLevel();

        // Assert
        level.ShouldBe(LogLevel.Error);
    }

    /// <summary>
    /// 测试目的：普通异常传入自定义 defaultLevel，应返回该自定义值。
    /// </summary>
    [Fact]
    public void GetLogLevel_PlainException_CustomDefault_ReturnsCustom()
    {
        // Arrange
        var ex = new Exception("error");

        // Act
        var level = ex.GetLogLevel(LogLevel.Warning);

        // Assert
        level.ShouldBe(LogLevel.Warning);
    }

    /// <summary>
    /// 测试目的：BusinessException LogLevel 为 Warning，不受 defaultLevel 参数影响。
    /// </summary>
    [Fact]
    public void GetLogLevel_IHasLogLevel_NotAffectedByDefaultLevel()
    {
        // Arrange
        var ex = new BusinessException("ERR", "msg", logLevel: LogLevel.Warning);

        // Act：传入 Error 作为默认值，但应使用异常自身的 Warning
        var level = ex.GetLogLevel(LogLevel.Error);

        // Assert
        level.ShouldBe(LogLevel.Warning);
    }
}
