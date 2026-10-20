using Bing.Caching;
using EasyCaching.Core.Bus;
using StackExchange.Redis;
using System.Text.Json;

namespace Bing.EasyCaching;

/// <summary>
/// 使用 Redis 实现 Hybrid 失效消息 outbox。
/// </summary>
/// <remarks>按配置维护投递尝试计数、dead-letter 队列和 payload 字节预算。</remarks>
internal sealed class RedisHybridInvalidationOutbox : IHybridInvalidationOutbox,
    IHybridInvalidationDeadLetterAdminOutbox
{
    /// <summary>
    /// 在容量允许时原子写入待处理队列。
    /// </summary>
    private const string EnqueueScript =
        "local pending = redis.call('LLEN', KEYS[1]); " +
        "local processing = redis.call('LLEN', KEYS[2]); " +
        "if pending + processing >= tonumber(ARGV[2]) then return 0 end; " +
        "redis.call('LPUSH', KEYS[1], ARGV[1]); return 1;";

    /// <summary>
    /// 确认已发布消息并删除其投递次数记录。
    /// </summary>
    private const string AcknowledgeScript =
        "local removed = redis.call('LREM', KEYS[1], 1, ARGV[1]); " +
        "redis.call('HDEL', KEYS[2], ARGV[1]); return removed;";

    /// <summary>
    /// 原子迁移达到条件的消息到 dead-letter 并维护容量统计。
    /// </summary>
    private const string DeadLetterScript =
        "local maxBytes = tonumber(ARGV[4]) or 0; " +
        "local payloadBytes = string.len(ARGV[1]); " +
        "local existing = redis.call('ZSCORE', KEYS[2], ARGV[2]); " +
        "if ARGV[1] ~= ARGV[2] then " +
        "local legacyScore = redis.call('ZSCORE', KEYS[2], ARGV[1]); " +
        "if legacyScore then " +
        "local stored = redis.call('HGET', KEYS[6], ARGV[2]); " +
        "if stored and stored ~= ARGV[1] then return redis.error_reply('dead-letter handle collision'); end; " +
        "redis.call('ZREM', KEYS[2], ARGV[1]); " +
        "if not existing then redis.call('ZADD', KEYS[2], legacyScore, ARGV[2]); end; " +
        "local legacySize = redis.call('HGET', KEYS[4], ARGV[1]); " +
        "redis.call('HDEL', KEYS[4], ARGV[1]); " +
        "if existing then if maxBytes > 0 then " +
        "local current = tonumber(redis.call('GET', KEYS[5]) or ''); " +
        "local size = tonumber(legacySize); " +
        "if current and size then local next = current - size; " +
        "if next <= 0 then redis.call('DEL', KEYS[5]); else redis.call('SET', KEYS[5], next); end; " +
        "else redis.call('DEL', KEYS[5]); end; end; " +
        "elseif legacySize then redis.call('HSET', KEYS[4], ARGV[2], legacySize); end; " +
        "redis.call('HSET', KEYS[6], ARGV[2], ARGV[1]); " +
        "existing = redis.call('ZSCORE', KEYS[2], ARGV[2]); end; end; " +
        "if not existing and redis.call('ZCARD', KEYS[2]) >= tonumber(ARGV[3]) then return 0 end; " +
        "if maxBytes > 0 then " +
        "local count = redis.call('ZCARD', KEYS[2]); " +
        "local known = redis.call('HLEN', KEYS[4]); " +
        "local current = tonumber(redis.call('GET', KEYS[5]) or ''); " +
        "if current == nil or known ~= count or (count == 0 and current ~= 0) then " +
        "local total = 0; redis.call('DEL', KEYS[4]); " +
        "local entries = redis.call('ZRANGE', KEYS[2], 0, -1); " +
        "for _, member in ipairs(entries) do local payload = redis.call('HGET', KEYS[6], member) or member; " +
        "local size = string.len(payload); " +
        "redis.call('HSET', KEYS[4], member, size); total = total + size; end; " +
        "if total == 0 then redis.call('DEL', KEYS[5]); " +
        "else redis.call('SET', KEYS[5], total); end; current = total; end; " +
        "if not existing and current + payloadBytes > maxBytes then return 0 end; end; " +
        "local removed = redis.call('LREM', KEYS[1], 1, ARGV[1]); " +
        "if removed == 0 then return -1 end; " +
        "redis.call('ZADD', KEYS[2], tonumber(ARGV[5]), ARGV[2]); " +
        "redis.call('HSET', KEYS[6], ARGV[2], ARGV[1]); " +
        "redis.call('HDEL', KEYS[3], ARGV[1]); " +
        "if maxBytes > 0 and not existing then " +
        "redis.call('HSET', KEYS[4], ARGV[2], payloadBytes); " +
        "redis.call('INCRBY', KEYS[5], payloadBytes); end; return 1;";

    /// <summary>
    /// 清理过期 dead-letter 并维护载荷字节数。
    /// </summary>
    private const string CleanupDeadLetterScript =
        "local entries = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1], " +
        "'LIMIT', 0, ARGV[2]); " +
        "local track = tonumber(ARGV[3]) or 0; local total = 0; " +
        "if track > 0 then " +
        "local count = redis.call('ZCARD', KEYS[1]); " +
        "local known = redis.call('HLEN', KEYS[2]); " +
        "local current = tonumber(redis.call('GET', KEYS[3]) or ''); " +
        "if current == nil or known ~= count or (count == 0 and current ~= 0) then " +
        "total = 0; redis.call('DEL', KEYS[2]); " +
        "local all = redis.call('ZRANGE', KEYS[1], 0, -1); " +
        "for _, member in ipairs(all) do local payload = redis.call('HGET', KEYS[4], member) or member; " +
        "local size = string.len(payload); " +
        "redis.call('HSET', KEYS[2], member, size); total = total + size; end; " +
        "if total == 0 then redis.call('DEL', KEYS[3]); " +
        "else redis.call('SET', KEYS[3], total); end; " +
        "else total = current; end; end; " +
        "local removed = 0; " +
        "for _, member in ipairs(entries) do " +
        "local payload = redis.call('HGET', KEYS[4], member) or member; " +
        "if redis.call('ZREM', KEYS[1], member) == 1 then removed = removed + 1; " +
        "redis.call('HDEL', KEYS[4], member); " +
        "local size = tonumber(redis.call('HGET', KEYS[2], member)) or " +
        "tonumber(redis.call('HGET', KEYS[2], payload)) or string.len(payload); " +
        "if track > 0 then total = total - size; end; " +
        "redis.call('HDEL', KEYS[2], member); " +
        "if member ~= payload then redis.call('HDEL', KEYS[2], payload); end; end; end; " +
        "if track > 0 then if total <= 0 then redis.call('DEL', KEYS[3]); " +
        "else redis.call('SET', KEYS[3], total); end; end; return removed;";

    /// <summary>
    /// 校准并返回 dead-letter 载荷总字节数。
    /// </summary>
    private const string GetDeadLetterBytesScript =
        "local count = redis.call('ZCARD', KEYS[1]); " +
        "local known = redis.call('HLEN', KEYS[2]); " +
        "local current = tonumber(redis.call('GET', KEYS[3]) or ''); " +
        "if current == nil or known ~= count or (count == 0 and current ~= 0) then " +
        "local total = 0; redis.call('DEL', KEYS[2]); " +
        "local entries = redis.call('ZRANGE', KEYS[1], 0, -1); " +
        "for _, member in ipairs(entries) do local payload = redis.call('HGET', KEYS[4], member) or member; " +
        "local size = string.len(payload); " +
        "redis.call('HSET', KEYS[2], member, size); total = total + size; end; " +
        "if total == 0 then redis.call('DEL', KEYS[3]); " +
        "else redis.call('SET', KEYS[3], total); end; current = total; end; " +
        "return current or 0;";

    /// <summary>
    /// 将 dead-letter 消息重新放回待处理队列。
    /// </summary>
    private const string ReplayDeadLetterScript =
        "local track = tonumber(ARGV[3]) or 0; " +
        "local payload = redis.call('HGET', KEYS[7], ARGV[1]); " +
        "if not payload or redis.call('ZSCORE', KEYS[1], ARGV[1]) == false then return -1 end; " +
        "if redis.call('LLEN', KEYS[2]) + redis.call('LLEN', KEYS[3]) >= tonumber(ARGV[2]) then return 0 end; " +
        "if track > 0 then " +
        "local count = redis.call('ZCARD', KEYS[1]); local known = redis.call('HLEN', KEYS[5]); " +
        "local current = tonumber(redis.call('GET', KEYS[6]) or ''); " +
        "if current == nil or known ~= count or (count == 0 and current ~= 0) then " +
        "local total = 0; redis.call('DEL', KEYS[5]); " +
        "local entries = redis.call('ZRANGE', KEYS[1], 0, -1); " +
        "for _, member in ipairs(entries) do local item = redis.call('HGET', KEYS[7], member) or member; " +
        "local size = string.len(item); " +
        "redis.call('HSET', KEYS[5], member, size); total = total + size; end; " +
        "if total == 0 then redis.call('DEL', KEYS[6]); " +
        "else redis.call('SET', KEYS[6], total); end; end; end; " +
        "local size = track > 0 and (tonumber(redis.call('HGET', KEYS[5], ARGV[1])) or string.len(payload)) or 0; " +
        "local removed = redis.call('ZREM', KEYS[1], ARGV[1]); " +
        "if removed == 0 then return -1 end; redis.call('LPUSH', KEYS[2], payload); " +
        "redis.call('HDEL', KEYS[4], payload); redis.call('HDEL', KEYS[5], ARGV[1]); " +
        "redis.call('HDEL', KEYS[7], ARGV[1]); " +
        "if track > 0 then " +
        "local current = tonumber(redis.call('GET', KEYS[6]) or '0') or 0; " +
        "local next = current - size; if next <= 0 then redis.call('DEL', KEYS[6]); " +
        "else redis.call('SET', KEYS[6], next); end; end; return 1;";

    /// <summary>
    /// 删除指定 dead-letter 并更新容量统计。
    /// </summary>
    private const string DeleteDeadLetterScript =
        "local track = tonumber(ARGV[2]) or 0; " +
        "local payload = redis.call('HGET', KEYS[4], ARGV[1]); " +
        "if not payload or redis.call('ZSCORE', KEYS[1], ARGV[1]) == false then return 0 end; " +
        "if track > 0 then " +
        "local count = redis.call('ZCARD', KEYS[1]); local known = redis.call('HLEN', KEYS[2]); " +
        "local current = tonumber(redis.call('GET', KEYS[3]) or ''); " +
        "if current == nil or known ~= count or (count == 0 and current ~= 0) then " +
        "local total = 0; redis.call('DEL', KEYS[2]); " +
        "local entries = redis.call('ZRANGE', KEYS[1], 0, -1); " +
        "for _, member in ipairs(entries) do local item = redis.call('HGET', KEYS[4], member) or member; " +
        "local size = string.len(item); " +
        "redis.call('HSET', KEYS[2], member, size); total = total + size; end; " +
        "if total == 0 then redis.call('DEL', KEYS[3]); " +
        "else redis.call('SET', KEYS[3], total); end; end; end; " +
        "local removed = redis.call('ZREM', KEYS[1], ARGV[1]); if removed == 0 then return 0 end; " +
        "local size = tonumber(redis.call('HGET', KEYS[2], ARGV[1])) or string.len(payload); " +
        "redis.call('HDEL', KEYS[2], ARGV[1]); redis.call('HDEL', KEYS[4], ARGV[1]); if track > 0 then " +
        "local current = tonumber(redis.call('GET', KEYS[3]) or '0') or 0; " +
        "local next = current - size; if next <= 0 then redis.call('DEL', KEYS[3]); " +
        "else redis.call('SET', KEYS[3], next); end; end; return 1;";

    /// <summary>
    /// 将旧版 payload 键迁移为不透明句柄索引。
    /// </summary>
    private const string MigrateLegacyDeadLettersScript =
        "for index = 1, #ARGV, 2 do " +
        "local payload = ARGV[index]; local handle = ARGV[index + 1]; " +
        "local existing = redis.call('HGET', KEYS[2], handle); " +
        "if existing and existing ~= payload then return redis.error_reply('dead-letter handle collision'); end; end; " +
        "for index = 1, #ARGV, 2 do " +
        "local payload = ARGV[index]; local handle = ARGV[index + 1]; " +
        "local score = redis.call('ZSCORE', KEYS[1], payload); " +
        "if score then " +
        "local size = redis.call('HGET', KEYS[3], payload); " +
        "local handleScore = redis.call('ZSCORE', KEYS[1], handle); " +
        "if handleScore then " +
        "local current = tonumber(redis.call('GET', KEYS[4]) or ''); local bytes = tonumber(size); " +
        "if current and bytes then local next = current - bytes; " +
        "if next <= 0 then redis.call('DEL', KEYS[4]); else redis.call('SET', KEYS[4], next); end; " +
        "else redis.call('DEL', KEYS[4]); end; " +
        "redis.call('ZREM', KEYS[1], payload); redis.call('HDEL', KEYS[3], payload); " +
        "else redis.call('ZREM', KEYS[1], payload); redis.call('ZADD', KEYS[1], score, handle); " +
        "if size then redis.call('HDEL', KEYS[3], payload); redis.call('HSET', KEYS[3], handle, size); end; end; " +
        "redis.call('HSET', KEYS[2], handle, payload); end; end; " +
        "return 1;";

    /// <summary>
    /// 存储 outbox 队列和状态的 Redis 数据库。
    /// </summary>
    private readonly IDatabase _database;
    /// <summary>
    /// 当前缓存命名空间下待处理消息列表的 Redis 物理键（<c>:pending</c> 后缀）。
    /// </summary>
    private readonly RedisKey _pendingKey;
    /// <summary>
    /// 当前缓存命名空间下正在发布消息列表的 Redis 物理键（<c>:processing</c> 后缀）。
    /// </summary>
    private readonly RedisKey _processingKey;
    /// <summary>
    /// 当前缓存命名空间下消息投递次数哈希表的 Redis 物理键（<c>:attempts</c> 后缀）。
    /// </summary>
    private readonly RedisKey _attemptsKey;
    /// <summary>
    /// 当前缓存命名空间下 dead-letter 句柄及时间分值集合的 Redis 物理键（<c>:dead</c> 后缀）。
    /// </summary>
    private readonly RedisKey _deadLetterKey;
    /// <summary>
    /// 当前缓存命名空间下 dead-letter 句柄到原始载荷映射的 Redis 物理键（<c>:dead-payloads</c> 后缀）。
    /// </summary>
    private readonly RedisKey _deadLetterPayloadsKey;
    /// <summary>
    /// 当前缓存命名空间下 dead-letter 载荷大小映射的 Redis 物理键（<c>:sizes</c> 后缀）。
    /// </summary>
    private readonly RedisKey _deadLetterSizesKey;
    /// <summary>
    /// 当前缓存命名空间下 dead-letter 载荷总字节数的 Redis 物理键（<c>:bytes</c> 后缀）。
    /// </summary>
    private readonly RedisKey _deadLetterBytesKey;
    /// <summary>
    /// 待处理与处理中消息合计容量上限。
    /// </summary>
    private readonly int _maximumMessages;
    /// <summary>
    /// dead-letter 消息容量上限；零表示禁用 dead-letter。
    /// </summary>
    private readonly int _deadLetterMaxMessages;
    /// <summary>
    /// dead-letter 载荷字节上限；零表示不跟踪字节预算。
    /// </summary>
    private readonly long _deadLetterMaxBytes;
    /// <summary>
    /// 记录当前 outbox 是否已经释放；非零表示已释放。
    /// </summary>
    private int _disposed;

    /// <summary>
    /// 初始化一个 <see cref="RedisHybridInvalidationOutbox"/> 类型的实例。
    /// </summary>
    /// <param name="database">存储队列数据的 Redis 数据库。</param>
    /// <param name="configuration">缓存命名空间配置。</param>
    /// <param name="maximumMessages">待处理与处理中消息的总容量上限。</param>
    /// <param name="deadLetterMaxMessages">dead-letter 容量上限；零表示禁用。</param>
    /// <param name="deadLetterMaxBytes">dead-letter 载荷字节上限；零表示不限制。</param>
    internal RedisHybridInvalidationOutbox(IDatabase database, CacheConfiguration configuration,
        int maximumMessages, int deadLetterMaxMessages = 0, long deadLetterMaxBytes = 0)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (maximumMessages <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumMessages));
        if (deadLetterMaxMessages < 0)
            throw new ArgumentOutOfRangeException(nameof(deadLetterMaxMessages));
        if (deadLetterMaxBytes < 0 || deadLetterMaxBytes > 1_073_741_824)
            throw new ArgumentOutOfRangeException(nameof(deadLetterMaxBytes));

        var root = CacheKeyFormat.GetPhysicalKey(configuration, "__hybrid_invalidation_outbox__");
        _pendingKey = root + ":pending";
        _processingKey = root + ":processing";
        _attemptsKey = root + ":attempts";
        _deadLetterKey = root + ":dead";
        _deadLetterPayloadsKey = root + ":dead-payloads";
        _deadLetterSizesKey = root + ":sizes";
        _deadLetterBytesKey = root + ":bytes";
        _maximumMessages = maximumMessages;
        _deadLetterMaxMessages = deadLetterMaxMessages;
        _deadLetterMaxBytes = deadLetterMaxBytes;
    }

    /// <inheritdoc />
    public bool TryEnqueue(string topic, EasyCachingMessage message)
    {
        ThrowIfDisposed();
        var payload = Serialize(topic, message);
        var result = _database.ScriptEvaluate(EnqueueScript,
            new RedisKey[] { _pendingKey, _processingKey },
            new RedisValue[] { payload, _maximumMessages });
        return (long)result == 1;
    }

    /// <inheritdoc />
    public long CleanupExpiredDeadLetters(DateTimeOffset cutoffUtc, int maximumMessages)
    {
        ThrowIfDisposed();
        ValidateCleanupBatchSize(maximumMessages);
        var result = _database.ScriptEvaluate(CleanupDeadLetterScript,
            new RedisKey[]
            {
                _deadLetterKey, _deadLetterSizesKey, _deadLetterBytesKey,
                _deadLetterPayloadsKey
            },
            new RedisValue[]
            {
                cutoffUtc.ToUnixTimeMilliseconds(), maximumMessages,
                _deadLetterMaxBytes > 0 ? 1 : 0
            });
        return (long)result;
    }

    /// <inheritdoc />
    public async Task<long> CleanupExpiredDeadLettersAsync(DateTimeOffset cutoffUtc,
        int maximumMessages, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCleanupBatchSize(maximumMessages);
        var result = await _database.ScriptEvaluateAsync(CleanupDeadLetterScript,
            new RedisKey[]
            {
                _deadLetterKey, _deadLetterSizesKey, _deadLetterBytesKey,
                _deadLetterPayloadsKey
            },
            new RedisValue[]
            {
                cutoffUtc.ToUnixTimeMilliseconds(), maximumMessages,
                _deadLetterMaxBytes > 0 ? 1 : 0
            })
            .ConfigureAwait(false);
        return (long)result;
    }

    /// <inheritdoc />
    public async Task<bool> TryEnqueueAsync(string topic, EasyCachingMessage message,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var payload = Serialize(topic, message);
        var result = await _database.ScriptEvaluateAsync(EnqueueScript,
            new RedisKey[] { _pendingKey, _processingKey },
            new RedisValue[] { payload, _maximumMessages }).ConfigureAwait(false);
        return (long)result == 1;
    }

    /// <inheritdoc />
    public bool TryTake(out HybridInvalidationOutboxItem item)
    {
        ThrowIfDisposed();
        var payload = _database.ListGetByIndex(_processingKey, -1);
        if (payload.IsNullOrEmpty)
            payload = _database.ListRightPopLeftPush(_pendingKey, _processingKey);
        if (payload.IsNullOrEmpty)
        {
            item = null;
            return false;
        }

        var deliveryAttempt = _deadLetterMaxMessages > 0
            ? _database.HashIncrement(_attemptsKey, payload, 1)
            : 1;
        item = Deserialize(payload, deliveryAttempt);
        return true;
    }

    /// <inheritdoc />
    public async Task<HybridInvalidationOutboxItem> TryTakeAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var payload = await _database.ListGetByIndexAsync(_processingKey, -1)
            .ConfigureAwait(false);
        if (payload.IsNullOrEmpty)
            payload = await _database.ListRightPopLeftPushAsync(_pendingKey, _processingKey)
                .ConfigureAwait(false);
        if (payload.IsNullOrEmpty)
            return null;
        var deliveryAttempt = _deadLetterMaxMessages > 0
            ? await _database.HashIncrementAsync(_attemptsKey, payload, 1).ConfigureAwait(false)
            : 1;
        return Deserialize(payload, deliveryAttempt);
    }

    /// <inheritdoc />
    public bool TryDeadLetter(HybridInvalidationOutboxItem item)
    {
        ThrowIfDisposed();
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        if (_deadLetterMaxMessages <= 0)
            return false;

        var result = _database.ScriptEvaluate(DeadLetterScript,
            new RedisKey[]
            {
                _processingKey, _deadLetterKey, _attemptsKey,
                _deadLetterSizesKey, _deadLetterBytesKey, _deadLetterPayloadsKey
            },
            new RedisValue[]
            {
                item.Payload,
                HybridInvalidationDeadLetterHandle.Create(item.Payload),
                _deadLetterMaxMessages,
                _deadLetterMaxBytes,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        return (long)result == 1;
    }

    /// <inheritdoc />
    public async Task<bool> TryDeadLetterAsync(HybridInvalidationOutboxItem item,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        cancellationToken.ThrowIfCancellationRequested();
        if (_deadLetterMaxMessages <= 0)
            return false;

        var result = await _database.ScriptEvaluateAsync(DeadLetterScript,
            new RedisKey[]
            {
                _processingKey, _deadLetterKey, _attemptsKey,
                _deadLetterSizesKey, _deadLetterBytesKey, _deadLetterPayloadsKey
            },
            new RedisValue[]
            {
                item.Payload,
                HybridInvalidationDeadLetterHandle.Create(item.Payload),
                _deadLetterMaxMessages,
                _deadLetterMaxBytes,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }).ConfigureAwait(false);
        return (long)result == 1;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HybridInvalidationDeadLetterRecord>> ReadDeadLettersAsync(
        int maximumMessages, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ValidateAdminBatchSize(maximumMessages);
        var entries = await _database.SortedSetRangeByRankWithScoresAsync(
            _deadLetterKey, 0, maximumMessages - 1, Order.Ascending).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (entries.Length == 0)
            return Array.Empty<HybridInvalidationDeadLetterRecord>();

        var members = entries.Select(entry => (RedisValue)entry.Element).ToArray();
        var payloadValues = await _database.HashGetAsync(_deadLetterPayloadsKey, members)
            .ConfigureAwait(false);
        var migrationArguments = new List<RedisValue>();
        for (var index = 0; index < entries.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var member = entries[index].Element.ToString();
            var payload = payloadValues[index].IsNull ? member : payloadValues[index].ToString();
            var handle = payloadValues[index].IsNull
                ? HybridInvalidationDeadLetterHandle.Create(payload)
                : member;
            if (payloadValues[index].IsNull)
            {
                migrationArguments.Add(payload);
                migrationArguments.Add(handle);
            }
        }
        if (migrationArguments.Count > 0)
        {
            await _database.ScriptEvaluateAsync(MigrateLegacyDeadLettersScript,
                new RedisKey[]
                {
                    _deadLetterKey, _deadLetterPayloadsKey, _deadLetterSizesKey,
                    _deadLetterBytesKey
                },
                migrationArguments.ToArray()).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            // Migration can remove duplicate legacy members from the page snapshot.
            entries = await _database.SortedSetRangeByRankWithScoresAsync(
                _deadLetterKey, 0, maximumMessages - 1, Order.Ascending).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (entries.Length == 0)
                return Array.Empty<HybridInvalidationDeadLetterRecord>();
            payloadValues = await _database.HashGetAsync(_deadLetterPayloadsKey,
                entries.Select(entry => (RedisValue)entry.Element).ToArray()).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        var records = new List<HybridInvalidationDeadLetterRecord>(entries.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var member = entries[index].Element.ToString();
            var payload = payloadValues[index].IsNull ? member : payloadValues[index].ToString();
            var handle = payloadValues[index].IsNull
                ? HybridInvalidationDeadLetterHandle.Create(payload)
                : member;
            records.Add(new HybridInvalidationDeadLetterRecord(payload,
                GetDeadLetteredAt(entries[index].Score), handle));
        }
        return records;
    }

    /// <inheritdoc />
    public async Task<string> GetDeadLetterPayloadAsync(string handle,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ValidateHandle(handle);
        cancellationToken.ThrowIfCancellationRequested();
        var payload = await _database.HashGetAsync(_deadLetterPayloadsKey, handle)
            .ConfigureAwait(false);
        if (payload.IsNull)
            return null;
        var value = payload.ToString();
        if (!string.Equals(HybridInvalidationDeadLetterHandle.Create(value), handle,
                StringComparison.Ordinal))
            throw new InvalidOperationException("Hybrid dead-letter handle 索引与 payload 不匹配。");
        return value;
    }

    /// <inheritdoc />
    public async Task<bool> ReplayDeadLetterAsync(string handle,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ValidateHandle(handle);
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _database.ScriptEvaluateAsync(ReplayDeadLetterScript,
            new RedisKey[]
            {
                _deadLetterKey, _pendingKey, _processingKey, _attemptsKey,
                _deadLetterSizesKey, _deadLetterBytesKey, _deadLetterPayloadsKey
            },
            new RedisValue[]
            {
                handle, _maximumMessages, _deadLetterMaxBytes > 0 ? 1 : 0
            }).ConfigureAwait(false);
        return (long)result == 1;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDeadLetterAsync(string handle,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ValidateHandle(handle);
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _database.ScriptEvaluateAsync(DeleteDeadLetterScript,
            new RedisKey[]
            {
                _deadLetterKey, _deadLetterSizesKey, _deadLetterBytesKey,
                _deadLetterPayloadsKey
            },
            new RedisValue[] { handle, _deadLetterMaxBytes > 0 ? 1 : 0 })
            .ConfigureAwait(false);
        return (long)result == 1;
    }

    /// <inheritdoc />
    public HybridInvalidationOutboxStatus GetStatus()
    {
        ThrowIfDisposed();
        var pendingMessages = _database.ListLength(_pendingKey);
        var processingMessages = _database.ListLength(_processingKey);
        var deadLetterMessages = _database.SortedSetLength(_deadLetterKey);
        var oldestPending = pendingMessages == 0
            ? RedisValue.Null
            : _database.ListGetByIndex(_pendingKey, -1);
        var oldestProcessing = processingMessages == 0
            ? RedisValue.Null
            : _database.ListGetByIndex(_processingKey, -1);
        var oldestDeadLetter = deadLetterMessages == 0
            ? Array.Empty<SortedSetEntry>()
            : _database.SortedSetRangeByRankWithScores(_deadLetterKey, 0, 0, Order.Ascending);
        long? deadLetterBytes = _deadLetterMaxBytes > 0
            ? (long)_database.ScriptEvaluate(GetDeadLetterBytesScript,
                new RedisKey[]
                {
                    _deadLetterKey, _deadLetterSizesKey, _deadLetterBytesKey,
                    _deadLetterPayloadsKey
                })
            : null;
        return new HybridInvalidationOutboxStatus(pendingMessages, processingMessages,
            deadLetterMessages, GetEnqueuedAt(oldestPending), GetEnqueuedAt(oldestProcessing),
            GetDeadLetteredAt(oldestDeadLetter), deadLetterBytes);
    }

    /// <inheritdoc />
    public async Task<HybridInvalidationOutboxStatus> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var pendingTask = _database.ListLengthAsync(_pendingKey);
        var processingTask = _database.ListLengthAsync(_processingKey);
        var deadLetterTask = _database.SortedSetLengthAsync(_deadLetterKey);
        var deadLetterBytesTask = _deadLetterMaxBytes > 0
            ? _database.ScriptEvaluateAsync(GetDeadLetterBytesScript,
                new RedisKey[]
                {
                    _deadLetterKey, _deadLetterSizesKey, _deadLetterBytesKey,
                    _deadLetterPayloadsKey
                })
            : null;
        await Task.WhenAll(pendingTask, processingTask, deadLetterTask).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var pendingMessages = pendingTask.Result;
        var processingMessages = processingTask.Result;
        var deadLetterMessages = deadLetterTask.Result;
        long? deadLetterBytes = null;
        if (deadLetterBytesTask != null)
            deadLetterBytes = (long)await deadLetterBytesTask.ConfigureAwait(false);
        var oldestPendingTask = pendingMessages == 0
            ? Task.FromResult((RedisValue)RedisValue.Null)
            : _database.ListGetByIndexAsync(_pendingKey, -1);
        var oldestProcessingTask = processingMessages == 0
            ? Task.FromResult((RedisValue)RedisValue.Null)
            : _database.ListGetByIndexAsync(_processingKey, -1);
        var oldestDeadLetterTask = deadLetterMessages == 0
            ? Task.FromResult(Array.Empty<SortedSetEntry>())
            : _database.SortedSetRangeByRankWithScoresAsync(_deadLetterKey, 0, 0, Order.Ascending);
        await Task.WhenAll(oldestPendingTask, oldestProcessingTask, oldestDeadLetterTask)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new HybridInvalidationOutboxStatus(pendingMessages, processingMessages,
            deadLetterMessages, GetEnqueuedAt(oldestPendingTask.Result),
            GetEnqueuedAt(oldestProcessingTask.Result), GetDeadLetteredAt(oldestDeadLetterTask.Result),
            deadLetterBytes);
    }

    /// <inheritdoc />
    public void Acknowledge(HybridInvalidationOutboxItem item)
    {
        ThrowIfDisposed();
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        _database.ScriptEvaluate(AcknowledgeScript,
            new RedisKey[] { _processingKey, _attemptsKey },
            new RedisValue[] { item.Payload });
    }

    /// <inheritdoc />
    public async Task AcknowledgeAsync(HybridInvalidationOutboxItem item,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        cancellationToken.ThrowIfCancellationRequested();
        await _database.ScriptEvaluateAsync(AcknowledgeScript,
            new RedisKey[] { _processingKey, _attemptsKey },
            new RedisValue[] { item.Payload }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

    /// <summary>
    /// 将失效消息及主题序列化为 outbox 载荷。
    /// </summary>
    /// <param name="topic">消息主题。</param>
    /// <param name="message">失效消息。</param>
    /// <returns>包含入队时间的 JSON 载荷。</returns>
    private static string Serialize(string topic, EasyCachingMessage message)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("失效消息主题不能为空。", nameof(topic));
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        return JsonSerializer.Serialize(new Envelope
        {
            Topic = topic,
            Id = message.Id,
            CacheKeys = message.CacheKeys,
            IsPrefix = message.IsPrefix,
            IsPattern = message.IsPattern,
            EnqueuedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    /// <summary>
    /// 从 outbox 载荷读取入队时间。
    /// </summary>
    /// <param name="payload">消息载荷。</param>
    /// <returns>入队时间；载荷无效或缺少时间时返回 <see langword="null"/>。</returns>
    private static DateTimeOffset? GetEnqueuedAt(RedisValue payload)
    {
        if (payload.IsNullOrEmpty)
            return null;
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(payload.ToString());
            if (envelope == null || envelope.EnqueuedAtUnixMilliseconds <= 0)
                return null;
            return DateTimeOffset.FromUnixTimeMilliseconds(envelope.EnqueuedAtUnixMilliseconds);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// 获取 dead-letter 记录时间。
    /// </summary>
    /// <param name="entries">按时间升序的 dead-letter 项。</param>
    /// <returns>最早记录时间；结果为空或时间无效时返回 <see langword="null"/>。</returns>
    private static DateTimeOffset? GetDeadLetteredAt(SortedSetEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
            return null;
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(entries[0].Score));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// 获取 dead-letter 记录时间。
    /// </summary>
    /// <param name="score">Unix 毫秒时间分值。</param>
    /// <returns>记录时间；分值超出时间范围时返回 <see langword="null"/>。</returns>
    private static DateTimeOffset? GetDeadLetteredAt(double score)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(score));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// 将 Redis 载荷解码为 outbox 消息。
    /// </summary>
    /// <param name="payload">序列化消息载荷。</param>
    /// <param name="deliveryAttempt">当前投递次数。</param>
    /// <returns>解码后的 outbox 消息。</returns>
    private static HybridInvalidationOutboxItem Deserialize(RedisValue payload,
        long deliveryAttempt)
    {
        var raw = payload.ToString();
        var envelope = JsonSerializer.Deserialize<Envelope>(raw);
        if (envelope == null || string.IsNullOrWhiteSpace(envelope.Topic) ||
            string.IsNullOrWhiteSpace(envelope.Id) || envelope.CacheKeys == null)
            throw new InvalidOperationException("Hybrid Redis outbox 消息格式无效。");

        return new HybridInvalidationOutboxItem(raw, envelope.Topic,
            new EasyCachingMessage
            {
                Id = envelope.Id,
                CacheKeys = envelope.CacheKeys,
                IsPrefix = envelope.IsPrefix,
                IsPattern = envelope.IsPattern
            }, deliveryAttempt);
    }

    /// <summary>
    /// 检查当前 outbox 是否已经释放。
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(RedisHybridInvalidationOutbox));
    }

    /// <summary>
    /// 验证过期消息清理的单次数量上限。
    /// </summary>
    /// <param name="maximumMessages">请求清理的最大消息数。</param>
    private static void ValidateCleanupBatchSize(int maximumMessages)
    {
        if (maximumMessages <= 0 || maximumMessages > 10_000)
            throw new ArgumentOutOfRangeException(nameof(maximumMessages));
    }

    /// <summary>
    /// 验证 dead-letter 管理读取的单次数量上限。
    /// </summary>
    /// <param name="maximumMessages">请求读取的最大消息数。</param>
    private static void ValidateAdminBatchSize(int maximumMessages)
    {
        if (maximumMessages <= 0 || maximumMessages > 1_000)
            throw new ArgumentOutOfRangeException(nameof(maximumMessages));
    }

    /// <summary>
    /// 验证 dead-letter 管理句柄格式。
    /// </summary>
    /// <param name="handle">dead-letter 句柄。</param>
    private static void ValidateHandle(string handle)
    {
        if (!HybridInvalidationDeadLetterHandle.IsValid(handle))
            throw new ArgumentException("Hybrid dead-letter handle 格式无效。", nameof(handle));
    }

    /// <summary>
    /// 表示 Redis outbox 中持久化的失效消息信封。
    /// </summary>
    private sealed class Envelope
    {
        /// <summary>
        /// 获取或设置失效消息主题。
        /// </summary>
        public string Topic { get; set; }
        /// <summary>
        /// 获取或设置失效消息标识。
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// 获取或设置待失效的物理缓存键。
        /// </summary>
        public string[] CacheKeys { get; set; }
        /// <summary>
        /// 获取或设置是否按键前缀执行失效。
        /// </summary>
        public bool IsPrefix { get; set; }
        /// <summary>
        /// 获取或设置是否按键模式执行失效。
        /// </summary>
        public bool IsPattern { get; set; }
        /// <summary>
        /// 获取或设置消息进入 outbox 的 Unix 毫秒时间戳。
        /// </summary>
        public long EnqueuedAtUnixMilliseconds { get; set; }
    }
}
