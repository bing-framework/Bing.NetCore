using System.Globalization;

namespace Bing.Resilience.Operations.Relational;

public sealed partial class RelationalOperationStore
{
    /// <summary>只处理 Deadline 保护期已结束的明确终态；Saga 父子载荷在同一事务清除。</summary>
    public async ValueTask<OperationRetentionResult> CleanupAsync(string kind, OperationRetentionPolicy policy,
        Func<OperationRecord, string> redactPayload, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(policy); ArgumentNullException.ThrowIfNull(redactPayload);
        policy.Validate();
        if (kind is not ("replay" or "saga")) throw new ResilienceConfigurationException("清理仅支持 Replay/Saga 终态。");
        await using var session = (Session)await BeginAsync(token).ConfigureAwait(false);
        var result = await session.Cleanup(kind, policy, redactPayload, token).ConfigureAwait(false);
        await session.CommitAsync(token).ConfigureAwait(false);
        ResilienceDiagnostics.RecordLifecycle("journal.cleanup", "success");
        return result;
    }

    private sealed partial class Session
    {
        internal async ValueTask<OperationRetentionResult> Cleanup(string kind, OperationRetentionPolicy policy,
            Func<OperationRecord, string> redact, CancellationToken token)
        {
            var now = await Now(token).ConfigureAwait(false);
            var parents = new List<OperationRecord>();
            var locks = owner._dialect == ResilienceRelationalDialect.MySql ? " FOR UPDATE SKIP LOCKED" : "";
            var attemptsCutoff = policy.AttemptRetention is { } a ? now - a : DateTimeOffset.MinValue;
            var auditCutoff = policy.AuditRetention is { } b ? now - b : DateTimeOffset.MinValue;
            var states = kind == "replay" ? "'Succeeded','Failed','Cancelled','Expired'" : "'Completed','Compensated'";
            // 过滤已清理且没有到期历史的记录，避免小批次永远停留在最早的一组。
            await using (var command = Command($"SELECT * FROM {Table} p WHERE kind=@kind AND state IN ({states}) AND deadline<=@protected AND (lease_until IS NULL OR lease_until<=@now) AND ((updated_at<=@payload AND data NOT LIKE @marker) OR EXISTS (SELECT 1 FROM {owner._prefix}_attempts a WHERE a.operation_id=p.id AND a.started_at<=@attempt) OR EXISTS (SELECT 1 FROM {owner._prefix}_audit h WHERE h.operation_id=p.id AND (h.at_utc<=@audit OR h.event_type='retention.pending'))) ORDER BY deadline,id LIMIT {policy.BatchSize}{locks}",
                ("@kind", kind), ("@protected", now - policy.ProtectionPeriod), ("@now", now),
                ("@payload", now - policy.PayloadRetention), ("@marker", OperationRetainedData.Prefix + "%"),
                ("@attempt", attemptsCutoff), ("@audit", auditCutoff)))
            await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                while (await reader.ReadAsync(token).ConfigureAwait(false)) parents.Add(Read(reader));

            var count = 0; var payloads = 0; var attempts = 0; var audits = 0;
            foreach (var parent in parents)
            {
                token.ThrowIfCancellationRequested();
                var records = new List<OperationRecord> { parent };
                if (kind == "saga")
                {
                    await using var command = Command($"SELECT * FROM {Table} WHERE kind='saga-step' AND scope_hash=@scope ORDER BY id LIMIT 129{(owner._dialect == ResilienceRelationalDialect.MySql ? " FOR UPDATE" : "")}", ("@scope", OperationFingerprint.Hash(parent.Id)));
                    await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) records.Add(Read(reader));
                    if (records.Count > 129 || records.Skip(1).Any(r => r.State is not ("Succeeded" or "Compensated" or "Failed" or "Skipped" or "Pending") || r.LeaseUntil > now))
                        continue;
                }
                foreach (var record in records)
                {
                    if (parent.UpdatedAt <= now - policy.PayloadRetention && !OperationRetainedData.IsRedacted(record.Data))
                    {
                        var updated = record with { Data = OperationRetainedData.Prefix + redact(record), Version = checked(record.Version + 1), LeaseToken = null, LeaseUntil = null };
                        if (!await CompareExchangeAsync(updated, record.Version, token: token).ConfigureAwait(false)) throw new OperationConcurrencyException(record.Id);
                        await AppendAuditAsync(new(record.Id, updated.Version, "retention.payload", "retention", "终态载荷清理；保留身份和幂等摘要", now), token).ConfigureAwait(false);
                        payloads++;
                    }
                    // 每批历史行数独立有界，按完整主键删除，不能删除当前或未来记录。
                    if (policy.AttemptRetention is not null && attempts < policy.HistoryBatchSize)
                        attempts += await DeleteAttempts(record.Id, attemptsCutoff, policy.HistoryBatchSize - attempts, token).ConfigureAwait(false);
                    if (policy.AuditRetention is not null && audits < policy.HistoryBatchSize)
                        audits += await DeleteAudits(record.Id, auditCutoff, policy.HistoryBatchSize - audits, token).ConfigureAwait(false);
                }
                // Saga 子记录的历史可能超过单批上限；持久续扫标记保证后续批次不会遗漏。
                await using (var clear = Command($"DELETE FROM {owner._prefix}_audit WHERE operation_id=@id AND event_type='retention.pending'", ("@id", parent.Id)))
                    await clear.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                var pending = false;
                foreach (var record in records)
                {
                    await using var check = Command($"SELECT (SELECT COUNT(*) FROM {owner._prefix}_attempts WHERE operation_id=@id AND started_at<=@attempt) + (SELECT COUNT(*) FROM {owner._prefix}_audit WHERE operation_id=@id AND at_utc<=@audit AND event_type<>'retention.pending')", ("@id", record.Id), ("@attempt", attemptsCutoff), ("@audit", auditCutoff));
                    if (Convert.ToInt64(await check.ExecuteScalarAsync(token).ConfigureAwait(false), CultureInfo.InvariantCulture) > 0) { pending = true; break; }
                }
                if (pending) await AppendAuditAsync(new(parent.Id, parent.Version, "retention.pending", "retention", "历史清理待下一批继续", now), token).ConfigureAwait(false);
                count++;
            }
            return new(count, payloads, attempts, audits);
        }

        private async ValueTask<int> DeleteAttempts(string id, DateTimeOffset cutoff, int limit, CancellationToken token)
        {
            var keys = new List<int>();
            await using (var command = Command($"SELECT attempt_no FROM {owner._prefix}_attempts WHERE operation_id=@id AND started_at<=@cutoff ORDER BY attempt_no LIMIT {limit}", ("@id", id), ("@cutoff", cutoff)))
            await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                while (await reader.ReadAsync(token).ConfigureAwait(false)) keys.Add(Convert.ToInt32(reader[0], CultureInfo.InvariantCulture));
            foreach (var key in keys)
            {
                await using var command = Command($"DELETE FROM {owner._prefix}_attempts WHERE operation_id=@id AND attempt_no=@number", ("@id", id), ("@number", key));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            return keys.Count;
        }
        private async ValueTask<int> DeleteAudits(string id, DateTimeOffset cutoff, int limit, CancellationToken token)
        {
            var keys = new List<(long Version, string Event)>();
            await using (var command = Command($"SELECT version,event_type FROM {owner._prefix}_audit WHERE operation_id=@id AND at_utc<=@cutoff AND event_type<>'retention.pending' ORDER BY version,event_type LIMIT {limit}", ("@id", id), ("@cutoff", cutoff)))
            await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                while (await reader.ReadAsync(token).ConfigureAwait(false)) keys.Add((Convert.ToInt64(reader[0], CultureInfo.InvariantCulture), reader.GetString(1)));
            foreach (var key in keys)
            {
                await using var command = Command($"DELETE FROM {owner._prefix}_audit WHERE operation_id=@id AND version=@version AND event_type=@event", ("@id", id), ("@version", key.Version), ("@event", key.Event));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            return keys.Count;
        }
    }
}
