using System;
using System.Diagnostics;
using System.Net;
using Bing.Contexts;
using Bing.Helpers;
using Bing.Logs.Abstractions;
using Bing.Logs.Internal;

namespace Bing.Logs.Core
{
    /// <summary>
    /// 日志上下文
    /// </summary>
    public class LogContext : ILogContext
    {
        #region 属性

        /// <summary>
        /// 日志上下文信息
        /// </summary>
        private LogContextInfo _info;

        /// <summary>
        /// 序号
        /// </summary>
        private int _orderId;

        /// <summary>
        /// 上下文
        /// </summary>
        private IContext _context;

        /// <summary>
        /// 日志标识
        /// </summary>
        public string LogId => $"{GetInfo().TraceId}-{++_orderId}";

        /// <summary>
        /// 跟踪号
        /// </summary>
        public string TraceId => $"{GetInfo().TraceId}";

        /// <summary>
        /// 计时器
        /// </summary>
        public Stopwatch Stopwatch => GetInfo().Stopwatch;

        /// <summary>
        /// IP
        /// </summary>
        public string Ip => GetInfo().Ip;

        /// <summary>
        /// 主机
        /// </summary>
        public string Host => GetInfo().Host;

        /// <summary>
        /// 浏览器
        /// </summary>
        public string Browser => GetInfo().Browser;

        /// <summary>
        /// 请求地址
        /// </summary>
        public string Url => GetInfo().Url;

        /// <summary>
        /// 上下文
        /// </summary>
        public IContext Context => _context ??= ContextFactory.Create();

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="LogContext"/>类型的实例
        /// </summary>
        public LogContext() => _orderId = 0;

        #endregion

        /// <summary>
        /// 获取日志上下文信息
        /// </summary>
        private LogContextInfo GetInfo()
        {
            if (_info != null)
                return _info;
            var key = "Bing.Logs.LogContext";
            _info = Context.Get<LogContextInfo>(key);
            if (_info != null)
                return _info;
            _info = CreateInfo();
            Context.Add(key, _info);
            return _info;
        }

        /// <summary>
        /// 创建日志上下文信息
        /// </summary>
        protected virtual LogContextInfo CreateInfo() =>
            new LogContextInfo
            {
                TraceId = GetTraceId(),
                Stopwatch = GetStopwatch(),
                Ip = Web.IP,
                Host = Dns.GetHostName(),
                Browser = Web.Browser,
                Url = Web.Url,
            };

        /// <summary>
        /// 获取跟踪号
        /// </summary>
        protected string GetTraceId()
        {
            var traceId = Context.TraceId;
            return string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString() : traceId;
        }

        /// <summary>
        /// 获取计时器
        /// </summary>
        protected Stopwatch GetStopwatch()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            return stopwatch;
        }
    }
}
