using Microsoft.Extensions.Logging;
using Shouldly;

namespace Bing.Tests.Exceptions;

/// <summary>
/// BusinessException 业务异常 / UserFriendlyException 用户友好异常 测试
/// </summary>
public class BusinessExceptionTest
{
    // ==================== BusinessException 构造与属性 ====================

    /// <summary>
    /// 测试目的：Code 属性应保存构造时传入的错误码。
    /// </summary>
    [Fact]
    public void BusinessException_Code_IsSetFromConstructor()
    {
        // Arrange & Act
        var ex = new BusinessException("ERR001", "出错了");

        // Assert
        ex.Code.ShouldBe("ERR001");
    }

    /// <summary>
    /// 测试目的：Details 属性应保存构造时传入的错误详情。
    /// </summary>
    [Fact]
    public void BusinessException_Details_IsSetFromConstructor()
    {
        // Arrange & Act
        var ex = new BusinessException("ERR001", "出错了", details: "详细描述");

        // Assert
        ex.Details.ShouldBe("详细描述");
    }

    /// <summary>
    /// 测试目的：不传 logLevel 时，默认日志级别应为 Warning。
    /// </summary>
    [Fact]
    public void BusinessException_LogLevel_DefaultIsWarning()
    {
        // Arrange & Act
        var ex = new BusinessException("ERR001", "出错了");

        // Assert
        ex.LogLevel.ShouldBe(LogLevel.Warning);
    }

    /// <summary>
    /// 测试目的：显式传入 LogLevel 应被正确保存。
    /// </summary>
    [Fact]
    public void BusinessException_LogLevel_CanBeSetExplicitly()
    {
        // Arrange & Act
        var ex = new BusinessException("ERR001", "出错了", logLevel: LogLevel.Error);

        // Assert
        ex.LogLevel.ShouldBe(LogLevel.Error);
    }

    /// <summary>
    /// 测试目的：Details 不传时默认为 null。
    /// </summary>
    [Fact]
    public void BusinessException_Details_DefaultIsNull()
    {
        // Arrange & Act
        var ex = new BusinessException("ERR001", "出错了");

        // Assert
        ex.Details.ShouldBeNull();
    }

    // ==================== WithData 流式 API ====================

    /// <summary>
    /// 测试目的：WithData 应将键值对写入 Data 字典，并返回 this（支持链式调用）。
    /// </summary>
    [Fact]
    public void BusinessException_WithData_AddsDataAndReturnsThis()
    {
        // Arrange
        var ex = new BusinessException("ERR001", "出错了");

        // Act
        var returned = ex.WithData("userId", 42).WithData("orderId", "ORD-001");

        // Assert
        returned.ShouldBeSameAs(ex);
        ex.Data["userId"].ShouldBe(42);
        ex.Data["orderId"].ShouldBe("ORD-001");
    }

    /// <summary>
    /// 测试目的：WithData 对同一键赋值两次，后者覆盖前者。
    /// </summary>
    [Fact]
    public void BusinessException_WithData_OverwriteExistingKey()
    {
        // Arrange
        var ex = new BusinessException("ERR", "msg");

        // Act
        ex.WithData("key", "first");
        ex.WithData("key", "second");

        // Assert
        ex.Data["key"].ShouldBe("second");
    }

    // ==================== IBusinessException 接口 ====================

    /// <summary>
    /// 测试目的：BusinessException 应实现 IBusinessException 接口。
    /// </summary>
    [Fact]
    public void BusinessException_Implements_IBusinessException()
    {
        // Arrange & Act
        var ex = new BusinessException("ERR", "msg");

        // Assert
        ex.ShouldBeAssignableTo<IBusinessException>();
    }

    // ==================== UserFriendlyException ====================

    /// <summary>
    /// 测试目的：UserFriendlyException 应继承 BusinessException。
    /// </summary>
    [Fact]
    public void UserFriendlyException_InheritsBusinessException()
    {
        // Arrange & Act
        var ex = new UserFriendlyException("操作失败");

        // Assert
        ex.ShouldBeAssignableTo<BusinessException>();
    }

    /// <summary>
    /// 测试目的：UserFriendlyException 应实现 IUserFriendlyException 接口。
    /// </summary>
    [Fact]
    public void UserFriendlyException_Implements_IUserFriendlyException()
    {
        // Arrange & Act
        var ex = new UserFriendlyException("操作失败");

        // Assert
        ex.ShouldBeAssignableTo<IUserFriendlyException>();
    }

    /// <summary>
    /// 测试目的：UserFriendlyException 的 Code 和 Details 应正确被传递。
    /// </summary>
    [Fact]
    public void UserFriendlyException_Code_Details_SetCorrectly()
    {
        // Arrange & Act
        var ex = new UserFriendlyException(
            message: "操作失败",
            code: "USER_ERR",
            details: "详情");

        // Assert
        ex.Code.ShouldBe("USER_ERR");
        ex.Details.ShouldBe("详情");
    }

    /// <summary>
    /// 测试目的：UserFriendlyException 默认 LogLevel 为 Warning（继承自 BusinessException）。
    /// </summary>
    [Fact]
    public void UserFriendlyException_LogLevel_DefaultIsWarning()
    {
        // Arrange & Act
        var ex = new UserFriendlyException("出错了");

        // Assert
        ex.LogLevel.ShouldBe(LogLevel.Warning);
    }

    /// <summary>
    /// 测试目的：UserFriendlyException 不传 code 时，Code 应为 null。
    /// </summary>
    [Fact]
    public void UserFriendlyException_NullCode_IsNull()
    {
        // Arrange & Act
        var ex = new UserFriendlyException("出错了");

        // Assert
        ex.Code.ShouldBeNull();
    }
}
