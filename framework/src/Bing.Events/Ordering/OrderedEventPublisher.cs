using System.Data.Common;
using Bing.Aspects;
using Bing.Data.Events.Ordering;
using Bing.Events.Cap;
using Bing.Events.Messages;
using DotNetCore.CAP;

namespace Bing.Events.Ordering;

/// <summary>
/// 在业务事务中分配序号并写入 CAP 出箱。
/// </summary>
[IgnoreAspect]
public sealed class OrderedEventPublisher
{
    /// <summary>
    /// 连续序号存储。
    /// </summary>
    private readonly IOrderedEventStore _store;
    /// <summary>
    /// 当前作用域的 CAP 发布器。
    /// </summary>
    private readonly ICapPublisher _publisher;

    /// <summary>
    /// 初始化一个 <see cref="OrderedEventPublisher" /> 类型的实例。
    /// </summary>
    public OrderedEventPublisher(IOrderedEventStore store, ICapPublisher publisher)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <summary>
    /// 分配序号并发布事件。
    /// </summary>
    /// <param name="transaction">已经接入 CAP 的业务事务。</param>
    /// <param name="event">尚未分配序号的事件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>事件发布失败必须回滚整笔事务；事务失败后丢弃消息实例，不能直接沿用已分配序号。</remarks>
    public async Task PublishAsync(DbTransaction transaction, MessageEvent @event,
        CancellationToken cancellationToken = default)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));
        if (string.IsNullOrWhiteSpace(@event.Name))
            throw new ArgumentException("消息名称不能为空。", nameof(@event));
        if (@event.Sequence.HasValue)
            throw new ArgumentException("消息已经分配序号，不能再次分配。", nameof(@event));
        if (!IsSameTransaction(_publisher.Transaction.Value?.DbTransaction, transaction))
            throw new InvalidOperationException("有序消息必须在同一 CAP 业务事务内发布。");
        var stream = new OrderedEventStream(@event.TenantId, @event.Stream, @event.EntityKey);
        cancellationToken.ThrowIfCancellationRequested();
        @event.Sequence = await _store.AllocateSequenceAsync(transaction, stream, cancellationToken);
        var headers = new Dictionary<string, string>();
        ClusterEventHeaders.Write(headers, @event);
        if (!string.IsNullOrWhiteSpace(@event.Callback))
            headers[DotNetCore.CAP.Messages.Headers.CallbackName] = @event.Callback;
        await _publisher.PublishAsync(@event.Name, @event.Data, headers, cancellationToken);
    }

    /// <summary>
    /// 判断 CAP 事务是否包装了调用方提供的数据库事务。
    /// </summary>
    private static bool IsSameTransaction(object capTransaction, DbTransaction transaction)
    {
        if (ReferenceEquals(capTransaction, transaction))
            return true;
        if (capTransaction == null)
            return false;

        try
        {
            var method = capTransaction.GetType().GetMethod("GetDbTransaction", Type.EmptyTypes);
            if (method != null && typeof(DbTransaction).IsAssignableFrom(method.ReturnType) &&
                ReferenceEquals(method.Invoke(capTransaction, null), transaction))
                return true;

            var efTransactionInterface = capTransaction.GetType().GetInterfaces()
                .FirstOrDefault(type => string.Equals(type.FullName,
                    "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction",
                    StringComparison.Ordinal));
            if (efTransactionInterface == null)
                return false;
            var extensionType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions"))
                .FirstOrDefault(type => type != null);
            var extensionMethod = extensionType?.GetMethod("GetDbTransaction",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                binder: null, new[] { efTransactionInterface }, modifiers: null);
            return extensionMethod != null &&
                   ReferenceEquals(extensionMethod.Invoke(null, new[] { capTransaction }), transaction);
        }
        catch (System.Reflection.TargetInvocationException)
        {
            return false;
        }
    }
}
