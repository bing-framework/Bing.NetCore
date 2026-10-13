using Bing.Caching;
using EasyCaching.Core.Bus;
using EasyCaching.Core.Serialization;
using System.Text.Json;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证 Hybrid 失效投递器的重试、背压和 processing 状态恢复。
/// </summary>
public sealed class HybridInvalidationDeliveryTest
{
    /// <summary>
    /// 验证发布失败重试后入队并传播原始异常。
    /// </summary>
    [Fact]
    public void Publish_RetriesThenEnqueuesAndPropagatesOriginalFailure()
    {
        var bus = new TestBus(_ => new InvalidOperationException("bus failed"));
        var outbox = new TestOutbox { AllowTake = false };
        using var delivery = CreateDelivery(bus, outbox,
            new HybridInvalidationDeliveryOptions
            {
                MaxPublishAttempts = 2,
                MaxPendingMessages = 4,
                OutboxPollInterval = TimeSpan.FromMilliseconds(10)
            });

        var error = Assert.Throws<InvalidOperationException>(() =>
            delivery.Publish(new[] { "physical:key" }, 0));

        Assert.Equal("bus failed", error.Message);
        Assert.Equal(2, bus.PublishCount);
        Assert.Single(outbox.Enqueued);
    }

    /// <summary>
    /// 验证出站箱入队失败时传播发布异常。
    /// </summary>
    [Fact]
    public void Publish_WhenOutboxIsFull_PropagatesFailureWithoutSilentSuccess()
    {
        var bus = new TestBus(_ => new InvalidOperationException("bus failed"));
        var outbox = new TestOutbox { EnqueueResult = false, AllowTake = false };
        using var delivery = CreateDelivery(bus, outbox,
            new HybridInvalidationDeliveryOptions
            {
                MaxPublishAttempts = 1,
                MaxPendingMessages = 1,
                OutboxPollInterval = TimeSpan.FromMilliseconds(10)
            });

        var error = Assert.Throws<InvalidOperationException>(() =>
            delivery.Publish(new[] { "physical:key" }, 0));

        Assert.Equal("bus failed", error.Message);
        Assert.Equal(1, bus.PublishCount);
        Assert.Equal(1, outbox.EnqueueCalls);
    }

    /// <summary>
    /// 验证发布暂时失败后重试成功且不入队。
    /// </summary>
    [Fact]
    public async Task Publish_WhenTransientFailureRecovers_DoesNotEnqueue()
    {
        var bus = new TestBus(attempt => attempt == 1
            ? new InvalidOperationException("transient")
            : null);
        using var delivery = CreateDelivery(bus, outbox: null,
            new HybridInvalidationDeliveryOptions { MaxPublishAttempts = 2 });

        await delivery.PublishAsync(new[] { "physical:key" }, 0, CancellationToken.None);

        Assert.Equal(2, bus.PublishCount);
    }

    /// <summary>
    /// 验证调度前取消不会发布或入队。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenCanceledBeforeDispatch_DoesNotEnqueue()
    {
        var bus = new TestBus(_ => new InvalidOperationException("bus should not run"));
        var outbox = new TestOutbox { AllowTake = false };
        using var delivery = CreateDelivery(bus, outbox,
            new HybridInvalidationDeliveryOptions
            {
                MaxPublishAttempts = 2,
                MaxPendingMessages = 4
            });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            delivery.PublishAsync(new[] { "physical:key" }, 0, cancellation.Token));

        Assert.Equal(0, bus.PublishCount);
        Assert.Empty(outbox.Enqueued);
    }

    /// <summary>
    /// 验证未配置出站箱时快照返回空队列状态。
    /// </summary>
    [Fact]
    public void Snapshot_WithoutOutbox_ReturnsStableEmptyQueue()
    {
        var bus = new TestBus(_ => null);
        using var delivery = CreateDelivery(bus, outbox: null,
            new HybridInvalidationDeliveryOptions());

        var snapshot = delivery.GetSnapshot();

        Assert.Equal("topic", snapshot.Topic);
        Assert.False(snapshot.OutboxEnabled);
        Assert.Equal(0, snapshot.PendingMessages);
        Assert.Equal(0, snapshot.ProcessingMessages);
        Assert.Equal(0, snapshot.DeadLetterMessages);
        Assert.Null(snapshot.DeadLetterBytes);
        Assert.Null(snapshot.OldestPendingAge);
        Assert.Null(snapshot.OldestProcessingAge);
        Assert.Null(snapshot.OldestDeadLetterAge);
        Assert.Equal(0, snapshot.SuccessfulPublishCount);
        Assert.Equal(0, snapshot.FailedPublishAttemptCount);
        Assert.Equal(0, snapshot.EnqueuedMessageCount);
        Assert.Equal(0, snapshot.DeadLetteredMessageCount);
        Assert.Equal(0, snapshot.ExpiredDeadLetterCount);
        Assert.Null(snapshot.LastFailureUtc);
    }

    /// <summary>
    /// 验证死信投递次数设置必须成对配置。
    /// </summary>
    [Fact]
    public void Options_RequirePairedDeadLetterSettings()
    {
        Assert.Throws<ArgumentException>(() => CreateDelivery(new TestBus(_ => null), null,
            new HybridInvalidationDeliveryOptions
            {
                MaxDeliveryAttempts = 2
            }));
    }

    /// <summary>
    /// 验证启用死信保留期时必须提供死信出站箱。
    /// </summary>
    [Fact]
    public void Options_RequireDeadLetterWhenRetentionIsEnabled()
    {
        Assert.Throws<ArgumentException>(() => CreateDelivery(new TestBus(_ => null), null,
            new HybridInvalidationDeliveryOptions
            {
                DeadLetterRetention = TimeSpan.FromMinutes(1)
        }));
    }

    /// <summary>
    /// 验证启用死信字节上限时必须提供死信出站箱。
    /// </summary>
    [Fact]
    public void Options_RequireDeadLetterWhenByteBudgetIsEnabled()
    {
        Assert.Throws<ArgumentException>(() => CreateDelivery(new TestBus(_ => null), null,
            new HybridInvalidationDeliveryOptions
            {
                DeadLetterMaxBytes = 1024
            }));
    }

    /// <summary>
    /// 验证拒绝超出范围的死信字节上限。
    /// </summary>
    [Fact]
    public void Options_RejectInvalidDeadLetterByteBudget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDelivery(new TestBus(_ => null), null,
            new HybridInvalidationDeliveryOptions
            {
                DeadLetterMaxBytes = -1
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDelivery(new TestBus(_ => null), null,
            new HybridInvalidationDeliveryOptions
            {
                DeadLetterMaxBytes = 1_073_741_825
            }));
    }

    /// <summary>
    /// 验证快照报告发布失败计数、队列年龄和死信统计。
    /// </summary>
    [Fact]
    public async Task Snapshot_ReportsFailureCountersAndQueueAge()
    {
        var bus = new TestBus(_ => new InvalidOperationException("bus failed"));
        var outbox = new TestOutbox { AllowTake = false };
        using var delivery = CreateDelivery(bus, outbox,
            new HybridInvalidationDeliveryOptions
            {
                MaxPublishAttempts = 2,
                MaxPendingMessages = 4
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            delivery.PublishAsync(new[] { "physical:key" }, 0, CancellationToken.None));
        outbox.Status = new HybridInvalidationOutboxStatus(1, 0, 1,
            DateTimeOffset.UtcNow.AddSeconds(-2), null,
            DateTimeOffset.UtcNow.AddSeconds(-1), 42);

        var snapshot = await delivery.GetSnapshotAsync(CancellationToken.None);

        Assert.True(snapshot.OutboxEnabled);
        Assert.Equal(1, snapshot.PendingMessages);
        Assert.Equal(0, snapshot.ProcessingMessages);
        Assert.True(snapshot.OldestPendingAge >= TimeSpan.FromSeconds(1));
        Assert.Equal(2, snapshot.FailedPublishAttemptCount);
        Assert.Equal(1, snapshot.EnqueuedMessageCount);
        Assert.Equal(1, snapshot.DeadLetterMessages);
        Assert.Equal(42L, snapshot.DeadLetterBytes);
        Assert.True(snapshot.OldestDeadLetterAge >= TimeSpan.Zero);
        Assert.Equal(0, snapshot.ExpiredDeadLetterCount);
        Assert.Equal("bus failed", snapshot.LastFailureMessage);
        Assert.NotNull(snapshot.LastFailureUtc);
    }

    /// <summary>
    /// 验证读取死信前需要通过授权。
    /// </summary>
    [Fact]
    public async Task DeadLetterAdmin_RequiresAuthorizationAndAuditBeforeReading()
    {
        var outbox = new TestOutbox();
        outbox.DeadLetters.Add(new HybridInvalidationDeadLetterRecord(
            CreateDeadLetterPayload("admin-message"), DateTimeOffset.UtcNow));
        using var delivery = CreateDelivery(new TestBus(_ => null), outbox,
            new HybridInvalidationDeliveryOptions { MaxPendingMessages = 4 });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            delivery.GetDeadLettersAsync("operator", 10, CancellationToken.None));

        Assert.Equal(0, outbox.ReadDeadLetterCalls);
    }

    /// <summary>
    /// 验证死信列表、重放和删除操作记录审计。
    /// </summary>
    [Fact]
    public async Task DeadLetterAdmin_ListsReplaysAndAuditsOpaqueEntry()
    {
        var outbox = new TestOutbox();
        outbox.DeadLetters.Add(new HybridInvalidationDeadLetterRecord(
            CreateDeadLetterPayload("admin-message"), DateTimeOffset.UtcNow.AddMinutes(-1)));
        outbox.DeadLetters.Add(new HybridInvalidationDeadLetterRecord(
            CreateDeadLetterPayload("delete-message"), DateTimeOffset.UtcNow.AddMinutes(-2)));
        var authorizer = new TestDeadLetterAuthorizer();
        var audit = new TestDeadLetterAuditSink();
        using var delivery = CreateDelivery(new TestBus(_ => null), outbox,
            new HybridInvalidationDeliveryOptions { MaxPendingMessages = 4 },
            authorizer, audit);

        var entries = await delivery.GetDeadLettersAsync("operator", 10, CancellationToken.None);

        Assert.Equal(2, entries.Count);
        var entry = Assert.Single(entries.Where(item => item.MessageId == "admin-message"));
        var deleteEntry = Assert.Single(entries.Where(item => item.MessageId == "delete-message"));
        Assert.Equal("topic", entry.Topic);
        Assert.Equal("admin-message", entry.MessageId);
        Assert.Equal(new[] { "physical:key" }, entry.CacheKeys);
        Assert.False(entry.Malformed);
        Assert.NotEqual(entry.Handle, entry.ConfirmationToken);
        Assert.Equal(HybridInvalidationDeadLetterAction.View, authorizer.Requests[0].Action);
        Assert.Equal(HybridInvalidationDeadLetterAction.View, audit.Entries[0].Action);

        Assert.True(await delivery.ReplayAsync(entry, "operator", CancellationToken.None));
        Assert.Contains("admin-message", outbox.LastAdminPayload);
        Assert.Equal(HybridInvalidationDeadLetterAction.Replay, authorizer.Requests[1].Action);

        Assert.True(await delivery.ConfirmDeleteAsync(deleteEntry, deleteEntry.ConfirmationToken,
            "operator", CancellationToken.None));
        Assert.Equal(HybridInvalidationDeadLetterAction.ConfirmDelete,
            authorizer.Requests[2].Action);
        Assert.Equal(3, audit.Entries.Count);
        Assert.All(audit.Entries, item => Assert.True(item.Succeeded));
    }

    /// <summary>
    /// 验证死信句柄和确认令牌跨投递器 JSON 往返后仍可用。
    /// </summary>
    [Fact]
    public async Task DeadLetterAdmin_HandleAndTokenSurviveJsonRoundTripAcrossDeliveryInstances()
    {
        var outbox = new TestOutbox();
        outbox.DeadLetters.Add(new HybridInvalidationDeadLetterRecord(
            CreateDeadLetterPayload("roundtrip-replay"), DateTimeOffset.UtcNow));
        outbox.DeadLetters.Add(new HybridInvalidationDeadLetterRecord(
            CreateDeadLetterPayload("roundtrip-delete"), DateTimeOffset.UtcNow));
        var authorizer = new TestDeadLetterAuthorizer();
        var audit = new TestDeadLetterAuditSink();
        using var listingDelivery = CreateDelivery(new TestBus(_ => null), outbox,
            new HybridInvalidationDeliveryOptions { MaxPendingMessages = 4 }, authorizer, audit);
        using var operationDelivery = CreateDelivery(new TestBus(_ => null), outbox,
            new HybridInvalidationDeliveryOptions { MaxPendingMessages = 4 }, authorizer, audit);

        var entries = await listingDelivery.GetDeadLettersAsync("operator", 10,
            CancellationToken.None);
        var replayEntry = Assert.Single(entries.Where(item => item.MessageId == "roundtrip-replay"));
        var deleteEntry = Assert.Single(entries.Where(item => item.MessageId == "roundtrip-delete"));
        var replayHandle = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(replayEntry.Handle));
        var deleteHandle = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(deleteEntry.Handle));
        var confirmationToken = JsonSerializer.Deserialize<string>(
            JsonSerializer.Serialize(deleteEntry.ConfirmationToken));

        Assert.True(await operationDelivery.ReplayAsync(replayHandle, "operator",
            CancellationToken.None));
        Assert.Contains("roundtrip-replay", outbox.LastAdminPayload);
        Assert.True(await operationDelivery.ConfirmDeleteAsync(deleteHandle, confirmationToken,
            "operator", CancellationToken.None));
        Assert.Equal(0, outbox.DeadLetters.Count);
        Assert.Contains(authorizer.Requests, request => request.Handle == replayHandle);
        Assert.Contains(authorizer.Requests, request => request.Handle == deleteHandle);
        Assert.Equal(3, audit.Entries.Count);
        Assert.All(audit.Entries, item => Assert.True(item.Succeeded));
    }

    /// <summary>
    /// 验证错误确认令牌和格式错误载荷会被拒绝。
    /// </summary>
    [Fact]
    public async Task DeadLetterAdmin_RejectsWrongConfirmationAndMalformedReplay()
    {
        var outbox = new TestOutbox();
        outbox.DeadLetters.Add(new HybridInvalidationDeadLetterRecord("not-json",
            DateTimeOffset.UtcNow));
        var authorizer = new TestDeadLetterAuthorizer();
        var audit = new TestDeadLetterAuditSink();
        using var delivery = CreateDelivery(new TestBus(_ => null), outbox,
            new HybridInvalidationDeliveryOptions { MaxPendingMessages = 4 },
            authorizer, audit);

        var entry = Assert.Single(await delivery.GetDeadLettersAsync(
            "operator", 10, CancellationToken.None));
        Assert.True(entry.Malformed);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            delivery.ReplayAsync(entry, "operator", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            delivery.ConfirmDeleteAsync(entry, "wrong", "operator", CancellationToken.None));
        Assert.Equal(0, outbox.ReplayDeadLetterCalls);
        Assert.Equal(0, outbox.DeleteDeadLetterCalls);
    }

    /// <summary>
    /// 验证授权失败会记录审计并传播异常。
    /// </summary>
    [Fact]
    public async Task DeadLetterAdmin_AuthorizationFailureIsAudited()
    {
        var outbox = new TestOutbox();
        var authorizer = new TestDeadLetterAuthorizer
        {
            Failure = new UnauthorizedAccessException("denied")
        };
        var audit = new TestDeadLetterAuditSink();
        using var delivery = CreateDelivery(new TestBus(_ => null), outbox,
            new HybridInvalidationDeliveryOptions { MaxPendingMessages = 4 },
            authorizer, audit);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            delivery.GetDeadLettersAsync("operator", 10, CancellationToken.None));

        Assert.Equal("denied", error.Message);
        var auditEntry = Assert.Single(audit.Entries);
        Assert.Equal(HybridInvalidationDeadLetterAction.View, auditEntry.Action);
        Assert.False(auditEntry.Succeeded);
        Assert.Contains("denied", auditEntry.FailureReason);
        Assert.Equal(0, outbox.ReadDeadLetterCalls);
    }

    /// <summary>
    /// 验证未授权句柄不会读取死信载荷。
    /// </summary>
    [Fact]
    public async Task DeadLetterAdmin_UnauthorizedHandleDoesNotReadRedisPayload()
    {
        var outbox = new TestOutbox();
        outbox.DeadLetters.Add(new HybridInvalidationDeadLetterRecord(
            CreateDeadLetterPayload("denied-handle"), DateTimeOffset.UtcNow));
        var authorizer = new TestDeadLetterAuthorizer
        {
            Failure = new UnauthorizedAccessException("denied")
        };
        var audit = new TestDeadLetterAuditSink();
        using var delivery = CreateDelivery(new TestBus(_ => null), outbox,
            new HybridInvalidationDeliveryOptions { MaxPendingMessages = 4 }, authorizer, audit);
        var handle = outbox.DeadLetters.Single().Handle;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            delivery.ReplayAsync(handle, "operator", CancellationToken.None));

        Assert.Equal(0, outbox.GetDeadLetterPayloadCalls);
        Assert.Equal(handle, Assert.Single(authorizer.Requests).Handle);
    }

    /// <summary>
    /// 验证格式错误句柄在授权和读取前被拒绝。
    /// </summary>
    [Fact]
    public async Task DeadLetterAdmin_RejectsMalformedHandleBeforeAuthorizationOrRedisRead()
    {
        var outbox = new TestOutbox();
        var authorizer = new TestDeadLetterAuthorizer();
        var audit = new TestDeadLetterAuditSink();
        using var delivery = CreateDelivery(new TestBus(_ => null), outbox,
            new HybridInvalidationDeliveryOptions { MaxPendingMessages = 4 }, authorizer, audit);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            delivery.ReplayAsync("invalid-handle", "operator", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            delivery.ConfirmDeleteAsync("invalid-handle", "token", "operator",
                CancellationToken.None));

        Assert.Empty(authorizer.Requests);
        Assert.Equal(0, outbox.GetDeadLetterPayloadCalls);
    }

    /// <summary>
    /// 验证读取死信失败会记录审计并传播异常。
    /// </summary>
    [Fact]
    public async Task DeadLetterAdmin_RedisFailureIsAuditedAndPropagated()
    {
        var outbox = new TestOutbox { ReadDeadLettersFailure = new InvalidOperationException("redis read") };
        var authorizer = new TestDeadLetterAuthorizer();
        var audit = new TestDeadLetterAuditSink();
        using var delivery = CreateDelivery(new TestBus(_ => null), outbox,
            new HybridInvalidationDeliveryOptions { MaxPendingMessages = 4 },
            authorizer, audit);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            delivery.GetDeadLettersAsync("operator", 10, CancellationToken.None));

        Assert.Equal("redis read", error.Message);
        var auditEntry = Assert.Single(audit.Entries);
        Assert.Equal(HybridInvalidationDeadLetterAction.View, auditEntry.Action);
        Assert.False(auditEntry.Succeeded);
        Assert.Contains("redis read", auditEntry.FailureReason);
    }

    /// <summary>
    /// 验证预先取消的请求不会授权、读取或审计。
    /// </summary>
    [Fact]
    public async Task DeadLetterAdmin_PreCanceledRequestDoesNotAuthorizeOrRead()
    {
        var outbox = new TestOutbox();
        var authorizer = new TestDeadLetterAuthorizer();
        var audit = new TestDeadLetterAuditSink();
        using var delivery = CreateDelivery(new TestBus(_ => null), outbox,
            new HybridInvalidationDeliveryOptions { MaxPendingMessages = 4 },
            authorizer, audit);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            delivery.GetDeadLettersAsync("operator", 10, cancellation.Token));

        Assert.Empty(authorizer.Requests);
        Assert.Equal(0, outbox.ReadDeadLetterCalls);
        Assert.Empty(audit.Entries);
    }

    /// <summary>
    /// 验证总线恢复后 processing 消息重试并确认。
    /// </summary>
    [Fact]
    public async Task ProcessingMessage_IsRetriedAndAcknowledgedAfterBusRecovery()
    {
        var bus = new TestBus(attempt => attempt == 1
            ? new InvalidOperationException("transient")
            : null);
        var message = new EasyCachingMessage
        {
            Id = "message-1",
            CacheKeys = new[] { "physical:key" }
        };
        var outbox = new TestOutbox { AllowTake = true };
        outbox.Seed(new HybridInvalidationOutboxItem("seed", "topic", message));
        using var delivery = CreateDelivery(bus, outbox,
            new HybridInvalidationDeliveryOptions
            {
                MaxPublishAttempts = 2,
                MaxPendingMessages = 4,
                RetryDelay = TimeSpan.FromMilliseconds(1),
                OutboxPollInterval = TimeSpan.FromMilliseconds(5)
            });

        await WaitUntilAsync(() => outbox.AcknowledgedCount == 1,
            "processing 消息未在总线恢复后确认删除。");

        Assert.True(bus.PublishCount >= 2);
        Assert.Equal(1, outbox.AcknowledgedCount);
    }

    /// <summary>
    /// 验证达到投递上限的消息转入死信且不确认。
    /// </summary>
    [Fact]
    public async Task ProcessingMessage_ReachesLimitAndMovesToDeadLetter()
    {
        var bus = new TestBus(_ => new InvalidOperationException("poison"));
        var message = new EasyCachingMessage
        {
            Id = "poison-1",
            CacheKeys = new[] { "physical:key" }
        };
        var outbox = new TestOutbox { AllowTake = true };
        outbox.Seed(new HybridInvalidationOutboxItem("poison", "topic", message, 2));
        using var delivery = CreateDelivery(bus, outbox,
            new HybridInvalidationDeliveryOptions
            {
                MaxPublishAttempts = 1,
                MaxPendingMessages = 4,
                MaxDeliveryAttempts = 2,
                DeadLetterMaxMessages = 2,
                OutboxPollInterval = TimeSpan.FromMilliseconds(5)
            });

        await WaitUntilAsync(() => outbox.DeadLetteredCount == 1,
            "达到投递阈值的消息未转移到 dead-letter。 ");

        Assert.Equal(0, outbox.AcknowledgedCount);
        Assert.Equal(1, outbox.Status.DeadLetterMessages);
        Assert.Equal(1, delivery.GetSnapshot().DeadLetteredMessageCount);
        Assert.Equal(1, bus.PublishCount);
    }

    /// <summary>
    /// 验证死信按限定批大小清理并记录清理数量。
    /// </summary>
    [Fact]
    public async Task DeadLetterRetention_CleansInBoundedBatchesAndReportsCount()
    {
        var outbox = new TestOutbox
        {
            AllowTake = false,
            CleanupResult = 2,
            Status = new HybridInvalidationOutboxStatus(0, 0, 2, null, null,
                DateTimeOffset.UtcNow.AddMinutes(-2))
        };
        using var delivery = CreateDelivery(new TestBus(_ => null), outbox,
            new HybridInvalidationDeliveryOptions
            {
                MaxPublishAttempts = 1,
                MaxPendingMessages = 4,
                MaxDeliveryAttempts = 2,
                DeadLetterMaxMessages = 4,
                DeadLetterRetention = TimeSpan.FromMinutes(1),
                DeadLetterCleanupInterval = TimeSpan.FromMilliseconds(5),
                DeadLetterCleanupBatchSize = 3,
                OutboxPollInterval = TimeSpan.FromMilliseconds(10)
            });

        await WaitUntilAsync(() => outbox.CleanupCalls > 0 &&
            delivery.GetSnapshot().ExpiredDeadLetterCount == 2,
            "dead-letter 保留期清理未被调度。");

        var snapshot = delivery.GetSnapshot();
        Assert.Equal(2, snapshot.ExpiredDeadLetterCount);
        Assert.True(outbox.CleanupCalls >= 1);
        Assert.Equal(3, outbox.LastCleanupBatchSize);
        Assert.Equal(0, outbox.Status.DeadLetterMessages);
    }

    /// <summary>
    /// 验证死信清理失败时保留 processing 消息并报告故障。
    /// </summary>
    [Fact]
    public async Task DeadLetterCleanupFailure_PreservesProcessingAndReportsFailure()
    {
        var outbox = new TestOutbox
        {
            AllowTake = false,
            CleanupThrows = true,
            Status = new HybridInvalidationOutboxStatus(0, 1, 1, null, null,
                DateTimeOffset.UtcNow.AddMinutes(-2))
        };
        using var delivery = CreateDelivery(new TestBus(_ => null), outbox,
            new HybridInvalidationDeliveryOptions
            {
                MaxPublishAttempts = 1,
                MaxPendingMessages = 4,
                MaxDeliveryAttempts = 2,
                DeadLetterMaxMessages = 4,
                DeadLetterRetention = TimeSpan.FromMinutes(1),
                DeadLetterCleanupInterval = TimeSpan.FromMilliseconds(5),
                OutboxPollInterval = TimeSpan.FromMilliseconds(10)
            });

        await WaitUntilAsync(() => delivery.GetSnapshot().LastFailureMessage == "cleanup failed",
            "dead-letter 清理失败未进入诊断。");

        Assert.Equal(1, delivery.GetSnapshot().ProcessingMessages);
        Assert.Equal(0, outbox.AcknowledgedCount);
    }

    /// <summary>
    /// 创建失效消息投递测试夹具。
    /// </summary>
    /// <param name="bus">测试消息总线。</param>
    /// <param name="outbox">测试失效消息出站箱。</param>
    /// <param name="options">测试选项。</param>
    /// <param name="authorizer">死信管理授权器。</param>
    /// <param name="audit">死信管理审计接收器。</param>
    /// <returns>返回配置好的失效消息投递器。</returns>
    private static HybridInvalidationDelivery CreateDelivery(TestBus bus, TestOutbox outbox,
        HybridInvalidationDeliveryOptions options,
        IHybridInvalidationDeadLetterAuthorizer authorizer = null,
        IHybridInvalidationDeadLetterAuditSink audit = null)
    {
        var tracker = new HybridBusFailureTracker(bus);
        var configuration = new CacheConfiguration("delivery-test", "delivery-test");
        return new HybridInvalidationDelivery(tracker, new TestSerializer(), configuration,
            "topic", database: null, options, outbox, authorizer, audit);
    }

    /// <summary>
    /// 创建指定标识的死信载荷。
    /// </summary>
    /// <param name="id">死信载荷标识。</param>
    /// <returns>返回包含主题、键和载荷标识的死信 JSON。</returns>
    private static string CreateDeadLetterPayload(string id) => JsonSerializer.Serialize(new
    {
        Topic = "topic",
        Id = id,
        CacheKeys = new[] { "physical:key" },
        IsPrefix = false,
        IsPattern = false
    });

    /// <summary>
    /// 等待条件满足或超时。
    /// </summary>
    /// <param name="condition">等待条件。</param>
    /// <param name="error">错误信息。</param>
    private static async Task WaitUntilAsync(Func<bool> condition, string error)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(error);
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// 记录发布次数并按调用次数注入异常的测试总线。
    /// </summary>
    private sealed class TestBus : IEasyCachingBus
    {
        /// <summary>
        /// 保存按发布次数返回异常的回调。
        /// </summary>
        private readonly Func<int, Exception> _failure;
        /// <summary>
        /// 保存已发布消息的次数。
        /// </summary>
        private int _publishCount;

        /// <summary>
        /// 初始化一个 <see cref="TestBus"/> 类型的实例。
        /// </summary>
        /// <param name="failure">根据发布次数返回异常的回调。</param>
        internal TestBus(Func<int, Exception> failure) => _failure = failure;

        /// <summary>
        /// 获取已发布消息的次数。
        /// </summary>
        internal int PublishCount => Volatile.Read(ref _publishCount);

        /// <inheritdoc />
        /// <remarks>
        /// 返回固定为 delivery-test-bus 的测试总线名称。
        /// </remarks>
        public string Name => "delivery-test-bus";

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="message">消息内容。</param>
        public void Publish(string topic, EasyCachingMessage message)
        {
            var attempt = Interlocked.Increment(ref _publishCount);
            var error = _failure?.Invoke(attempt);
            if (error != null)
                throw error;
        }

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="message">消息内容。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task PublishAsync(string topic, EasyCachingMessage message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Publish(topic, message);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="handler">处理器。</param>
        /// <param name="onConnect">连接回调。</param>
        public void Subscribe(string topic, Action<EasyCachingMessage> handler, Action onConnect) { }

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="handler">处理器。</param>
        /// <param name="onConnect">连接回调。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task SubscribeAsync(string topic, Action<EasyCachingMessage> handler, Action onConnect,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// 模拟失效消息出站箱及死信管理操作。
    /// </summary>
    private sealed class TestOutbox : IHybridInvalidationOutbox,
        IHybridInvalidationDeadLetterAdminOutbox
    {
        /// <summary>
        /// 保护测试出站箱状态的同步对象。
        /// </summary>
        private readonly object _gate = new();
        /// <summary>
        /// 保存待取出的种子失效消息。
        /// </summary>
        private HybridInvalidationOutboxItem _seed;

        /// <summary>
        /// 获取或设置指定入队操作是否成功。
        /// </summary>
        internal bool EnqueueResult { get; set; } = true;

        /// <summary>
        /// 获取或设置指定是否返回种子消息。
        /// </summary>
        internal bool AllowTake { get; set; }

        /// <summary>
        /// 获取已加入测试发件箱的失效消息。
        /// </summary>
        internal List<HybridInvalidationOutboxItem> Enqueued { get; } = new();

        /// <summary>
        /// 获取或设置记录入队调用次数。
        /// </summary>
        internal int EnqueueCalls { get; private set; }

        /// <summary>
        /// 获取或设置记录确认调用次数。
        /// </summary>
        internal int AcknowledgedCount { get; private set; }

        /// <summary>
        /// 获取或设置记录转入死信的调用次数。
        /// </summary>
        internal int DeadLetteredCount { get; private set; }

        /// <summary>
        /// 获取或设置指定转入死信操作是否成功。
        /// </summary>
        internal bool DeadLetterResult { get; set; } = true;

        /// <summary>
        /// 获取或设置指定过期死信清理返回的消息数。
        /// </summary>
        internal long CleanupResult { get; set; }

        /// <summary>
        /// 获取或设置指定清理操作是否抛出异常。
        /// </summary>
        internal bool CleanupThrows { get; set; }

        /// <summary>
        /// 获取或设置记录清理调用次数。
        /// </summary>
        internal int CleanupCalls { get; private set; }

        /// <summary>
        /// 获取或设置记录最近一次清理使用的批大小。
        /// </summary>
        internal int LastCleanupBatchSize { get; private set; }

        /// <summary>
        /// 获取测试发件箱中的死信记录。
        /// </summary>
        internal List<HybridInvalidationDeadLetterRecord> DeadLetters { get; } = new();

        /// <summary>
        /// 获取或设置记录读取死信的调用次数。
        /// </summary>
        internal int ReadDeadLetterCalls { get; private set; }

        /// <summary>
        /// 获取或设置记录重放死信的调用次数。
        /// </summary>
        internal int ReplayDeadLetterCalls { get; private set; }

        /// <summary>
        /// 获取或设置记录删除死信的调用次数。
        /// </summary>
        internal int DeleteDeadLetterCalls { get; private set; }

        /// <summary>
        /// 获取或设置记录读取死信载荷的调用次数。
        /// </summary>
        internal int GetDeadLetterPayloadCalls { get; private set; }

        /// <summary>
        /// 获取或设置保存最近一次管理操作使用的载荷。
        /// </summary>
        internal string LastAdminPayload { get; private set; }

        /// <summary>
        /// 获取或设置指定读取死信时要抛出的异常。
        /// </summary>
        internal Exception ReadDeadLettersFailure { get; set; }

        /// <summary>
        /// 获取或设置保存当前出站箱状态。
        /// </summary>
        internal HybridInvalidationOutboxStatus Status { get; set; } =
            new HybridInvalidationOutboxStatus(0, 0, 0, null, null, null);

        /// <summary>
        /// 设置待取出的种子失效消息。
        /// </summary>
        /// <param name="item">待处理项。</param>
        internal void Seed(HybridInvalidationOutboxItem item) => _seed = item;

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="message">消息内容。</param>
        public bool TryEnqueue(string topic, EasyCachingMessage message)
        {
            lock (_gate)
            {
                EnqueueCalls++;
                if (!EnqueueResult)
                    return false;
                Enqueued.Add(new HybridInvalidationOutboxItem("enqueued", topic, message));
                return true;
            }
        }

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="message">消息内容。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task<bool> TryEnqueueAsync(string topic, EasyCachingMessage message,
            CancellationToken cancellationToken) =>
            Task.FromResult(TryEnqueue(topic, message));

        /// <inheritdoc />
        /// <param name="item">待处理项。</param>
        public bool TryTake(out HybridInvalidationOutboxItem item)
        {
            lock (_gate)
            {
                item = AllowTake ? _seed : null;
                return item != null;
            }
        }

        /// <inheritdoc />
        /// <param name="cancellationToken">取消令牌。</param>
        public Task<HybridInvalidationOutboxItem> TryTakeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                var item = AllowTake ? _seed : null;
                return Task.FromResult(item);
            }
        }

        /// <inheritdoc />
        /// <param name="item">待处理项。</param>
        public bool TryDeadLetter(HybridInvalidationOutboxItem item)
        {
            lock (_gate)
            {
                if (!DeadLetterResult)
                    return false;
                if (ReferenceEquals(_seed, item))
                    _seed = null;
                DeadLetteredCount++;
                Status = new HybridInvalidationOutboxStatus(Status.PendingMessages, 0,
                    Status.DeadLetterMessages + 1, Status.OldestPendingEnqueuedAtUtc,
                    null, Status.OldestDeadLetteredAtUtc);
                return true;
            }
        }

        /// <inheritdoc />
        /// <param name="item">待处理项。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task<bool> TryDeadLetterAsync(HybridInvalidationOutboxItem item,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryDeadLetter(item));
        }

        /// <inheritdoc />
        /// <param name="cutoffUtc">死信过期时间界限。</param>
        /// <param name="maximumMessages">最大消息数。</param>
        public long CleanupExpiredDeadLetters(DateTimeOffset cutoffUtc, int maximumMessages)
        {
            lock (_gate)
            {
                CleanupCalls++;
                LastCleanupBatchSize = maximumMessages;
                if (CleanupThrows)
                    throw new InvalidOperationException("cleanup failed");
                var removed = CleanupResult;
                CleanupResult = 0;
                Status = new HybridInvalidationOutboxStatus(Status.PendingMessages,
                    Status.ProcessingMessages,
                    Math.Max(0, Status.DeadLetterMessages - removed),
                    Status.OldestPendingEnqueuedAtUtc,
                    Status.OldestProcessingEnqueuedAtUtc,
                    removed > 0 ? null : Status.OldestDeadLetteredAtUtc);
                return removed;
            }
        }

        /// <inheritdoc />
        /// <param name="cutoffUtc">死信过期时间界限。</param>
        /// <param name="maximumMessages">最大消息数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task<long> CleanupExpiredDeadLettersAsync(DateTimeOffset cutoffUtc,
            int maximumMessages, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CleanupExpiredDeadLetters(cutoffUtc, maximumMessages));
        }

        /// <inheritdoc />
        /// <param name="maximumMessages">最大消息数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task<IReadOnlyList<HybridInvalidationDeadLetterRecord>> ReadDeadLettersAsync(
            int maximumMessages, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadDeadLetterCalls++;
            if (ReadDeadLettersFailure != null)
                throw ReadDeadLettersFailure;
            return Task.FromResult<IReadOnlyList<HybridInvalidationDeadLetterRecord>>(
                DeadLetters.Take(maximumMessages).ToArray());
        }

        /// <inheritdoc />
        /// <param name="handle">操作句柄。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task<string> GetDeadLetterPayloadAsync(string handle,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetDeadLetterPayloadCalls++;
            return Task.FromResult(DeadLetters.FirstOrDefault(item => item.Handle == handle)?.Payload);
        }

        /// <inheritdoc />
        /// <param name="handle">操作句柄。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task<bool> ReplayDeadLetterAsync(string handle,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = DeadLetters.FirstOrDefault(item => item.Handle == handle);
            if (record == null)
                return Task.FromResult(false);
            ReplayDeadLetterCalls++;
            LastAdminPayload = record.Payload;
            DeadLetters.Remove(record);
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        /// <param name="handle">操作句柄。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task<bool> DeleteDeadLetterAsync(string handle,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = DeadLetters.FirstOrDefault(item => item.Handle == handle);
            if (record == null)
                return Task.FromResult(false);
            DeleteDeadLetterCalls++;
            DeadLetters.Remove(record);
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        public HybridInvalidationOutboxStatus GetStatus() => Status;

        /// <inheritdoc />
        /// <param name="cancellationToken">取消令牌。</param>
        public Task<HybridInvalidationOutboxStatus> GetStatusAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Status);
        }

        /// <inheritdoc />
        /// <param name="item">待处理项。</param>
        public void Acknowledge(HybridInvalidationOutboxItem item)
        {
            lock (_gate)
            {
                if (ReferenceEquals(_seed, item))
                    _seed = null;
                AcknowledgedCount++;
            }
        }

        /// <inheritdoc />
        /// <param name="item">待处理项。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task AcknowledgeAsync(HybridInvalidationOutboxItem item,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Acknowledge(item);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose() { }
    }

    /// <summary>
    /// 记录死信管理授权请求并注入授权故障。
    /// </summary>
    private sealed class TestDeadLetterAuthorizer : IHybridInvalidationDeadLetterAuthorizer
    {
        /// <summary>
        /// 获取保存已收到的死信授权请求。
        /// </summary>
        internal List<HybridInvalidationDeadLetterAuthorizationRequest> Requests { get; } = new();

        /// <summary>
        /// 获取或设置指定授权失败时抛出的异常。
        /// </summary>
        internal Exception Failure { get; set; }

        /// <inheritdoc />
        /// <param name="request">死信管理授权请求。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task AuthorizeAsync(HybridInvalidationDeadLetterAuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (Failure != null)
                throw Failure;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 记录死信管理审计条目。
    /// </summary>
    private sealed class TestDeadLetterAuditSink : IHybridInvalidationDeadLetterAuditSink
    {
        /// <summary>
        /// 获取保存已记录的死信审计条目。
        /// </summary>
        internal List<HybridInvalidationDeadLetterAuditEntry> Entries { get; } = new();

        /// <inheritdoc />
        /// <param name="entry">死信管理审计条目。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task RecordAsync(HybridInvalidationDeadLetterAuditEntry entry,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 提供固定结果的 EasyCaching 测试序列化器。
    /// </summary>
    private sealed class TestSerializer : IEasyCachingSerializer
    {
        /// <inheritdoc />
        /// <remarks>
        /// 返回固定为 delivery-test-serializer 的序列化器名称。
        /// </remarks>
        public string Name => "delivery-test-serializer";

        /// <inheritdoc />
        /// <param name="value">待序列化的值。</param>
        public byte[] Serialize<T>(T value) => new byte[1];

        /// <inheritdoc />
        /// <param name="bytes">待反序列化的字节数组。</param>
        public T Deserialize<T>(byte[] bytes) => default;

        /// <inheritdoc />
        /// <param name="bytes">字节数。</param>
        /// <param name="type">类型。</param>
        public object Deserialize(byte[] bytes, Type type) => null;

        /// <inheritdoc />
        /// <param name="obj">待序列化的对象。</param>
        public ArraySegment<byte> SerializeObject(object obj) => new(new byte[1]);

        /// <inheritdoc />
        /// <param name="value">待处理值。</param>
        public object DeserializeObject(ArraySegment<byte> value) => null;
    }
}
