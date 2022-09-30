using System;
using Microsoft.Extensions.Logging;

namespace Bing.Logging
{
    /// <summary>
    /// 日志操作
    /// </summary>
    /// <typeparam name="TCategoryName">日志类别</typeparam>
    public class Log<TCategoryName> : ILog<TCategoryName>
    {
        /// <summary>
        /// 日志操作
        /// </summary>
        private readonly ILog _log;

        /// <summary>
        /// 初始化一个<see cref="Log{TCategoryName}"/>类型的实例
        /// </summary>
        /// <param name="factory">日志操作工厂</param>
        public Log(ILogFactory factory)
        {
            if (factory is null)
                throw new ArgumentNullException(nameof(factory));
            _log = factory.CreateLog(typeof(TCategoryName));
        }

        /// <summary>
        /// 空日志操作实例
        /// </summary>
        public static ILog<TCategoryName> Null = NullLog<TCategoryName>.Instance;

        /// <summary>
        /// 设置日志事件标识
        /// </summary>
        /// <param name="eventId">日志事件标识</param>
        public virtual ILog EventId(EventId eventId) => _log.EventId(eventId);

        /// <summary>
        /// 设置异常
        /// </summary>
        /// <param name="exception">异常</param>
        public virtual ILog Exception(Exception exception) => _log.Exception(exception);

        /// <summary>
        /// 设置自定义扩展属性
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="propertyValue">属性值</param>
        public virtual ILog Property(string propertyName, string propertyValue) => _log.Property(propertyName, propertyValue);

        /// <summary>
        /// 设置扩展属性
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="propertyValue">属性值</param>
        public virtual ILog ExtraProperty(string propertyName, object propertyValue) => _log.ExtraProperty(propertyName, propertyValue);

        /// <summary>
        /// 设置标签
        /// </summary>
        /// <param name="tags">标签</param>
        public virtual ILog Tags(params string[] tags) => _log.Tags(tags);

        /// <summary>
        /// 设置日志状态对象
        /// </summary>
        /// <param name="state">状态对象</param>
        public virtual ILog State(object state) => _log.State(state);

        /// <summary>
        /// 设置日志消息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="args">日志消息参数</param>
        public virtual ILog Message(string message, params object[] args) => _log.Message(message, args);

        /// <summary>
        /// 是否启用
        /// </summary>
        /// <param name="logLevel">日志级别</param>
        /// <returns>true=启用；false=禁用</returns>
        public virtual bool IsEnabled(LogLevel logLevel) => _log.IsEnabled(logLevel);

        /// <summary>
        /// 开启日志范围
        /// </summary>
        /// <typeparam name="TState">日志状态类型</typeparam>
        /// <param name="state">日志状态</param>
        public virtual IDisposable BeginScope<TState>(TState state) => _log.BeginScope(state);

        /// <summary>
        /// 写跟踪日志
        /// </summary>
        public virtual ILog LogTrace() => _log.LogTrace();

        /// <summary>
        /// 写调试日志
        /// </summary>
        public virtual ILog LogDebug() => _log.LogDebug();

        /// <summary>
        /// 写信息日志
        /// </summary>
        public virtual ILog LogInformation() => _log.LogInformation();

        /// <summary>
        /// 写警告日志
        /// </summary>
        public virtual ILog LogWarning() => _log.LogWarning();

        /// <summary>
        /// 写错误日志
        /// </summary>
        public virtual ILog LogError() => _log.LogError();

        /// <summary>
        /// 写致命日志
        /// </summary>
        public virtual ILog LogCritical() => _log.LogCritical();
    }
}
