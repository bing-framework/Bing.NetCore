using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Database;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;
using MySqlConnector;

namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Diagnostics;

/// <summary>
/// 从两份 MySQL 库、CAP 原生表和消费业务表读取诊断事实。
/// </summary>
public sealed class CapEventDiagnosticsService
{
    private readonly SqlUnitOfWorkSampleOptions _options;
    private readonly ScenarioOperationStore _operations;
    private readonly IConfiguration _configuration;

    public CapEventDiagnosticsService(SqlUnitOfWorkSampleOptions options, ScenarioOperationStore operations,
        IConfiguration configuration)
    {
        _options = options;
        _operations = operations;
        _configuration = configuration;
    }

    public async Task<CapEventsDiagnosticsSnapshot> ReadAsync(string? operationId, string? eventId,
        string? traceId, string? source, string? status, CancellationToken cancellationToken)
    {
        var observedAt = DateTimeOffset.UtcNow;
        var operationSnapshots = _operations.Snapshot();
        var sourceSnapshots = _options.CapMySqlMode
            ? new[]
            {
                await ReadSourceAsync("orders", _options.OrdersConnectionString!, cancellationToken),
                await ReadSourceAsync("reporting", _options.ReportingConnectionString!, cancellationToken)
            }
            : Array.Empty<SourceReadSnapshot>();
        var orders = sourceSnapshots.SingleOrDefault(item => item.Source == "orders");
        var reporting = sourceSnapshots.SingleOrDefault(item => item.Source == "reporting");
        var processEvents = operationSnapshots.SelectMany(operation =>
                (operation.ProcessEvents ?? Array.Empty<ScenarioProcessEventEvidence>())
                .Select(processEvent => (Operation: operation, Event: processEvent)))
            .ToArray();
        var databaseEvents = sourceSnapshots.SelectMany(sourceSnapshot => sourceSnapshot.BusinessEvents)
            .Select(businessEvent => ToEventRow(businessEvent, sourceSnapshots,
                processEvents.FirstOrDefault(item => item.Event.EventId == businessEvent.EventId &&
                    item.Event.Source == businessEvent.Source).Event,
                processEvents.FirstOrDefault(item => item.Event.EventId == businessEvent.EventId &&
                    item.Event.Source == businessEvent.Source).Operation?.ProcessSteps))
            .ToArray();
        var processOnlyEvents = processEvents
            .Where(item => !databaseEvents.Any(databaseEvent => databaseEvent.EventId == item.Event.EventId &&
                databaseEvent.Source == item.Event.Source))
            .Select(item => ToProcessEventRow(item.Operation, item.Event, sourceSnapshots))
            .ToArray();
        var events = databaseEvents.Concat(processOnlyEvents).ToArray();

        var operationIds = operationSnapshots.Select(item => item.OperationId)
            .Concat(events.Select(item => item.OperationId))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var operationRows = operationIds.Select(id =>
        {
            var operation = operationSnapshots.FirstOrDefault(item => item.OperationId == id);
            var operationEvents = events.Where(item => item.OperationId == id).ToArray();
            return ToOperationRow(operation,
                orders,
                reporting,
                operationEvents);
        }).ToArray();

        if (!string.IsNullOrWhiteSpace(operationId))
        {
            events = events.Where(item => item.OperationId.Contains(operationId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            operationRows = operationRows.Where(item => item.OperationId.Contains(operationId,
                StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        if (!string.IsNullOrWhiteSpace(eventId))
        {
            events = events.Where(item => item.EventId.Contains(eventId, StringComparison.OrdinalIgnoreCase)).ToArray();
            var ids = events.Select(item => item.OperationId).ToHashSet(StringComparer.Ordinal);
            operationRows = operationRows.Where(item => ids.Contains(item.OperationId)).ToArray();
        }
        if (!string.IsNullOrWhiteSpace(traceId))
        {
            events = events.Where(item => item.TraceId.Contains(traceId, StringComparison.OrdinalIgnoreCase)).ToArray();
            var ids = events.Select(item => item.OperationId).ToHashSet(StringComparer.Ordinal);
            operationRows = operationRows.Where(item => ids.Contains(item.OperationId)).ToArray();
        }
        if (!string.IsNullOrWhiteSpace(source))
        {
            events = events.Where(item => item.Source.Contains(source, StringComparison.OrdinalIgnoreCase)).ToArray();
            var ids = events.Select(item => item.OperationId).ToHashSet(StringComparer.Ordinal);
            operationRows = operationRows.Where(item => ids.Contains(item.OperationId)).ToArray();
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            operationRows = operationRows.Where(item => item.Status.Contains(status,
                StringComparison.OrdinalIgnoreCase)).ToArray();
            var ids = operationRows.Select(item => item.OperationId).ToHashSet(StringComparer.Ordinal);
            events = events.Where(item => ids.Contains(item.OperationId) ||
                                           item.OutboxStatus.Contains(status, StringComparison.OrdinalIgnoreCase) ||
                                           item.CapReceivedStatus.Contains(status, StringComparison.OrdinalIgnoreCase) ||
                                           item.ConsumerStatus.Contains(status, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var observations = _options.CapMySqlMode
            ? sourceSnapshots.Select(item => new CapDataSourceObservation(item.Source,
                item.Error == null ? "observed" : "unavailable", item.DatabaseName, item.Error,
                item.PublishedObserved, item.ReceivedObserved, item.ConsumerTablesObserved)).ToArray()
            : new[]
            {
                new CapDataSourceObservation("orders", "not-configured", null, null, false, false, false),
                new CapDataSourceObservation("reporting", "not-configured", null, null, false, false, false)
            };
        var topStatus = !_options.CapMySqlMode ? "unknown" : sourceSnapshots.Any(item => item.Error != null)
            ? "partial" : "observed";
        return new CapEventsDiagnosticsSnapshot(observedAt, topStatus, events, operationRows, observations,
            _configuration["BING_SQLUOW_CAP_DASHBOARD_URL"]);
    }

    private static CapEventDiagnosticRow ToEventRow(BusinessEventRow businessEvent,
        IReadOnlyList<SourceReadSnapshot> sources, ScenarioProcessEventEvidence? processEvent,
        IReadOnlyList<ScenarioProcessStepEvidence>? processSteps)
    {
        var source = sources.Single(item => item.Source == businessEvent.Source);
        var outbox = source.PublishedMessages.Where(item => item.EventId == businessEvent.EventId).ToArray();
        var received = source.ReceivedMessages.Where(item => item.EventId == businessEvent.EventId).ToArray();
        var attempts = sources.SelectMany(item => item.ConsumerAttempts)
            .Where(item => item.EventId == businessEvent.EventId && item.Source == businessEvent.Source)
            .OrderBy(item => item.AttemptId)
            .ToArray();
        var effectCount = sources.SelectMany(item => item.ConsumerEffects)
            .Count(item => item.EventId == businessEvent.EventId && item.Source == businessEvent.Source);
        return new CapEventDiagnosticRow(businessEvent.OperationId, businessEvent.EventId,
            businessEvent.TraceId, businessEvent.RootId, businessEvent.Source, businessEvent.Topic,
            outbox.FirstOrDefault()?.MessageId, outbox.Select(item => item.MessageId).Where(item => item != null)
                .Cast<string>().Distinct(StringComparer.Ordinal).ToArray(),
            received.FirstOrDefault()?.Status ??
                (source.ReceivedObserved ? "not-present" : "unobserved"), received.Length,
            NormalizeOutboxStatus(outbox.FirstOrDefault()?.Status, source.PublishedObserved),
            outbox.Sum(item => item.Retries), outbox.Length, attempts.Length,
            attempts.LastOrDefault()?.Status ?? "unobserved", effectCount,
            attempts.LastOrDefault()?.ErrorText, businessEvent.Scenario,
            attempts.Select(item => new CapConsumerAttemptDiagnostic(item.AttemptId, item.ConsumerName,
                item.Status, item.ErrorText, item.CreatedAt)).ToArray(),
            attempts.Select(item => item.ConsumerName).Concat(received.Select(item => item.ConsumerGroup))
                .OfType<string>().Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal).ToArray(),
            "database", "observed-rows", processEvent?.PublisherCallStartedAt,
            processEvent?.PublisherReturnedAt, processSteps?.Where(item => item.EventId == businessEvent.EventId &&
                item.Source == businessEvent.Source).ToArray() ?? Array.Empty<ScenarioProcessStepEvidence>());
    }

    private static CapEventDiagnosticRow ToProcessEventRow(ScenarioOperationSnapshot operation,
        ScenarioProcessEventEvidence processEvent, IReadOnlyList<SourceReadSnapshot> sources)
    {
        var source = sources.SingleOrDefault(item => item.Source == processEvent.Source);
        var outbox = source?.PublishedMessages.Where(item => item.EventId == processEvent.EventId).ToArray()
                     ?? Array.Empty<NativeMessageRow>();
        var received = source?.ReceivedMessages.Where(item => item.EventId == processEvent.EventId).ToArray()
                       ?? Array.Empty<NativeMessageRow>();
        var attempts = sources.SelectMany(item => item.ConsumerAttempts)
            .Where(item => item.EventId == processEvent.EventId && item.Source == processEvent.Source)
            .OrderBy(item => item.AttemptId).ToArray();
        var effects = sources.SelectMany(item => item.ConsumerEffects)
            .Count(item => item.EventId == processEvent.EventId && item.Source == processEvent.Source);
        var databaseStatus = source == null || source.Error != null ? "unknown" :
            outbox.Length > 0 || (source.BusinessEvents.Any(item => item.EventId == processEvent.EventId) ||
                                  (source.OperationCounts.GetValueOrDefault(operation.OperationId) > 0 &&
                                   source.BusinessEvents.Any(item => item.OperationId == operation.OperationId)))
                ? "observed-rows" : "no-matching-rows";
        return new CapEventDiagnosticRow(operation.OperationId, processEvent.EventId, processEvent.TraceId,
            processEvent.RootId, processEvent.Source, processEvent.Topic, outbox.FirstOrDefault()?.MessageId,
            outbox.Select(item => item.MessageId).Where(item => item != null).Cast<string>()
                .Distinct(StringComparer.Ordinal).ToArray(),
            received.FirstOrDefault()?.Status ?? (source?.ReceivedObserved == true ? "not-present" : "unobserved"),
            received.Length, NormalizeOutboxStatus(outbox.FirstOrDefault()?.Status,
                source?.PublishedObserved == true), outbox.Sum(item => item.Retries), outbox.Length,
            attempts.Length, attempts.LastOrDefault()?.Status ?? "unobserved", effects,
            attempts.LastOrDefault()?.ErrorText, processEvent.Scenario,
            attempts.Select(item => new CapConsumerAttemptDiagnostic(item.AttemptId, item.ConsumerName,
                item.Status, item.ErrorText, item.CreatedAt)).ToArray(),
            attempts.Select(item => item.ConsumerName).Concat(received.Select(item => item.ConsumerGroup))
                .OfType<string>().Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal).ToArray(),
            "process-journal", databaseStatus, processEvent.PublisherCallStartedAt,
            processEvent.PublisherReturnedAt, operation.ProcessSteps?.Where(item =>
                item.EventId == processEvent.EventId && item.Source == processEvent.Source).ToArray()
                ?? Array.Empty<ScenarioProcessStepEvidence>());
    }

    private static CapOperationDiagnosticRow ToOperationRow(ScenarioOperationSnapshot? operation,
        SourceReadSnapshot? orders, SourceReadSnapshot? reporting,
        IReadOnlyList<CapEventDiagnosticRow>? events)
    {
        var id = operation?.OperationId ?? events?.FirstOrDefault()?.OperationId ?? string.Empty;
        var scenario = operation?.Scenario ?? events?.FirstOrDefault()?.Scenario ?? "recovered-from-storage";
        var eventRows = events ?? Array.Empty<CapEventDiagnosticRow>();
        var ordersEvents = eventRows.Where(item => item.Source == "orders").ToArray();
        var reportingEvents = eventRows.Where(item => item.Source == "reporting").ToArray();
        var ordersBusiness = orders?.OperationCounts.GetValueOrDefault(id) ?? 0;
        var reportingBusiness = reporting?.OperationCounts.GetValueOrDefault(id) ?? 0;
        var ordersOutbox = ordersEvents.Sum(item => item.OutboxMessageCount);
        var reportingOutbox = reportingEvents.Sum(item => item.OutboxMessageCount);
        var consumerAttempts = eventRows.Sum(item => item.ConsumerAttempts);
        var consumerEffects = eventRows.Sum(item => item.ConsumerEffectCount);
        var status = ResolveOperationStatus(operation, scenario, orders, reporting, ordersBusiness,
            reportingBusiness, ordersEvents, reportingEvents);
        var ordersCommitStatus = ResolveSourceCommitStatus(ordersBusiness, ordersEvents, orders);
        var reportingCommitStatus = ResolveSourceCommitStatus(reportingBusiness, reportingEvents, reporting);
        return new CapOperationDiagnosticRow(id, scenario, status, operation?.Status ?? "recovered",
            operation?.Result?.ScopeId.ToString("N") ?? eventRows.FirstOrDefault()?.RootId,
            ordersCommitStatus, reportingCommitStatus,
            ordersBusiness, ordersOutbox, reportingBusiness, reportingOutbox, eventRows.Count,
            consumerAttempts, consumerEffects, operation?.ErrorType, operation?.Error,
            orders?.Error ?? reporting?.Error, operation?.JobId, operation?.ExecutionOutcome,
            operation?.ProcessSteps ?? Array.Empty<ScenarioProcessStepEvidence>(),
            operation?.CallbackFailures ?? Array.Empty<ScenarioCallbackFailureEvidence>(),
            orders == null || reporting == null ? "not-configured" :
            orders.Error != null || reporting.Error != null ? "unavailable" : "observed");
    }

    private static string ResolveOperationStatus(ScenarioOperationSnapshot? operation, string scenario,
        SourceReadSnapshot? orders, SourceReadSnapshot? reporting, int ordersBusiness, int reportingBusiness,
        IReadOnlyList<CapEventDiagnosticRow> ordersEvents, IReadOnlyList<CapEventDiagnosticRow> reportingEvents)
    {
        if (orders?.Error != null || reporting?.Error != null)
            return "unknown";
        if (operation?.ExecutionOutcome == "RolledBack" || operation?.Result?.Outcome == "RolledBack")
            return "rolled-back";
        var ordersCommitted = ordersBusiness > 0 || ordersEvents.Any(item => item.EvidenceSource == "database");
        var reportingCommitted = reportingBusiness > 0 || reportingEvents.Any(item => item.EvidenceSource == "database");
        if (scenario == "cap-partial-commit" && ordersCommitted && !reportingCommitted)
            return "partial-commit";
        if (operation?.Status == "Running")
            return "in-progress";
        if (operation?.Status == "Failed")
            return "failed";
        if (ordersCommitted || reportingCommitted)
        {
            if (scenario == "committed" && ordersCommitted != reportingCommitted)
                return "unknown";
            return orders?.PublishedObserved == true && reporting?.PublishedObserved == true
                ? "committed"
                : "unknown";
        }
        return operation?.Result?.Outcome == "ReadOnly" ? "read-only" : "unknown";
    }

    private static string ResolveSourceCommitStatus(int businessRows, IReadOnlyList<CapEventDiagnosticRow> events,
        SourceReadSnapshot? observation)
    {
        if (observation == null || observation.Error != null)
            return "unknown";
        if (businessRows > 0 || events.Any(item => item.EvidenceSource == "database" ||
                                                  item.DatabaseCommitStatus == "observed-rows"))
            return "committed";
        return "no-matching-rows";
    }

    private static string NormalizeOutboxStatus(string? status, bool tableObserved)
    {
        if (string.IsNullOrWhiteSpace(status))
            return tableObserved ? "unknown" : "unobserved";
        if (status.Equals("Scheduled", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Queued", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Processing", StringComparison.OrdinalIgnoreCase))
            return "pending";
        if (status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase))
            return "succeeded";
        if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            return "failed";
        return status;
    }

    private static async Task<SourceReadSnapshot> ReadSourceAsync(string source, string connectionString,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            var databaseName = connection.Database;
            var businessEvents = await ReadBusinessEventsAsync(connection, source, cancellationToken);
            var operationCounts = await ReadOperationCountsAsync(connection,
                source == "orders" ? "OrderRecords" : "ReportRecords", cancellationToken);
            var published = await ReadNativeMessagesAsync(connection, "published", cancellationToken);
            var received = await ReadNativeMessagesAsync(connection, "received", cancellationToken);
            var attempts = await ReadRowsAsync(connection, "CapConsumerAttempts", cancellationToken);
            var effects = await ReadRowsAsync(connection, "CapConsumerEffects", cancellationToken);
            var attemptsObserved = attempts.Observed;
            return new SourceReadSnapshot(source, databaseName, null, businessEvents, operationCounts,
                published.Messages, received.Messages,
                attempts.Rows.Select(row => ToConsumerAttempt(row)).Where(row => row != null).Cast<ConsumerAttemptRow>().ToArray(),
                effects.Rows.Select(row => ToConsumerEffect(row)).Where(row => row != null).Cast<ConsumerEffectRow>().ToArray(),
                published.Observed, received.Observed, attemptsObserved && effects.Observed);
        }
        catch (Exception exception)
        {
            return new SourceReadSnapshot(source, null, exception.GetType().Name, Array.Empty<BusinessEventRow>(),
                new Dictionary<string, int>(StringComparer.Ordinal), Array.Empty<NativeMessageRow>(),
                Array.Empty<NativeMessageRow>(), Array.Empty<ConsumerAttemptRow>(), Array.Empty<ConsumerEffectRow>(),
                false, false, false);
        }
    }

    private static async Task<BusinessEventRow[]> ReadBusinessEventsAsync(MySqlConnection connection, string source,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT EventId, OperationId, TraceId, RootId, Source, Topic, Scenario
            FROM CapBusinessEvents ORDER BY CreatedAt DESC LIMIT 500;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<BusinessEventRow>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new BusinessEventRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6)));
        return rows.ToArray();
    }

    private static async Task<Dictionary<string, int>> ReadOperationCountsAsync(MySqlConnection connection,
        string tableName, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, tableName, cancellationToken))
            return new Dictionary<string, int>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT OperationId, COUNT(*) FROM `{tableName}` GROUP BY OperationId";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new Dictionary<string, int>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
            rows[reader.GetString(0)] = reader.GetInt32(1);
        return rows;
    }

    private static async Task<NativeTableReadResult> ReadNativeMessagesAsync(MySqlConnection connection,
        string namePart, CancellationToken cancellationToken)
    {
        var tables = await FindTablesAsync(connection, $"%{namePart}%", cancellationToken);
        if (tables.Length == 0)
            return new NativeTableReadResult(false, Array.Empty<NativeMessageRow>());
        var rows = new List<NativeMessageRow>();
        foreach (var tableName in tables)
        {
            var tableRows = await ReadRowsAsync(connection, tableName, cancellationToken, 500 - rows.Count);
            foreach (var row in tableRows.Rows)
            {
                var content = Read(row, "Content");
                var contentEventId = ExtractJsonField(content, "EventId", "eventId");
                rows.Add(new NativeMessageRow(tableName, Read(row, "Id", "MessageId"), Read(row, "Name"),
                    Read(row, "StatusName", "Status", "State") ?? "unknown",
                    ParseInteger(Read(row, "Retries")), Read(row, "Added"),
                    ExtractJsonField(Read(row, "Headers"), "Bing-EventId", "EventId", "eventId") ?? contentEventId,
                    ExtractJsonField(Read(row, "Headers"), "Bing-UowRootId", "RootId", "rootId") ??
                    ExtractJsonField(content, "RootId", "rootId"),
                    ExtractJsonField(content, "OperationId", "operationId"),
                    ExtractJsonField(content, "TraceId", "traceId"),
                    contentEventId, content, Read(row, "Group")));
            }
        }
        return new NativeTableReadResult(true, rows.ToArray());
    }

    private static async Task<TableReadResult> ReadRowsAsync(MySqlConnection connection, string tableName,
        CancellationToken cancellationToken, int limit = 500)
    {
        if (!await TableExistsAsync(connection, tableName, cancellationToken))
            return new TableReadResult(false, Array.Empty<Dictionary<string, string?>>());
        var quotedName = QuoteIdentifier(tableName);
        var columns = await ReadColumnsAsync(connection, tableName, cancellationToken);
        var orderBy = columns.Any(column => string.Equals(column, "Id", StringComparison.OrdinalIgnoreCase))
            ? " ORDER BY `Id` DESC"
            : string.Empty;
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {quotedName}{orderBy} LIMIT {Math.Clamp(limit, 0, 500)}";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Dictionary<string, string?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                if (reader.IsDBNull(index))
                {
                    row[reader.GetName(index)] = null;
                    continue;
                }

                var value = reader.GetValue(index);
                row[reader.GetName(index)] = value switch
                {
                    DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
                    DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
                    _ => Convert.ToString(value, CultureInfo.InvariantCulture)
                };
            }
            rows.Add(row);
        }
        return new TableReadResult(true, rows.ToArray());
    }

    private static async Task<string[]> ReadColumnsAsync(MySqlConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName";
        command.Parameters.AddWithValue("@tableName", tableName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var names = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            names.Add(reader.GetString(0));
        return names.ToArray();
    }

    private static async Task<string[]> FindTablesAsync(MySqlConnection connection, string pattern,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME LIKE @pattern ORDER BY TABLE_NAME";
        command.Parameters.AddWithValue("@pattern", pattern);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var names = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
            names.Add(reader.GetString(0));
        return names.ToArray();
    }

    private static async Task<bool> TableExistsAsync(MySqlConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName";
        command.Parameters.AddWithValue("@tableName", tableName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static ConsumerAttemptRow? ToConsumerAttempt(IReadOnlyDictionary<string, string?> row)
    {
        var eventId = Read(row, "EventId");
        if (eventId == null)
            return null;
        return new ConsumerAttemptRow(Read(row, "Source") ?? "unknown", eventId,
            Read(row, "OperationId") ?? string.Empty, Read(row, "ConsumerName") ?? "unknown",
            Read(row, "Status") ?? "unknown", ParseLong(Read(row, "AttemptId")), Read(row, "ErrorText"),
            ParseDateTimeOffset(Read(row, "CreatedAt")));
    }

    private static ConsumerEffectRow? ToConsumerEffect(IReadOnlyDictionary<string, string?> row)
    {
        var eventId = Read(row, "EventId");
        if (eventId == null)
            return null;
        return new ConsumerEffectRow(Read(row, "Source") ?? "unknown", eventId,
            Read(row, "OperationId") ?? string.Empty, Read(row, "ConsumerName") ?? "unknown");
    }

    private static string? ExtractJsonField(string? json, params string[] fieldNames)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return FindJsonValue(document.RootElement, fieldNames);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FindJsonValue(JsonElement element, IReadOnlyList<string> fieldNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (fieldNames.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                    return property.Value.ToString();
                var nested = FindJsonValue(property.Value, fieldNames);
                if (nested != null)
                    return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindJsonValue(item, fieldNames);
                if (nested != null)
                    return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var nestedDocument = JsonDocument.Parse(element.GetString() ?? string.Empty);
                return FindJsonValue(nestedDocument.RootElement, fieldNames);
            }
            catch (JsonException)
            {
            }
        }
        return null;
    }

    private static string? Read(IReadOnlyDictionary<string, string?> row, params string[] keys)
    {
        foreach (var key in keys)
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        return null;
    }

    private static int ParseInteger(string? value) => int.TryParse(value, NumberStyles.Integer,
        CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static long ParseLong(string? value) => long.TryParse(value, NumberStyles.Integer,
        CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static DateTimeOffset? ParseDateTimeOffset(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed : null;

    private static string QuoteIdentifier(string value) => $"`{value.Replace("`", "``", StringComparison.Ordinal)}`";

    private sealed record TableReadResult(bool Observed, IReadOnlyList<Dictionary<string, string?>> Rows);
    private sealed record NativeTableReadResult(bool Observed, IReadOnlyList<NativeMessageRow> Messages);
    private sealed record BusinessEventRow(string EventId, string OperationId, string TraceId, string RootId,
        string Source, string Topic, string Scenario);
    private sealed record ConsumerAttemptRow(string Source, string EventId, string OperationId,
        string ConsumerName, string Status, long AttemptId, string? ErrorText, DateTimeOffset? CreatedAt);
    private sealed record ConsumerEffectRow(string Source, string EventId, string OperationId, string ConsumerName);
    private sealed record NativeMessageRow(string TableName, string? MessageId, string? Name, string Status,
        int Retries, string? Added, string? EventId, string? RootId, string? OperationId, string? TraceId,
        string? BodyEventId, string? Content, string? ConsumerGroup);

    private sealed record SourceReadSnapshot(string Source, string? DatabaseName, string? Error,
        IReadOnlyList<BusinessEventRow> BusinessEvents, IReadOnlyDictionary<string, int> OperationCounts,
        IReadOnlyList<NativeMessageRow> PublishedMessages, IReadOnlyList<NativeMessageRow> ReceivedMessages,
        IReadOnlyList<ConsumerAttemptRow> ConsumerAttempts, IReadOnlyList<ConsumerEffectRow> ConsumerEffects,
        bool PublishedObserved, bool ReceivedObserved, bool ConsumerTablesObserved);
}

public sealed record CapEventsDiagnosticsSnapshot(DateTimeOffset ObservedAt, string Status,
    IReadOnlyList<CapEventDiagnosticRow> Events, IReadOnlyList<CapOperationDiagnosticRow> Operations,
    IReadOnlyList<CapDataSourceObservation> DataSources, string? CapDashboardUrl);

public sealed record CapDataSourceObservation(string Source, string Status, string? DatabaseName, string? Error,
    bool CapPublishedObserved, bool CapReceivedObserved, bool ConsumerTablesObserved);

public sealed record CapEventDiagnosticRow(string OperationId, string EventId, string TraceId, string RootId,
    string Source, string Topic, string? CapMessageId, IReadOnlyList<string> CapMessageIds,
    string CapReceivedStatus, int CapReceivedMessageCount,
    string OutboxStatus, int OutboxRetries, int OutboxMessageCount, int ConsumerAttempts,
    string ConsumerStatus, int ConsumerEffectCount, string? ConsumerError, string Scenario,
    IReadOnlyList<CapConsumerAttemptDiagnostic> ConsumerAttemptDetails, IReadOnlyList<string> ConsumerNames,
    string EvidenceSource, string DatabaseCommitStatus, DateTimeOffset? PublisherCallStartedAt,
    DateTimeOffset? PublisherReturnedAt, IReadOnlyList<ScenarioProcessStepEvidence> ProcessSteps);

public sealed record CapConsumerAttemptDiagnostic(long AttemptId, string ConsumerName, string Status,
    string? Error, DateTimeOffset? CreatedAt);

public sealed record CapOperationDiagnosticRow(string OperationId, string Scenario, string Status,
    string RequestStatus, string? RootId, string OrdersCommitStatus, string ReportingCommitStatus,
    int OrdersBusinessRows, int OrdersOutboxRows, int ReportingBusinessRows, int ReportingOutboxRows,
    int EventCount, int ConsumerAttempts, int ConsumerEffects, string? ErrorType, string? Error,
    string? ObservationError, string? JobId, string? ExecutionOutcome,
    IReadOnlyList<ScenarioProcessStepEvidence> ProcessSteps,
    IReadOnlyList<ScenarioCallbackFailureEvidence> CallbackFailures, string DatabaseObservationStatus);
