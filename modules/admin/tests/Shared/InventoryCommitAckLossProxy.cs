using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>
/// 为库存集成测试丢弃一次真实 MySQL 提交成功确认。
/// </summary>
/// <remarks>仅连接回环地址，透明转发普通报文；不保存认证、SQL 或业务报文，不伪造数据库提交结果。</remarks>
internal sealed class InventoryCommitAckLossProxy : IAsyncDisposable
{
    /// <summary>
    /// 仅标记当前数据库连接的测试控制命令。
    /// </summary>
    internal const string MarkerCommand = "SET @bing_test_commit_ack_loss=1";

    /// <summary>
    /// 测试代理允许转发的单个报文最大负载字节数。
    /// </summary>
    private const int MaximumPayloadLength = 1024 * 1024;

    /// <summary>
    /// MySQL 普通报文头包含的字节数。
    /// </summary>
    private const int PacketHeaderLength = 4;

    /// <summary>
    /// 协商查询参数属性的客户端能力位。
    /// </summary>
    private const uint QueryAttributesCapability = 0x08000000;

    /// <summary>
    /// MySQL 查询命令的协议字节。
    /// </summary>
    private const byte QueryCommand = 0x03;

    /// <summary>
    /// 当前测试代理拥有的回环监听器。
    /// </summary>
    private readonly TcpListener _listener;

    /// <summary>
    /// 经过校验的原始数据库回环地址。
    /// </summary>
    private readonly IPAddress _targetAddress;

    /// <summary>
    /// 原始数据库监听端口。
    /// </summary>
    private readonly int _targetPort;

    /// <summary>
    /// 覆盖接受循环及所有转发连接的取消源。
    /// </summary>
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>
    /// 保护连接登记和共享清理任务的同步对象。
    /// </summary>
    private readonly object _sync = new();

    /// <summary>
    /// 从启动以来登记的连接及完成任务，保留到全部清理结束。
    /// </summary>
    private readonly List<ConnectionSession> _connections = new();

    /// <summary>
    /// 实际丢弃提交确认或发生代理故障的完成信号。
    /// </summary>
    private readonly TaskCompletionSource<bool> _acknowledgementDropped =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 当前代理的接受循环完成任务。
    /// </summary>
    private readonly Task _acceptLoop;

    /// <summary>
    /// 所有重复释放调用共同等待的清理任务。
    /// </summary>
    private Task _disposeTask;

    /// <summary>
    /// 是否已请求关闭代理。
    /// </summary>
    private int _disposeRequested;

    /// <summary>
    /// 是否已经消费全代理唯一的提交确认丢失故障。
    /// </summary>
    private int _faultConsumed;

    /// <summary>
    /// 实际没有向客户端转发的提交确认数量。
    /// </summary>
    private int _dropCount;

    /// <summary>
    /// 初始化一个 <see cref="InventoryCommitAckLossProxy" /> 类型的实例。
    /// </summary>
    /// <param name="originalConnectionString">原始专用回环测试数据库连接字符串。</param>
    internal InventoryCommitAckLossProxy(string originalConnectionString)
    {
        MySqlConnectionStringBuilder builder;
        try
        {
            builder = new MySqlConnectionStringBuilder(originalConnectionString);
        }
        catch (ArgumentException)
        {
            throw new ArgumentException("提交确认丢失代理需要有效的测试数据库连接配置。",
                nameof(originalConnectionString));
        }

        if (string.Equals(builder.Server, "localhost", StringComparison.OrdinalIgnoreCase))
            _targetAddress = IPAddress.Loopback;
        else if (!IPAddress.TryParse(builder.Server, out _targetAddress) || !IPAddress.IsLoopback(_targetAddress))
            throw new ArgumentException("提交确认丢失代理只允许回环数据库目标。", nameof(originalConnectionString));
        if (builder.Port is 0 or > 65535)
            throw new ArgumentException("测试数据库端口必须在有效范围内。", nameof(originalConnectionString));
        _targetPort = checked((int)builder.Port);
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        builder.Server = "127.0.0.1";
        builder.Port = checked((uint)((IPEndPoint)_listener.LocalEndpoint).Port);
        builder.SslMode = MySqlSslMode.None;
        builder.UseCompression = false;
        builder.Pooling = false;
        builder.AllowPublicKeyRetrieval = true;
        // 测试标记使用当前连接的 MySQL 用户变量，不能被驱动当作未绑定参数。
        builder.AllowUserVariables = true;
        ConnectionString = builder.ConnectionString;
        _acceptLoop = AcceptConnectionsAsync();
    }

    /// <summary>
    /// 获取使用本测试代理的数据库连接字符串。
    /// </summary>
    internal string ConnectionString { get; }

    /// <summary>
    /// 获取实际丢弃提交确认的等待任务。
    /// </summary>
    /// <remarks>未丢弃确认时正常释放会取消此任务；协议或转发故障会使任务失败，不伪造成功信号。</remarks>
    internal Task AcknowledgementDropped => _acknowledgementDropped.Task;

    /// <summary>
    /// 获取实际丢弃提交确认的次数。
    /// </summary>
    internal int DropCount => Volatile.Read(ref _dropCount);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_sync)
            return new ValueTask(_disposeTask ??= DisposeCoreAsync());
    }

    /// <summary>
    /// 接受并登记当前代理拥有的转发连接。
    /// </summary>
    private async Task AcceptConnectionsAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_lifetime.Token).ConfigureAwait(false);
                ConnectionSession session = null;
                try
                {
                    session = new ConnectionSession(this, client);
                    lock (_sync)
                    {
                        _connections.Add(session);
                        if (Volatile.Read(ref _disposeRequested) != 0)
                            session.Close();
                        session.Start();
                    }
                }
                catch
                {
                    if (session == null)
                        client.Dispose();
                    throw;
                }
            }
        }
        catch (Exception exception) when (Volatile.Read(ref _disposeRequested) != 0 &&
                                          exception is OperationCanceledException or SocketException or ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            ReportUnexpectedFailure(exception);
            throw;
        }
    }

    /// <summary>
    /// 停止接受连接并等待所有转发和资源清理完成。
    /// </summary>
    private async Task DisposeCoreAsync()
    {
        var errors = new List<Exception>();
        ConnectionSession[] sessions;
        Interlocked.Exchange(ref _disposeRequested, 1);
        try
        {
            _lifetime.Cancel();
            _listener.Stop();
            lock (_sync)
                sessions = _connections.ToArray();
            foreach (var session in sessions)
                session.Close();

            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            // 接受循环完全结束后再取最终快照，覆盖关闭请求附近已经接受的连接。
            lock (_sync)
                sessions = _connections.ToArray();
            foreach (var session in sessions)
                session.Close();
            foreach (var session in sessions)
            {
                try
                {
                    await session.Completion.ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    if (!errors.Any(error => ReferenceEquals(error, exception)))
                        errors.Add(exception);
                }
            }
        }
        finally
        {
            _lifetime.Dispose();
            _acknowledgementDropped.TrySetCanceled();
            lock (_sync)
                _connections.Clear();
        }

        if (errors.Count == 1)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException("提交确认丢失代理存在未预期的转发故障。", errors);
    }

    /// <summary>
    /// 消费唯一故障并记录真实提交确认已经被丢弃。
    /// </summary>
    /// <returns>当前连接成功消费故障时返回 true，否则返回 false。</returns>
    private bool TryDropAcknowledgement()
    {
        if (Interlocked.CompareExchange(ref _faultConsumed, 1, 0) != 0)
            return false;
        Interlocked.Increment(ref _dropCount);
        _acknowledgementDropped.TrySetResult(true);
        return true;
    }

    /// <summary>
    /// 将未预期的代理故障暴露给等待测试。
    /// </summary>
    /// <param name="exception">不包含认证或业务报文的代理异常。</param>
    private void ReportUnexpectedFailure(Exception exception) => _acknowledgementDropped.TrySetException(exception);

    /// <summary>
    /// 管理一个客户端与数据库连接的双向转发。
    /// </summary>
    private sealed class ConnectionSession
    {
        /// <summary>
        /// 登记当前连接及故障信号的代理所有者。
        /// </summary>
        private readonly InventoryCommitAckLossProxy _owner;

        /// <summary>
        /// 当前代理接受并拥有的客户端连接。
        /// </summary>
        private readonly TcpClient _client;

        /// <summary>
        /// 当前代理创建并拥有的数据库连接。
        /// </summary>
        private readonly TcpClient _server;

        /// <summary>
        /// 当前连接的双向转发取消源。
        /// </summary>
        private readonly CancellationTokenSource _cancellation;

        /// <summary>
        /// 防止连接关闭和取消源释放相互竞争的同步对象。
        /// </summary>
        private readonly object _closeSync = new();

        /// <summary>
        /// 当前连接的转发及清理完成任务。
        /// </summary>
        private Task _completion = Task.CompletedTask;

        /// <summary>
        /// 是否已经关闭当前连接。
        /// </summary>
        private int _closed;

        /// <summary>
        /// 是否已识别首个客户端握手响应。
        /// </summary>
        private bool _handshakeSeen;

        /// <summary>
        /// 当前连接是否协商查询参数属性能力。
        /// </summary>
        private bool _queryAttributes;

        /// <summary>
        /// 当前连接是否收到精确的测试控制命令。
        /// </summary>
        private bool _marked;

        /// <summary>
        /// 当前连接是否正在等待标记后的提交确认。
        /// </summary>
        private int _awaitingCommit;

        /// <summary>
        /// 初始化一个 <see cref="ConnectionSession" /> 类型的实例。
        /// </summary>
        /// <param name="owner">当前连接的代理所有者。</param>
        /// <param name="client">本代理接受并拥有的客户端连接。</param>
        internal ConnectionSession(InventoryCommitAckLossProxy owner, TcpClient client)
        {
            _owner = owner;
            _client = client;
            _server = new TcpClient(owner._targetAddress.AddressFamily);
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(owner._lifetime.Token);
        }

        /// <summary>
        /// 获取双向转发及清理完成任务。
        /// </summary>
        internal Task Completion => _completion;

        /// <summary>
        /// 启动当前连接的双向转发。
        /// </summary>
        internal void Start() => _completion = RunAsync();

        /// <summary>
        /// 取消转发并关闭当前连接的两端。
        /// </summary>
        internal void Close()
        {
            lock (_closeSync)
            {
                if (_closed != 0)
                    return;
                Volatile.Write(ref _closed, 1);
                _cancellation.Cancel();
                _client.Close();
                _server.Close();
            }
        }

        /// <summary>
        /// 连接真实数据库并等待两方向转发及资源释放。
        /// </summary>
        private async Task RunAsync()
        {
            try
            {
                await _server.ConnectAsync(_owner._targetAddress, _owner._targetPort, _cancellation.Token)
                    .ConfigureAwait(false);
                var clientStream = _client.GetStream();
                var serverStream = _server.GetStream();
                var toServer = PumpAsync(clientStream, serverStream, fromClient: true);
                var toClient = PumpAsync(serverStream, clientStream, fromClient: false);
                var both = Task.WhenAll(toServer, toClient);
                await Task.WhenAny(toServer, toClient).ConfigureAwait(false);
                Close();
                try
                {
                    await both.ConfigureAwait(false);
                }
                catch
                {
                    var failures = both.Exception?.Flatten().InnerExceptions
                        .Where(exception => !IsExpectedClosedFailure(exception)).ToArray();
                    if (failures?.Length == 1)
                        ExceptionDispatchInfo.Capture(failures[0]).Throw();
                    if (failures?.Length > 1)
                        throw new AggregateException("数据库报文转发存在未预期的协议故障。", failures);
                    if (!both.IsFaulted && !both.IsCanceled)
                        throw;
                }
            }
            catch (Exception exception)
            {
                if (_cancellation.IsCancellationRequested &&
                    exception is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
                    Close();
                if (IsExpectedClosedFailure(exception))
                    return;
                _owner.ReportUnexpectedFailure(exception);
                throw;
            }
            finally
            {
                Close();
                lock (_closeSync)
                {
                    _client.Dispose();
                    _server.Dispose();
                    _cancellation.Dispose();
                }
            }
        }

        /// <summary>
        /// 转发一个方向的普通 MySQL 报文。
        /// </summary>
        /// <param name="source">当前方向的读取连接。</param>
        /// <param name="destination">当前方向的写入连接。</param>
        /// <param name="fromClient">是否读取客户端发送的命令。</param>
        private async Task PumpAsync(NetworkStream source, NetworkStream destination, bool fromClient)
        {
            while (!_cancellation.IsCancellationRequested)
            {
                var packet = await ReadPacketAsync(source, _cancellation.Token).ConfigureAwait(false);
                if (packet == null)
                    return;
                if (fromClient)
                    ObserveClientControl(packet);
                else if (ShouldDropAcknowledgement(packet))
                {
                    Close();
                    return;
                }
                await destination.WriteAsync(packet, _cancellation.Token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 识别握手能力以及当前连接的测试控制命令。
        /// </summary>
        /// <param name="packet">仅在本次转发期间持有的客户端报文。</param>
        private void ObserveClientControl(byte[] packet)
        {
            var payloadLength = packet.Length - PacketHeaderLength;
            if (!_handshakeSeen && packet[3] == 1 && payloadLength >= 32)
            {
                _handshakeSeen = true;
                _queryAttributes = (BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(PacketHeaderLength, 4)) &
                                    QueryAttributesCapability) != 0;
                return;
            }
            if (packet[3] != 0 || payloadLength < 1 || packet[PacketHeaderLength] != QueryCommand)
                return;
            var offset = PacketHeaderLength + 1;
            if (_queryAttributes)
            {
                // 只识别无参数、单参数集合的控制命令，其他属性报文完全透明转发。
                if (packet.Length < offset + 2 || packet[offset] != 0 || packet[offset + 1] != 1)
                    return;
                offset += 2;
            }
            var sql = Encoding.UTF8.GetString(packet, offset, packet.Length - offset);
            if (string.Equals(sql, MarkerCommand, StringComparison.Ordinal))
            {
                _marked = true;
                return;
            }
            if (!_marked)
                return;
            var command = sql.Trim();
            if (command.EndsWith(";", StringComparison.Ordinal))
                command = command[..^1].TrimEnd();
            if (string.Equals(command, "COMMIT", StringComparison.OrdinalIgnoreCase))
                Volatile.Write(ref _awaitingCommit, 1);
        }

        /// <summary>
        /// 仅在当前连接等待真实提交成功确认时消费故障。
        /// </summary>
        /// <param name="packet">仅在本次转发期间持有的数据库报文。</param>
        /// <returns>本报文确认为唯一需要丢弃的提交确认时返回 true，否则返回 false。</returns>
        private bool ShouldDropAcknowledgement(byte[] packet)
        {
            if (Volatile.Read(ref _awaitingCommit) == 0 || packet.Length <= PacketHeaderLength)
                return false;
            if (packet[PacketHeaderLength] == 0xff)
            {
                Volatile.Write(ref _awaitingCommit, 0);
                return false;
            }
            if (packet.Length - PacketHeaderLength < 7 || packet[PacketHeaderLength] != 0x00)
                return false;
            Volatile.Write(ref _awaitingCommit, 0);
            return _owner.TryDropAcknowledgement();
        }

        /// <summary>
        /// 判断异常是否属于连接已经关闭后的常规转发结束。
        /// </summary>
        /// <param name="exception">当前转发异常。</param>
        /// <returns>当前连接已关闭且异常表示网络或取消结束时返回 true，否则返回 false。</returns>
        private bool IsExpectedClosedFailure(Exception exception) => Volatile.Read(ref _closed) != 0 &&
            (exception is OperationCanceledException or IOException or SocketException or ObjectDisposedException) &&
            exception is not InvalidDataException;

        /// <summary>
        /// 读取一个有界普通 MySQL 报文。
        /// </summary>
        /// <param name="stream">读取报文的当前连接。</param>
        /// <param name="cancellationToken">当前连接的取消令牌。</param>
        /// <returns>完整报文；连接结束时返回 null。</returns>
        private static async Task<byte[]> ReadPacketAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var header = new byte[PacketHeaderLength];
            if (!await ReadUntilCompleteAsync(stream, header, cancellationToken).ConfigureAwait(false))
                return null;
            var length = header[0] | header[1] << 8 | header[2] << 16;
            if (length > MaximumPayloadLength)
                throw new InvalidDataException("测试代理收到超出限制的数据库报文。");
            var packet = new byte[PacketHeaderLength + length];
            header.CopyTo(packet, 0);
            if (length > 0 && !await ReadUntilCompleteAsync(stream, packet.AsMemory(PacketHeaderLength), cancellationToken)
                    .ConfigureAwait(false))
                return null;
            return packet;
        }

        /// <summary>
        /// 读取指定缓冲区直到完整或连接结束。
        /// </summary>
        /// <param name="stream">读取数据的当前连接。</param>
        /// <param name="buffer">仅用于当前报文的有界缓冲区。</param>
        /// <param name="cancellationToken">当前连接的取消令牌。</param>
        /// <returns>缓冲区完整时返回 true，连接结束时返回 false。</returns>
        private static async Task<bool> ReadUntilCompleteAsync(NetworkStream stream, Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var count = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
                if (count == 0)
                    return false;
                offset += count;
            }
            return true;
        }
    }
}
