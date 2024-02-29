using System;
using System.Collections.Generic;
using Bing.Helpers;
using Bing.Logging.Tests.Samples;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bing.Logging.Tests;

/// <summary>
/// 日志操作测试
/// </summary>
public class LogTest
{
    /// <summary>
    /// 日志操作
    /// </summary>
    private readonly ILog<LogTest> _log;

    private readonly ILogger<LogTest> _logger;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public LogTest(IServiceProvider serviceProvider, ILog<LogTest> log, ILogContextAccessor accessor, ILogger<LogTest> logger)
    {
        Id.StringGenerateFunc = () => Guid.NewGuid().ToString("N");
        serviceProvider.UseBing();
        _log = log;
        _logger = logger;
    }

    /// <summary>
    /// 测试写跟踪日志 - 设置简单消息
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_1()
    {
        _log.Message($"Test_LogTrace_Message_1_{Id.CreateString()}").LogTrace();
    }

    /// <summary>
    /// 测试写跟踪日志 - 给日志传递1个参数
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_2()
    {
        _log.Message("Test_LogTrace_Message_2_{ProductCode}", Id.CreateString()).LogTrace();
    }

    /// <summary>
    /// 测试写跟踪日志 - 给日志传递2个参数
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_3()
    {
        _log.Message("Test_LogTrace_Message_3_{id}_{code}", $"id_{Id.CreateString()}",$"code_{Id.CreateString()}").LogTrace();
    }

    /// <summary>
    /// 测试写跟踪日志 - 多次调用Message方法设置日志消息
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_4()
    {
        _log.Message("Test_LogTrace_Message_4_{id}_{code}_", $"id_{Id.CreateString()}", $"code_{Id.CreateString()}")
            .Message("{name}_{note}", $"name_{Id.CreateString()}", $"note_{Id.CreateString()}")
            .LogTrace();
    }

    /// <summary>
    /// 测试写跟踪日志 - 设置@消息 - 对象
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_5()
    {
        _log.Message("Test_LogTrace_Message_5,@Product={@Product}", new Product {Code = "a", Name = "b", Price = 123})
            .LogTrace();
    }

    /// <summary>
    /// 测试写跟踪日志 - 设置$消息 - 对象
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_6()
    {
        _log.Message("Test_LogTrace_Message_6,$Product={$Product}", new Product { Code = "a", Name = "b", Price = 123 })
            .LogTrace();
    }

    /// <summary>
    /// 测试写跟踪日志 - 设置@消息 - 字典
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_7()
    {
        _log.Message("Test_LogTrace_Message_7,@Dictionary={@Dictionary}", new Dictionary<string, object> {{"Code", "a"}, {"Name", "b"}, {"Price", 123}})
            .LogTrace();
    }

    /// <summary>
    /// 测试写跟踪日志 - 设$消息 - 字典
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_8()
    {
        _log.Message("Test_LogTrace_Message_8,$Dictionary={$Dictionary}", new Dictionary<string, object> { { "Code", "a" }, { "Name", "b" }, { "Price", 123 } })
            .LogTrace();
    }

    /// <summary>
    /// 测试写跟踪日志 - 同时设置各属性
    /// </summary>
    [Fact]
    public void Test_LogTrace_Message_9()
    {
        _log.EventId(119)
            .Message("Test_LogTrace_Message_9_{id}", $"id_{Id.CreateString()}")
            .State(new Product { Code = "a", Name = "b", Price = 123 })
            .Property("Code", "a")
            .Property("Age", "18")
            .Property("Description", "hello")
            .LogTrace();
    }

    /// <summary>
    /// 测试写跟踪日志 - 设置自定义扩展属性
    /// </summary>
    [Fact]
    public void Test_LogTrace_Property_1()
    {
        _log.EventId(120)
            .Property("Age", "18")
            .Property("Description", "hello")
            .LogTrace();
    }

    /// <summary>
    /// 测试写跟踪日志 - 设置自定义扩展属性、状态对象
    /// </summary>
    [Fact]
    public void Test_LogTrace_Property_2()
    {
        _log.EventId(121)
            .Property("Age", "18")
            .Property("Description", "hello")
            .State(new Product { Code = "a", Name = "b", Price = 123 })
            .LogTrace();
    }

    [Fact]
    public void Test_Log()
    {
        var product = new Product() { Code = "007", Name = "隔壁老王", Price = 996 };
        _logger.LogInformation("Test Log: {@Product}", product);
    }
}
