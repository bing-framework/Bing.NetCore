using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Bing.Samples.SqlUnitOfWork.Application;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;

namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;

/// <summary>
/// 保存 Sample 操作的过程证据；该日志不驱动事件，也不代表数据库提交事实。
/// </summary>
public sealed class ScenarioOperationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, ScenarioOperationSnapshot> _operations = new();
    private readonly string? _journalPath;

    public ScenarioOperationStore(SqlUnitOfWorkSampleOptions? options = null)
    {
        _journalPath = string.IsNullOrWhiteSpace(options?.ProcessEvidencePath)
            ? null
            : Path.GetFullPath(options.ProcessEvidencePath);
        LoadJournal();
    }

    public ScenarioOperationSnapshot Create(string operationId, string scenario)
    {
        lock (_gate)
        {
            if (_operations.TryGetValue(operationId, out var existing))
                return existing;
            var snapshot = new ScenarioOperationSnapshot(operationId, scenario, "Queued", DateTimeOffset.UtcNow);
            _operations[operationId] = snapshot;
            Persist(snapshot);
            return snapshot;
        }
    }

    public void EnsureTracked(string operationId, string scenario)
    {
        lock (_gate)
        {
            if (_operations.ContainsKey(operationId))
                return;
            var now = DateTimeOffset.UtcNow;
            var snapshot = new ScenarioOperationSnapshot(operationId, scenario, "Running", now, now);
            _operations[operationId] = snapshot;
            Persist(snapshot);
        }
    }

    public void SetJobId(string operationId, string jobId) => Update(operationId,
        operation => operation with { JobId = jobId });

    public void MarkRunning(string operationId) => Update(operationId, operation => operation with
    {
        Status = "Running",
        StartedAt = DateTimeOffset.UtcNow
    });

    public void MarkSucceeded(string operationId, ScenarioExecutionResult result) => Update(operationId,
        operation => operation with
        {
            Status = "Completed",
            FinishedAt = DateTimeOffset.UtcNow,
            Result = result,
            ExecutionOutcome = result.Outcome
        });

    public void RecordExecutionIdentity(string operationId, ScenarioOperationIdentityEvidence identity) =>
        Update(operationId, operation => operation with { ExecutionIdentity = identity });

    public void MarkFailed(string operationId, Exception exception) => Update(operationId,
        operation => operation with
        {
            Status = "Failed",
            FinishedAt = DateTimeOffset.UtcNow,
            ErrorType = exception.GetType().Name,
            Error = exception.Message,
            CallbackFailures = CollectCallbackFailures(exception, operation.CallbackFailures)
        });

    public void MarkCancelled(string operationId) => Update(operationId, operation => operation with
    {
        Status = "Cancelled",
        FinishedAt = DateTimeOffset.UtcNow
    });

    public void RecordCapEvent(string operationId, string scenario, string eventId, string traceId,
        string rootId, string source, string topic)
    {
        EnsureTracked(operationId, scenario);
        Update(operationId, operation =>
        {
            var events = (operation.ProcessEvents ?? Array.Empty<ScenarioProcessEventEvidence>()).ToList();
            var index = events.FindIndex(item => item.EventId == eventId && item.Source == source);
            var evidence = new ScenarioProcessEventEvidence(eventId, traceId, rootId, source, topic, scenario,
                DateTimeOffset.UtcNow);
            if (index < 0)
                events.Add(evidence);
            else
                events[index] = evidence;
            return operation with { ProcessEvents = events };
        });
    }

    public void MarkPublisherReturned(string operationId, string eventId, string source)
    {
        RecordProcessStep(operationId, eventId, source, "publisher-returned");
        Update(operationId, operation =>
        {
            var events = (operation.ProcessEvents ?? Array.Empty<ScenarioProcessEventEvidence>()).ToList();
            var index = events.FindIndex(item => item.EventId == eventId && item.Source == source);
            if (index < 0)
                return operation;
            events[index] = events[index] with { PublisherReturnedAt = DateTimeOffset.UtcNow };
            return operation with { ProcessEvents = events };
        });
    }

    public void MarkPublisherCallStarted(string operationId, string eventId, string source)
    {
        RecordProcessStep(operationId, eventId, source, "publisher-call-started");
        Update(operationId, operation =>
        {
            var events = (operation.ProcessEvents ?? Array.Empty<ScenarioProcessEventEvidence>()).ToList();
            var index = events.FindIndex(item => item.EventId == eventId && item.Source == source);
            if (index < 0)
                return operation;
            events[index] = events[index] with { PublisherCallStartedAt = DateTimeOffset.UtcNow };
            return operation with { ProcessEvents = events };
        });
    }

    public void RecordProcessStep(string operationId, string eventId, string source, string step)
    {
        lock (_gate)
        {
            if (!_operations.TryGetValue(operationId, out var current))
            {
                var now = DateTimeOffset.UtcNow;
                current = new ScenarioOperationSnapshot(operationId, "unknown", "Running", now, now);
            }
            var steps = (current.ProcessSteps ?? Array.Empty<ScenarioProcessStepEvidence>()).ToList();
            var evidence = new ScenarioProcessStepEvidence(steps.Count == 0 ? 1 : steps[^1].Sequence + 1,
                eventId, source, step, DateTimeOffset.UtcNow);
            var updated = current with { ProcessSteps = steps.Append(evidence).ToArray() };
            _operations[operationId] = updated;
            Persist(updated);
        }
    }

    public bool TryGet(string operationId, out ScenarioOperationSnapshot snapshot) =>
        _operations.TryGetValue(operationId, out snapshot!);

    public IReadOnlyList<ScenarioOperationSnapshot> Snapshot() => _operations.Values
        .OrderByDescending(operation => operation.CreatedAt)
        .Take(200)
        .ToArray();

    private void Update(string operationId, Func<ScenarioOperationSnapshot, ScenarioOperationSnapshot> update)
    {
        lock (_gate)
        {
            if (!_operations.TryGetValue(operationId, out var current))
                throw new InvalidOperationException($"操作 {operationId} 不存在。");
            var updated = update(current);
            _operations[operationId] = updated;
            Persist(updated);
        }
    }

    private void LoadJournal()
    {
        if (_journalPath == null || !File.Exists(_journalPath))
            return;
        foreach (var line in File.ReadLines(_journalPath, Encoding.UTF8))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<ScenarioOperationSnapshot>(line, JsonOptions);
                if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.OperationId))
                    _operations[snapshot.OperationId] = snapshot;
            }
            catch (JsonException)
            {
                // 崩溃时可能只写入最后一行的一部分；保留并读取之前完整的快照。
            }
        }
    }

    private void Persist(ScenarioOperationSnapshot snapshot)
    {
        if (_journalPath == null)
            return;
        var directory = Path.GetDirectoryName(_journalPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.AppendAllText(_journalPath,
            JsonSerializer.Serialize(snapshot, JsonOptions) + Environment.NewLine, new UTF8Encoding(false));
    }

    private static IReadOnlyList<ScenarioCallbackFailureEvidence> CollectCallbackFailures(Exception exception,
        IReadOnlyList<ScenarioCallbackFailureEvidence>? existing)
    {
        var failures = (existing ?? Array.Empty<ScenarioCallbackFailureEvidence>()).ToList();
        IEnumerable<Exception> exceptions = exception is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions
            : new[] { exception };
        foreach (var failure in exceptions)
        {
            var outcome = failure.Data["Bing.Uow.ResourceOutcome"]?.ToString();
            if (!string.Equals(outcome, "CommittedCallbackFailed", StringComparison.Ordinal))
                continue;
            var source = failure.Data["Bing.Cap.DataSourceKey"]?.ToString();
            var sequenceValue = failure.Data["Bing.Uow.ResourceSequence"];
            int? sequence = sequenceValue == null
                ? null
                : int.TryParse(sequenceValue.ToString(), out var parsed) ? parsed : null;
            failures.Add(new ScenarioCallbackFailureEvidence(source, sequence, "CommittedCallbackFailed",
                failure.GetType().Name, failure.Message, DateTimeOffset.UtcNow));
        }
        return failures;
    }
}
