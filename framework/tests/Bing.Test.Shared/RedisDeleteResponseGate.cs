#nullable enable

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Bing.Test.Shared;

/// <summary>
/// 将单个 Redis 端点暴露为本地回环代理，并可暂存第一个目标 DEL 响应。
/// </summary>
/// <remarks>
/// 代理解析 RESP 请求和响应后再转发。目标命令由 RESP 参数中的命令名和键标记
/// 判断，不依赖 TCP 分片或 MONITOR 文本时序。目标 DEL 已转发到上游，但其完整
/// 响应会暂存到 <see cref="ReleaseHeldDeleteResponse"/> 被调用后才返回客户端。
/// </remarks>
public sealed class RedisDeleteResponseGate : IAsyncDisposable
{
    /// <summary>
    /// 限制单个 RESP 帧的最大字节数。
    /// </summary>
    private const int MaxFrameBytes = 64 * 1024 * 1024;
    /// <summary>
    /// 限制 RESP 行的最大字节数。
    /// </summary>
    private const int MaxLineBytes = 8 * 1024;
    /// <summary>
    /// 保存单次解析允许的最大集合元素数。
    /// </summary>
    private const int MaxCollectionItems = 1_000_000;

    /// <summary>
    /// 用于读写 Redis 协议文本的 UTF-8 编码实例。
    /// </summary>
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    /// <summary>
    /// 保存上游 Redis 主机名。
    /// </summary>
    private readonly string _upstreamHost;
    /// <summary>
    /// 保存上游 Redis 服务端口。
    /// </summary>
    private readonly int _upstreamPort;
    /// <summary>
    /// 保存用于识别目标 DEL 请求的键标记。
    /// </summary>
    private readonly byte[] _targetMarker;
    /// <summary>
    /// 保存本地代理监听器。
    /// </summary>
    private readonly TcpListener _listener;
    /// <summary>
    /// 控制代理关闭和后台循环退出的取消源。
    /// </summary>
    private readonly CancellationTokenSource _shutdown = new();
    /// <summary>
    /// 通知首个目标 DEL 响应已完整捕获。
    /// </summary>
    private readonly TaskCompletionSource<object?> _deleteResponseCaptured =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>
    /// 跟踪代理连接后台任务的集合。
    /// </summary>
    private readonly ConcurrentDictionary<Connection, Task> _connectionTasks = new();
    /// <summary>
    /// 保护挂起响应状态的同步对象。
    /// </summary>
    private readonly object _heldResponseLock = new();

    /// <summary>
    /// 保存接受代理连接的后台任务。
    /// </summary>
    private Task? _acceptLoop;
    /// <summary>
    /// 保存当前暂存响应所属的代理连接。
    /// </summary>
    private Connection? _heldConnection;
    /// <summary>
    /// 记录已识别的目标 DEL 请求数。
    /// </summary>
    private int _targetDeleteCount;
    /// <summary>
    /// 记录是否已暂存目标 DEL 响应。
    /// </summary>
    private bool _responseGateUsed;
    /// <summary>
    /// 记录代理是否已释放。
    /// </summary>
    private int _disposed;

    /// <summary>
    /// 初始化一个 <see cref="RedisDeleteResponseGate"/> 类型的实例。
    /// </summary>
    /// <param name="host">上游 Redis 主机名或 IP 地址。</param>
    /// <param name="port">上游 Redis TCP 端口。</param>
    /// <param name="targetMarker">目标 DEL 请求中必须出现的键标记。</param>
    public RedisDeleteResponseGate(string host, int port, string targetMarker)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Redis 主机不能为空。", nameof(host));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        if (string.IsNullOrEmpty(targetMarker))
            throw new ArgumentException("目标键标记不能为空。", nameof(targetMarker));

        _upstreamHost = host.Trim();
        _upstreamPort = port;
        try
        {
            _targetMarker = Utf8.GetBytes(targetMarker);
        }
        catch (EncoderFallbackException error)
        {
            throw new ArgumentException("目标键标记必须是合法的 UTF-16 字符串。", nameof(targetMarker), error);
        }

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = AcceptLoopAsync();
    }

    /// <summary>
    /// 获取代理监听的本地回环端口。
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// 获取第一个目标 DEL 响应已从上游完整读取并被代理暂存时完成的任务。
    /// </summary>
    public Task DeleteResponseCaptured => _deleteResponseCaptured.Task;

    /// <summary>
    /// 获取已观察到的目标 DEL 请求数量。
    /// </summary>
    public int TargetDeleteCount => Volatile.Read(ref _targetDeleteCount);

    /// <summary>
    /// 释放已暂存的目标 DEL 响应，使其继续返回到客户端。
    /// </summary>
    public void ReleaseHeldDeleteResponse()
    {
        Connection? connection;
        lock (_heldResponseLock)
        {
            connection = _heldConnection;
            _heldConnection = null;
        }

        connection?.ReleaseHeldResponse();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 停止接受连接并关闭代理拥有的所有连接。
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _shutdown.Cancel();
        _listener.Stop();

        foreach (var connection in _connectionTasks.Keys)
            connection.Abort();

        var acceptLoop = _acceptLoop;
        if (acceptLoop != null)
            await IgnoreConnectionFailureAsync(acceptLoop).ConfigureAwait(false);

        while (true)
        {
            var tasks = _connectionTasks.Values.ToArray();
            if (tasks.Length == 0)
                break;

            await IgnoreConnectionFailureAsync(Task.WhenAll(tasks)).ConfigureAwait(false);
        }

        _deleteResponseCaptured.TrySetCanceled();
        _shutdown.Dispose();
    }

    /// <summary>
    /// 接受客户端连接并启动代理任务。
    /// </summary>
    private async Task AcceptLoopAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (_shutdown.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException) when (_shutdown.IsCancellationRequested)
            {
                break;
            }

            client.NoDelay = true;
            var connection = new Connection(client);
            var task = RunConnectionAsync(connection);
            _connectionTasks[connection] = task;
            _ = ObserveConnectionAsync(connection, task);
        }
    }

    /// <summary>
    /// 等待连接任务结束并移除跟踪记录。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <param name="task">异步任务。</param>
    private async Task ObserveConnectionAsync(Connection connection, Task task)
    {
        await IgnoreConnectionFailureAsync(task).ConfigureAwait(false);
        _connectionTasks.TryRemove(connection, out _);
    }

    /// <summary>
    /// 连接客户端与上游 Redis 并转发双向流量。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    private async Task RunConnectionAsync(Connection connection)
    {
        try
        {
            var upstream = new TcpClient
            {
                NoDelay = true
            };
            try
            {
                await upstream.ConnectAsync(_upstreamHost, _upstreamPort, _shutdown.Token)
                    .ConfigureAwait(false);
                connection.AttachUpstream(upstream);
            }
            catch
            {
                upstream.Dispose();
                throw;
            }

            var commands = new ConcurrentQueue<bool>();
            var requests = ForwardRequestsAsync(connection, commands, _shutdown.Token);
            var responses = ForwardResponsesAsync(connection, commands, _shutdown.Token);
            await Task.WhenAny(requests, responses).ConfigureAwait(false);
            connection.Abort();
            await IgnoreConnectionFailureAsync(Task.WhenAll(requests, responses)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            connection.Abort();
        }
        catch
        {
            connection.Abort();
        }
        finally
        {
            connection.Abort();
        }
    }

    /// <summary>
    /// 读取客户端 RESP 请求，标记目标 DEL 并转发到上游。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <param name="commands">Redis 命令集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task ForwardRequestsAsync(
        Connection connection,
        ConcurrentQueue<bool> commands,
        CancellationToken cancellationToken)
    {
        var clientStream = connection.Client.GetStream();
        var upstreamStream = connection.GetUpstreamStream();
        while (true)
        {
            var frame = await ReadRespFrameAsync(clientStream, cancellationToken).ConfigureAwait(false);
            var isTargetDelete = IsTargetDeleteCommand(frame, _targetMarker);
            if (isTargetDelete)
                Interlocked.Increment(ref _targetDeleteCount);

            // 必须在写入上游之前入队，避免上游响应先于命令标记进入响应循环。
            commands.Enqueue(isTargetDelete);
            await upstreamStream.WriteAsync(frame, 0, frame.Length, cancellationToken).ConfigureAwait(false);
            await upstreamStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 读取上游 RESP 响应，暂存目标 DEL 响应后转发。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <param name="commands">Redis 命令集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task ForwardResponsesAsync(
        Connection connection,
        ConcurrentQueue<bool> commands,
        CancellationToken cancellationToken)
    {
        var clientStream = connection.Client.GetStream();
        var upstreamStream = connection.GetUpstreamStream();
        while (true)
        {
            var frame = await ReadRespFrameAsync(upstreamStream, cancellationToken).ConfigureAwait(false);
            var isOutOfBand = IsOutOfBandResponse(frame);
            var isTargetDelete = false;
            if (!isOutOfBand)
                commands.TryDequeue(out isTargetDelete);

            if (isTargetDelete && TryHoldDeleteResponse(connection, frame))
            {
                await connection.WaitForHeldResponseReleaseAsync(cancellationToken).ConfigureAwait(false);
                var releasedResponse = connection.TakeHeldResponse();
                if (releasedResponse == null)
                    return;

                await clientStream.WriteAsync(releasedResponse, 0, releasedResponse.Length, cancellationToken)
                    .ConfigureAwait(false);
                await clientStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            await clientStream.WriteAsync(frame, 0, frame.Length, cancellationToken).ConfigureAwait(false);
            await clientStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 尝试暂存首个目标 DEL 响应。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <param name="response">响应内容。</param>
    /// <returns>如果响应已被暂存，则返回 <see langword="true" />。</returns>
    private bool TryHoldDeleteResponse(Connection connection, byte[] response)
    {
        lock (_heldResponseLock)
        {
            if (_responseGateUsed || Volatile.Read(ref _disposed) != 0)
                return false;

            _responseGateUsed = true;
            connection.HoldResponse(response);
            _heldConnection = connection;
        }

        _deleteResponseCaptured.TrySetResult(null);
        return true;
    }

    /// <summary>
    /// 判断 RESP 帧是否为带外响应。
    /// </summary>
    /// <param name="frame">协议帧。</param>
    /// <returns>如果帧以带外 RESP 类型开头，则返回 <see langword="true" />。</returns>
    private static bool IsOutOfBandResponse(byte[] frame) =>
        frame.Length != 0 && (frame[0] == (byte)'>' || frame[0] == (byte)'|');

    /// <summary>
    /// 判断 RESP 请求是否为包含标记键的 DEL 命令。
    /// </summary>
    /// <param name="frame">协议帧。</param>
    /// <param name="marker">测试标记。</param>
    /// <returns>如果请求匹配目标 DEL 命令，则返回 <see langword="true" />。</returns>
    private static bool IsTargetDeleteCommand(byte[] frame, byte[] marker)
    {
        var offset = 0;
        if (frame.Length == 0 || frame[offset++] != (byte)'*')
            return false;
        if (!TryReadIntegerLine(frame, ref offset, out var itemCount) || itemCount < 1 ||
            itemCount > MaxCollectionItems)
            return false;

        var isDelete = false;
        var markerFound = false;
        for (var index = 0; index < itemCount; index++)
        {
            if (offset >= frame.Length || frame[offset++] != (byte)'$')
                return false;
            if (!TryReadIntegerLine(frame, ref offset, out var length) || length < 0 ||
                length > MaxFrameBytes || length > frame.Length - offset - 2)
                return false;

            var payloadLength = (int)length;
            if (index == 0)
                isDelete = EqualsAsciiIgnoreCase(frame, offset, payloadLength, "DEL");
            else if (isDelete && ContainsBytes(frame, offset, payloadLength, marker))
                markerFound = true;

            offset += payloadLength;
            if (offset + 1 >= frame.Length || frame[offset] != (byte)'\r' || frame[offset + 1] != (byte)'\n')
                return false;
            offset += 2;
        }

        return isDelete && markerFound;
    }

    /// <summary>
    /// 按 ASCII 忽略大小写比较帧中的文本。
    /// </summary>
    /// <param name="frame">协议帧。</param>
    /// <param name="offset">缓冲区偏移量。</param>
    /// <param name="length">长度。</param>
    /// <param name="value">预期 ASCII 文本。</param>
    /// <returns>如果指定范围与文本相同，则返回 <see langword="true" />。</returns>
    private static bool EqualsAsciiIgnoreCase(byte[] frame, int offset, int length, string value)
    {
        if (length != value.Length)
            return false;
        for (var index = 0; index < length; index++)
        {
            var expected = (byte)value[index];
            var actual = frame[offset + index];
            if (actual >= (byte)'a' && actual <= (byte)'z')
                actual = (byte)(actual - ('a' - 'A'));
            if (actual != expected)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 判断帧载荷是否包含指定字节序列。
    /// </summary>
    /// <param name="frame">协议帧。</param>
    /// <param name="offset">缓冲区偏移量。</param>
    /// <param name="length">长度。</param>
    /// <param name="value">要查找的字节序列。</param>
    /// <returns>如果载荷包含该序列，则返回 <see langword="true" />。</returns>
    private static bool ContainsBytes(byte[] frame, int offset, int length, byte[] value)
    {
        if (value.Length == 0)
            return true;
        if (length < value.Length)
            return false;
        var last = offset + length - value.Length;
        for (var start = offset; start <= last; start++)
        {
            var matched = true;
            for (var index = 0; index < value.Length; index++)
            {
                if (frame[start + index] != value[index])
                {
                    matched = false;
                    break;
                }
            }
            if (matched)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 读取 CRLF 结尾的整数行并更新偏移量。
    /// </summary>
    /// <param name="frame">协议帧。</param>
    /// <param name="offset">缓冲区偏移量。</param>
    /// <param name="value">待处理值。</param>
    /// <returns>如果整数行格式有效，则返回 <see langword="true" />。</returns>
    private static bool TryReadIntegerLine(byte[] frame, ref int offset, out long value)
    {
        value = 0;
        var lineStart = offset;
        while (offset + 1 < frame.Length && !(frame[offset] == (byte)'\r' && frame[offset + 1] == (byte)'\n'))
        {
            offset++;
            if (offset - lineStart > MaxLineBytes)
                return false;
        }

        if (offset + 1 >= frame.Length)
            return false;

        var text = Encoding.ASCII.GetString(frame, lineStart, offset - lineStart);
        offset += 2;
        return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// 读取一个完整 RESP 帧。
    /// </summary>
    /// <returns>异步操作，完成后返回读取到的 RESP 帧。</returns>
    /// <param name="stream">数据流。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private static async Task<byte[]> ReadRespFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var frame = new MemoryStream();
        var singleByte = new byte[1];
        var type = await ReadByteAsync(stream, singleByte, cancellationToken).ConfigureAwait(false);
        frame.WriteByte(type);
        await ReadRespValueAsync(stream, frame, type, singleByte, 0, cancellationToken).ConfigureAwait(false);
        return frame.ToArray();
    }

    /// <summary>
    /// 按 RESP 类型递归读取值并追加到帧缓冲区。
    /// </summary>
    /// <param name="stream">数据流。</param>
    /// <param name="frame">协议帧。</param>
    /// <param name="type">RESP 类型字节。</param>
    /// <param name="singleByte">单字节数据。</param>
    /// <param name="depth">当前嵌套深度。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private static async Task ReadRespValueAsync(
        Stream stream,
        MemoryStream frame,
        byte type,
        byte[] singleByte,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > 128)
            throw new InvalidDataException("RESP 嵌套层级超过代理限制。");

        switch (type)
        {
            case (byte)'+':
            case (byte)'-':
            case (byte)':':
            case (byte)'_':
            case (byte)'#':
            case (byte)',':
            case (byte)'(':
                await ReadLineIntoFrameAsync(stream, frame, singleByte, cancellationToken).ConfigureAwait(false);
                return;

            case (byte)'$':
            case (byte)'!':
            case (byte)'=':
                await ReadBulkValueAsync(stream, frame, singleByte, cancellationToken).ConfigureAwait(false);
                return;

            case (byte)'*':
            case (byte)'~':
            case (byte)'>':
                var itemCount = await ReadCollectionCountAsync(stream, frame, singleByte, cancellationToken)
                    .ConfigureAwait(false);
                if (itemCount < 0)
                    return;
                for (var index = 0; index < itemCount; index++)
                {
                    var itemType = await ReadByteAsync(stream, singleByte, cancellationToken).ConfigureAwait(false);
                    AppendByte(frame, itemType);
                    await ReadRespValueAsync(stream, frame, itemType, singleByte, depth + 1, cancellationToken)
                        .ConfigureAwait(false);
                }
                return;

            case (byte)'%':
            case (byte)'|':
                var pairCount = await ReadCollectionCountAsync(stream, frame, singleByte, cancellationToken)
                    .ConfigureAwait(false);
                if (pairCount < 0)
                    return;
                for (var index = 0; index < pairCount * 2; index++)
                {
                    var itemType = await ReadByteAsync(stream, singleByte, cancellationToken).ConfigureAwait(false);
                    AppendByte(frame, itemType);
                    await ReadRespValueAsync(stream, frame, itemType, singleByte, depth + 1, cancellationToken)
                        .ConfigureAwait(false);
                }
                return;

            default:
                throw new InvalidDataException("Redis 返回了代理不支持的 RESP 类型。");
        }
    }

    /// <summary>
    /// 读取并校验 RESP 集合元素数量。
    /// </summary>
    /// <returns>异步操作，完成后返回 RESP 集合中的元素数量。</returns>
    /// <param name="stream">数据流。</param>
    /// <param name="frame">协议帧。</param>
    /// <param name="singleByte">单字节数据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private static async Task<long> ReadCollectionCountAsync(
        Stream stream,
        MemoryStream frame,
        byte[] singleByte,
        CancellationToken cancellationToken)
    {
        var line = await ReadLineIntoFrameAsync(stream, frame, singleByte, cancellationToken).ConfigureAwait(false);
        if (!long.TryParse(line, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var count))
            throw new InvalidDataException("RESP 集合数量无效。");
        if (count < -1 || count > MaxCollectionItems)
            throw new InvalidDataException("RESP 集合数量超过代理限制。");
        return count;
    }

    /// <summary>
    /// 读取 RESP 批量字符串载荷及结束符。
    /// </summary>
    /// <param name="stream">数据流。</param>
    /// <param name="frame">协议帧。</param>
    /// <param name="singleByte">单字节数据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private static async Task ReadBulkValueAsync(
        Stream stream,
        MemoryStream frame,
        byte[] singleByte,
        CancellationToken cancellationToken)
    {
        var line = await ReadLineIntoFrameAsync(stream, frame, singleByte, cancellationToken).ConfigureAwait(false);
        if (!long.TryParse(line, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var length))
            throw new InvalidDataException("RESP 二进制载荷长度无效。");
        if (length < -1 || length > MaxFrameBytes)
            throw new InvalidDataException("RESP 二进制载荷超过代理限制。");
        if (length < 0)
            return;

        var payload = new byte[8192];
        var remaining = length;
        while (remaining > 0)
        {
            var requested = (int)Math.Min(payload.Length, remaining);
            var read = await stream.ReadAsync(payload, 0, requested, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Redis RESP 二进制载荷提前结束。");
            AppendBytes(frame, payload, read);
            remaining -= read;
        }

        var terminator = new byte[2];
        await ReadExactlyAsync(stream, terminator, 0, terminator.Length, cancellationToken).ConfigureAwait(false);
        if (terminator[0] != (byte)'\r' || terminator[1] != (byte)'\n')
            throw new InvalidDataException("RESP 二进制载荷缺少 CRLF 结束符。");
        AppendBytes(frame, terminator, terminator.Length);
    }

    /// <summary>
    /// 读取 RESP 行并追加到帧。
    /// </summary>
    /// <returns>异步操作，完成后返回读取到的 RESP 行文本。</returns>
    /// <param name="stream">数据流。</param>
    /// <param name="frame">协议帧。</param>
    /// <param name="singleByte">单字节数据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private static async Task<string> ReadLineIntoFrameAsync(
        Stream stream,
        MemoryStream frame,
        byte[] singleByte,
        CancellationToken cancellationToken)
    {
        var line = new List<byte>(32);
        while (true)
        {
            var value = await ReadByteAsync(stream, singleByte, cancellationToken).ConfigureAwait(false);
            AppendByte(frame, value);
            line.Add(value);
            if (line.Count >= 2 && line[line.Count - 2] == (byte)'\r' && line[line.Count - 1] == (byte)'\n')
            {
                line.RemoveRange(line.Count - 2, 2);
                return Encoding.ASCII.GetString(line.ToArray());
            }
            if (line.Count > MaxLineBytes)
                throw new InvalidDataException("RESP 行长度超过代理限制。");
        }
    }

    /// <summary>
    /// 从流中读取一个字节。
    /// </summary>
    /// <returns>异步操作，完成后返回读取到的字节。</returns>
    /// <param name="stream">数据流。</param>
    /// <param name="buffer">缓冲区。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private static async Task<byte> ReadByteAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = await stream.ReadAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);
        if (read == 0)
            throw new EndOfStreamException("Redis RESP 帧提前结束。");
        return buffer[0];
    }

    /// <summary>
    /// 读取指定数量的字节。
    /// </summary>
    /// <param name="stream">数据流。</param>
    /// <param name="buffer">缓冲区。</param>
    /// <param name="offset">缓冲区偏移量。</param>
    /// <param name="count">待读取字节数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        while (count > 0)
        {
            var read = await stream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Redis RESP 帧提前结束。");
            offset += read;
            count -= read;
        }
    }

    /// <summary>
    /// 向 RESP 帧追加单个字节。
    /// </summary>
    /// <param name="frame">协议帧。</param>
    /// <param name="value">待处理值。</param>
    private static void AppendByte(MemoryStream frame, byte value)
    {
        EnsureFrameCapacity(frame, 1);
        frame.WriteByte(value);
    }

    /// <summary>
    /// 向 RESP 帧追加字节缓冲区。
    /// </summary>
    /// <param name="frame">协议帧。</param>
    /// <param name="buffer">缓冲区。</param>
    /// <param name="count">数量。</param>
    private static void AppendBytes(MemoryStream frame, byte[] buffer, int count)
    {
        EnsureFrameCapacity(frame, count);
        frame.Write(buffer, 0, count);
    }

    /// <summary>
    /// 确保追加字节后帧不超过大小上限。
    /// </summary>
    /// <param name="frame">协议帧。</param>
    /// <param name="additionalBytes">待追加的字节数。</param>
    private static void EnsureFrameCapacity(MemoryStream frame, int additionalBytes)
    {
        if (additionalBytes < 0 || frame.Length > MaxFrameBytes - additionalBytes)
            throw new InvalidDataException("RESP 帧超过代理限制。");
    }

    /// <summary>
    /// 忽略连接关闭过程中产生的任务异常。
    /// </summary>
    /// <param name="task">异步任务。</param>
    private static async Task IgnoreConnectionFailureAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // 代理关闭连接时，另一侧的读写通常会以 SocketException 结束。
        }
    }

    /// <summary>
    /// 封装客户端与上游 Redis 的代理连接状态。
    /// </summary>
    private sealed class Connection
    {
        /// <summary>
        /// 保护连接套接字读写操作的同步对象。
        /// </summary>
        private readonly object _socketLock = new();
        /// <summary>
        /// 保存客户端 TCP 连接。
        /// </summary>
        private readonly TcpClient _client;
        /// <summary>
        /// 保存上游 Redis 连接。
        /// </summary>
        private TcpClient? _upstream;
        /// <summary>
        /// 保存释放暂存响应的完成信号。
        /// </summary>
        private TaskCompletionSource<object?>? _heldResponseRelease;
        /// <summary>
        /// 保存暂存的 Redis 响应内容。
        /// </summary>
        private byte[]? _heldResponse;
        /// <summary>
        /// 保存连接是否已中止。
        /// </summary>
        private int _aborted;

        /// <summary>
        /// 初始化一个 <see cref="Connection"/> 类型的实例。
        /// </summary>
        /// <param name="client">Redis 测试客户端。</param>
        public Connection(TcpClient client)
        {
            _client = client;
        }

        /// <summary>
        /// 获取客户端 TCP 连接。
        /// </summary>
        public TcpClient Client => _client;

        /// <summary>
        /// 关联上游 Redis TCP 连接。
        /// </summary>
    /// <param name="upstream">上游 Redis TCP 客户端。</param>
        public void AttachUpstream(TcpClient upstream)
        {
            lock (_socketLock)
            {
                if (Volatile.Read(ref _aborted) != 0)
                {
                    upstream.Dispose();
                    return;
                }
                _upstream = upstream;
            }
        }

        /// <summary>
        /// 返回与上游 Redis 建立的网络流。
        /// </summary>
        /// <returns>已建立的上游网络流。</returns>
        public Stream GetUpstreamStream()
        {
            lock (_socketLock)
            {
                if (_upstream == null)
                    throw new InvalidOperationException("代理上游连接尚未建立。");
                return _upstream.GetStream();
            }
        }

        /// <summary>
        /// 暂存待发送的 Redis 响应。
        /// </summary>
        /// <param name="response">响应内容。</param>
        public void HoldResponse(byte[] response)
        {
            lock (_socketLock)
            {
                _heldResponse = response;
                _heldResponseRelease = new TaskCompletionSource<object?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        /// <summary>
        /// 等待暂存响应被释放或取消。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task WaitForHeldResponseReleaseAsync(CancellationToken cancellationToken)
        {
            Task release;
            lock (_socketLock)
            {
                release = _heldResponseRelease?.Task ?? Task.CompletedTask;
            }
            return release.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// 释放暂存的 Redis 响应。
        /// </summary>
        public void ReleaseHeldResponse()
        {
            TaskCompletionSource<object?>? release;
            lock (_socketLock)
                release = _heldResponseRelease;
            release?.TrySetResult(null);
        }

        /// <summary>
        /// 取出暂存的 Redis 响应。
        /// </summary>
        /// <returns>暂存的响应；没有响应时返回 <see langword="null" />。</returns>
        public byte[]? TakeHeldResponse()
        {
            lock (_socketLock)
            {
                var response = _heldResponse;
                _heldResponse = null;
                _heldResponseRelease = null;
                return response;
            }
        }

        /// <summary>
        /// 中止当前测试连接。
        /// </summary>
        public void Abort()
        {
            if (Interlocked.Exchange(ref _aborted, 1) != 0)
                return;

            TaskCompletionSource<object?>? release;
            TcpClient? upstream;
            lock (_socketLock)
            {
                release = _heldResponseRelease;
                upstream = _upstream;
                _heldResponse = null;
                _heldResponseRelease = null;
                _upstream = null;
            }

            release?.TrySetResult(null);
            upstream?.Dispose();
            _client.Dispose();
        }
    }
}
