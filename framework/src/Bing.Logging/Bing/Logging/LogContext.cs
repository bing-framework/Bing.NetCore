using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Bing.Logging
{
    /// <summary>
    /// 日志上下文
    /// </summary>
    public class LogContext
    {
        /// <summary>
        /// 初始化一个<see cref="LogContext"/>类型的实例
        /// </summary>
        public LogContext() => Data = new Dictionary<string, object>();

        /// <summary>
        /// 计时器
        /// </summary>
        public Stopwatch Stopwatch { get; set; }

        /// <summary>
        /// 跟踪标识
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// 用户标识
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 应用程序
        /// </summary>
        public string Application { get; set; }

        /// <summary>
        /// 执行环境
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// IP
        /// </summary>
        public string Ip { get; set; }

        /// <summary>
        /// 主机
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// 浏览器
        /// </summary>
        public string Browser { get; set; }

        /// <summary>
        /// 请求地址
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 是否Web环境
        /// </summary>
        public bool IsWebEnv { get; set; }

        /// <summary>
        /// 标签列表
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// 扩展数据
        /// </summary>
        public IDictionary<string, object> Data { get; }

        /// <summary>
        /// 当前日志上下文
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static readonly AsyncLocal<LogContext> _current = new();

        /// <summary>
        /// 当前日志上下文
        /// </summary>
        public static LogContext Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }
    }
}
