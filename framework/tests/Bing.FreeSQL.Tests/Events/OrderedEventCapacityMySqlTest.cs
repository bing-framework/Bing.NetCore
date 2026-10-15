using Bing.Data.Events.Ordering;
using Bing.Test.Shared;
using MySqlConnector;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Bing.FreeSQL.Tests.Events;

/// <summary>
/// 验证真实 MySQL 上有序事件消费者全局容量闸门。
/// </summary>
[Collection("MySqlOrderedEvents")]
public sealed class OrderedEventCapacityMySqlTest
{
    /// <summary>
    /// 验证容量按逻辑消费者跨业务流限制，并在成功消费后释放。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task EnqueueAsync_WhenConsumerCapacityIsFull_ShouldRejectAndReleaseAfterProcessing()
    {
        var connectionString = IntegrationTestConnectionStringResolver.Resolve("MySql");
        IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connectionString, "MySql");
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var statement in OrderedEventSchema.GetCreateStatements(OrderedEventDatabase.MySql))
        {
            await using var command = new MySqlCommand(statement, connection);
            await command.ExecuteNonQueryAsync();
        }

        var scope = Guid.NewGuid().ToString("N");
        var consumer = "capacity-" + scope;
        var streams = new[]
        {
            new OrderedEventStream(scope, "inventory", "first"),
            new OrderedEventStream(scope, "inventory", "second"),
            new OrderedEventStream(scope, "inventory", "third")
        };
        var store = new DbOrderedEventStore(OrderedEventDatabase.MySql, maxPendingPerConsumer: 2);
        try
        {
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await store.EnqueueAsync(transaction, consumer, streams[0], Message("first"));
                await store.EnqueueAsync(transaction, consumer, streams[1], Message("second"));
                await transaction.CommitAsync();
            }

            await using (var rejected = await connection.BeginTransactionAsync())
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    store.EnqueueAsync(rejected, consumer, streams[2], Message("third")));
                await rejected.RollbackAsync();
            }

            await using (var process = await connection.BeginTransactionAsync())
            {
                Assert.Equal(OrderedEventProcessingResult.Processed,
                    await store.ProcessNextAsync(process, consumer, streams[0],
                        (_, _, _) => Task.CompletedTask));
                await process.CommitAsync();
            }

            await using (var enqueue = await connection.BeginTransactionAsync())
            {
                await store.EnqueueAsync(enqueue, consumer, streams[2], Message("third"));
                await enqueue.CommitAsync();
            }

            await using var read = new MySqlCommand(
                "SELECT PendingCount FROM bing_event_consumer_capacity WHERE ConsumerKey = @consumer",
                connection);
            read.Parameters.AddWithValue("@consumer", ConsumerKey(consumer));
            Assert.Equal(2L, Convert.ToInt64(await read.ExecuteScalarAsync()));
        }
        finally
        {
            await CleanupAsync(connection, consumer, store, streams);
        }
    }

    /// <summary>
    /// 创建测试消息。
    /// </summary>
    private static OrderedEventMessage Message(string id) =>
        new(id, 1, "payload", "1");

    /// <summary>
    /// 清理本测试创建的所有业务流和容量记录。
    /// </summary>
    private static async Task CleanupAsync(MySqlConnection connection, string consumer,
        DbOrderedEventStore store, IReadOnlyList<OrderedEventStream> streams)
    {
        var consumerKey = ConsumerKey(consumer);
        foreach (var stream in streams)
        {
            var streamKey = store.GetStreamKey(stream);
            foreach (var sql in new[]
                     {
                         "DELETE FROM bing_event_inbox WHERE ConsumerKey = @consumer AND StreamKey = @stream",
                         "DELETE FROM bing_event_inbox_archive WHERE ConsumerKey = @consumer AND StreamKey = @stream",
                         "DELETE FROM bing_event_cursor WHERE ConsumerKey = @consumer AND StreamKey = @stream",
                         "DELETE FROM bing_event_sequence WHERE StreamKey = @stream"
                     })
            {
                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@consumer", consumerKey);
                command.Parameters.AddWithValue("@stream", streamKey);
                await command.ExecuteNonQueryAsync();
            }
        }
        await using (var capacity = new MySqlCommand(
                         "DELETE FROM bing_event_consumer_capacity WHERE ConsumerKey = @consumer", connection))
        {
            capacity.Parameters.AddWithValue("@consumer", consumerKey);
            await capacity.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// 按存储契约生成测试消费者键。
    /// </summary>
    private static string ConsumerKey(string consumer)
    {
        using var algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(consumer)))
            .Replace("-", string.Empty, StringComparison.Ordinal);
    }
}
