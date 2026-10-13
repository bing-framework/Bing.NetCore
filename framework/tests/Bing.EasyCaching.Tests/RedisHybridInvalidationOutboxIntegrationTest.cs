using Bing.Caching;
using EasyCaching.Core.Bus;
using StackExchange.Redis;
using System.Text;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 只在显式指定非生产 Redis 时验证 Redis outbox 的列表恢复和容量边界。
/// </summary>
public sealed class RedisHybridInvalidationOutboxFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="RedisHybridInvalidationOutboxFactAttribute"/> 类型的实例。
    /// </summary>
    public RedisHybridInvalidationOutboxFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_REDIS_OUTBOX"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_HYBRID_OUTBOX_REDIS")))
            Skip = "需要显式允许并指定非生产 Redis outbox 端点。";
    }
}

/// <summary>
/// 验证 Redis Hybrid 失效 outbox 的恢复、容量和管理操作。
/// </summary>
public sealed class RedisHybridInvalidationOutboxIntegrationTest
{
    /// <summary>
    /// outbox 重建后恢复 processing 项，并保持队列容量边界。
    /// </summary>
    [RedisHybridInvalidationOutboxFact]
    public async Task Outbox_EnforcesBoundAndRecoversProcessingAfterRebuild()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_HYBRID_OUTBOX_REDIS")!;
        var configuration = new CacheConfiguration(
            "hybrid-outbox-" + Guid.NewGuid().ToString("N"),
            "hybrid-outbox-" + Guid.NewGuid().ToString("N"));
        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        var pending = (RedisKey)(root + ":pending");
        var processing = (RedisKey)(root + ":processing");
        var attempts = (RedisKey)(root + ":attempts");
        var dead = (RedisKey)(root + ":dead");
        using var connection = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var database = connection.GetDatabase();
        var first = new RedisHybridInvalidationOutbox(database, configuration, 2);
        var second = new RedisHybridInvalidationOutbox(database, configuration, 2);
        try
        {
            Assert.True(first.TryEnqueue("topic", CreateMessage("one")));
            Assert.True(first.TryEnqueue("topic", CreateMessage("two")));
            Assert.False(first.TryEnqueue("topic", CreateMessage("three")));
            Assert.Equal(2, await database.ListLengthAsync(pending));
            Assert.Equal(0, await database.ListLengthAsync(processing));
            var queuedStatus = await first.GetStatusAsync(CancellationToken.None);
            Assert.Equal(2, queuedStatus.PendingMessages);
            Assert.Equal(0, queuedStatus.ProcessingMessages);
            Assert.Equal(0, queuedStatus.DeadLetterMessages);
            Assert.NotNull(queuedStatus.OldestPendingEnqueuedAtUtc);

            Assert.True(first.TryTake(out var inFlight));
            Assert.Equal("one", inFlight.Message.CacheKeys.Single());
            Assert.Equal(1, await database.ListLengthAsync(pending));
            Assert.Equal(1, await database.ListLengthAsync(processing));
            var processingStatus = first.GetStatus();
            Assert.Equal(1, processingStatus.PendingMessages);
            Assert.Equal(1, processingStatus.ProcessingMessages);
            Assert.Equal(0, processingStatus.DeadLetterMessages);
            Assert.NotNull(processingStatus.OldestProcessingEnqueuedAtUtc);

            // 新投递器优先恢复 processing，模拟进程在发布后、确认前退出。
            Assert.True(second.TryTake(out var recovered));
            Assert.Equal(inFlight.Message.Id, recovered.Message.Id);
            second.Acknowledge(recovered);
            Assert.Equal(0, await database.ListLengthAsync(processing));

            Assert.True(second.TryTake(out var next));
            Assert.Equal("two", next.Message.CacheKeys.Single());
            second.Acknowledge(next);
            Assert.False(second.TryTake(out _));
            Assert.Equal(0, await database.ListLengthAsync(pending));
            Assert.False(await database.KeyExistsAsync(attempts));
        }
        finally
        {
            first.Dispose();
            second.Dispose();
            await database.KeyDeleteAsync(new[] { pending, processing, attempts, dead,
                (RedisKey)(root + ":dead-payloads") });
        }
    }

    /// <summary>
    /// 记录投递尝试并将超限消息移入有界死信队列。
    /// </summary>
    [RedisHybridInvalidationOutboxFact]
    public async Task Outbox_PersistsAttemptsAndMovesToBoundedDeadLetter()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_HYBRID_OUTBOX_REDIS")!;
        var configuration = new CacheConfiguration(
            "hybrid-outbox-dead-" + Guid.NewGuid().ToString("N"),
            "hybrid-outbox-dead-" + Guid.NewGuid().ToString("N"));
        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        var pending = (RedisKey)(root + ":pending");
        var processing = (RedisKey)(root + ":processing");
        var attempts = (RedisKey)(root + ":attempts");
        var dead = (RedisKey)(root + ":dead");
        using var connection = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var database = connection.GetDatabase();
        using var first = new RedisHybridInvalidationOutbox(database, configuration, 4, 1);
        using var second = new RedisHybridInvalidationOutbox(database, configuration, 4, 1);
        try
        {
            Assert.True(first.TryEnqueue("topic", CreateMessage("poison")));
            Assert.True(first.TryTake(out var firstItem));
            Assert.Equal(1, firstItem.DeliveryAttempt);

            Assert.True(second.TryTake(out var recovered));
            Assert.Equal(2, recovered.DeliveryAttempt);
            Assert.True(second.TryDeadLetter(recovered));
            Assert.Equal(0, await database.ListLengthAsync(processing));
            Assert.False(await database.KeyExistsAsync(attempts));
            Assert.Equal(1, await database.SortedSetLengthAsync(dead));
            Assert.Equal(1, second.GetStatus().DeadLetterMessages);

            Assert.True(second.TryEnqueue("topic", CreateMessage("capacity")));
            Assert.True(second.TryTake(out var capacityItem));
            Assert.Equal(1, capacityItem.DeliveryAttempt);
            Assert.False(second.TryDeadLetter(capacityItem));
            Assert.Equal(1, await database.ListLengthAsync(processing));
            Assert.Equal(1, second.GetStatus().DeadLetterMessages);
            second.Acknowledge(capacityItem);
        }
        finally
        {
            await database.KeyDeleteAsync(new[] { pending, processing, attempts, dead,
                (RedisKey)(root + ":dead-payloads") });
        }
    }

    /// <summary>
    /// outbox 按有界批次清理过期死信。
    /// </summary>
    [RedisHybridInvalidationOutboxFact]
    public async Task Outbox_CleansExpiredDeadLettersInBoundedBatches()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_HYBRID_OUTBOX_REDIS")!;
        var configuration = new CacheConfiguration(
            "hybrid-outbox-clean-" + Guid.NewGuid().ToString("N"),
            "hybrid-outbox-clean-" + Guid.NewGuid().ToString("N"));
        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        var pending = (RedisKey)(root + ":pending");
        var processing = (RedisKey)(root + ":processing");
        var attempts = (RedisKey)(root + ":attempts");
        var dead = (RedisKey)(root + ":dead");
        using var connection = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var database = connection.GetDatabase();
        using var outbox = new RedisHybridInvalidationOutbox(database, configuration, 4, 4);
        try
        {
            for (var index = 0; index < 3; index++)
            {
                Assert.True(outbox.TryEnqueue("topic", CreateMessage("poison-" + index)));
                Assert.True(outbox.TryTake(out var item));
                Assert.True(outbox.TryDeadLetter(item));
            }

            var status = outbox.GetStatus();
            Assert.Equal(3, status.DeadLetterMessages);
            Assert.NotNull(status.OldestDeadLetteredAtUtc);
            Assert.Equal(0, await outbox.CleanupExpiredDeadLettersAsync(
                DateTimeOffset.UtcNow.AddMinutes(-1), 10, CancellationToken.None));

            Assert.Equal(2, await outbox.CleanupExpiredDeadLettersAsync(
                DateTimeOffset.UtcNow, 2, CancellationToken.None));
            Assert.Equal(1, await database.SortedSetLengthAsync(dead));
            Assert.Equal(1, outbox.GetStatus().DeadLetterMessages);

            Assert.Equal(1, outbox.CleanupExpiredDeadLetters(
                DateTimeOffset.UtcNow.AddSeconds(1), 10));
            Assert.Equal(0, await database.SortedSetLengthAsync(dead));
            Assert.Equal(0, await database.ListLengthAsync(pending));
            Assert.Equal(0, await database.ListLengthAsync(processing));
        }
        finally
        {
            await database.KeyDeleteAsync(new[] { pending, processing, attempts, dead,
                (RedisKey)(root + ":dead-payloads") });
        }
    }

    /// <summary>
    /// outbox 按精确死信载荷字节数限制并重建旧尺寸记录。
    /// </summary>
    [RedisHybridInvalidationOutboxFact]
    public async Task Outbox_EnforcesExactDeadLetterPayloadBytesAndRebuildsLegacySizes()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_HYBRID_OUTBOX_REDIS")!;
        var configuration = new CacheConfiguration(
            "hybrid-outbox-bytes-" + Guid.NewGuid().ToString("N"),
            "hybrid-outbox-bytes-" + Guid.NewGuid().ToString("N"));
        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        var pending = (RedisKey)(root + ":pending");
        var processing = (RedisKey)(root + ":processing");
        var attempts = (RedisKey)(root + ":attempts");
        var dead = (RedisKey)(root + ":dead");
        var sizes = (RedisKey)(root + ":sizes");
        var bytes = (RedisKey)(root + ":bytes");
        using var connection = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var database = connection.GetDatabase();
        using var seed = new RedisHybridInvalidationOutbox(database, configuration,
            maximumMessages: 4, deadLetterMaxMessages: 4);
        try
        {
            Assert.True(seed.TryEnqueue("topic", CreateMessage("exact")));
            Assert.True(seed.TryTake(out var exactItem));
            var exactBytes = Encoding.UTF8.GetByteCount(exactItem.Payload);
            using var exactBudget = new RedisHybridInvalidationOutbox(database, configuration,
                maximumMessages: 4, deadLetterMaxMessages: 4, deadLetterMaxBytes: exactBytes);

            Assert.True(exactBudget.TryDeadLetter(exactItem));
            Assert.Equal((long?)exactBytes, exactBudget.GetStatus().DeadLetterBytes);

            // 删除新版本的辅助键，模拟 095 产生的历史 dead-letter。
            await database.KeyDeleteAsync(new[] { sizes, bytes });
            Assert.Equal((long?)exactBytes, exactBudget.GetStatus().DeadLetterBytes);

            Assert.True(exactBudget.TryEnqueue("topic",
                CreateMessage(new string('x', 128))));
            Assert.True(exactBudget.TryTake(out var rejected));
            Assert.False(exactBudget.TryDeadLetter(rejected));
            Assert.Equal(1, await database.ListLengthAsync(processing));
            Assert.Equal((long?)exactBytes, exactBudget.GetStatus().DeadLetterBytes);

            Assert.Equal(1, exactBudget.CleanupExpiredDeadLetters(
                DateTimeOffset.UtcNow.AddSeconds(1), 10));
            Assert.Equal(0L, exactBudget.GetStatus().DeadLetterBytes);
            exactBudget.Acknowledge(rejected);
        }
        finally
        {
            await database.KeyDeleteAsync(new[] { pending, processing, attempts, dead, sizes, bytes,
                (RedisKey)(root + ":dead-payloads") });
        }
    }

    /// <summary>
    /// 并发死信写入者以原子方式遵守载荷字节预算。
    /// </summary>
    [RedisHybridInvalidationOutboxFact]
    public async Task Outbox_CompetingDeadLetterWritersRespectByteBudgetAtomically()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_HYBRID_OUTBOX_REDIS")!;
        var configuration = new CacheConfiguration(
            "hybrid-outbox-bytes-race-" + Guid.NewGuid().ToString("N"),
            "hybrid-outbox-bytes-race-" + Guid.NewGuid().ToString("N"));
        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        var pending = (RedisKey)(root + ":pending");
        var processing = (RedisKey)(root + ":processing");
        var attempts = (RedisKey)(root + ":attempts");
        var dead = (RedisKey)(root + ":dead");
        var sizes = (RedisKey)(root + ":sizes");
        var bytes = (RedisKey)(root + ":bytes");
        using var connection = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var database = connection.GetDatabase();
        using var first = new RedisHybridInvalidationOutbox(database, configuration, 4, 4, 1_073_741_824);
        using var second = new RedisHybridInvalidationOutbox(database, configuration, 4, 4, 1_073_741_824);
        try
        {
            var shortPayload = "{\"Topic\":\"topic\",\"Id\":\"short\",\"CacheKeys\":[\"a\"],\"IsPrefix\":false,\"IsPattern\":false}";
            var longPayload = "{\"Topic\":\"topic\",\"Id\":\"long\",\"CacheKeys\":[\"" +
                new string('y', 256) + "\"],\"IsPrefix\":false,\"IsPattern\":false}";
            var shortBytes = Encoding.UTF8.GetByteCount(shortPayload);
            var shortMessage = new EasyCachingMessage
            {
                Id = "short",
                CacheKeys = new[] { "a" }
            };
            var longMessage = new EasyCachingMessage
            {
                Id = "long",
                CacheKeys = new[] { new string('y', 256) }
            };
            await database.ListRightPushAsync(processing, new RedisValue[] { shortPayload, longPayload });

            using var boundedFirst = new RedisHybridInvalidationOutbox(database, configuration,
                4, 4, shortBytes);
            using var boundedSecond = new RedisHybridInvalidationOutbox(database, configuration,
                4, 4, shortBytes);
            var results = await Task.WhenAll(
                Task.Run(() => boundedFirst.TryDeadLetter(
                    new HybridInvalidationOutboxItem(shortPayload, "topic", shortMessage, 1))),
                Task.Run(() => boundedSecond.TryDeadLetter(
                    new HybridInvalidationOutboxItem(longPayload, "topic", longMessage, 1))));

            Assert.Equal(1, results.Count(result => result));
            Assert.Equal(1, await database.ListLengthAsync(processing));
            Assert.True((long)(await database.StringGetAsync(bytes)) <= shortBytes);
            Assert.Equal(1, await database.SortedSetLengthAsync(dead));
        }
        finally
        {
            await database.KeyDeleteAsync(new[] { pending, processing, attempts, dead, sizes, bytes,
                (RedisKey)(root + ":dead-payloads") });
        }
    }

    /// <summary>
    /// outbox 将旧载荷成员迁移为句柄且不改变字节预算。
    /// </summary>
    [RedisHybridInvalidationOutboxFact]
    public async Task Outbox_MigratesLegacyPayloadMemberToHandleWithoutChangingByteBudget()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_HYBRID_OUTBOX_REDIS")!;
        var configuration = new CacheConfiguration(
            "hybrid-outbox-legacy-dead-" + Guid.NewGuid().ToString("N"),
            "hybrid-outbox-legacy-dead-" + Guid.NewGuid().ToString("N"));
        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        var pending = (RedisKey)(root + ":pending");
        var processing = (RedisKey)(root + ":processing");
        var attempts = (RedisKey)(root + ":attempts");
        var dead = (RedisKey)(root + ":dead");
        var payloads = (RedisKey)(root + ":dead-payloads");
        var sizes = (RedisKey)(root + ":sizes");
        var bytes = (RedisKey)(root + ":bytes");
        using var connection = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var database = connection.GetDatabase();
        using var outbox = new RedisHybridInvalidationOutbox(database, configuration,
            maximumMessages: 4, deadLetterMaxMessages: 4, deadLetterMaxBytes: 1_073_741_824);
        const string payload = "{\"Topic\":\"topic\",\"Id\":\"legacy-dead\",\"CacheKeys\":[\"legacy:key\"],\"IsPrefix\":false,\"IsPattern\":false}";
        var payloadBytes = Encoding.UTF8.GetByteCount(payload);
        var legacyScore = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds();
        await database.SortedSetAddAsync(dead, payload, legacyScore);
        await database.HashSetAsync(sizes, payload, payloadBytes);
        await database.StringSetAsync(bytes, payloadBytes);

        try
        {
            var record = Assert.Single(await outbox.ReadDeadLettersAsync(10, CancellationToken.None));
            var handle = HybridInvalidationDeadLetterHandle.Create(payload);

            Assert.Equal(payload, record.Payload);
            Assert.Equal(handle, record.Handle);
            Assert.False((await database.SortedSetScoreAsync(dead, payload)).HasValue);
            Assert.True((await database.SortedSetScoreAsync(dead, handle)).HasValue);
            Assert.Equal(payload, (await database.HashGetAsync(payloads, handle)).ToString());
            Assert.Equal(payloadBytes, (int)(await database.HashGetAsync(sizes, handle)));
            Assert.True((await database.HashGetAsync(sizes, payload)).IsNull);
            Assert.Equal((long?)payloadBytes, outbox.GetStatus().DeadLetterBytes);

            Assert.True(await outbox.ReplayDeadLetterAsync(handle, CancellationToken.None));
            Assert.Equal(payload, (await database.ListLeftPopAsync(pending)).ToString());
            Assert.Equal(0, await database.SortedSetLengthAsync(dead));
            Assert.Equal(0L, outbox.GetStatus().DeadLetterBytes);
        }
        finally
        {
            await database.KeyDeleteAsync(new[]
                { pending, processing, attempts, dead, payloads, sizes, bytes });
        }
    }

    /// <summary>
    /// outbox 去重旧载荷和句柄且不重复计算字节预算。
    /// </summary>
    [RedisHybridInvalidationOutboxFact]
    public async Task Outbox_DeduplicatesLegacyPayloadAndHandleWithoutInflatingByteBudget()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_HYBRID_OUTBOX_REDIS")!;
        var configuration = new CacheConfiguration(
            "hybrid-outbox-mixed-dead-" + Guid.NewGuid().ToString("N"),
            "hybrid-outbox-mixed-dead-" + Guid.NewGuid().ToString("N"));
        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        var pending = (RedisKey)(root + ":pending");
        var processing = (RedisKey)(root + ":processing");
        var attempts = (RedisKey)(root + ":attempts");
        var dead = (RedisKey)(root + ":dead");
        var payloads = (RedisKey)(root + ":dead-payloads");
        var sizes = (RedisKey)(root + ":sizes");
        var bytes = (RedisKey)(root + ":bytes");
        using var connection = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var database = connection.GetDatabase();
        using var outbox = new RedisHybridInvalidationOutbox(database, configuration,
            maximumMessages: 4, deadLetterMaxMessages: 4, deadLetterMaxBytes: 1_048_576);
        const string payload = "{\"Topic\":\"topic\",\"Id\":\"mixed-dead\",\"CacheKeys\":[\"mixed:key\"],\"IsPrefix\":false,\"IsPattern\":false}";
        var handle = HybridInvalidationDeadLetterHandle.Create(payload);
        var payloadBytes = Encoding.UTF8.GetByteCount(payload);
        try
        {
            await database.ListRightPushAsync(processing, payload);
            await database.SortedSetAddAsync(dead, payload, DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeMilliseconds());
            await database.SortedSetAddAsync(dead, handle, DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds());
            await database.HashSetAsync(payloads, handle, payload);
            await database.HashSetAsync(sizes, new[]
            {
                new HashEntry(payload, payloadBytes),
                new HashEntry(handle, payloadBytes)
            });
            await database.StringSetAsync(bytes, payloadBytes * 2L);

            Assert.True(outbox.TryTake(out var item));
            Assert.Equal(payload, item.Payload);
            Assert.True(outbox.TryDeadLetter(item));

            Assert.Equal(1, await database.SortedSetLengthAsync(dead));
            Assert.False((await database.SortedSetScoreAsync(dead, payload)).HasValue);
            Assert.True((await database.SortedSetScoreAsync(dead, handle)).HasValue);
            Assert.Equal((long?)payloadBytes, outbox.GetStatus().DeadLetterBytes);

            await database.KeyDeleteAsync(new[] { dead, payloads, sizes, bytes });
            await database.SortedSetAddAsync(dead, payload,
                DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeMilliseconds());
            await database.SortedSetAddAsync(dead, handle,
                DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds());
            await database.HashSetAsync(payloads, handle, payload);
            await database.HashSetAsync(sizes, new[]
            {
                new HashEntry(payload, payloadBytes),
                new HashEntry(handle, payloadBytes)
            });
            await database.StringSetAsync(bytes, payloadBytes * 2L);

            var migrated = Assert.Single(await outbox.ReadDeadLettersAsync(10,
                CancellationToken.None));
            Assert.Equal(handle, migrated.Handle);
            Assert.Equal(1, await database.SortedSetLengthAsync(dead));
            Assert.Equal((long?)payloadBytes, outbox.GetStatus().DeadLetterBytes);
        }
        finally
        {
            await database.KeyDeleteAsync(new[]
                { pending, processing, attempts, dead, payloads, sizes, bytes });
        }
    }

    /// <summary>
    /// 管理重放和删除操作保持死信载荷预算。
    /// </summary>
    [RedisHybridInvalidationOutboxFact]
    public async Task Outbox_AdminReplayAndDeleteMaintainPayloadBudget()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_HYBRID_OUTBOX_REDIS")!;
        var configuration = new CacheConfiguration(
            "hybrid-outbox-admin-" + Guid.NewGuid().ToString("N"),
            "hybrid-outbox-admin-" + Guid.NewGuid().ToString("N"));
        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        var pending = (RedisKey)(root + ":pending");
        var processing = (RedisKey)(root + ":processing");
        var attempts = (RedisKey)(root + ":attempts");
        var dead = (RedisKey)(root + ":dead");
        var sizes = (RedisKey)(root + ":sizes");
        var bytes = (RedisKey)(root + ":bytes");
        using var connection = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var database = connection.GetDatabase();
        using var outbox = new RedisHybridInvalidationOutbox(database, configuration,
            maximumMessages: 4, deadLetterMaxMessages: 4, deadLetterMaxBytes: 1_073_741_824);
        try
        {
            Assert.True(outbox.TryEnqueue("topic", CreateMessage("admin")));
            Assert.True(outbox.TryTake(out var item));
            Assert.True(outbox.TryDeadLetter(item));

            var records = await outbox.ReadDeadLettersAsync(10, CancellationToken.None);
            var record = Assert.Single(records);
            Assert.Equal(item.Payload, record.Payload);
            Assert.NotNull(record.DeadLetteredAtUtc);
            var beforeReplay = outbox.GetStatus();
            Assert.Equal(Encoding.UTF8.GetByteCount(item.Payload), beforeReplay.DeadLetterBytes);

            Assert.True(await outbox.ReplayDeadLetterAsync(record.Handle, CancellationToken.None));
            Assert.Equal(0, await database.SortedSetLengthAsync(dead));
            Assert.Equal(1, await database.ListLengthAsync(pending));
            Assert.False(await database.KeyExistsAsync(attempts));
            Assert.Equal(0L, outbox.GetStatus().DeadLetterBytes);

            Assert.True(outbox.TryTake(out var replayed));
            Assert.True(outbox.TryDeadLetter(replayed));
            var replayedRecord = Assert.Single(await outbox.ReadDeadLettersAsync(
                10, CancellationToken.None));
            Assert.True(await outbox.DeleteDeadLetterAsync(replayedRecord.Handle,
                CancellationToken.None));
            Assert.False(await outbox.DeleteDeadLetterAsync(replayedRecord.Handle,
                CancellationToken.None));
            Assert.Equal(0, await database.SortedSetLengthAsync(dead));
            Assert.Equal(0L, outbox.GetStatus().DeadLetterBytes);
        }
        finally
        {
            await database.KeyDeleteAsync(new[] { pending, processing, attempts, dead, sizes, bytes,
                (RedisKey)(root + ":dead-payloads") });
        }
    }

    /// <summary>
    /// 队列已满时管理重放保留死信记录。
    /// </summary>
    [RedisHybridInvalidationOutboxFact]
    public async Task Outbox_AdminReplayLeavesDeadLetterWhenQueueIsFull()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_HYBRID_OUTBOX_REDIS")!;
        var configuration = new CacheConfiguration(
            "hybrid-outbox-admin-capacity-" + Guid.NewGuid().ToString("N"),
            "hybrid-outbox-admin-capacity-" + Guid.NewGuid().ToString("N"));
        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        var pending = (RedisKey)(root + ":pending");
        var processing = (RedisKey)(root + ":processing");
        var attempts = (RedisKey)(root + ":attempts");
        var dead = (RedisKey)(root + ":dead");
        var sizes = (RedisKey)(root + ":sizes");
        var bytes = (RedisKey)(root + ":bytes");
        using var connection = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var database = connection.GetDatabase();
        using var outbox = new RedisHybridInvalidationOutbox(database, configuration,
            maximumMessages: 2, deadLetterMaxMessages: 4);
        try
        {
            Assert.True(outbox.TryEnqueue("topic", CreateMessage("admin")));
            Assert.True(outbox.TryTake(out var item));
            Assert.True(outbox.TryDeadLetter(item));
            Assert.True(outbox.TryEnqueue("topic", CreateMessage("occupied-1")));
            Assert.True(outbox.TryEnqueue("topic", CreateMessage("occupied-2")));
            var record = Assert.Single(await outbox.ReadDeadLettersAsync(10,
                CancellationToken.None));

            Assert.False(await outbox.ReplayDeadLetterAsync(record.Handle, CancellationToken.None));
            Assert.Equal(1, await database.SortedSetLengthAsync(dead));
            Assert.Equal(2, await database.ListLengthAsync(pending));

            Assert.True(outbox.TryTake(out var first));
            outbox.Acknowledge(first);
            Assert.True(outbox.TryTake(out var second));
            outbox.Acknowledge(second);
            Assert.True(await outbox.ReplayDeadLetterAsync(record.Handle, CancellationToken.None));
            Assert.Equal(1, await database.ListLengthAsync(pending));
        }
        finally
        {
            await database.KeyDeleteAsync(new[] { pending, processing, attempts, dead, sizes, bytes,
                (RedisKey)(root + ":dead-payloads") });
        }
    }

    /// <summary>
    /// 管理重放和删除并发执行时只有一个操作获胜。
    /// </summary>
    [RedisHybridInvalidationOutboxFact]
    public async Task Outbox_AdminConcurrentReplayAndDeleteHaveSingleWinner()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_HYBRID_OUTBOX_REDIS")!;
        var configuration = new CacheConfiguration(
            "hybrid-outbox-admin-race-" + Guid.NewGuid().ToString("N"),
            "hybrid-outbox-admin-race-" + Guid.NewGuid().ToString("N"));
        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        var pending = (RedisKey)(root + ":pending");
        var processing = (RedisKey)(root + ":processing");
        var attempts = (RedisKey)(root + ":attempts");
        var dead = (RedisKey)(root + ":dead");
        var sizes = (RedisKey)(root + ":sizes");
        var bytes = (RedisKey)(root + ":bytes");
        using var connection = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var database = connection.GetDatabase();
        using var first = new RedisHybridInvalidationOutbox(database, configuration, 4, 4, 1024 * 1024);
        using var second = new RedisHybridInvalidationOutbox(database, configuration, 4, 4, 1024 * 1024);
        try
        {
            Assert.True(first.TryEnqueue("topic", CreateMessage("race")));
            Assert.True(first.TryTake(out var item));
            Assert.True(first.TryDeadLetter(item));
            var record = Assert.Single(await first.ReadDeadLettersAsync(10, CancellationToken.None));

            var replayResults = await Task.WhenAll(
                first.ReplayDeadLetterAsync(record.Handle, CancellationToken.None),
                second.ReplayDeadLetterAsync(record.Handle, CancellationToken.None));
            Assert.Equal(1, replayResults.Count(value => value));
            Assert.Equal(1, await database.ListLengthAsync(pending));
            Assert.Equal(0, await database.SortedSetLengthAsync(dead));

            Assert.True(first.TryTake(out var replayed));
            Assert.True(first.TryDeadLetter(replayed));
            record = Assert.Single(await second.ReadDeadLettersAsync(10, CancellationToken.None));

            var deleteResults = await Task.WhenAll(
                first.DeleteDeadLetterAsync(record.Handle, CancellationToken.None),
                second.DeleteDeadLetterAsync(record.Handle, CancellationToken.None));
            Assert.Equal(1, deleteResults.Count(value => value));
            Assert.Equal(0, await database.SortedSetLengthAsync(dead));
            Assert.Equal(0L, first.GetStatus().DeadLetterBytes);
        }
        finally
        {
            await database.KeyDeleteAsync(new[] { pending, processing, attempts, dead, sizes, bytes,
                (RedisKey)(root + ":dead-payloads") });
        }
    }

    /// <summary>
    /// outbox 可读取缺少入队时间的信封。
    /// </summary>
    [RedisHybridInvalidationOutboxFact]
    public async Task Outbox_ReadsEnvelopeWithoutEnqueueTime()
    {
        var endpoint = Environment.GetEnvironmentVariable("BING_TEST_HYBRID_OUTBOX_REDIS")!;
        var configuration = new CacheConfiguration(
            "hybrid-outbox-legacy-" + Guid.NewGuid().ToString("N"),
            "hybrid-outbox-legacy-" + Guid.NewGuid().ToString("N"));
        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        var pending = (RedisKey)(root + ":pending");
        var processing = (RedisKey)(root + ":processing");
        var attempts = (RedisKey)(root + ":attempts");
        var dead = (RedisKey)(root + ":dead");
        using var connection = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var database = connection.GetDatabase();
        using var outbox = new RedisHybridInvalidationOutbox(database, configuration, 2);
        try
        {
            await database.ListLeftPushAsync(pending,
                "{\"Topic\":\"topic\",\"Id\":\"legacy\",\"CacheKeys\":[\"legacy:key\"],\"IsPrefix\":false,\"IsPattern\":false}");

            Assert.True(outbox.TryTake(out var item));
            Assert.Equal("legacy", item.Message.Id);
            var status = outbox.GetStatus();
            Assert.Equal(1, status.ProcessingMessages);
            Assert.Null(status.OldestProcessingEnqueuedAtUtc);
            outbox.Acknowledge(item);
            Assert.Equal(0, await database.ListLengthAsync(processing));
        }
        finally
        {
            await database.KeyDeleteAsync(new[] { pending, processing, attempts, dead,
                (RedisKey)(root + ":dead-payloads") });
        }
    }

    /// <summary>
    /// 创建包含指定逻辑键的失效消息。
    /// </summary>
    /// <param name="key">逻辑缓存键。</param>
    /// <returns>返回包含指定逻辑键和默认消息元数据的失效消息。</returns>
    private static EasyCachingMessage CreateMessage(string key) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        CacheKeys = new[] { key }
    };
}
