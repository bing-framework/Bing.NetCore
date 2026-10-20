using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Bing.Caching.FreeRedis;
using FreeRedis;
using Newtonsoft.Json;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// 使用 Redis MONITOR 验证批量流式读取在取消后不会继续派发下一批命令。
/// </summary>
public sealed class CommandCancellationTest
{
    /// <summary>
    /// 批量流式读取取消后不再派发第二条 MGET 命令。
    /// </summary>
    [Bing.Test.Shared.IntegrationFact]
    public async Task GetAllStream_CancellationDoesNotDispatchSecondMGet()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        using var client = CreateClient(connection);
        var configuration = new CacheConfiguration(
            "freeredis-command-cancel-" + Guid.NewGuid().ToString("N"),
            "redis",
            batchSize: 2);
        var cache = new FreeRedisCacheManager(client, configuration);
        var keys = Enumerable.Range(0, 4)
            .Select(index => "command-cancel-item:" + index)
            .ToArray();

        await cache.SetAllAsync(keys.ToDictionary(key => key, _ => 1));

        await using var monitor = await RedisMonitor.ConnectAsync(connection);
        using var cancellation = new CancellationTokenSource();
        var enumerator = cache.GetAllStreamAsync<int>(keys, cancellation.Token).GetAsyncEnumerator();
        try
        {
            Assert.True(await enumerator.MoveNextAsync());

            var keyMarker = "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":";
            await monitor.ReadUntilAsync(
                line => IsMGetForNamespace(line, keyMarker),
                TimeSpan.FromSeconds(5));

            // batchSize=2；消费完首批后才取消，验证取消不会派发下一批 MGET。
            Assert.True(await enumerator.MoveNextAsync());
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => enumerator.MoveNextAsync().AsTask());

            var pingMarker = "cancel-observed-" + Guid.NewGuid().ToString("N");
            await client.CallAsync(new CommandPacket("PING", pingMarker));
            await monitor.ReadUntilAsync(
                line => line.Contains("\"ping\"", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains(pingMarker, StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));

            var matchingMGets = monitor.ObservedLines
                .Where(line => IsMGetForNamespace(line, keyMarker))
                .ToArray();
            Assert.Single(matchingMGets);
        }
        finally
        {
            await enumerator.DisposeAsync();
            await cache.RemoveAllAsync(keys);
        }
    }

    /// <summary>
    /// 前缀流式读取取消后不再派发第二条 MGET 命令。
    /// </summary>
    [Bing.Test.Shared.IntegrationFact]
    public async Task GetByPrefixStream_CancellationDoesNotDispatchSecondMGet()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        using var client = CreateClient(connection);
        var configuration = new CacheConfiguration(
            "freeredis-prefix-command-cancel-" + Guid.NewGuid().ToString("N"),
            "redis",
            batchSize: 2);
        var cache = new FreeRedisCacheManager(client, configuration);
        var prefix = "command-cancel-item:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 4)
            .Select(index => prefix + index)
            .ToArray();

        await cache.SetAllAsync(keys.ToDictionary(key => key, _ => 1));

        await using var monitor = await RedisMonitor.ConnectAsync(connection);
        using var cancellation = new CancellationTokenSource();
        var enumerator = cache.GetByPrefixStreamAsync<int>(prefix, cancellation.Token).GetAsyncEnumerator();
        try
        {
            Assert.True(await enumerator.MoveNextAsync());

            var keyMarker = "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":";
            await monitor.ReadUntilAsync(
                line => IsMGetForNamespace(line, keyMarker),
                TimeSpan.FromSeconds(5));

            // batchSize=2；首个元素已经从首批 MGET 产生，取消后不应派发下一批 MGET。
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => enumerator.MoveNextAsync().AsTask());

            var pingMarker = "prefix-cancel-observed-" + Guid.NewGuid().ToString("N");
            await client.CallAsync(new CommandPacket("PING", pingMarker));
            await monitor.ReadUntilAsync(
                line => line.Contains("\"ping\"", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains(pingMarker, StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));

            var matchingMGets = monitor.ObservedLines
                .Where(line => IsMGetForNamespace(line, keyMarker))
                .ToArray();
            Assert.Single(matchingMGets);
        }
        finally
        {
            await enumerator.DisposeAsync();
            await cache.RemoveAllAsync(keys);
        }
    }

    /// <summary>
    /// 判断 MONITOR 行是否包含指定命名空间的 MGET 命令。
    /// </summary>
    /// <param name="line">Redis MONITOR 输出行。</param>
    /// <param name="namespacePrefix">命名空间前缀。</param>
    /// <returns>行中同时包含 MGET 命令和命名空间前缀时为 <see langword="true"/>。</returns>
    private static bool IsMGetForNamespace(string line, string namespacePrefix) =>
        line.Contains("\"mget\"", StringComparison.OrdinalIgnoreCase) &&
        line.Contains(namespacePrefix, StringComparison.Ordinal);

    /// <summary>
    /// 创建使用 JSON 序列化的 Redis 测试客户端。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>配置了 JSON 序列化的 FreeRedis 客户端。</returns>
    private static RedisClient CreateClient(string connection)
    {
        var client = new RedisClient(connection)
        {
            Serialize = JsonConvert.SerializeObject,
            Deserialize = JsonConvert.DeserializeObject
        };
        return client;
    }

    /// <summary>
    /// 通过独立 TCP 连接订阅 Redis MONITOR 并读取命令行。
    /// </summary>
    private sealed class RedisMonitor : IAsyncDisposable
    {
        /// <summary>
        /// 用于读写 Redis 协议文本的 UTF-8 编码实例。
        /// </summary>
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// 连接被监视 Redis 服务的 TCP 客户端。
        /// </summary>
        private readonly TcpClient _tcpClient;
        /// <summary>
        /// 与 Redis 服务交换 RESP 数据的网络流。
        /// </summary>
        private readonly Stream _stream;
        /// <summary>
        /// 读取 Redis 响应和 MONITOR 命令行。
        /// </summary>
        private readonly StreamReader _reader;
        /// <summary>
        /// 已从 MONITOR 连接读取的命令行。
        /// </summary>
        private readonly List<string> _observedLines = new();

        /// <summary>
        /// 初始化一个 <see cref="RedisMonitor"/> 类型的实例。
        /// </summary>
        /// <param name="tcpClient">连接 Redis 服务的 TCP 客户端。</param>
        /// <param name="stream">用于交换 Redis 协议数据的网络流。</param>
        private RedisMonitor(TcpClient tcpClient, Stream stream)
        {
            _tcpClient = tcpClient;
            _stream = stream;
            _reader = new StreamReader(stream, Utf8, false, 4096, true);
        }

        /// <summary>
        /// 获取已从 MONITOR 连接读取的命令行。
        /// </summary>
        public IReadOnlyList<string> ObservedLines => _observedLines;

        /// <summary>
        /// 建立测试 Redis 连接。
        /// </summary>
        /// <param name="connection">Redis 连接。</param>
        /// <returns>已开启 MONITOR 的 Redis 命令监视器。</returns>
        public static async Task<RedisMonitor> ConnectAsync(string connection)
        {
            var builder = ConnectionStringBuilder.Parse(connection);
            var (host, port) = ParseEndpoint(builder.Host);
            var tcpClient = new TcpClient();

            try
            {
                await tcpClient.ConnectAsync(host, port);
                Stream stream = tcpClient.GetStream();
                if (builder.Ssl)
                {
                    var sslStream = new SslStream(stream, false);
                    await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = host,
                        EnabledSslProtocols = SslProtocols.None
                    });
                    stream = sslStream;
                }

                var monitor = new RedisMonitor(tcpClient, stream);
                if (!string.IsNullOrEmpty(builder.User) || !string.IsNullOrEmpty(builder.Password))
                {
                    var auth = string.IsNullOrEmpty(builder.User)
                        ? new[] { "AUTH", builder.Password }
                        : new[] { "AUTH", builder.User, builder.Password };
                    await monitor.SendAsync(auth);
                    await monitor.ExpectOkAsync(TimeSpan.FromSeconds(5));
                }

                if (builder.Database != 0)
                {
                    await monitor.SendAsync("SELECT", builder.Database.ToString());
                    await monitor.ExpectOkAsync(TimeSpan.FromSeconds(5));
                }

                await monitor.SendAsync("MONITOR");
                await monitor.ExpectOkAsync(TimeSpan.FromSeconds(5));
                return monitor;
            }
            catch
            {
                tcpClient.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 读取 Redis 命令，直到命中指定条件。
        /// </summary>
        /// <remarks>未命中的命令行仍会保留在 <see cref="ObservedLines"/> 中。</remarks>
        /// <param name="predicate">筛选条件。</param>
        /// <param name="timeout">超时时间。</param>
        /// <returns>首条满足筛选条件的 Redis 命令行。</returns>
        public async Task<string> ReadUntilAsync(Func<string, bool> predicate, TimeSpan timeout)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));

            var deadline = DateTime.UtcNow + timeout;
            while (true)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    throw new TimeoutException("在限定时间内未观察到预期 Redis 命令。");

                var line = await _reader.ReadLineAsync().WaitAsync(remaining);
                if (line == null)
                    throw new EndOfStreamException("Redis MONITOR 连接已关闭。");
                _observedLines.Add(line);
                if (predicate(line))
                    return line;
            }
        }

        /// <summary>
        /// 确认 Redis 命令返回 OK。
        /// </summary>
        /// <param name="timeout">超时时间。</param>
        private async Task ExpectOkAsync(TimeSpan timeout)
        {
            var response = await _reader.ReadLineAsync().WaitAsync(timeout);
            if (!string.Equals(response, "+OK", StringComparison.Ordinal))
                throw new InvalidOperationException("Redis 命令未返回 OK: " + response);
        }

        /// <summary>
        /// 按 RESP 格式发送 Redis 命令。
        /// </summary>
        /// <param name="arguments">命令行参数。</param>
        private async Task SendAsync(params string[] arguments)
        {
            using var payload = new MemoryStream();
            WriteAscii(payload, "*" + arguments.Length + "\r\n");
            foreach (var argument in arguments)
            {
                var bytes = Utf8.GetBytes(argument ?? string.Empty);
                WriteAscii(payload, "$" + bytes.Length + "\r\n");
                await payload.WriteAsync(bytes, 0, bytes.Length);
                WriteAscii(payload, "\r\n");
            }

            var data = payload.ToArray();
            await _stream.WriteAsync(data, 0, data.Length);
            await _stream.FlushAsync();
        }

        /// <summary>
        /// 将 ASCII 文本写入数据流。
        /// </summary>
        /// <param name="stream">数据流。</param>
        /// <param name="value">待处理值。</param>
        private static void WriteAscii(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// 解析 Redis 端点。
        /// </summary>
        /// <param name="endpoint">Redis 端点。</param>
        /// <returns>Redis 主机名和端口。</returns>
        private static (string Host, int Port) ParseEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("Redis 连接地址不能为空。", nameof(endpoint));

            endpoint = endpoint.Trim();
            if (endpoint[0] == '[')
            {
                var closingBracket = endpoint.IndexOf(']');
                if (closingBracket < 0)
                    throw new FormatException("Redis IPv6 地址格式无效。");
                var host = endpoint.Substring(1, closingBracket - 1);
                var port = ParsePort(endpoint.Substring(closingBracket + 1));
                return (host, port);
            }

            var separator = endpoint.LastIndexOf(':');
            if (separator > 0 && int.TryParse(endpoint.Substring(separator + 1), out var parsedPort))
                return (endpoint.Substring(0, separator), parsedPort);
            return (endpoint, 6379);
        }

        /// <summary>
        /// 解析测试 Redis 端口。
        /// </summary>
        /// <param name="suffix">端点中的端口后缀。</param>
        /// <returns>解析出的 Redis 端口。</returns>
        private static int ParsePort(string suffix)
        {
            if (string.IsNullOrEmpty(suffix))
                return 6379;
            if (!suffix.StartsWith(":", StringComparison.Ordinal) ||
                !int.TryParse(suffix.Substring(1), out var port))
                throw new FormatException("Redis 端口格式无效。");
            return port;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _reader.Dispose();
            _stream.Dispose();
            _tcpClient.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
