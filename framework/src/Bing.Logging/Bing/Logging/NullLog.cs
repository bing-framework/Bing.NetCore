﻿using System;
using Microsoft.Extensions.Logging;

namespace Bing.Logging
{
    /// <summary>
    /// 空日志操作
    /// </summary>
    /// <typeparam name="TCategoryName">日志类别</typeparam>
    public class NullLog<TCategoryName> : ILog<TCategoryName>
    {
        /// <summary>
        /// 空日志操作实例
        /// </summary>
        public static readonly ILog<TCategoryName> Instance = new NullLog<TCategoryName>();

        /// <summary>
        /// 设置日志事件标识
        /// </summary>
        /// <param name="eventId">日志事件标识</param>
        public ILog<TCategoryName> EventId(EventId eventId) => this;

        /// <summary>
        /// 设置异常
        /// </summary>
        /// <param name="exception">异常</param>
        public ILog<TCategoryName> Exception(Exception exception) => this;

        /// <summary>
        /// 设置自定义扩展属性
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="propertyValue">属性值</param>
        public ILog<TCategoryName> Property(string propertyName, string propertyValue) => this;

        /// <summary>
        /// 设置标签
        /// </summary>
        /// <param name="tags">标签</param>
        public ILog<TCategoryName> Tags(params string[] tags) => this;

        /// <summary>
        /// 设置日志状态对象
        /// </summary>
        /// <param name="state">状态对象</param>
        public ILog<TCategoryName> State(object state) => this;

        /// <summary>
        /// 设置日志消息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="args">日志消息参数</param>
        public ILog<TCategoryName> Message(string message, params object[] args) => this;

        /// <summary>
        /// 是否启用
        /// </summary>
        /// <param name="logLevel">日志级别</param>
        /// <returns>true=启用；false=禁用</returns>
        public bool IsEnabled(LogLevel logLevel) => false;

        /// <summary>
        /// 开启日志范围
        /// </summary>
        /// <typeparam name="TState">日志状态类型</typeparam>
        /// <param name="state">日志状态</param>
        public IDisposable BeginScope<TState>(TState state) => new DisposeAction(() => { });

        /// <summary>
        /// 写跟踪日志
        /// </summary>
        public ILog<TCategoryName> LogTrace() => this;

        /// <summary>
        /// 写调试日志
        /// </summary>
        public ILog<TCategoryName> LogDebug() => this;

        /// <summary>
        /// 写信息日志
        /// </summary>
        public ILog<TCategoryName> LogInformation() => this;

        /// <summary>
        /// 写警告日志
        /// </summary>
        public ILog<TCategoryName> LogWarning() => this;

        /// <summary>
        /// 写错误日志
        /// </summary>
        public ILog<TCategoryName> LogError() => this;

        /// <summary>
        /// 写致命日志
        /// </summary>
        public ILog<TCategoryName> LogCritical() => this;
    }
}