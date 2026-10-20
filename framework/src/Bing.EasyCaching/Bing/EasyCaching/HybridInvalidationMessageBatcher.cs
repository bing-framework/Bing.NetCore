using Bing.Caching;
using EasyCaching.Core.Bus;
using EasyCaching.Core.Serialization;

namespace Bing.EasyCaching;

/// <summary>
/// 按 EasyCaching 实际序列化载荷拆分 Hybrid 失效消息。
/// </summary>
internal static class HybridInvalidationMessageBatcher
{
    /// <summary>
    /// 按序列化载荷大小拆分并同步发布失效消息。
    /// </summary>
    /// <param name="bus">发布失效消息的总线。</param>
    /// <param name="topic">消息主题。</param>
    /// <param name="serializer">用于计算消息载荷大小的序列化器。</param>
    /// <param name="physicalKeys">需要广播失效的物理键。</param>
    /// <param name="maximumBytes">单条消息允许的最大载荷字节数；零表示不限制。</param>
    internal static void Publish(IEasyCachingBus bus, string topic, IEasyCachingSerializer serializer,
        IReadOnlyList<string> physicalKeys, long maximumBytes)
    {
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("失效消息主题不能为空。", nameof(topic));
        foreach (var message in CreateMessages(serializer, physicalKeys, maximumBytes))
            bus.Publish(topic, message);
    }

    /// <summary>
    /// 按序列化载荷大小拆分并异步发布失效消息。
    /// </summary>
    /// <param name="bus">发布失效消息的总线。</param>
    /// <param name="topic">消息主题。</param>
    /// <param name="serializer">用于计算消息载荷大小的序列化器。</param>
    /// <param name="physicalKeys">需要广播失效的物理键。</param>
    /// <param name="maximumBytes">单条消息允许的最大载荷字节数；零表示不限制。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    internal static async Task PublishAsync(IEasyCachingBus bus, string topic,
        IEasyCachingSerializer serializer, IReadOnlyList<string> physicalKeys, long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (bus == null)
            throw new ArgumentNullException(nameof(bus));
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("失效消息主题不能为空。", nameof(topic));
        foreach (var message in CreateMessages(serializer, physicalKeys, maximumBytes))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await bus.PublishAsync(topic, message, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 根据序列化载荷预算构造失效消息批次。
    /// </summary>
    /// <param name="serializer">用于计算消息载荷大小的序列化器。</param>
    /// <param name="physicalKeys">需要广播失效的物理键。</param>
    /// <param name="maximumBytes">单条消息允许的最大载荷字节数；零表示不限制。</param>
    /// <returns>按输入键顺序拆分的失效消息。</returns>
    internal static IEnumerable<EasyCachingMessage> CreateMessages(IEasyCachingSerializer serializer,
        IReadOnlyList<string> physicalKeys, long maximumBytes)
    {
        if (serializer == null)
            throw new ArgumentNullException(nameof(serializer));
        if (physicalKeys == null)
            throw new ArgumentNullException(nameof(physicalKeys));
        if (maximumBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if (physicalKeys.Count == 0)
            yield break;

        if (maximumBytes == 0)
        {
            yield return CreateMessage(physicalKeys);
            yield break;
        }

        var current = new List<string>(Math.Min(physicalKeys.Count, 16));
        EasyCachingMessage measured = null;
        for (var index = 0; index < physicalKeys.Count; index++)
        {
            var physicalKey = physicalKeys[index];
            if (physicalKey == null)
                throw new ArgumentException("失效消息物理键不能为 null。", nameof(physicalKeys));

            current.Add(physicalKey);
            var candidate = CreateMessage(current);
            var size = SerializedSize(serializer, candidate);
            if (size <= maximumBytes)
            {
                measured = candidate;
                continue;
            }

            current.RemoveAt(current.Count - 1);
            if (current.Count == 0)
                throw new CacheInvalidationMessageLimitExceededException(maximumBytes, size);

            yield return measured;
            current.Clear();
            current.Add(physicalKey);
            measured = CreateMessage(current);
            size = SerializedSize(serializer, measured);
            if (size > maximumBytes)
                throw new CacheInvalidationMessageLimitExceededException(maximumBytes, size);
        }

        if (current.Count > 0)
            yield return measured;
    }

    /// <summary>
    /// 为物理键集合创建失效消息。
    /// </summary>
    /// <param name="physicalKeys">失效消息中的物理键。</param>
    /// <returns>包含这些键的新消息。</returns>
    private static EasyCachingMessage CreateMessage(IReadOnlyList<string> physicalKeys)
    {
        var keys = new string[physicalKeys.Count];
        for (var index = 0; index < keys.Length; index++)
            keys[index] = physicalKeys[index];
        return new EasyCachingMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            CacheKeys = keys
        };
    }

    /// <summary>
    /// 获取失效消息经 Provider 序列化后的字节数。
    /// </summary>
    /// <param name="serializer">消息序列化器。</param>
    /// <param name="message">待序列化的失效消息。</param>
    /// <returns>序列化载荷的字节数。</returns>
    private static long SerializedSize(IEasyCachingSerializer serializer, EasyCachingMessage message)
    {
        var payload = serializer.Serialize(message);
        return payload?.LongLength ?? 0;
    }
}
