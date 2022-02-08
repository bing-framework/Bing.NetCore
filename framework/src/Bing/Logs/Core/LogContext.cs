using System;
using System.Diagnostics;
using System.Net;
using Bing.Collections;
using Bing.DependencyInjection;
using Bing.Logs.Abstractions;
using Bing.Logs.Internal;
using Bing.Tracing;

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
        /// 作用域字典
        /// </summary>
        private readonly ScopedDictionary _scopedDictionary;

        /// <summary>
        /// 日志标识
        /// </summary>
        public string LogId => $"{TraceId}-{GetInfo().GetOrderId()}";

        /// <summary>
        /// 跟踪号
        /// </summary>
        public string TraceId => $"{TraceIdContext.Current?.TraceId ?? GetInfo().TraceId}";

        ///// <summary>
        ///// 跟踪号
        ///// </summary>
        //public string TraceId
        //{
        //    get
        //    {
        //        TraceIdContext.Current ??= new TraceIdContext(string.Empty);
        //        return TraceIdContext.Current.TraceId;
        //    }
        //}

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

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="LogContext"/>类型的实例
        /// </summary>
        /// <param name="scopedDictionary">作用域字典</param>
        public LogContext(ScopedDictionary scopedDictionary)
        {
            _orderId = 0;
            _scopedDictionary = scopedDictionary;
        }

        #endregion

        /// <summary>
        /// 初始化日志标识
        /// </summary>
        public void InitLogId()
        {
            var key = "Bing.Logs.LogContext_orderId";
            _scopedDictionary[key] = _scopedDictionary.ContainsKey(key) ? ++_orderId : _orderId;
        }

        ///// <summary>
        ///// 获取日志上下文信息
        ///// </summary>
        //private LogContextInfo GetInfo()
        //{
        //    if (_info != null)
        //        return _info;
        //    var key = "Bing.Logs.LogContext";
        //    _info = _scopedDictionary.ContainsKey(key) ? _scopedDictionary[key] as LogContextInfo : null;
        //    if (_info != null)
        //        return _info;
        //    _info = CreateInfo();
        //    _scopedDictionary[key] = _info;
        //    return _info;
        //}

        /// <summary>
        /// 获取日志上下文信息
        /// </summary>
        private LogContextInfo GetInfo()
        {
            if (LogContextInfo.Current == null)
                LogContextInfo.Current = CreateInfo();
            return LogContextInfo.Current;
        }

        /// <summary>
        /// 创建日志上下文信息
        /// </summary>
        protected virtual LogContextInfo CreateInfo() =>
            new LogContextInfo
            {
                TraceId = GetTraceId(),
                Stopwatch = GetStopwatch(),
                Host = Dns.GetHostName(),
            };

        /// <summary>
        /// 获取跟踪号
        /// </summary>
        protected virtual string GetTraceId() => Guid.NewGuid().ToString();

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
