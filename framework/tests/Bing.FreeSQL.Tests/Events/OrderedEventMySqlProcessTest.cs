using System.Data;
using System.Diagnostics;
using System.Text;
using Bing.Data.Events.Ordering;
using Bing.Test.Shared;
using MySqlConnector;
using Xunit;

namespace Bing.FreeSQL.Tests.Events;

/// <summary>
/// 验证真实 MySQL 上跨进程的有序事件提交与中断恢复。
/// </summary>
[Collection("MySqlOrderedEvents")]
public sealed class OrderedEventMySqlProcessTest
{
    /// <summary>
    /// 验证两个独立进程同时分配序号时业务与序号连续提交。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task AllocateSequence_TwoProcesses_ShouldCommitContiguousNumbers()
    {
        await WithScopeAsync(async (connection, scope, children) =>
        {
            children.Add(StartProbe("allocate", scope, 20));
            children.Add(StartProbe("allocate", scope, 20));
            await WaitForReadyAsync(connection, scope, children, 2);
            await ReleaseAsync(connection, scope);
            foreach (var child in children)
                await WaitForSuccessAsync(child);

            var rows = await ReadBusinessAsync(connection, scope, "allocate");
            Assert.Equal(Enumerable.Range(1, 40).Select(value => (long)value), rows.Select(row => row.Sequence));
            Assert.Equal(2, rows.Select(row => row.ProcessId).Distinct().Count());
            Assert.Equal(40, await ReadAllocatedSequenceAsync(connection, scope));
        });
    }

    /// <summary>
    /// 验证两个独立进程接收乱序事件后按同实体序号提交。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task Consume_TwoProcesses_ShouldCommitFortyEventsInOrder()
    {
        await WithScopeAsync(async (connection, scope, children) =>
        {
            var stream = Stream(scope);
            var store = Store();
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                foreach (var sequence in new[] { 3, 1, 2 }.Concat(Enumerable.Range(4, 37).Reverse()))
                    await store.EnqueueAsync(transaction, Consumer(scope), stream,
                        new OrderedEventMessage($"event-{sequence}", sequence, $"payload-{sequence}"));
                await transaction.CommitAsync();
            }
            children.Add(StartProbe("consume", scope, 40));
            children.Add(StartProbe("consume", scope, 40));
            await WaitForReadyAsync(connection, scope, children, 2);
            await ReleaseAsync(connection, scope);
            foreach (var child in children)
                await WaitForSuccessAsync(child);

            var rows = await ReadBusinessAsync(connection, scope, "consume");
            Assert.Equal(Enumerable.Range(1, 40).Select(value => (long)value), rows.Select(row => row.Sequence));
            await using var read = await connection.BeginTransactionAsync();
            var state = await store.GetStateAsync(read, Consumer(scope), stream);
            Assert.Equal(40, state.LastSequence);
            Assert.Equal(0, state.PendingCount);
            await read.RollbackAsync();
        });
    }

    /// <summary>
    /// 验证业务尚未提交时进程中断会回滚且重启后可处理。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task Consume_WhenProcessDiesBeforeCommit_ShouldRollbackAndRetry()
    {
        await WithScopeAsync(async (connection, scope, children) =>
        {
            await EnqueueFirstAsync(connection, scope);
            var interrupted = StartProbe("before-commit", scope, 1);
            children.Add(interrupted);
            await WaitForReadyAsync(connection, scope, children, 1);
            await ReleaseAsync(connection, scope);
            Assert.Equal("ACTIVE", await WaitForSignalAsync(interrupted));
            await StopOwnedAsync(interrupted);

            Assert.Empty(await ReadBusinessAsync(connection, scope, "consume"));
            Assert.Equal(0, await ReadCursorAsync(connection, scope));
            var retry = StartProbe("consume", scope, 1);
            children.Add(retry);
            await WaitForSuccessAsync(retry);
            Assert.Equal(new long[] { 1 },
                (await ReadBusinessAsync(connection, scope, "consume")).Select(row => row.Sequence));
            Assert.Equal(1, await ReadCursorAsync(connection, scope));
        });
    }

    /// <summary>
    /// 验证提交后进程中断不会导致重入时重复执行业务。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task Consume_WhenProcessDiesAfterCommit_ShouldNotRepeatBusiness()
    {
        await WithScopeAsync(async (connection, scope, children) =>
        {
            await EnqueueFirstAsync(connection, scope);
            var interrupted = StartProbe("after-commit", scope, 1);
            children.Add(interrupted);
            await WaitForReadyAsync(connection, scope, children, 1);
            await ReleaseAsync(connection, scope);
            Assert.Equal("COMMITTED", await WaitForSignalAsync(interrupted));
            await StopOwnedAsync(interrupted);

            var retry = StartProbe("consume", scope, 1);
            children.Add(retry);
            await WaitForSuccessAsync(retry);
            Assert.Equal(new long[] { 1 },
                (await ReadBusinessAsync(connection, scope, "consume")).Select(row => row.Sequence));
            Assert.Equal(1, await ReadCursorAsync(connection, scope));
        });
    }

    /// <summary>
    /// 验证旧一致性快照下的锁定读取仍获取最新游标。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task ProcessNextAsync_RepeatableReadSnapshot_ShouldSeeCommittedCursor()
    {
        await WithScopeAsync(async (connection, scope, _) =>
        {
            var store = Store();
            var stream = Stream(scope);
            await using (var seed = await connection.BeginTransactionAsync())
            {
                await store.EnqueueAsync(seed, Consumer(scope), stream, new OrderedEventMessage("first", 1, "one"));
                await store.EnqueueAsync(seed, Consumer(scope), stream, new OrderedEventMessage("second", 2, "two"));
                await seed.CommitAsync();
            }
            // 已打开连接的 ConnectionString 默认不再包含口令，第二连接使用原始测试配置。
            await using var snapshotConnection = new MySqlConnection(
                IntegrationTestConnectionStringResolver.Resolve("MySql"));
            await snapshotConnection.OpenAsync();
            await using var snapshot = await snapshotConnection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
            using (var read = new MySqlCommand(
                       "SELECT LastSequence FROM bing_event_cursor WHERE StreamKey = @stream", snapshotConnection, snapshot))
            {
                read.Parameters.AddWithValue("@stream", store.GetStreamKey(stream));
                Assert.Equal(0L, Convert.ToInt64(await read.ExecuteScalarAsync()));
            }
            await using (var advance = await connection.BeginTransactionAsync())
            {
                Assert.Equal(OrderedEventProcessingResult.Processed,
                    await store.ProcessNextAsync(advance, Consumer(scope), stream,
                        (message, tx, token) => InsertBusinessAsync(tx, scope, message.Sequence, token)));
                await advance.CommitAsync();
            }

            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(snapshot, Consumer(scope), stream,
                    (message, tx, token) => InsertBusinessAsync(tx, scope, message.Sequence, token)));
            await snapshot.CommitAsync();
            Assert.Equal(new long[] { 1, 2 },
                (await ReadBusinessAsync(connection, scope, "consume")).Select(row => row.Sequence));
            Assert.Equal(2, await ReadCursorAsync(connection, scope));
        });
    }

    /// <summary>
    /// 在专用测试数据库中为每项测试建立隔离的业务流。
    /// </summary>
    private static async Task WithScopeAsync(Func<MySqlConnection, string, List<Process>, Task> action)
    {
        var connectionString = IntegrationTestConnectionStringResolver.Resolve("MySql");
        IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connectionString, "MySql");
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var sql in OrderedEventSchema.GetCreateStatements(OrderedEventDatabase.MySql))
        {
            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
        using (var gate = new MySqlCommand(
                   "CREATE TABLE IF NOT EXISTS bing_order_probe_gate " +
                   "(ScopeKey varchar(100) NOT NULL PRIMARY KEY, ReadyCount int NOT NULL, Released tinyint NOT NULL) ENGINE=InnoDB",
                   connection))
            await gate.ExecuteNonQueryAsync();
        using (var business = new MySqlCommand(
                   "CREATE TABLE IF NOT EXISTS bing_order_probe_business " +
                   "(ScopeKey varchar(100) NOT NULL, Phase varchar(20) NOT NULL, Sequence bigint NOT NULL, " +
                   "ProcessId int NOT NULL, Ordinal bigint NOT NULL AUTO_INCREMENT UNIQUE, " +
                   "PRIMARY KEY (ScopeKey, Phase, Sequence)) ENGINE=InnoDB", connection))
            await business.ExecuteNonQueryAsync();
        var scope = Guid.NewGuid().ToString("N");
        using (var gate = Command(connection,
                   "INSERT INTO bing_order_probe_gate (ScopeKey, ReadyCount, Released) VALUES (@scope, 0, 0)", scope))
            await gate.ExecuteNonQueryAsync();
        var children = new List<Process>();
        try
        {
            await action(connection, scope, children);
        }
        finally
        {
            foreach (var child in children)
            {
                await StopOwnedAsync(child);
                child.Dispose();
            }
            await CleanupScopeAsync(connection, scope);
        }
    }

    /// <summary>
    /// 启动当前测试专属的独立探针进程。
    /// </summary>
    private static Process StartProbe(string mode, string scope, int iterations)
    {
        var assembly = Path.Combine(AppContext.BaseDirectory, "OrderingProbe", "Bing.Events.Ordering.MySqlProbe.dll");
        Assert.True(File.Exists(assembly), "有序事件进程探针未复制到测试输出目录。");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(assembly);
        start.ArgumentList.Add(mode);
        start.ArgumentList.Add(scope);
        start.ArgumentList.Add(iterations.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var process = Process.Start(start);
        return Assert.IsType<Process>(process);
    }

    /// <summary>
    /// 有界等待所有子进程登记就绪。
    /// </summary>
    private static async Task WaitForReadyAsync(MySqlConnection connection, string scope,
        IReadOnlyList<Process> children, int expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            Assert.All(children, child => Assert.False(child.HasExited, "探针放行前异常退出。"));
            using var command = Command(connection,
                "SELECT ReadyCount FROM bing_order_probe_gate WHERE ScopeKey = @scope", scope);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) == expected)
                return;
            await Task.Delay(20);
        }
        throw new TimeoutException("探针就绪超时。");
    }

    /// <summary>
    /// 同时放行当前业务流的探针。
    /// </summary>
    private static async Task ReleaseAsync(MySqlConnection connection, string scope)
    {
        using var command = Command(connection,
            "UPDATE bing_order_probe_gate SET Released = 1 WHERE ScopeKey = @scope", scope);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    /// <summary>
    /// 有界等待进程成功退出。
    /// </summary>
    private static async Task WaitForSuccessAsync(Process process)
    {
        // 子进程自身期限为 60 秒；父进程留出回滚、连接释放和异常输出的时间。
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(75));
        await process.WaitForExitAsync(deadline.Token);
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        Assert.True(process.ExitCode == 0, $"探针失败，退出码 {process.ExitCode}。{error}；进度：{output}");
        Assert.Contains("DONE", output);
    }

    /// <summary>
    /// 有界等待中断探针写出指定阶段标识。
    /// </summary>
    private static async Task<string> WaitForSignalAsync(Process process)
    {
        var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
        Assert.NotNull(line);
        return line;
    }

    /// <summary>
    /// 仅终止当前测试创建的进程。
    /// </summary>
    private static async Task StopOwnedAsync(Process process)
    {
        if (process.HasExited)
            return;
        process.Kill(entireProcessTree: true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(deadline.Token);
    }

    /// <summary>
    /// 将首个待处理事件保存到当前隔离业务流。
    /// </summary>
    private static async Task EnqueueFirstAsync(MySqlConnection connection, string scope)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        await Store().EnqueueAsync(transaction, Consumer(scope), Stream(scope),
            new OrderedEventMessage("first", 1, "payload"));
        await transaction.CommitAsync();
    }

    /// <summary>
    /// 按实际写入顺序读取测试业务结果及进程标识。
    /// </summary>
    private static async Task<(long Sequence, int ProcessId)[]> ReadBusinessAsync(MySqlConnection connection,
        string scope, string phase)
    {
        using var command = Command(connection,
            "SELECT Sequence, ProcessId FROM bing_order_probe_business WHERE ScopeKey = @scope " +
            "AND Phase = @phase ORDER BY Ordinal", scope);
        command.Parameters.AddWithValue("@phase", phase);
        using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(long, int)>();
        while (await reader.ReadAsync())
            rows.Add((reader.GetInt64(0), reader.GetInt32(1)));
        return rows.ToArray();
    }

    /// <summary>
    /// 读取生产有序存储中的业务流游标。
    /// </summary>
    private static async Task<long> ReadCursorAsync(MySqlConnection connection, string scope)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        var state = await Store().GetStateAsync(transaction, Consumer(scope), Stream(scope));
        await transaction.RollbackAsync();
        return state.LastSequence;
    }

    /// <summary>
    /// 读取生产序号表的最后分配值。
    /// </summary>
    private static async Task<long> ReadAllocatedSequenceAsync(MySqlConnection connection, string scope)
    {
        using var command = new MySqlCommand(
            "SELECT LastSequence FROM bing_event_sequence WHERE StreamKey = @stream", connection);
        command.Parameters.AddWithValue("@stream", Store().GetStreamKey(Stream(scope)));
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 使用处理事务写入业务结果。
    /// </summary>
    private static async Task InsertBusinessAsync(System.Data.Common.DbTransaction transaction, string scope,
        long sequence, CancellationToken cancellationToken)
    {
        using var command = Command((MySqlConnection)transaction.Connection,
            "INSERT INTO bing_order_probe_business (ScopeKey, Phase, Sequence, ProcessId) " +
            "VALUES (@scope, 'consume', @sequence, @process)", scope);
        command.Transaction = (MySqlTransaction)transaction;
        command.Parameters.AddWithValue("@sequence", sequence);
        command.Parameters.AddWithValue("@process", Environment.ProcessId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 删除本次随机业务流产生的数据。
    /// </summary>
    private static async Task CleanupScopeAsync(MySqlConnection connection, string scope)
    {
        var streamKey = Store().GetStreamKey(Stream(scope));
        foreach (var sql in new[]
                 {
                     "DELETE FROM bing_order_probe_business WHERE ScopeKey = @scope",
                     "DELETE FROM bing_order_probe_gate WHERE ScopeKey = @scope"
                 })
        {
            using var command = Command(connection, sql, scope);
            await command.ExecuteNonQueryAsync();
        }
        foreach (var sql in new[]
                 {
                     "DELETE FROM bing_event_inbox WHERE StreamKey = @stream",
                     "DELETE FROM bing_event_inbox_archive WHERE StreamKey = @stream",
                     "DELETE FROM bing_event_cursor WHERE StreamKey = @stream",
                     "DELETE FROM bing_event_sequence WHERE StreamKey = @stream"
                 })
        {
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@stream", streamKey);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// 创建带随机隔离标识的 SQL 命令。
    /// </summary>
    private static MySqlCommand Command(MySqlConnection connection, string sql, string scope)
    {
        var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@scope", scope);
        return command;
    }

    /// <summary>
    /// 创建生产 MySQL 有序事件存储。
    /// </summary>
    private static DbOrderedEventStore Store() => new(OrderedEventDatabase.MySql);

    /// <summary>
    /// 创建当前测试专属业务流。
    /// </summary>
    private static OrderedEventStream Stream(string scope) => new(scope, "inventory", "item");

    /// <summary>
    /// 创建跨进程稳定的逻辑消费者标识。
    /// </summary>
    private static string Consumer(string scope) => "consumer-" + scope;
}
