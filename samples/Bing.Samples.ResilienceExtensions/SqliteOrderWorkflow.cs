using System.Data.Common;
using System.Text.Json;
using Bing.Resilience.Compensation;
using Microsoft.Data.Sqlite;

namespace Bing.Samples.ResilienceExtensions;

/// <summary>演示持久库存预留和模拟支付账本；不是第三方支付网关实现。</summary>
public sealed class SqliteOrderWorkflow(string connectionString,
    Func<string, SagaStepAction, bool>? loseResponse = null) : ISagaStepHandler
{
    public sealed record Order(string OrderId, string Sku, int Quantity, long AmountCents, bool RejectShipping = true);

    /// <summary>只用于专属示例库。业务账本独立于 Journal，模拟远程服务已提交但响应丢失。</summary>
    public async Task InitializeAsync(CancellationToken token = default)
    {
        await using var connection = await OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS demo_stock(sku TEXT PRIMARY KEY, available INTEGER NOT NULL CHECK(available>=0));
            INSERT OR IGNORE INTO demo_stock VALUES ('sku-1',10);
            CREATE TABLE IF NOT EXISTS demo_effects(forward_key TEXT PRIMARY KEY, kind TEXT NOT NULL, payload TEXT NOT NULL, state TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS demo_compensations(compensation_key TEXT PRIMARY KEY, forward_key TEXT NOT NULL UNIQUE);
            """;
        await command.ExecuteNonQueryAsync(token);
    }

    public async ValueTask<SagaStepResult> ForwardAsync(SagaStepContext context, CancellationToken token)
    {
        var input = Parse(context.Step.Input); var payload = JsonSerializer.Serialize(input);
        if (context.Step.Id == "ship" && input.RejectShipping) return new(SagaOutcome.FailedNoEffect, Reason: "shipping_unavailable");
        if (context.Step.Id is not ("reserve" or "pay" or "ship")) return new(SagaOutcome.FailedNoEffect, Reason: "unsupported_step");
        await using var connection = await OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var prior = await ReadEffectAsync(connection, transaction, context.ForwardKey, token);
        if (prior is not null)
        {
            CheckContract(prior.Value, context.Step.Id, payload);
            return new(prior.Value.State == "Applied" ? SagaOutcome.Succeeded : SagaOutcome.FailedNoEffect, prior.Value.State);
        }
        if (context.Step.Id == "reserve")
        {
            var count = await ExecuteAsync(connection, transaction,
                "UPDATE demo_stock SET available=available-@quantity WHERE sku=@sku AND available>=@quantity", token,
                ("@quantity", input.Quantity), ("@sku", input.Sku));
            if (count == 0) return new(SagaOutcome.FailedNoEffect, Reason: "out_of_stock");
        }
        // pay 的 Applied 记录模拟支付授权；金额、订单和稳定键一同持久保存。
        await ExecuteAsync(connection, transaction, "INSERT INTO demo_effects VALUES (@key,@kind,@payload,'Applied')", token,
            ("@key", context.ForwardKey), ("@kind", context.Step.Id), ("@payload", payload));
        await transaction.CommitAsync(token);
        return loseResponse?.Invoke(context.Step.Id, SagaStepAction.Forward) == true
            ? new(SagaOutcome.Unknown, Reason: "response_lost_after_commit") : new(SagaOutcome.Succeeded, "Applied");
    }

    public async ValueTask<SagaStepResult> ConfirmAsync(SagaStepContext context, CancellationToken token)
    {
        await using var connection = await OpenAsync(token);
        var effect = await ReadEffectAsync(connection, null, context.ForwardKey, token);
        if (effect is null) return new(SagaOutcome.FailedNoEffect, Reason: "authoritative_absence");
        CheckContract(effect.Value, context.Step.Id, JsonSerializer.Serialize(Parse(context.Step.Input)));
        if (context.Action == SagaStepAction.ConfirmCompensation)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM demo_compensations WHERE compensation_key=@key AND forward_key=@forward";
            command.Parameters.AddWithValue("@key", context.CompensationKey); command.Parameters.AddWithValue("@forward", context.ForwardKey);
            return Convert.ToInt32(await command.ExecuteScalarAsync(token)) == 1
                ? new(SagaOutcome.Succeeded, "Compensated")
                : effect.Value.State == "Compensated" ? new(SagaOutcome.Unknown, Reason: "compensation_key_mismatch")
                : new(SagaOutcome.FailedNoEffect, Reason: "compensation_not_applied");
        }
        return new(effect.Value.State == "Applied" ? SagaOutcome.Succeeded : SagaOutcome.FailedNoEffect, effect.Value.State);
    }

    public async ValueTask<SagaStepResult> CompensateAsync(SagaStepContext context, CancellationToken token)
    {
        var input = Parse(context.Step.Input);
        await using var connection = await OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var effect = await ReadEffectAsync(connection, transaction, context.ForwardKey, token);
        if (effect is null) return new(SagaOutcome.Unknown, Reason: "cannot_compensate_unknown_effect");
        CheckContract(effect.Value, context.Step.Id, JsonSerializer.Serialize(input));
        if (effect.Value.State == "Compensated")
        {
            await using var prior = connection.CreateCommand(); prior.Transaction = (SqliteTransaction)transaction;
            prior.CommandText = "SELECT COUNT(*) FROM demo_compensations WHERE compensation_key=@key AND forward_key=@forward";
            prior.Parameters.AddWithValue("@key", context.CompensationKey); prior.Parameters.AddWithValue("@forward", context.ForwardKey);
            if (Convert.ToInt32(await prior.ExecuteScalarAsync(token)) != 1)
                throw new InvalidOperationException("已完成的补偿不能更换补偿幂等键。");
            return new(SagaOutcome.Succeeded, "Compensated");
        }
        if (context.Step.Id == "reserve")
            await ExecuteAsync(connection, transaction, "UPDATE demo_stock SET available=available+@quantity WHERE sku=@sku", token,
                ("@quantity", input.Quantity), ("@sku", input.Sku));
        // pay 的 Compensated 表示撤销模拟授权；真实网关需提供同键撤销与权威查询协议。
        await ExecuteAsync(connection, transaction, "UPDATE demo_effects SET state='Compensated' WHERE forward_key=@key", token, ("@key", context.ForwardKey));
        await ExecuteAsync(connection, transaction, "INSERT INTO demo_compensations VALUES (@key,@forward)", token,
            ("@key", context.CompensationKey), ("@forward", context.ForwardKey));
        await transaction.CommitAsync(token);
        return loseResponse?.Invoke(context.Step.Id, SagaStepAction.Compensate) == true
            ? new(SagaOutcome.Unknown, Reason: "compensation_response_lost") : new(SagaOutcome.Succeeded, "Compensated");
    }

    private static Order Parse(string input)
    {
        var value = JsonSerializer.Deserialize<Order>(input);
        if (value is null || string.IsNullOrWhiteSpace(value.OrderId) || string.IsNullOrWhiteSpace(value.Sku) || value.Quantity <= 0 || value.AmountCents <= 0)
            throw new ArgumentException("示例订单参数无效。");
        return value;
    }
    private static void CheckContract((string Kind, string Payload, string State) prior, string kind, string payload)
    {
        if (prior.Kind != kind || prior.Payload != payload) throw new InvalidOperationException("同一幂等键不能用于不同业务请求。");
    }
    private async Task<SqliteConnection> OpenAsync(CancellationToken token)
    {
        var connection = new SqliteConnection(connectionString);
        try { await connection.OpenAsync(token); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
    private static async Task<(string Kind, string Payload, string State)?> ReadEffectAsync(SqliteConnection connection, DbTransaction? transaction, string key, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = (SqliteTransaction?)transaction;
        command.CommandText = "SELECT kind,payload,state FROM demo_effects WHERE forward_key=@key"; command.Parameters.AddWithValue("@key", key);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? (reader.GetString(0), reader.GetString(1), reader.GetString(2)) : null;
    }
    private static async Task<int> ExecuteAsync(SqliteConnection connection, DbTransaction transaction, string sql, CancellationToken token, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand(); command.Transaction = (SqliteTransaction)transaction; command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return await command.ExecuteNonQueryAsync(token);
    }
}
