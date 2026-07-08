using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Bing.Logging.Tests;

/// <summary>
/// 日志操作测试 - 异常与事件标识
/// </summary>
public partial class LogTest
{
    #region Exception

    /// <summary>
    /// 测试目的：通过 Exception() 设置异常后调用 LogError，异常对象应传递给底层 Logger。
    /// </summary>
    [Fact]
    public void Test_LogError_WithException_ShouldPassExceptionToLogger()
    {
        // Arrange
        var exception = new InvalidOperationException("测试异常");

        // Act
        _log.Exception(exception).LogError();

        // Assert
        _mockLogger.Verify(t => t.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<IDictionary<string, object>>(),
            exception,
            It.IsAny<Func<IDictionary<string, object>, Exception, string>>()));
    }

    /// <summary>
    /// 测试目的：通过 Exception() + Message() 组合调用 LogError，异常与消息均应传递给 Logger。
    /// </summary>
    [Fact]
    public void Test_LogError_WithExceptionAndMessage_ShouldPassBothToLogger()
    {
        // Arrange
        var exception = new InvalidOperationException("操作失败");

        // Act
        _log.Message("发生错误 {msg}", "详情")
            .Exception(exception)
            .LogError();

        // Assert
        _mockLogger.Verify(t => t.Log(
            LogLevel.Error,
            0,
            exception,
            It.IsAny<string>(),
            It.IsAny<object[]>()));
    }

    /// <summary>
    /// 测试目的：通过 Exception() 设置异常后调用 LogWarning，异常应传递到 Warning 级别。
    /// </summary>
    [Fact]
    public void Test_LogWarning_WithException_ShouldPassExceptionToLogger()
    {
        // Arrange
        var exception = new ArgumentException("参数异常");

        // Act
        _log.Exception(exception).LogWarning();

        // Assert
        _mockLogger.Verify(t => t.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<IDictionary<string, object>>(),
            exception,
            It.IsAny<Func<IDictionary<string, object>, Exception, string>>()));
    }

    /// <summary>
    /// 测试目的：通过 Exception() 设置异常后调用 LogCritical，异常应传递到 Critical 级别。
    /// </summary>
    [Fact]
    public void Test_LogCritical_WithException_ShouldPassExceptionToLogger()
    {
        // Arrange
        var exception = new OutOfMemoryException("内存不足");

        // Act
        _log.Exception(exception).LogCritical();

        // Assert
        _mockLogger.Verify(t => t.Log(
            LogLevel.Critical,
            It.IsAny<EventId>(),
            It.IsAny<IDictionary<string, object>>(),
            exception,
            It.IsAny<Func<IDictionary<string, object>, Exception, string>>()));
    }

    #endregion

    #region EventId

    /// <summary>
    /// 测试目的：通过 EventId() 设置事件ID + Message() 调用 LogInformation，EventId 应传递到 Logger。
    /// </summary>
    [Fact]
    public void Test_LogInformation_WithEventId_ShouldPassEventIdToLogger()
    {
        // Arrange
        var eventId = new EventId(1001, "UserCreated");

        // Act
        _log.Message("用户 {name} 已创建", "Alice")
            .EventId(eventId)
            .LogInformation();

        // Assert
        _mockLogger.Verify(t => t.Log(
            LogLevel.Information,
            eventId,
            null,
            It.IsAny<string>(),
            It.IsAny<object[]>()));
    }

    /// <summary>
    /// 测试目的：通过 EventId() 设置事件ID + Property() 调用 LogDebug，EventId 应传递到 Logger（带属性路径）。
    /// </summary>
    [Fact]
    public void Test_LogDebug_WithEventId_ShouldPassEventIdToLogger()
    {
        // Arrange
        var eventId = new EventId(2001, "OrderProcessed");

        // Act
        _log.Property("OrderId", "ORD-001")
            .EventId(eventId)
            .LogDebug();

        // Assert
        _mockLogger.Verify(t => t.Log(
            LogLevel.Debug,
            eventId,
            It.Is<IDictionary<string, object>>(d => d.ContainsKey("OrderId")),
            null,
            It.IsAny<Func<IDictionary<string, object>, Exception, string>>()));
    }

    #endregion

    #region 状态重置行为

    /// <summary>
    /// 测试目的：连续两次 LogError 调用，第二次不应携带第一次设置的 Message（状态在每次调用后重置）。
    /// </summary>
    [Fact]
    public void Test_Log_ShouldClearState_AfterEachWrite()
    {
        // Arrange
        var callCount = 0;
        _mockLogger.Setup(t => t.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<Exception>(),
                It.IsAny<string>(),
                It.IsAny<object[]>()))
            .Callback(() => callCount++);

        // Act - 第一次写日志
        _log.Message("第一条消息").LogError();

        // Act - 第二次写日志（不设置 Message）
        _log.Property("Key", "Value").LogError();

        // Assert - 两次均调用了底层 Logger
        callCount.ShouldBe(1); // 仅第一条带 Message 的日志会走该重载
        _mockLogger.Verify(t => t.Log(
            LogLevel.Error, 0, It.IsAny<Exception>(), "第一条消息",
            It.IsAny<object[]>()), Times.Once);
        _mockLogger.Verify(t => t.Log(
            LogLevel.Error, It.IsAny<EventId>(),
            It.Is<IDictionary<string, object>>(d => d.ContainsKey("Key")),
            null,
            It.IsAny<Func<IDictionary<string, object>, Exception, string>>()), Times.Once);
    }

    #endregion

    #region 完整流式链

    /// <summary>
    /// 测试目的：Message + Property + Exception + EventId 完整链式调用，所有参数均正确传递给 Logger。
    /// </summary>
    [Fact]
    public void Test_FullChain_Message_Property_Exception_EventId()
    {
        // Arrange
        var exception = new Exception("链式异常");
        var eventId = new EventId(9999, "FullChain");

        // Act
        _log.Message("处理结果: {result}", "成功")
            .Property("RequestId", "REQ-001")
            .Exception(exception)
            .EventId(eventId)
            .LogError();

        // Assert - 有 Message 走 message 路径
        _mockLogger.Verify(t => t.Log(
            LogLevel.Error,
            eventId,
            exception,
            It.Is<string>(s => s.Contains("RequestId") && s.Contains("result")),
            It.IsAny<object[]>()));
    }

    #endregion
}
