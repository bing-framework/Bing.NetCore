using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Bing.Caching.CSRedis;
using CSRedis;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// 使用 Redis MONITOR 验证批量流式读取在取消后不会继续派发下一批命令。
/// </summary>
public sealed class CommandCancellationTest
{
    /// <summary>
    /// 验证批量流式读取取消后不会派发第二个 MGET 命令。
    /// </summary>
    [Bing.Test.Shared.IntegrationFact]
    public async Task GetAllStream_CancellationDoesNotDispatchSecondMGet()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        using var client = new CSRedisClient(connection);
        var configuration = new CacheConfiguration(
            "csredis-command-cancel-" + Guid.NewGuid().ToString("N"),
            "redis",
            batchSize: 2);
        var cache = new CSRedisCacheManager(client, configuration);
        var keys = Enumerable.Range(0, 4)
            .Select(index => "command-cancel-item:" + index)
            .ToArray();

        await cache.SetAllAsync(keys.ToDictionary(key => key, _ => 1));

        await using var monitor = await RedisMonitor.ConnectAsync(connection);
        using var cancellation = new CancellationTokenSource();
        var enumerator = cache.GetAllStreamAsync<int>(keys, cancellation.Token).GetAsyncEnumerator();
        var barrierKey = "cancel-observed-" + Guid.NewGuid().ToString("N");
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

            await client.SetAsync(barrierKey, "barrier");
            await monitor.ReadUntilAsync(
                line => line.Contains("\"set\"", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains(barrierKey, StringComparison.Ordinal),
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
            await client.DelAsync(barrierKey);
        }
    }

    /// <summary>
    /// 验证前缀流式读取取消后不会派发第二个 MGET 命令。
    /// </summary>
    [Bing.Test.Shared.IntegrationFact]
    public async Task GetByPrefixStream_CancellationDoesNotDispatchSecondMGet()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Redis 集成测试要求设置 ConnectionStrings__RedisConnection。");

        using var client = new CSRedisClient(connection);
        var configuration = new CacheConfiguration(
            "csredis-prefix-command-cancel-" + Guid.NewGuid().ToString("N"),
            "redis",
            batchSize: 2);
        var cache = new CSRedisCacheManager(client, configuration);
        var prefix = "command-cancel-item:" + Guid.NewGuid().ToString("N") + ":";
        var keys = Enumerable.Range(0, 4)
            .Select(index => prefix + index)
            .ToArray();

        await cache.SetAllAsync(keys.ToDictionary(key => key, _ => 1));

        await using var monitor = await RedisMonitor.ConnectAsync(connection);
        using var cancellation = new CancellationTokenSource();
        var enumerator = cache.GetByPrefixStreamAsync<int>(prefix, cancellation.Token).GetAsyncEnumerator();
        var barrierKey = "prefix-cancel-observed-" + Guid.NewGuid().ToString("N");
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

            await client.SetAsync(barrierKey, "barrier");
            await monitor.ReadUntilAsync(
                line => line.Contains("\"set\"", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains(barrierKey, StringComparison.Ordinal),
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
            await client.DelAsync(barrierKey);
        }
    }

    /// <summary>
    /// 判断 MONITOR 行是否为指定命名空间的 MGET 命令。
    /// </summary>
    /// <param name="line">输入行。</param>
    /// <param name="namespacePrefix">命名空间前缀。</param>
    /// <returns>如果命令是指定命名空间的 MGET，则返回 <see langword="true" />。</returns>
    private static bool IsMGetForNamespace(string line, string namespacePrefix) =>
        line.Contains("\"mget\"", StringComparison.OrdinalIgnoreCase) &&
        line.Contains(namespacePrefix, StringComparison.Ordinal);

    /// <summary>
    /// 连接 Redis MONITOR 并记录收到的命令行。
    /// </summary>
    private sealed class RedisMonitor : IAsyncDisposable
    {
        /// <summary>
        /// 用于读写 Redis 协议文本的 UTF-8 编码实例。
        /// </summary>
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// 保存 MONITOR 连接的 TCP 客户端。
        /// </summary>
        private readonly TcpClient _tcpClient;
        /// <summary>
        /// 保存 MONITOR 连接的数据流。
        /// </summary>
        private readonly Stream _stream;
        /// <summary>
        /// 保存读取 Redis 协议的读取器。
        /// </summary>
        private readonly StreamReader _reader;
        /// <summary>
        /// 已从 MONITOR 连接读取的命令行。
        /// </summary>
        private readonly List<string> _observedLines = new();

        /// <summary>
        /// 初始化一个 <see cref="RedisMonitor"/> 类型的实例。
        /// </summary>
        /// <param name="tcpClient">Redis 测试客户端。</param>
        /// <param name="stream">MONITOR 连接的数据流。</param>
        private RedisMonitor(TcpClient tcpClient, Stream stream)
        {
            _tcpClient = tcpClient;
            _stream = stream;
            _reader = new StreamReader(stream, Utf8, false, 4096, true);
        }

        /// <summary>
        /// 获取已记录的 MONITOR 命令行。
        /// </summary>
        public IReadOnlyList<string> ObservedLines => _observedLines;

        /// <summary>
        /// 建立测试 Redis 连接。
        /// </summary>
        /// <returns>异步操作，完成后返回 Redis 监控器。</returns>
        /// <param name="connection">Redis 连接。</param>
        public static async Task<RedisMonitor> ConnectAsync(string connection)
        {
            var options = RedisConnectionOptions.Parse(connection);
            var tcpClient = new TcpClient();

            try
            {
                await tcpClient.ConnectAsync(options.Host, options.Port);
                Stream stream = tcpClient.GetStream();
                if (options.Ssl)
                {
                    var sslStream = new SslStream(stream, false);
                    await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = options.Host,
                        EnabledSslProtocols = SslProtocols.None
                    });
                    stream = sslStream;
                }

                var monitor = new RedisMonitor(tcpClient, stream);
                if (!string.IsNullOrEmpty(options.Password))
                {
                    var auth = string.IsNullOrEmpty(options.User)
                        ? new[] { "AUTH", options.Password }
                        : new[] { "AUTH", options.User, options.Password };
                    await monitor.SendAsync(auth);
                    await monitor.ExpectOkAsync(TimeSpan.FromSeconds(5));
                }

                if (options.Database != 0)
                {
                    await monitor.SendAsync("SELECT", options.Database.ToString());
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
        /// 读取测试响应直到满足条件或超时。
        /// </summary>
        /// <returns>异步操作，完成后返回满足条件的响应文本。</returns>
        /// <param name="predicate">筛选条件。</param>
        /// <param name="timeout">超时时间。</param>
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
        /// 读取并验证 Redis 的成功响应。
        /// </summary>
        /// <param name="timeout">超时时间。</param>
        private async Task ExpectOkAsync(TimeSpan timeout)
        {
            var response = await _reader.ReadLineAsync().WaitAsync(timeout);
            if (!string.Equals(response, "+OK", StringComparison.Ordinal))
                throw new InvalidOperationException("Redis 命令未返回 OK: " + response);
        }

        /// <summary>
        /// 将命令参数编码为 RESP 并发送到 Redis。
        /// </summary>
        /// <param name="arguments">RESP 命令参数。</param>
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
        /// <param name="value">待写入的 ASCII 文本。</param>
        private static void WriteAscii(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
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

    /// <summary>
    /// 保存 Redis MONITOR 连接配置。
    /// </summary>
    private sealed class RedisConnectionOptions
    {
        /// <summary>
        /// 获取或设置Redis 主机名。
        /// </summary>
        public string Host { get; private set; }
        /// <summary>
        /// 获取或设置Redis 端口。
        /// </summary>
        public int Port { get; private set; }
        /// <summary>
        /// 获取或设置Redis 用户名。
        /// </summary>
        public string User { get; private set; }
        /// <summary>
        /// 获取或设置Redis 密码。
        /// </summary>
        public string Password { get; private set; }
        /// <summary>
        /// 获取或设置Redis 数据库编号。
        /// </summary>
        public int Database { get; private set; }
        /// <summary>
        /// 获取或设置是否启用 SSL。
        /// </summary>
        public bool Ssl { get; private set; }

        /// <summary>
        /// 解析 Redis 连接字符串。
        /// </summary>
        /// <param name="connection">Redis 连接。</param>
        /// <returns>解析得到的 Redis 连接配置。</returns>
        public static RedisConnectionOptions Parse(string connection)
        {
            var parts = connection.Split(',');
            var endpoint = parts[0].Trim();
            if (endpoint.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
                endpoint = endpoint.Substring("redis://".Length);

            var result = new RedisConnectionOptions
            {
                Database = 0,
                Ssl = false
            };
            (result.Host, result.Port) = ParseEndpoint(endpoint);
            foreach (var part in parts.Skip(1))
            {
                var separator = part.IndexOf('=');
                if (separator <= 0)
                    continue;
                var name = part.Substring(0, separator).Trim();
                var value = part.Substring(separator + 1).Trim();
                switch (name.ToLowerInvariant())
                {
                    case "password":
                        result.Password = value;
                        break;
                    case "user":
                    case "username":
                        result.User = value;
                        break;
                    case "defaultdatabase":
                    case "database":
                        if (!int.TryParse(value, out var database))
                            throw new FormatException("Redis 数据库编号格式无效。");
                        result.Database = database;
                        break;
                    case "ssl":
                        if (!bool.TryParse(value, out var ssl))
                            throw new FormatException("Redis SSL 选项格式无效。");
                        result.Ssl = ssl;
                        break;
                }
            }
            return result;
        }

        /// <summary>
        /// 解析 Redis 端点文本。
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
        /// 解析 Redis 端口后缀。
        /// </summary>
        /// <param name="suffix">端口后缀，可以为空。</param>
        /// <returns>Redis 端口号。</returns>
        private static int ParsePort(string suffix)
        {
            if (string.IsNullOrEmpty(suffix))
                return 6379;
            if (!suffix.StartsWith(":", StringComparison.Ordinal) ||
                !int.TryParse(suffix.Substring(1), out var port))
                throw new FormatException("Redis 端口格式无效。");
            return port;
        }
    }
}
