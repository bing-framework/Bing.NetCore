﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Transaction;
using Bing.Events.Messages;
using Bing.Logs;
using Bing.Tracing;
using Bing.Utils.Json;
using DotNetCore.CAP;

namespace Bing.Events.Cap;

/// <summary>
/// Cap消息事件总线
/// </summary>
public class MessageEventBus : IMessageEventBus
{
    /// <summary>
    /// 事件发布器
    /// </summary>
    public ICapPublisher Publisher { get; set; }

    /// <summary>
    /// 事务操作管理器
    /// </summary>
    public ITransactionActionManager TransactionActionManager { get; set; }

    /// <summary>
    /// 日志操作
    /// </summary>
    protected ILog Log { get; set; }

    /// <summary>
    /// 初始化一个<see cref="MessageEventBus"/>类型的实例
    /// </summary>
    /// <param name="publisher">事件发布器</param>
    /// <param name="transactionActionManager">事务操作管理器</param>
    /// <param name="log">日志操作</param>
    public MessageEventBus(ICapPublisher publisher,
        ITransactionActionManager transactionActionManager,
        ILog log)
    {
        Publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        TransactionActionManager = transactionActionManager ?? throw new ArgumentNullException(nameof(transactionActionManager));
        Log = log;
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="event">事件</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IMessageEvent =>
        PublishAsync(@event.Name, @event.Data, @event.Callback, @event.Send, cancellationToken);

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="name">消息名称</param>
    /// <param name="data">事件数据</param>
    /// <param name="callback">回调名称</param>
    /// <param name="send">是否立即发送消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task PublishAsync(string name, object data, string callback = null, bool send = false, CancellationToken cancellationToken = default)
    {
        var headers = new Dictionary<string, string> { { DotNetCore.CAP.Messages.Headers.CallbackName, callback } };
        InitTraceIdContext(headers);
        if (send)
        {
            await InternalPublishAsync(name, data, headers, callback, cancellationToken);
            return;
        }
        TransactionActionManager.Register(async transaction =>
        {
            await InternalPublishAsync(name, data, headers, callback, cancellationToken);
        });
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="name">消息名称</param>
    /// <param name="data">事件数据</param>
    /// <param name="headers">数据投</param>
    /// <param name="callback">回调名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task InternalPublishAsync(string name, object data, IDictionary<string, string> headers, string callback, CancellationToken cancellationToken = default)
    {
        await Publisher.PublishAsync(name, data, headers, cancellationToken);
        WriteLog(name, data, callback);
    }

    /// <summary>
    /// 写日志
    /// </summary>
    /// <param name="name">消息名称</param>
    /// <param name="data">事件数据</param>
    /// <param name="callback">回调名称</param>
    private void WriteLog(string name, object data, string callback)
    {
        var log = Log;
        if (log.IsDebugEnabled == false)
            return;
        log.Tag(name)
            .Caption($"Cap已发送事件-{name}")
            .Content($"消息名称: {name}")
            .AddExtraProperty("event_data", data.ToJson())
            .Trace();
    }

    /// <summary>
    /// 初始化跟踪标识上下文
    /// </summary>
    private static void InitTraceIdContext(IDictionary<string, string> headers)
    {
        if (TraceIdContext.Current == null)
            TraceIdContext.Current = new TraceIdContext(string.Empty);
        if (!headers.ContainsKey(Headers.TraceId))
            headers[Headers.TraceId] = TraceIdContext.Current.TraceId;
    }
}