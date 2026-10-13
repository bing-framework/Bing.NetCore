using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace Bing.Test.Shared;

/// <summary>
/// 通过独立 TCP 连接订阅 Redis MONITOR，并按条件读取已执行命令。
/// </summary>
public sealed class RedisCommandMonitor : IAsyncDisposable
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
    private readonly List<string> _observedLines = new List<string>();

    /// <summary>
    /// 初始化一个 <see cref="RedisCommandMonitor"/> 类型的实例。
    /// </summary>
    /// <param name="tcpClient">连接 Redis 服务的 TCP 客户端。</param>
    /// <param name="stream">用于交换 Redis 协议数据的网络流。</param>
    private RedisCommandMonitor(TcpClient tcpClient, Stream stream)
    {
        _tcpClient = tcpClient;
        _stream = stream;
        _reader = new StreamReader(stream, Utf8, false, 4096, true);
    }

    /// <summary>
    /// 获取已从 MONITOR 连接消费的命令行。
    /// </summary>
    public IReadOnlyList<string> ObservedLines => _observedLines;

    /// <summary>
    /// 连接 Redis、完成认证和数据库选择后开启 MONITOR。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>已开启 MONITOR 的命令监视器。</returns>
    public static async Task<RedisCommandMonitor> ConnectAsync(string connection)
    {
        var options = RedisMonitorConnectionOptions.Parse(connection);
        var tcpClient = new TcpClient();
        try
        {
            await tcpClient.ConnectAsync(options.Host, options.Port).ConfigureAwait(false);
            Stream stream = tcpClient.GetStream();
            if (options.Ssl)
            {
                var sslStream = new SslStream(stream, false);
                await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = options.Host,
                    EnabledSslProtocols = SslProtocols.None
                }).ConfigureAwait(false);
                stream = sslStream;
            }

            var monitor = new RedisCommandMonitor(tcpClient, stream);
            if (!string.IsNullOrEmpty(options.Password))
            {
                var auth = string.IsNullOrEmpty(options.User)
                    ? new[] { "AUTH", options.Password }
                    : new[] { "AUTH", options.User, options.Password };
                await monitor.SendAsync(auth).ConfigureAwait(false);
                await monitor.ExpectOkAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }

            if (options.Database != 0)
            {
                await monitor.SendAsync("SELECT", options.Database.ToString()).ConfigureAwait(false);
                await monitor.ExpectOkAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }

            await monitor.SendAsync("MONITOR").ConfigureAwait(false);
            await monitor.ExpectOkAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
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
    /// <remarks>
    /// 未命中的命令行仍会保留在 <see cref="ObservedLines" /> 中。
    /// </remarks>
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

            var line = await _reader.ReadLineAsync().WaitAsync(remaining).ConfigureAwait(false);
            if (line == null)
                throw new EndOfStreamException("Redis MONITOR 连接已关闭。");
            _observedLines.Add(line);
            if (predicate(line))
                return line;
        }
    }

    /// <summary>
    /// 统计已消费命令中满足条件的命令数。
    /// </summary>
    /// <param name="predicate">用于筛选命令文本的条件。</param>
    /// <returns>满足条件的命令数量。</returns>
    public int Count(Func<string, bool> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));
        return _observedLines.Count(predicate);
    }

    /// <summary>
    /// 确认 Redis 命令返回 OK。
    /// </summary>
    /// <param name="timeout">超时时间。</param>
    private async Task ExpectOkAsync(TimeSpan timeout)
    {
        var response = await _reader.ReadLineAsync().WaitAsync(timeout).ConfigureAwait(false);
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
            await payload.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            WriteAscii(payload, "\r\n");
        }

        var data = payload.ToArray();
        await _stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
        await _stream.FlushAsync().ConfigureAwait(false);
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

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _reader.Dispose();
        _stream.Dispose();
        _tcpClient.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 保存 Redis MONITOR 连接所需的端点和认证参数。
    /// </summary>
    private sealed class RedisMonitorConnectionOptions
    {
        /// <summary>
        /// 获取或设置Redis MONITOR 连接的主机名。
        /// </summary>
        public string Host { get; private set; }
        /// <summary>
        /// 获取或设置Redis MONITOR 连接的端口。
        /// </summary>
        public int Port { get; private set; }
        /// <summary>
        /// 获取或设置Redis MONITOR 连接的用户名。
        /// </summary>
        public string User { get; private set; }
        /// <summary>
        /// 获取或设置Redis MONITOR 连接的密码。
        /// </summary>
        public string Password { get; private set; }
        /// <summary>
        /// 获取或设置Redis MONITOR 连接使用的数据库编号。
        /// </summary>
        public int Database { get; private set; }
        /// <summary>
        /// 获取或设置Redis MONITOR 连接是否启用 TLS。
        /// </summary>
        public bool Ssl { get; private set; }

        /// <summary>
        /// 解析 Redis 连接配置。
        /// </summary>
        /// <param name="connection">Redis 连接字符串。</param>
        /// <returns>解析出的 Redis MONITOR 连接参数。</returns>
        public static RedisMonitorConnectionOptions Parse(string connection)
        {
            if (string.IsNullOrWhiteSpace(connection))
                throw new ArgumentException("Redis 连接地址不能为空。", nameof(connection));

            var parts = connection.Split(',');
            var endpoint = parts[0].Trim();
            if (endpoint.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
                endpoint = endpoint.Substring("redis://".Length);

            var result = new RedisMonitorConnectionOptions
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
    }
}
