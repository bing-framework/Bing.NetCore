using Bing.Samples.SqlUnitOfWork.Infrastructure.Events;
using MySqlConnector;

namespace Bing.Samples.SqlUnitOfWork.Consumer;

/// <summary>
/// 在 CAP runtime 的当前数据源内持久化消费幂等记录和业务副作用。
/// </summary>
public sealed class CapConsumerProcessingService
{
    private readonly CapConsumerSourceOptions _options;

    public CapConsumerProcessingService(CapConsumerSourceOptions options) => _options = options;

    public async Task ProcessAsync(SqlUnitOfWorkEventPayload message, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var attemptId = await BeginAttemptAsync(connection, message, cancellationToken);

        try
        {
            if (message.Scenario == "retry-once" &&
                await TryInsertFailureMarkerAsync(connection, message.EventId, cancellationToken))
            {
                await FinishAttemptAsync(connection, attemptId, "Failed", "injected-once", cancellationToken);
                throw new InvalidOperationException("CAP sample injects one consumer failure before the idempotent transaction.");
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var inboxCommand = connection.CreateCommand();
            inboxCommand.Transaction = transaction;
            inboxCommand.CommandText = @"INSERT IGNORE INTO CapConsumerInbox
                (Source, EventId, ConsumerName, OperationId, TraceId, RootId, AttemptCount, AppliedAt)
                VALUES (@source, @eventId, @consumerName, @operationId, @traceId, @rootId, 1, UTC_TIMESTAMP(6));";
            AddMessageParameters(inboxCommand, message);
            inboxCommand.Parameters.AddWithValue("@consumerName", _options.ConsumerName);
            var inserted = await inboxCommand.ExecuteNonQueryAsync(cancellationToken);

            var outcome = inserted == 1 ? "Succeeded" : "Duplicate";
            if (inserted == 1)
            {
                await using var effectCommand = connection.CreateCommand();
                effectCommand.Transaction = transaction;
                effectCommand.CommandText = @"INSERT INTO CapConsumerEffects
                    (Source, EventId, ConsumerName, OperationId, EffectValue, AppliedAt)
                    VALUES (@source, @eventId, @consumerName, @operationId, @effectValue, UTC_TIMESTAMP(6));";
                AddMessageParameters(effectCommand, message);
                effectCommand.Parameters.AddWithValue("@consumerName", _options.ConsumerName);
                effectCommand.Parameters.AddWithValue("@effectValue", $"applied:{message.Source}");
                await effectCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await using var duplicateCommand = connection.CreateCommand();
                duplicateCommand.Transaction = transaction;
                duplicateCommand.CommandText = @"UPDATE CapConsumerInbox SET AttemptCount = AttemptCount + 1
                    WHERE Source = @source AND EventId = @eventId AND ConsumerName = @consumerName;";
                duplicateCommand.Parameters.AddWithValue("@source", message.Source);
                duplicateCommand.Parameters.AddWithValue("@eventId", message.EventId);
                duplicateCommand.Parameters.AddWithValue("@consumerName", _options.ConsumerName);
                await duplicateCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            await FinishAttemptAsync(connection, attemptId, outcome, null, cancellationToken);
        }
        catch (Exception exception)
        {
            await FinishAttemptAsync(connection, attemptId, "Failed", exception.Message, CancellationToken.None);
            throw;
        }
    }

    private async Task<long> BeginAttemptAsync(MySqlConnection connection, SqlUnitOfWorkEventPayload message,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO CapConsumerAttempts
            (Source, EventId, OperationId, TraceId, RootId, ConsumerName, Status, CreatedAt)
            VALUES (@source, @eventId, @operationId, @traceId, @rootId, @consumerName, 'Processing', UTC_TIMESTAMP(6));
            SELECT LAST_INSERT_ID();";
        AddMessageParameters(command, message);
        command.Parameters.AddWithValue("@consumerName", _options.ConsumerName);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private async Task<bool> TryInsertFailureMarkerAsync(MySqlConnection connection, string eventId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT IGNORE INTO CapConsumerFailures (Source, EventId, CreatedAt)
            VALUES (@source, @eventId, UTC_TIMESTAMP(6));";
        command.Parameters.AddWithValue("@source", _options.Source);
        command.Parameters.AddWithValue("@eventId", eventId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task FinishAttemptAsync(MySqlConnection connection, long attemptId, string status,
        string? errorText, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE CapConsumerAttempts SET Status = @status, ErrorText = @errorText WHERE AttemptId = @attemptId";
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@errorText", (object?)errorText ?? DBNull.Value);
        command.Parameters.AddWithValue("@attemptId", attemptId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddMessageParameters(MySqlCommand command, SqlUnitOfWorkEventPayload message)
    {
        command.Parameters.AddWithValue("@source", message.Source);
        command.Parameters.AddWithValue("@eventId", message.EventId);
        command.Parameters.AddWithValue("@operationId", message.OperationId);
        command.Parameters.AddWithValue("@traceId", message.TraceId);
        command.Parameters.AddWithValue("@rootId", message.RootId);
    }
}
