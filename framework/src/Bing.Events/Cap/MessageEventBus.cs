using Bing.Data.Transaction;
using Bing.Events.Messages;
using Bing.Tracing;
using DotNetCore.CAP;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.Events.Cap;

/// <summary>
/// Cap消息事件总线
/// </summary>
public class MessageEventBus : IMessageEventBus
{
    /// <summary>
    /// 获取或设置事件发布器
    /// </summary>
    public ICapPublisher Publisher { get; set; }

    /// <summary>
    /// 获取或设置事务操作管理器
    /// </summary>
    public ITransactionActionManager TransactionActionManager { get; set; }

    /// <summary>
    /// 获取日志
    /// </summary>
    protected ILogger<MessageEventBus> Logger { get; }

    /// <summary>
    /// 初始化一个 <see cref="MessageEventBus" /> 类型的实例。
    /// </summary>
    /// <param name="publisher">事件发布器</param>
    /// <param name="transactionActionManager">事务操作管理器</param>
    /// <param name="logger">日志</param>
    public MessageEventBus(ICapPublisher publisher,
        ITransactionActionManager transactionActionManager,
        ILogger<MessageEventBus> logger)
    {
        Publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        TransactionActionManager = transactionActionManager ?? throw new ArgumentNullException(nameof(transactionActionManager));
        Logger = logger ?? NullLogger<MessageEventBus>.Instance;
    }

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IMessageEvent
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));
        return PublishCoreAsync(@event.Name, @event.Data, @event.Callback, @event.Send,
            @event as IClusterEventMetadata, cancellationToken);
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="name">消息名称</param>
    /// <param name="data">事件数据</param>
    /// <param name="callback">回调名称</param>
    /// <param name="send">是否立即发送消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task PublishAsync(string name, object data, string callback = null, bool send = false, CancellationToken cancellationToken = default) =>
        PublishCoreAsync(name, data, callback, send, null, cancellationToken);

    /// <summary>
    /// 发布带有可选业务元数据的事件。
    /// </summary>
    private async Task PublishCoreAsync(string name, object data, string callback, bool send,
        IClusterEventMetadata metadata, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string> { { DotNetCore.CAP.Messages.Headers.CallbackName, callback } };
        InitTraceIdContext(headers);
        if (metadata != null)
            ClusterEventHeaders.Write(headers, metadata);
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
    /// <param name="headers">数据头</param>
    /// <param name="callback">回调名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task InternalPublishAsync(string name, object data, IDictionary<string, string> headers, string callback, CancellationToken cancellationToken = default)
    {
        WriteLog(name, headers, callback);
        await Publisher.PublishAsync(name, data, headers, cancellationToken);
        WriteLog(name, headers, callback, true);
    }

    /// <summary>
    /// 写日志
    /// </summary>
    /// <param name="name">消息名称</param>
    /// <param name="headers">数据头</param>
    /// <param name="callback">回调名称</param>
    /// <param name="isEnd">是否结束</param>
    private void WriteLog(string name, IDictionary<string, string> headers, string callback, bool isEnd = false)
    {
        if (Logger.IsEnabled(LogLevel.Trace) == false)
            return;
        // Trace 日志只记录定位所需的低敏元数据，不序列化事件正文或完整请求头。
        Logger.LogTrace("Cap发送事件{Phase}。EventName={EventName}, Callback={Callback}, HeaderCount={HeaderCount}",
            isEnd ? "结束" : "开始", name, callback, headers?.Count ?? 0);
    }

    /// <summary>
    /// 初始化跟踪标识上下文
    /// </summary>
    /// <param name="headers">待补充跟踪标识的消息头集合。</param>
    private static void InitTraceIdContext(IDictionary<string, string> headers)
    {
        if (TraceIdContext.Current == null)
            TraceIdContext.Current = new TraceIdContext(string.Empty);
        if (!headers.ContainsKey(Headers.TraceId))
            headers[Headers.TraceId] = TraceIdContext.Current.TraceId;
    }
}
