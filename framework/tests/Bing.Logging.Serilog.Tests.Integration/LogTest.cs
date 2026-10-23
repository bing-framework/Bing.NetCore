using System;
using System.Collections.Generic;
using Bing.Helpers;
using Bing.Logging.Tests.Samples;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Events;
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

    /// <summary>
    /// 记录日志集成测试信息的日志记录器。
    /// </summary>
    private readonly ILogger<LogTest> _logger;

    private readonly InMemoryLogEventSink _eventSink;

    /// <summary>
    /// 初始化一个 <see cref="LogTest"/> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">应用服务提供程序。</param>
    /// <param name="log">Bing 日志接口。</param>
    /// <param name="accessor">日志上下文访问器。</param>
    /// <param name="logger">Microsoft 日志接口。</param>
    /// <param name="eventSink">内存事件收集器。</param>
    public LogTest(IServiceProvider serviceProvider, ILog<LogTest> log, ILogContextAccessor accessor,
        ILogger<LogTest> logger, InMemoryLogEventSink eventSink)
    {
        Id.StringGenerateFunc = () => Guid.NewGuid().ToString("N");
        serviceProvider.UseBing();
        _log = log;
        _logger = logger;
        _eventSink = eventSink;
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

    /// <summary>
    /// 验证结构化日志可以记录产品对象。
    /// </summary>
    [Fact]
    public void Test_Log()
    {
        var correlationId = $"corr-{Guid.NewGuid():N}";
        var productCode = $"product-{Guid.NewGuid():N}";
        var product = new Product { Code = productCode, Name = "structured", Price = 996 };
        using (_logger.BeginScope(new Dictionary<string, object> { ["Tenant"] = correlationId }))
            _logger.LogInformation("Test Log for {CorrelationId}: {@Product}", correlationId, product);

        var logEvent = Assert.Single(_eventSink.Events, item =>
            item.Properties.TryGetValue("Tenant", out var value) && value.ToString().Trim('"') == correlationId);
        Assert.Equal(LogEventLevel.Information, logEvent.Level);
        var productValue = Assert.IsType<StructureValue>(logEvent.Properties["Product"]);
        var actualCode = Assert.Single(productValue.Properties, item => item.Name == "Code");
        Assert.Equal(productCode, actualCode.Value.ToString().Trim('"'));
    }
}
