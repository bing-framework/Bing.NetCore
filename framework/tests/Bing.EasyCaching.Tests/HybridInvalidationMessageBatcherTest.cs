using Bing.Caching;
using EasyCaching.Core.Bus;
using EasyCaching.Core.Serialization;
using System.Text;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 验证 Hybrid 失效消息按实际序列化载荷拆分。
/// </summary>
public sealed class HybridInvalidationMessageBatcherTest
{
    /// <summary>
    /// 默认大小预算保持单条消息和输入顺序。
    /// </summary>
    [Fact]
    public void Publish_DefaultBudgetKeepsOneMessageAndInputOrder()
    {
        var bus = new RecordingBus();
        var keys = new[] { "one", "two", "one" };

        HybridInvalidationMessageBatcher.Publish(bus, "topic", new SizeSerializer(), keys, 0);

        var message = Assert.Single(bus.Messages);
        Assert.Equal(keys, message.CacheKeys);
    }

    /// <summary>
    /// 按序列化字节数拆分消息并保留重复键。
    /// </summary>
    [Fact]
    public void Publish_SplitsBySerializedBytesAndPreservesDuplicates()
    {
        var bus = new RecordingBus();
        var serializer = new SizeSerializer();
        var keys = new[] { "a", "bb", "ccc", "dd" };

        HybridInvalidationMessageBatcher.Publish(bus, "topic", serializer, keys, 44);

        Assert.Equal(keys, bus.Messages.SelectMany(message => message.CacheKeys));
        Assert.Equal(3, bus.Messages.Count);
        Assert.All(bus.Messages, message =>
            Assert.True(serializer.Serialize(message).LongLength <= 44));
    }

    /// <summary>
    /// 按实际序列化结果记录消息实例并据此计算拆分边界。
    /// </summary>
    [Fact]
    public void Publish_UsesMeasuredMessageInstance()
    {
        var bus = new RecordingBus();
        var serializer = new TrackingSerializer();

        HybridInvalidationMessageBatcher.Publish(bus, "topic", serializer,
            new[] { "a", "bb", "ccc" }, 44);

        Assert.Equal(4, serializer.SerializeCalls);
        Assert.All(bus.Messages, message => Assert.Contains(message.Id, serializer.SerializedIds));
    }

    /// <summary>
    /// 验证单个键超出消息大小限制时拒绝发布。
    /// </summary>
    [Fact]
    public void Publish_RejectsSingleKeyThatCannotFit()
    {
        var bus = new RecordingBus();

        var error = Assert.Throws<CacheInvalidationMessageLimitExceededException>(() =>
            HybridInvalidationMessageBatcher.Publish(bus, "topic", new SizeSerializer(),
                new[] { "a" }, 40));

        Assert.Equal(40, error.Limit);
        Assert.Equal(41, error.Consumed);
        Assert.Empty(bus.Messages);
    }

    /// <summary>
    /// 异步发布收到取消后停止后续消息。
    /// </summary>
    [Fact]
    public async Task PublishAsync_CancellationStopsSubsequentMessages()
    {
        var cancellation = new CancellationTokenSource();
        var bus = new RecordingBus(() => cancellation.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HybridInvalidationMessageBatcher.PublishAsync(bus, "topic", new SizeSerializer(),
                new[] { "a", "bb", "ccc", "dd" }, 44, cancellation.Token));

        Assert.Single(bus.Messages);
    }

    /// <summary>
    /// 总线发布异常向调用方传播。
    /// </summary>
    [Fact]
    public void Publish_PropagatesBusFailureWithoutSwallowing()
    {
        var bus = new RecordingBus(() => throw new InvalidOperationException("bus failed"));

        var error = Assert.Throws<InvalidOperationException>(() =>
            HybridInvalidationMessageBatcher.Publish(bus, "topic", new SizeSerializer(),
                new[] { "a", "bb" }, 44));

        Assert.Equal("bus failed", error.Message);
    }

    /// <summary>
    /// 记录发布到总线的失效消息。
    /// </summary>
    private sealed class RecordingBus : IEasyCachingBus
    {
        /// <summary>
        /// 保存发布消息时执行的回调。
        /// </summary>
        private readonly Action _onPublish;

        /// <summary>
        /// 初始化一个 <see cref="RecordingBus"/> 类型的实例。
        /// </summary>
        /// <param name="onPublish">发布消息时执行的回调。</param>
        internal RecordingBus(Action onPublish = null) => _onPublish = onPublish;

        /// <summary>
        /// 获取已发布的 EasyCaching 消息。
        /// </summary>
        internal List<EasyCachingMessage> Messages { get; } = new();

        /// <inheritdoc />
        /// <remarks>
        /// 返回固定为 recording 的测试总线名称。
        /// </remarks>
        public string Name => "recording";

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="message">消息内容。</param>
        public void Publish(string topic, EasyCachingMessage message)
        {
            _onPublish?.Invoke();
            Messages.Add(message);
        }

        /// <inheritdoc />
        /// <param name="topic">发布主题。</param>
        /// <param name="message">消息内容。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task PublishAsync(string topic, EasyCachingMessage message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _onPublish?.Invoke();
            Messages.Add(message);
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
    /// 按消息键长度计算序列化大小的测试序列化器。
    /// </summary>
    private sealed class SizeSerializer : IEasyCachingSerializer
    {
        /// <inheritdoc />
        /// <remarks>
        /// 返回固定为 size 的序列化器名称。
        /// </remarks>
        public string Name => "size";

        /// <inheritdoc />
        /// <param name="value">待序列化的值。</param>
        public byte[] Serialize<T>(T value)
        {
            if (value is EasyCachingMessage message)
            {
                var keyBytes = message.CacheKeys.Sum(key => Encoding.UTF8.GetByteCount(key));
                return new byte[40 + keyBytes];
            }
            return Array.Empty<byte>();
        }

        /// <inheritdoc />
        /// <param name="bytes">待反序列化的字节数组。</param>
        public T Deserialize<T>(byte[] bytes) => default;

        /// <inheritdoc />
        /// <param name="bytes">字节数。</param>
        /// <param name="type">类型。</param>
        public object Deserialize(byte[] bytes, Type type) => null;

        /// <inheritdoc />
        /// <param name="obj">待序列化的对象。</param>
        public ArraySegment<byte> SerializeObject(object obj) => new(Serialize(obj));

        /// <inheritdoc />
        /// <param name="value">待处理值。</param>
        public object DeserializeObject(ArraySegment<byte> value) => null;
    }

    /// <summary>
    /// 记录序列化调用及消息标识的测试序列化器。
    /// </summary>
    private sealed class TrackingSerializer : IEasyCachingSerializer
    {
        /// <summary>
        /// 获取已序列化消息的标识集合。
        /// </summary>
        internal HashSet<string> SerializedIds { get; } = new(StringComparer.Ordinal);
        /// <summary>
        /// 获取或设置序列化调用次数。
        /// </summary>
        internal int SerializeCalls { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// 返回固定为 tracking 的序列化器名称。
        /// </remarks>
        public string Name => "tracking";

        /// <inheritdoc />
        /// <param name="value">待序列化的值。</param>
        public byte[] Serialize<T>(T value)
        {
            if (value is EasyCachingMessage message)
            {
                SerializeCalls++;
                SerializedIds.Add(message.Id);
                var keyBytes = message.CacheKeys.Sum(key => Encoding.UTF8.GetByteCount(key));
                return new byte[40 + keyBytes];
            }
            return Array.Empty<byte>();
        }

        /// <inheritdoc />
        /// <param name="bytes">待反序列化的字节数组。</param>
        public T Deserialize<T>(byte[] bytes) => default;

        /// <inheritdoc />
        /// <param name="bytes">字节数。</param>
        /// <param name="type">类型。</param>
        public object Deserialize(byte[] bytes, Type type) => null;

        /// <inheritdoc />
        /// <param name="obj">待序列化的对象。</param>
        public ArraySegment<byte> SerializeObject(object obj) => new(Serialize(obj));

        /// <inheritdoc />
        /// <param name="value">待处理值。</param>
        public object DeserializeObject(ArraySegment<byte> value) => null;
    }
}
