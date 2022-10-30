using System;
using System.Collections.Generic;
using System.Text;
using Bing.Logging.Tests.Samples;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Bing.Logging.Tests;

/// <summary>
/// 日志操作测试 - 测试跟踪级别
/// </summary>
public partial class LogTest
{
    /// <summary>
    /// 测试写跟踪日志 - 验证设置空消息
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_Validate_Empty()
    {
        _log.Message("").LogTrace();
        _mockLogger.Verify(t => t.LogTrace(It.IsAny<EventId>(), It.IsAny<Exception>(), It.IsAny<string>()),
            Times.Never);
    }

    /// <summary>
    /// 测试写跟踪日志 - 设置简单消息
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_1()
    {
        _log.Message("a").LogTrace();
        _mockLogger.Verify(t => t.LogTrace(0, null, "a"));
    }

    /// <summary>
    /// 测试写跟踪日志 - 给日志传递1个参数
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_2()
    {
        _log.Message("a{b}", 1).LogTrace();
        _mockLogger.Verify(t => t.LogTrace(0, null, "a{b}", 1));
    }

    /// <summary>
    /// 测试写跟踪日志 - 给日志传递2个参数
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_3()
    {
        _log.Message("a{b}{c}", 1, 2).LogTrace();
        _mockLogger.Verify(t => t.LogTrace(0, null, "a{b}{c}", 1, 2));
    }

    /// <summary>
    /// 测试写跟踪日志 - 多次调用Message方法设置日志消息
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_4()
    {
        _log.Message("a{b}{c}", 1, 2)
            .Message("e{f}{g}", 3, 4)
            .LogTrace();
        _mockLogger.Verify(t => t.LogTrace(0, null, "a{b}{c}e{f}{g}", 1, 2, 3, 4));
    }

    /// <summary>
    /// 测试写跟踪日志 - 同时设置自定义扩展属性、日志消息
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_5()
    {
        _log.Message("a{b}{c}", 1, 2)
            .Property("d","3")
            .Property("e","4")
            .LogTrace();
        _mockLogger.Verify(t => t.LogTrace(0, null, "[d:{d},e:{e}]a{b}{c}", "3", "4", 1, 2));
    }

    /// <summary>
    /// 测试写跟踪日志 - 同时设置状态对象、日志消息
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_6()
    {
        var product = new Product {Code = "a", Name = "b", Price = 123};
        _log.Message("a{b}{c}", 1, 2)
            .State(product)
            .LogTrace();
        _mockLogger.Verify(t => t.LogTrace(0, null, "[Code:{Code},Name:{Name},Price:{Price}]a{b}{c}", "a", "b", 123, 1, 2));
    }

    /// <summary>
    /// 测试写跟踪日志 - 同时设置自定义扩展属性、状态对象、日志消息
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_7()
    {
        var product = new Product { Code = "a", Name = "b", Price = 123 };
        _log.Message("a{b}{c}", 1, 2)
            .Property("d", "3")
            .Property("e", "4")
            .State(product)
            .LogTrace();
        _mockLogger.Verify(t => t.LogTrace(0, null, "[d:{d},e:{e},Code:{Code},Name:{Name},Price:{Price}]a{b}{c}", "3", "4", "a", "b", 123, 1, 2));
    }

    /// <summary>
    /// 测试写跟踪日志 - 同时设置EventId、异常、日志消息
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_8()
    {
        _log.EventId(110)
            .Exception(new Exception("a"))
            .Message("a{b}{c}", 1, 2)
            .LogTrace();
        _mockLogger.Verify(t => t.LogTrace(110, It.Is<Exception>(e => e.Message == "a"), "a{b}{c}", 1, 2));
    }

    /// <summary>
    /// 测试写跟踪日志 - 设置1个自定义扩展属性
    /// </summary>
    [Fact]
    public void Test_LogTrace_Property_1()
    {
        _log.Property("a","1").LogTrace();
        _mockLogger.Verify(t => t.Log(LogLevel.Trace, 0,
            It.Is<IDictionary<string, object>>(dict => dict.Count == 1 && dict["a"].ToString() == "1"), 
            null,
            It.IsAny<Func<IDictionary<string, object>, Exception, string>>()));
    }

    /// <summary>
    /// 测试写跟踪日志 - 设置2个自定义扩展属性
    /// </summary>
    [Fact]
    public void Test_LogTrace_Property_2()
    {
        _log.Property("a", "1")
            .Property("b","2")
            .LogTrace();
        _mockLogger.Verify(t => t.Log(LogLevel.Trace, 0,
            It.Is<IDictionary<string, object>>(dict => dict.Count == 2 && dict["a"].ToString() == "1" && dict["b"].ToString() == "2"),
            null,
            It.IsAny<Func<IDictionary<string, object>, Exception, string>>()));
    }

    /// <summary>
    /// 测试写跟踪日志 - 设置自定义扩展属性、状态对象
    /// </summary>
    [Fact]
    public void Test_LogTrace_Property_3()
    {
        var product = new Product { Code = "a", Name = "b", Price = 123 };
        _log.Property("Age", "18")
            .State(product)
            .LogTrace();
        _mockLogger.Verify(t => t.Log(LogLevel.Trace, 0,
            It.Is<IDictionary<string, object>>(dict => dict.Count == 4 && dict["Age"].ToString() == "18" && dict["Name"].ToString() == "b"),
            null,
            It.IsAny<Func<IDictionary<string, object>, Exception, string>>()));
    }

    /// <summary>
    /// 测试写跟踪日志 - 设置普通状态对象 - 忽略控制属性
    /// </summary>
    [Fact]
    public void Test_LogTrace_State_1()
    {
        var product = new Product { Code = "a" };
        _log.State(product)
            .LogTrace();
        _mockLogger.Verify(t => t.Log(LogLevel.Trace, 0,
            It.Is<IDictionary<string, object>>(dict => dict.Count == 2 && dict["Code"].ToString() == "a" && dict["Price"].ToString() == "0"),
            null,
            It.IsAny<Func<IDictionary<string, object>, Exception, string>>()));
    }

    /// <summary>
    /// 测试写跟踪日志 - 设置字典状态对象 - 忽略控制属性
    /// </summary>
    [Fact]
    public void Test_LogTrace_State_2()
    {
        var content = new Dictionary<string, object> {{"Code", "a"}, {"Name", ""}, {"Price", 0}};
        _log.State(content)
            .LogTrace();
        _mockLogger.Verify(t => t.Log(LogLevel.Trace, 0,
            It.Is<IDictionary<string, object>>(dict => dict.Count == 2 && dict["Code"].ToString() == "a" && dict["Price"].ToString() == "0"),
            null,
            It.IsAny<Func<IDictionary<string, object>, Exception, string>>()));
    }
}