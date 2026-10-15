using System.Collections.ObjectModel;

namespace Bing.Events.Ordering;

/// <summary>
/// 有序消费者指标告警规则的类型。
/// </summary>
public enum BingOrderedEventAlertKind
{
    /// <summary>
    /// 待处理事件数达到阈值。
    /// </summary>
    PendingEvents = 0,

    /// <summary>
    /// 最老待处理事件年龄达到阈值。
    /// </summary>
    OldestPendingEventAge = 1,

    /// <summary>
    /// 扫描失败累计值在本次采样中增加。
    /// </summary>
    ScanFailures = 2
}

/// <summary>
/// 描述一个有序消费者指标告警规则。
/// </summary>
public sealed class BingOrderedEventAlertRule
{
    /// <summary>
    /// 表示默认告警冷却时间。
    /// </summary>
    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(1);
    /// <summary>
    /// 初始化一个 <see cref="BingOrderedEventAlertRule" /> 类型的实例。
    /// </summary>

    private BingOrderedEventAlertRule(string name, BingOrderedEventAlertKind kind, long threshold,
        TimeSpan? ageThreshold, TimeSpan cooldown)
    {
        ValidateName(name);
        if (!Enum.IsDefined(typeof(BingOrderedEventAlertKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (threshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        if (ageThreshold.HasValue && ageThreshold.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ageThreshold));
        if (cooldown < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cooldown));
        if (kind == BingOrderedEventAlertKind.OldestPendingEventAge && !ageThreshold.HasValue)
            throw new ArgumentException("年龄告警规则必须提供年龄阈值。", nameof(ageThreshold));
        if (kind != BingOrderedEventAlertKind.OldestPendingEventAge && ageThreshold.HasValue)
            throw new ArgumentException("只有年龄告警规则可以提供年龄阈值。", nameof(ageThreshold));
        if (kind == BingOrderedEventAlertKind.ScanFailures && threshold != 1)
            throw new ArgumentException("扫描失败告警规则的阈值固定为一次采样内的增长。", nameof(threshold));

        Name = name;
        Kind = kind;
        Threshold = threshold;
        AgeThreshold = ageThreshold;
        Cooldown = cooldown;
    }

    /// <summary>
    /// 创建待处理事件数告警规则。
    /// </summary>
    /// <param name="name">规则名称。</param>
    /// <param name="threshold">触发告警的待处理事件数。</param>
    /// <returns>新告警规则。</returns>
    public static BingOrderedEventAlertRule PendingEvents(string name, long threshold) =>
        PendingEvents(name, threshold, DefaultCooldown);

    /// <summary>
    /// 创建待处理事件数告警规则。
    /// </summary>
    /// <param name="name">规则名称。</param>
    /// <param name="threshold">触发告警的待处理事件数。</param>
    /// <param name="cooldown">同一消费者持续触发时的告警冷却时间。</param>
    /// <returns>新告警规则。</returns>
    public static BingOrderedEventAlertRule PendingEvents(string name, long threshold, TimeSpan cooldown) =>
        new BingOrderedEventAlertRule(name, BingOrderedEventAlertKind.PendingEvents, threshold, null, cooldown);

    /// <summary>
    /// 创建最老待处理事件年龄告警规则。
    /// </summary>
    /// <param name="name">规则名称。</param>
    /// <param name="threshold">触发告警的最老事件年龄。</param>
    /// <returns>新告警规则。</returns>
    public static BingOrderedEventAlertRule OldestPendingEventAge(string name, TimeSpan threshold) =>
        OldestPendingEventAge(name, threshold, DefaultCooldown);

    /// <summary>
    /// 创建最老待处理事件年龄告警规则。
    /// </summary>
    /// <param name="name">规则名称。</param>
    /// <param name="threshold">触发告警的最老事件年龄。</param>
    /// <param name="cooldown">同一消费者持续触发时的告警冷却时间。</param>
    /// <returns>新告警规则。</returns>
    public static BingOrderedEventAlertRule OldestPendingEventAge(string name, TimeSpan threshold,
        TimeSpan cooldown) =>
        new BingOrderedEventAlertRule(name, BingOrderedEventAlertKind.OldestPendingEventAge, 1, threshold, cooldown);

    /// <summary>
    /// 创建扫描失败增量告警规则。
    /// </summary>
    /// <param name="name">规则名称。</param>
    /// <returns>新告警规则。</returns>
    public static BingOrderedEventAlertRule ScanFailures(string name) =>
        ScanFailures(name, DefaultCooldown);

    /// <summary>
    /// 创建扫描失败增量告警规则。
    /// </summary>
    /// <param name="name">规则名称。</param>
    /// <param name="cooldown">同一消费者连续出现扫描失败时的告警冷却时间。</param>
    /// <returns>新告警规则。</returns>
    public static BingOrderedEventAlertRule ScanFailures(string name, TimeSpan cooldown) =>
        new BingOrderedEventAlertRule(name, BingOrderedEventAlertKind.ScanFailures, 1, null, cooldown);

    /// <summary>
    /// 获取规则名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取规则类型。
    /// </summary>
    public BingOrderedEventAlertKind Kind { get; }

    /// <summary>
    /// 获取数量阈值；年龄规则固定为一。
    /// </summary>
    public long Threshold { get; }

    /// <summary>
    /// 获取年龄阈值；非年龄规则为空。
    /// </summary>
    public TimeSpan? AgeThreshold { get; }

    /// <summary>
    /// 获取告警冷却时间。
    /// </summary>
    public TimeSpan Cooldown { get; }

    /// <summary>
    /// 校验告警规则名称。
    /// </summary>
    /// <param name="name">规则名称。</param>
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("告警规则名称不能为空。", nameof(name));
        if (name.Length > 100)
            throw new ArgumentException("告警规则名称不能超过 100 个字符。", nameof(name));
        foreach (var character in name)
        {
            if (char.IsControl(character))
                throw new ArgumentException("告警规则名称不能包含控制字符。", nameof(name));
        }
    }
}

/// <summary>
/// 表示一次已触发的有序消费者指标告警。
/// </summary>
public sealed class BingOrderedEventAlert
{
    /// <summary>
    /// 初始化一个 <see cref="BingOrderedEventAlert" /> 类型的实例。
    /// </summary>
    internal BingOrderedEventAlert(string consumerName, BingOrderedEventAlertRule rule,
        long currentValue, long threshold, DateTimeOffset occurredAt)
    {
        ConsumerName = consumerName;
        RuleName = rule.Name;
        Kind = rule.Kind;
        CurrentValue = currentValue;
        Threshold = threshold;
        OccurredAt = occurredAt;
    }

    /// <summary>
    /// 获取逻辑消费者名称。
    /// </summary>
    public string ConsumerName { get; }

    /// <summary>
    /// 获取告警规则名称。
    /// </summary>
    public string RuleName { get; }

    /// <summary>
    /// 获取告警规则类型。
    /// </summary>
    public BingOrderedEventAlertKind Kind { get; }

    /// <summary>
    /// 获取触发时的指标值。
    /// </summary>
    public long CurrentValue { get; }

    /// <summary>
    /// 获取触发阈值。
    /// </summary>
    public long Threshold { get; }

    /// <summary>
    /// 获取告警产生时间。
    /// </summary>
    public DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// 根据有序消费者指标快照计算告警并执行进程内冷却。
/// </summary>
public interface IOrderedEventMetricsAlertEvaluator
{
    /// <summary>
    /// 评估一批指标快照。
    /// </summary>
    /// <param name="snapshots">当前指标快照。</param>
    /// <param name="sampledAt">本次采样时间。</param>
    /// <returns>本次未被冷却抑制的告警集合。</returns>
    IReadOnlyList<BingOrderedEventAlert> Evaluate(
        IReadOnlyList<BingOrderedEventMetricsSnapshot> snapshots, DateTimeOffset sampledAt);
}

/// <summary>
/// 在进程内根据指标快照执行有限告警判定和冷却。
/// </summary>
/// <remarks>
/// 实例不发送通知、不写外部状态，也不提供跨节点去重。需要集群级告警时，应由宿主或外部监控系统统一聚合。
/// </remarks>
public sealed class BingOrderedEventMetricsAlertEvaluator : IOrderedEventMetricsAlertEvaluator
{
    /// <summary>
    /// 保存单条规则的告警变化与冷却状态。
    /// </summary>
    private sealed class AlertState
    {
        /// <summary>
        /// 保存上一次观察到的扫描失败累计值。
        /// </summary>
        public long? LastScanFailureCount;

        /// <summary>
        /// 保存上一次发布告警的时间。
        /// </summary>
        public DateTimeOffset? LastPublishedAt;
    }

    /// <summary>
    /// 保存评估器使用的只读规则集合。
    /// </summary>
    private readonly IReadOnlyList<BingOrderedEventAlertRule> _rules;

    /// <summary>
    /// 保存消费者和规则对应的进程内状态。
    /// </summary>
    private readonly Dictionary<(string ConsumerName, string RuleName), AlertState> _states =
        new Dictionary<(string ConsumerName, string RuleName), AlertState>();

    /// <summary>
    /// 保护告警状态的并发访问。
    /// </summary>
    private readonly object _gate = new object();

    /// <summary>
    /// 初始化一个 <see cref="BingOrderedEventMetricsAlertEvaluator" /> 类型的实例。
    /// </summary>
    /// <param name="rules">告警规则集合。</param>
    public BingOrderedEventMetricsAlertEvaluator(IEnumerable<BingOrderedEventAlertRule> rules)
    {
        if (rules == null)
            throw new ArgumentNullException(nameof(rules));

        var source = rules.ToArray();
        if (source.Any(rule => rule == null))
            throw new ArgumentException("告警规则集合不能包含 null。", nameof(rules));
        var duplicate = source.GroupBy(rule => rule.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            throw new ArgumentException($"告警规则名称重复：{duplicate.Key}。", nameof(rules));
        _rules = new ReadOnlyCollection<BingOrderedEventAlertRule>(source);
    }

    /// <inheritdoc />
    public IReadOnlyList<BingOrderedEventAlert> Evaluate(
        IReadOnlyList<BingOrderedEventMetricsSnapshot> snapshots, DateTimeOffset sampledAt)
    {
        if (snapshots == null)
            throw new ArgumentNullException(nameof(snapshots));

        var ordered = snapshots.ToArray();
        var duplicate = ordered.Where(snapshot => snapshot != null)
            .GroupBy(snapshot => snapshot.ConsumerName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            throw new ArgumentException($"指标快照消费者名称重复：{duplicate.Key}。", nameof(snapshots));
        foreach (var snapshot in ordered)
        {
            if (snapshot == null)
                throw new ArgumentException("指标快照集合不能包含 null。", nameof(snapshots));
            OrderedEventConsumerName.Validate(snapshot.ConsumerName, nameof(snapshots));
            ValidateSnapshotValues(snapshot);
        }

        var result = new List<BingOrderedEventAlert>();
        var activeConsumers = new HashSet<string>(ordered.Select(snapshot => snapshot.ConsumerName),
            StringComparer.Ordinal);
        lock (_gate)
        {
            foreach (var key in _states.Keys.Where(key => !activeConsumers.Contains(key.ConsumerName)).ToArray())
                _states.Remove(key);

            foreach (var snapshot in ordered)
            foreach (var rule in _rules)
            {
                var stateKey = (snapshot.ConsumerName, rule.Name);
                if (!_states.TryGetValue(stateKey, out var state))
                {
                    state = new AlertState();
                    _states[stateKey] = state;
                }

                var triggered = TryGetTriggeredValue(snapshot, rule, state, out var currentValue, out var threshold);
                if (rule.Kind == BingOrderedEventAlertKind.ScanFailures)
                {
                    if (triggered && CanPublish(state.LastPublishedAt, rule.Cooldown, sampledAt))
                    {
                        result.Add(new BingOrderedEventAlert(snapshot.ConsumerName, rule, currentValue,
                            threshold, sampledAt));
                        state.LastPublishedAt = sampledAt;
                    }
                }
                else if (triggered)
                {
                    if (CanPublish(state.LastPublishedAt, rule.Cooldown, sampledAt))
                    {
                        result.Add(new BingOrderedEventAlert(snapshot.ConsumerName, rule, currentValue,
                            threshold, sampledAt));
                        state.LastPublishedAt = sampledAt;
                    }
                }
                else
                    state.LastPublishedAt = null;
            }
        }
        return new ReadOnlyCollection<BingOrderedEventAlert>(result);
    }

    /// <summary>
    /// 获取告警规则对应的当前值和阈值。
    /// </summary>
    /// <param name="snapshot">指标快照。</param>
    /// <param name="rule">告警规则。</param>
    /// <param name="state">规则运行状态。</param>
    /// <param name="currentValue">当前指标值。</param>
    /// <param name="threshold">触发阈值。</param>
    /// <returns>指标达到阈值时返回 <see langword="true"/>。</returns>
    private static bool TryGetTriggeredValue(BingOrderedEventMetricsSnapshot snapshot,
        BingOrderedEventAlertRule rule, AlertState state, out long currentValue, out long threshold)
    {
        switch (rule.Kind)
        {
            case BingOrderedEventAlertKind.PendingEvents:
                currentValue = snapshot.PendingEvents;
                threshold = rule.Threshold;
                return currentValue >= threshold;
            case BingOrderedEventAlertKind.OldestPendingEventAge:
                currentValue = snapshot.OldestPendingEventAge.HasValue
                    ? ToMilliseconds(snapshot.OldestPendingEventAge.Value)
                    : 0;
                threshold = ToMilliseconds(rule.AgeThreshold.Value);
                return snapshot.OldestPendingEventAge.HasValue && currentValue >= threshold;
            case BingOrderedEventAlertKind.ScanFailures:
                currentValue = snapshot.ScanFailureCount;
                threshold = 1;
                var previous = state.LastScanFailureCount;
                state.LastScanFailureCount = currentValue;
                return previous.HasValue && currentValue > previous.Value;
            default:
                throw new InvalidOperationException("未知的有序事件告警类型。");
        }
    }

    /// <summary>
    /// 判断告警是否已超过冷却时间。
    /// </summary>
    /// <param name="lastPublishedAt">上次发布时间。</param>
    /// <param name="cooldown">冷却时长。</param>
    /// <param name="sampledAt">当前采样时间。</param>
    /// <returns>允许发布时返回 <see langword="true"/>。</returns>
    private static bool CanPublish(DateTimeOffset? lastPublishedAt, TimeSpan cooldown, DateTimeOffset sampledAt)
    {
        if (!lastPublishedAt.HasValue)
            return true;
        var elapsed = sampledAt - lastPublishedAt.Value;
        return elapsed < TimeSpan.Zero || elapsed >= cooldown;
    }

    /// <summary>
    /// 将时长转换为非负毫秒数。
    /// </summary>
    /// <param name="value">待转换时长。</param>
    /// <returns>向上取整后的毫秒数。</returns>
    private static long ToMilliseconds(TimeSpan value)
    {
        if (value.TotalMilliseconds >= long.MaxValue)
            return long.MaxValue;
        return Math.Max(0, (long)Math.Ceiling(value.TotalMilliseconds));
    }

    /// <summary>
    /// 校验指标快照中的数值范围。
    /// </summary>
    /// <param name="snapshot">待校验的指标快照。</param>
    private static void ValidateSnapshotValues(BingOrderedEventMetricsSnapshot snapshot)
    {
        if (snapshot.PendingStreams < 0 || snapshot.PendingEvents < 0 || snapshot.ProcessedCount < 0 ||
            snapshot.GapCount < 0 || snapshot.FailureCount < 0 || snapshot.RetryCount < 0 ||
            snapshot.InFlightCount < 0 || snapshot.LastProcessingDurationMilliseconds < 0 ||
            snapshot.ScanFailureCount < 0 ||
            snapshot.OldestPendingEventAge.HasValue && snapshot.OldestPendingEventAge.Value < TimeSpan.Zero)
            throw new ArgumentException("指标快照不能包含负数值。", nameof(snapshot));
    }
}
