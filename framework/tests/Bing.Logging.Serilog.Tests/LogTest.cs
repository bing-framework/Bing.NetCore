using System;
using System.Collections.Generic;
using System.Diagnostics;
using Bing.Helpers;
using Bing.Logging.Tests.Samples;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Logging.Tests
{
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
        /// 测试初始化
        /// </summary>
        public LogTest(IServiceProvider serviceProvider, ILog<LogTest> log, ILogContextAccessor accessor)
        {
            serviceProvider.UseBing();
            _log = log;
            accessor.Context = new LogContext {Stopwatch = Stopwatch.StartNew(), TraceId = Id.NewString()};
        }

        /// <summary>
        /// 测试写跟踪日志 - 设置简单消息
        /// </summary>
        [Fact]
        public void Test_LogTrace_Message_1()
        {
            _log.Message($"Test_LogTrace_Message_1_{Id.NewString()}").LogTrace();
        }

        /// <summary>
        /// 测试写跟踪日志 - 给日志传递1个参数
        /// </summary>
        [Fact]
        public void Test_LogTrace_Message_2()
        {
            _log.Message("Test_LogTrace_Message_2_{ProductCode}", Id.NewString()).LogTrace();
        }

        /// <summary>
        /// 测试写跟踪日志 - 给日志传递2个参数
        /// </summary>
        [Fact]
        public void Test_LogTrace_Message_3()
        {
            _log.Message("Test_LogTrace_Message_3_{id}_{code}", $"id_{Id.NewString()}",$"code_{Id.NewString()}").LogTrace();
        }

        /// <summary>
        /// 测试写跟踪日志 - 多次调用Message方法设置日志消息
        /// </summary>
        [Fact]
        public void Test_LogTrace_Message_4()
        {
            _log.Message("Test_LogTrace_Message_4_{id}_{code}_", $"id_{Id.NewString()}", $"code_{Id.NewString()}")
                .Message("{name}_{note}", $"name_{Id.NewString()}", $"note_{Id.NewString()}")
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
                .Message("Test_LogTrace_Message_9_{id}", $"id_{Id.NewString()}")
                .State(new Product { Code = "a", Name = "b", Price = 123 })
                .Property("Age","18")
                .Property("Description","hello")
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
    }
}
