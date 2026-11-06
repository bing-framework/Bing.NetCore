using System.Runtime.CompilerServices;

namespace Bing.Domain.Entities.Events;

/// <summary>
/// 在一次工作单元保存期间分轮分发领域事件。
/// </summary>
public sealed class DomainEventDispatchSession
{
    /// <summary>
    /// 单次保存允许的最大事件轮数。
    /// </summary>
    private const int MaxRounds = 32;

    /// <summary>
    /// 获取本次保存关联的事件源。
    /// </summary>
    private readonly Func<IEnumerable<IHasDomainEvents>> _getSources;

    /// <summary>
    /// 领域事件分发器。
    /// </summary>
    private readonly IDomainEventDispatcher _dispatcher;

    /// <summary>
    /// 保存每轮领域事件分发前执行的回调。
    /// </summary>
    private readonly Func<Task> _beforeRound;

    /// <summary>
    /// 每个事件源在分发期间已读取的事件序列。
    /// </summary>
    private readonly Dictionary<IHasDomainEvents, List<DomainEvent>> _seenEvents = new(new SourceReferenceComparer());

    /// <summary>
    /// 已分发且等待保存成功后移除的事件。
    /// </summary>
    private readonly List<(IHasDomainEvents Source, DomainEvent Event)> _processedEvents = new();

    /// <summary>
    /// 初始化一个 <see cref="DomainEventDispatchSession" /> 类型的实例。
    /// </summary>
    /// <param name="getSources">获取当前事件源的回调。</param>
    /// <param name="dispatcher">领域事件分发器。</param>
    public DomainEventDispatchSession(Func<IEnumerable<IHasDomainEvents>> getSources,
        IDomainEventDispatcher dispatcher)
    {
        _getSources = getSources ?? throw new ArgumentNullException(nameof(getSources));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// 初始化一个 <see cref="DomainEventDispatchSession" /> 类型的实例。
    /// </summary>
    /// <param name="getSources">用于获取待分发领域事件来源的委托。</param>
    /// <param name="dispatcher">用于分发相应操作或事件的分发器。</param>
    /// <param name="beforeRound">每轮读取领域事件前执行的异步回调。</param>
    /// <remarks>创建在每轮事件读取前准备数据库生成键的会话；原构造入口行为不变。</remarks>
    public DomainEventDispatchSession(Func<IEnumerable<IHasDomainEvents>> getSources,
        IDomainEventDispatcher dispatcher, Func<Task> beforeRound) : this(getSources, dispatcher)
    {
        _beforeRound = beforeRound ?? throw new ArgumentNullException(nameof(beforeRound));
    }

    /// <summary>
    /// 分发当前事件以及处理器产生的后续事件。
    /// </summary>
    public async Task DispatchAsync()
    {
        for (var round = 0; round < MaxRounds; round++)
        {
            if (_beforeRound != null) await _beforeRound().ConfigureAwait(false);
            var pending = GetPendingEvents();
            if (pending.Count == 0)
                return;

            foreach (var item in pending)
            {
                await _dispatcher.DispatchAsync(item.Event);
                _processedEvents.Add(item);
            }
        }

        if (GetPendingEvents().Count > 0)
            throw new InvalidOperationException($"领域事件超过 {MaxRounds} 轮，可能存在循环产生事件。");
    }

    /// <summary>
    /// 保存成功后移除本次已分发的事件。
    /// </summary>
    public void Complete()
    {
        foreach (var item in _processedEvents)
            item.Source.RemoveDomainEvent(item.Event);
        _processedEvents.Clear();
    }

    /// <summary>
    /// 清理已提交事件并保留事务后续失败。
    /// </summary>
    /// <param name="originalException">数据库提交后的原始异常。</param>
    /// <remarks>清理失败附加到原始异常，不替换提交或资源释放的错误。</remarks>
    public void CompleteAfterCommitFailure(Exception originalException)
    {
        if (originalException == null)
            throw new ArgumentNullException(nameof(originalException));
        try
        {
            Complete();
        }
        catch (Exception cleanupException)
        {
            originalException.Data["Bing.DomainEventCleanupException"] = cleanupException;
        }
    }

    /// <summary>
    /// 收集尚未处理的领域事件。
    /// </summary>
    /// <returns>来源对象及其新领域事件组成的列表。</returns>
    /// <remarks>
    /// 同一来源对象仅处理一次；事件源在分发期间必须保留已读取事件的顺序和实例，并仅追加新事件。
    /// </remarks>
    private List<(IHasDomainEvents Source, DomainEvent Event)> GetPendingEvents()
    {
        var pending = new List<(IHasDomainEvents Source, DomainEvent Event)>();
        var seen = new HashSet<IHasDomainEvents>(new SourceReferenceComparer());
        foreach (var source in _getSources())
        {
            if (source == null || !seen.Add(source))
                continue;
            var events = source.GetDomainEvents()?.ToArray() ?? Array.Empty<DomainEvent>();
            if (!_seenEvents.TryGetValue(source, out var seenEvents))
            {
                seenEvents = new List<DomainEvent>();
                _seenEvents.Add(source, seenEvents);
            }
            if (events.Length < seenEvents.Count)
                throw new InvalidOperationException("领域事件分发期间不能移除或清空待处理事件。");
            for (var index = 0; index < seenEvents.Count; index++)
            {
                if (!ReferenceEquals(events[index], seenEvents[index]))
                    throw new InvalidOperationException("领域事件分发期间不能替换已读取的事件。");
            }
            for (var index = seenEvents.Count; index < events.Length; index++)
            {
                pending.Add((source, events[index]));
                seenEvents.Add(events[index]);
            }
        }
        return pending;
    }

    /// <summary>
    /// 按引用身份比较领域事件源。
    /// </summary>
    private sealed class SourceReferenceComparer : IEqualityComparer<IHasDomainEvents>
    {
        /// <inheritdoc />
        public bool Equals(IHasDomainEvents x, IHasDomainEvents y) => ReferenceEquals(x, y);

        /// <inheritdoc />
        public int GetHashCode(IHasDomainEvents obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
