using Bing.Events.Messages;

namespace Bing.Events.Cap;

/// <summary>
/// 在当前事务型业务工作单元中写入指定数据源的 CAP Outbox。
/// </summary>
public interface IUnitOfWorkMessagePublisher
{
    /// <summary>
    /// 立即将消息写入指定数据源当前未提交的业务事务。
    /// </summary>
    /// <remarks>
    /// 与旧 <see cref="IMessageEventBus"/> 不同，此入口忽略“延迟 action”语义：<c>Send</c> 必须为
    /// <see langword="false"/>，调用会立即写入当前 Uow 事务中的 Outbox；它不表示数据库已提交、消息已投递或消费者已完成。
    /// 旧入口的 <c>Send=false</c> 仍只登记 Uow 完成时运行的延迟 action。
    /// </remarks>
    /// <typeparam name="TMessage">消息事件类型。</typeparam>
    /// <param name="dataSourceKey">CAP Outbox 所属的 SQL 数据源键。</param>
    /// <param name="message">消息事件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Outbox 写入任务。</returns>
    Task PublishAsync<TMessage>(string dataSourceKey, TMessage message,
        CancellationToken cancellationToken = default) where TMessage : IMessageEvent;
}
