using Bing.Events.Ordering;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序消费者指标告警规则和进程内冷却。
/// </summary>
public sealed class OrderedEventAlertingTest
{
    /// <summary>
    /// 待处理事件超过阈值时应告警，并在冷却期间抑制重复告警。
    /// </summary>
    [Fact]
    public void Evaluate_WhenPendingEventsStayAboveThreshold_ShouldThrottleUntilCooldown()
    {
        var evaluator = new BingOrderedEventMetricsAlertEvaluator(new[]
        {
            BingOrderedEventAlertRule.PendingEvents("backlog", 10, TimeSpan.FromMinutes(1))
        });
        var first = DateTimeOffset.Parse("2026-01-01T00:00:00+00:00");

        evaluator.Evaluate(new[] { Snapshot(pendingEvents: 9) }, first).ShouldBeEmpty();
        var alert = evaluator.Evaluate(new[] { Snapshot(pendingEvents: 10) }, first.AddSeconds(1)).Single();
        alert.ConsumerName.ShouldBe("orders");
        alert.RuleName.ShouldBe("backlog");
        alert.CurrentValue.ShouldBe(10);
        alert.Threshold.ShouldBe(10);
        evaluator.Evaluate(new[] { Snapshot(pendingEvents: 11) }, first.AddSeconds(30)).ShouldBeEmpty();
        evaluator.Evaluate(new[] { Snapshot(pendingEvents: 11) }, first.AddMinutes(1).AddSeconds(1))
            .ShouldHaveSingleItem();
    }

    /// <summary>
    /// 指标恢复到阈值以下后再次超过阈值应重新告警。
    /// </summary>
    [Fact]
    public void Evaluate_WhenPendingEventsRecover_ShouldAllowNextAlert()
    {
        var evaluator = new BingOrderedEventMetricsAlertEvaluator(new[]
        {
            BingOrderedEventAlertRule.PendingEvents("backlog", 10, TimeSpan.FromHours(1))
        });
        var sampledAt = DateTimeOffset.UtcNow;

        evaluator.Evaluate(new[] { Snapshot(pendingEvents: 10) }, sampledAt).ShouldHaveSingleItem();
        evaluator.Evaluate(new[] { Snapshot(pendingEvents: 0) }, sampledAt.AddSeconds(1)).ShouldBeEmpty();
        evaluator.Evaluate(new[] { Snapshot(pendingEvents: 10) }, sampledAt.AddSeconds(2))
            .ShouldHaveSingleItem();
    }

    /// <summary>
    /// 没有年龄统计时不应触发年龄告警。
    /// </summary>
    [Fact]
    public void Evaluate_WhenOldestAgeIsMissing_ShouldNotAlert()
    {
        var evaluator = new BingOrderedEventMetricsAlertEvaluator(new[]
        {
            BingOrderedEventAlertRule.OldestPendingEventAge("age", TimeSpan.FromSeconds(5))
        });

        evaluator.Evaluate(new[] { Snapshot(oldestPendingEventAge: null) }, DateTimeOffset.UtcNow)
            .ShouldBeEmpty();
        var alert = evaluator.Evaluate(
            new[] { Snapshot(oldestPendingEventAge: TimeSpan.FromSeconds(5)) }, DateTimeOffset.UtcNow)
            .Single();
        alert.CurrentValue.ShouldBe(5000);
        alert.Threshold.ShouldBe(5000);
    }

    /// <summary>
    /// 扫描失败告警应依据累计值的增长而不是非零值重复触发。
    /// </summary>
    [Fact]
    public void Evaluate_WhenScanFailuresIncrease_ShouldUseDeltaAndCooldown()
    {
        var evaluator = new BingOrderedEventMetricsAlertEvaluator(new[]
        {
            BingOrderedEventAlertRule.ScanFailures("scan", TimeSpan.FromMinutes(1))
        });
        var first = DateTimeOffset.Parse("2026-01-01T00:00:00+00:00");

        evaluator.Evaluate(new[] { Snapshot(scanFailureCount: 3) }, first).ShouldBeEmpty();
        evaluator.Evaluate(new[] { Snapshot(scanFailureCount: 4) }, first.AddSeconds(1)).ShouldHaveSingleItem();
        evaluator.Evaluate(new[] { Snapshot(scanFailureCount: 4) }, first.AddSeconds(2)).ShouldBeEmpty();
        evaluator.Evaluate(new[] { Snapshot(scanFailureCount: 5) }, first.AddSeconds(30)).ShouldBeEmpty();
        evaluator.Evaluate(new[] { Snapshot(scanFailureCount: 6) }, first.AddMinutes(1).AddSeconds(1))
            .ShouldHaveSingleItem();
    }

    /// <summary>
    /// 不同消费者和规则应分别维护冷却状态。
    /// </summary>
    [Fact]
    public void Evaluate_WhenConsumersAndRulesDiffer_ShouldThrottleIndependently()
    {
        var evaluator = new BingOrderedEventMetricsAlertEvaluator(new[]
        {
            BingOrderedEventAlertRule.PendingEvents("backlog", 1, TimeSpan.FromMinutes(1)),
            BingOrderedEventAlertRule.OldestPendingEventAge("age", TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1))
        });
        var sampledAt = DateTimeOffset.UtcNow;

        var alerts = evaluator.Evaluate(new[]
        {
            Snapshot("orders", pendingEvents: 1),
            Snapshot("payments", oldestPendingEventAge: TimeSpan.FromSeconds(1))
        }, sampledAt);

        alerts.Select(alert => $"{alert.ConsumerName}:{alert.RuleName}")
            .ShouldBe(new[] { "orders:backlog", "payments:age" });
    }

    /// <summary>
    /// 非法规则和快照输入应在评估前失败。
    /// </summary>
    [Fact]
    public void ConstructorAndEvaluate_WhenInputIsInvalid_ShouldReject()
    {
        Should.Throw<ArgumentException>(() => BingOrderedEventAlertRule.PendingEvents("", 1));
        Should.Throw<ArgumentOutOfRangeException>(() => BingOrderedEventAlertRule.PendingEvents("backlog", 0));
        Should.Throw<ArgumentException>(() => new BingOrderedEventMetricsAlertEvaluator(new[]
        {
            BingOrderedEventAlertRule.PendingEvents("backlog", 1),
            BingOrderedEventAlertRule.PendingEvents("backlog", 2)
        }));

        var evaluator = new BingOrderedEventMetricsAlertEvaluator(new[]
        {
            BingOrderedEventAlertRule.PendingEvents("backlog", 1)
        });
        Should.Throw<ArgumentException>(() => evaluator.Evaluate(new[]
        {
            Snapshot(pendingEvents: -1)
        }, DateTimeOffset.UtcNow));
        Should.Throw<ArgumentException>(() => evaluator.Evaluate(new[]
        {
            Snapshot("orders"), Snapshot("orders")
        }, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// 空快照集合应清理已消失消费者的状态并返回空集合。
    /// </summary>
    [Fact]
    public void Evaluate_WhenSnapshotsAreEmpty_ShouldReturnEmptyAlerts()
    {
        var evaluator = new BingOrderedEventMetricsAlertEvaluator(new[]
        {
            BingOrderedEventAlertRule.PendingEvents("backlog", 1, TimeSpan.FromHours(1))
        });
        var sampledAt = DateTimeOffset.UtcNow;

        evaluator.Evaluate(new[] { Snapshot(pendingEvents: 1) }, sampledAt).ShouldHaveSingleItem();
        evaluator.Evaluate(Array.Empty<BingOrderedEventMetricsSnapshot>(), sampledAt.AddSeconds(1))
            .ShouldBeEmpty();
        evaluator.Evaluate(new[] { Snapshot(pendingEvents: 1) }, sampledAt.AddSeconds(2))
            .ShouldHaveSingleItem();
    }

    /// <summary>
    /// 创建指定指标值的测试快照。
    /// </summary>
    /// <param name="consumerName">消费者名称。</param>
    /// <param name="pendingEvents">待处理事件数。</param>
    /// <param name="oldestPendingEventAge">最早待处理事件的年龄。</param>
    /// <param name="scanFailureCount">扫描失败次数。</param>
    /// <returns>测试指标快照。</returns>
    private static BingOrderedEventMetricsSnapshot Snapshot(string consumerName = "orders",
        long pendingEvents = 0, TimeSpan? oldestPendingEventAge = null, long scanFailureCount = 0) =>
        new BingOrderedEventMetricsSnapshot(consumerName, 0, pendingEvents, 0, 0, 0, 0, 0, 0,
            oldestPendingEventAge, scanFailureCount);
}
