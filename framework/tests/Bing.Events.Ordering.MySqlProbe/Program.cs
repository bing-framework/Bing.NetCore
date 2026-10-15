using System.Data.Common;
using System.Text;
using Bing.Data.Events.Ordering;
using Bing.Test.Shared;
using MySqlConnector;

namespace Bing.Events.Ordering.MySqlProbe;

/// <summary>
/// 在独立测试进程中竞争序号和实体游标。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 运行有界 MySQL 竞争或中断探针。
    /// </summary>
    private static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            if (args.Length != 3 || !int.TryParse(args[2], out var limit) || limit < 1 || limit > 100)
                throw new ArgumentException("探针要求模式、隔离标识和 1 至 100 次操作。");
            var connectionString = IntegrationTestConnectionStringResolver.Resolve("MySql");
            IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connectionString, "MySql");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(deadline.Token);
            await WaitForReleaseAsync(connection, args[1], deadline.Token);
            var store = new DbOrderedEventStore(OrderedEventDatabase.MySql);
            var stream = new OrderedEventStream(args[1], "inventory", "item");
            for (var index = 0; index < limit; index++)
            {
                var processed = await ExecuteAsync(connection, store, stream, args[0], args[1], deadline.Token);
                if (!processed)
                    break;
                if (args[0] is "allocate" or "consume")
                    Console.WriteLine($"COMMIT {index + 1}");
            }
            Console.WriteLine("DONE");
            return 0;
        }
        catch (Exception exception)
        {
            // 不打印连接字符串或消息正文。
            Console.Error.WriteLine(exception.GetType().Name + ": " + exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// 登记进程就绪并等待父进程同时放行。
    /// </summary>
    private static async Task WaitForReleaseAsync(MySqlConnection connection, string scope, CancellationToken token)
    {
        using (var ready = CreateCommand(connection, null,
                   "UPDATE bing_order_probe_gate SET ReadyCount = ReadyCount + 1 WHERE ScopeKey = @scope", scope))
        {
            if (await ready.ExecuteNonQueryAsync(token) != 1)
                throw new InvalidOperationException("父进程未准备隔离探针门控。");
        }
        while (true)
        {
            using var read = CreateCommand(connection, null,
                "SELECT Released FROM bing_order_probe_gate WHERE ScopeKey = @scope", scope);
            if (Convert.ToInt32(await read.ExecuteScalarAsync(token)) == 1)
                return;
            await Task.Delay(10, token);
        }
    }

    /// <summary>
    /// 在完整事务中分配序号或处理下一事件。
    /// </summary>
    private static async Task<bool> ExecuteAsync(MySqlConnection connection, DbOrderedEventStore store,
        OrderedEventStream stream, string mode, string scope, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            await using var transaction = await connection.BeginTransactionAsync(token);
            try
            {
                if (mode == "allocate")
                {
                    var sequence = await store.AllocateSequenceAsync(transaction, stream, token);
                    await InsertBusinessAsync(transaction, scope, "allocate", sequence, token);
                }
                else if (mode is "consume" or "before-commit" or "after-commit")
                {
                    var result = await store.ProcessNextAsync(transaction, "consumer-" + scope, stream,
                        async (message, current, currentToken) =>
                        {
                            await InsertBusinessAsync(current, scope, "consume", message.Sequence, currentToken);
                            if (mode == "before-commit")
                            {
                                Console.WriteLine("ACTIVE");
                                await Task.Delay(Timeout.Infinite, currentToken);
                            }
                            // 延长持锁窗口，使两个进程实际竞争同一实体。
                            await Task.Delay(15, currentToken);
                        }, token);
                    if (result == OrderedEventProcessingResult.Empty)
                    {
                        await transaction.RollbackAsync(token);
                        return false;
                    }
                    if (result != OrderedEventProcessingResult.Processed)
                        throw new InvalidOperationException("探针业务流意外阻塞：" + result);
                }
                else
                    throw new ArgumentException("不支持的探针模式。");
                await transaction.CommitAsync(token);
                if (mode == "after-commit")
                {
                    Console.WriteLine("COMMITTED");
                    await Task.Delay(Timeout.Infinite, token);
                }
                return true;
            }
            catch (MySqlException exception) when (exception.Number is 1205 or 1213 && attempt < 5)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                await Task.Delay(20 * (attempt + 1), token);
            }
        }
    }

    /// <summary>
    /// 写入与游标或序号共享事务的测试业务结果。
    /// </summary>
    private static async Task InsertBusinessAsync(DbTransaction transaction, string scope, string phase,
        long sequence, CancellationToken token)
    {
        using var command = CreateCommand(transaction.Connection, transaction,
            "INSERT INTO bing_order_probe_business (ScopeKey, Phase, Sequence, ProcessId) VALUES (@scope, @phase, @sequence, @process)", scope);
        command.Parameters.AddWithValue("@phase", phase);
        command.Parameters.AddWithValue("@sequence", sequence);
        command.Parameters.AddWithValue("@process", Environment.ProcessId);
        await command.ExecuteNonQueryAsync(token);
    }

    /// <summary>
    /// 创建测试连接内的参数化命令。
    /// </summary>
    private static MySqlCommand CreateCommand(DbConnection connection, DbTransaction transaction, string sql, string scope)
    {
        var command = new MySqlCommand(sql, (MySqlConnection)connection, (MySqlTransaction)transaction);
        command.Parameters.AddWithValue("@scope", scope);
        return command;
    }
}
