using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Dapper;
using Bing.Dapper.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>使用正式 DI 查询和执行器路径运行固定的 PostgreSQL 混合负载。</summary>
internal static class SqlMixedCapacityProbe
{
    private const string DatabaseName = "bing_sql_aot_test";
    private const int CommandTimeoutSeconds = 30;

    internal static async Task<int> RunAsync(string[] args)
    {
        var diagnoseStages = args.Length > 0 && args[0] == "--mixed-stage-probe";
        if (args.Length != 5 || (!diagnoseStages && args[0] != "--mixed-capacity-probe") ||
            !TryConcurrency(args[3], out var concurrency))
        {
            Console.Error.WriteLine("Usage: --mixed-capacity-probe|--mixed-stage-probe <workload.json> <1C2G|2C4G|4C8G|8C16G> <1|8|16|32|64> <report.json>.");
            return 2;
        }

        var assemblyHashes = GetAssemblyHashes();
        var connectionAcquisition = SqlPoolCounterListener.CreateQueueCapability("PostgreSQL", "Npgsql",
            typeof(NpgsqlConnection).Assembly.GetName().Version);
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_POSTGRESQL_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase))
            return Reject("RUN_POSTGRESQL_INTEGRATION_TESTS=true is required.");

        try
        {
            var workload = ReadWorkload(args[1], args[2], concurrency);
            if (!TryReadContainerLimits(args[2], out var containerLimits))
                return Reject($"The active cgroup limits do not match {args[2]}.");

            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSqlConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                return Reject("ConnectionStrings__PostgreSqlConnection is required.");
            var connection = new NpgsqlConnectionStringBuilder(connectionString);
            if (!string.Equals(connection.Database, DatabaseName, StringComparison.Ordinal) || !connection.Pooling)
                return Reject("Use the dedicated bing_sql_aot_test database with pooling enabled.");

            connection.MaxPoolSize = workload.MaxPoolSize;
            connection.Timeout = 15;
            connection.CommandTimeout = CommandTimeoutSeconds;
            connection.ApplicationName = $"sql_mixed_{Guid.NewGuid():N}";
            var measurement = await MeasureAsync(connection, workload, concurrency, diagnoseStages);
            var report = new
            {
                schema = diagnoseStages ? "SqlMixedStageDiagnostic-v2" : "SqlMixedCapacityReport-v1",
                probe = "SqlMixedCapacityProbe-v1",
                workload,
                profile = args[2],
                containerLimits,
                concurrency,
                commandTimeoutSeconds = CommandTimeoutSeconds,
                connection = new
                {
                    provider = "PostgreSQL",
                    database = connection.Database,
                    pooling = connection.Pooling,
                    maxPoolSize = connection.MaxPoolSize,
                    applicationName = connection.ApplicationName
                },
                runtime = RuntimeInformation.FrameworkDescription,
                os = RuntimeInformation.OSDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                processorsVisibleToProcess = Environment.ProcessorCount,
                assemblySha256 = assemblyHashes,
                connectionTimeoutSeconds = connection.Timeout,
                connectionAcquisition,
                databaseHostResources = new
                {
                    status = "NOT_AVAILABLE",
                    detail = "PostgreSQL SQL statistics do not expose server host CPU or memory usage."
                },
                measurement
            };

            var output = Path.GetFullPath(args[4]);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous);
            await JsonSerializer.SerializeAsync(stream, report,
                new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            return measurement.Warmup.Failed == 0 && measurement.Measure.Failed == 0 ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"SQL mixed capacity probe failed: {error.GetType().Name}: {error.Message}");
            return 2;
        }
    }

    internal static async Task<Measurement> MeasureAsync(NpgsqlConnectionStringBuilder connection,
        Workload workload, int concurrency, bool diagnoseStages = false)
    {
        // Fixture creation bypasses the measured pool and uses a unique table per run.
        var setupConnection = new NpgsqlConnectionStringBuilder(connection.ConnectionString)
        {
            Pooling = false,
            ApplicationName = "sql_mixed_fixture"
        };
        await using var setup = new NpgsqlConnection(setupConnection.ConnectionString);
        await setup.OpenAsync();
        if (diagnoseStages)
        {
            // 只在专用诊断库启用扩展；宿主需显式预加载，缺失时直接失败。
            await using var configure = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS pg_stat_statements", setup);
            await configure.ExecuteNonQueryAsync();
        }
        var tableName = $"sql_mixed_{Guid.NewGuid():N}";
        var table = $"public.{tableName}";
        await using (var transaction = await setup.BeginTransactionAsync())
        {
            await using var seed = new NpgsqlCommand($"""
                CREATE TABLE {table} (id integer PRIMARY KEY, value bigint NOT NULL DEFAULT 0);
                INSERT INTO {table} (id) SELECT generate_series(1, @rows);
                ANALYZE {table};
                """, setup, transaction) { CommandTimeout = CommandTimeoutSeconds };
            seed.Parameters.AddWithValue("rows", workload.Rows);
            await seed.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }

        try
        {
            SqlStageTimings stages = null;
            SqlConnectionOpenTimings connectionOpens = null;
            var services = new ServiceCollection();
            services.AddSqlDataSource("default", DatabaseType.PgSql, connection.ConnectionString);
            services.AddPostgreSqlProvider();
            if (diagnoseStages)
                services.AddSingleton<ISqlDbConnectionFactoryResolver>(new DiagnosticConnectionFactory(() => connectionOpens));
            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            async Task ExecuteDiagnostic(int request, bool write)
            {
                var started = Stopwatch.GetTimestamp();
                var id = (int)(unchecked((uint)request * 2654435761u) % (uint)workload.Rows) + 1;
                await using var scope = provider.CreateAsyncScope();
                if (write)
                {
                    await using var executor = scope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create();
                    stages.Record(SqlRequestStage.Compose, Stopwatch.GetTimestamp() - started);
                    started = Stopwatch.GetTimestamp();
                    var affected = await executor.ExecuteSqlAsync(
                        $"UPDATE {table} SET value = value + 1 WHERE id = @id", new { id }, CommandTimeoutSeconds);
                    stages.Record(SqlRequestStage.WriteExecute, Stopwatch.GetTimestamp() - started);
                    if (affected != 1) throw new InvalidDataException("Primary-key update did not affect exactly one row.");
                    return;
                }

                await using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
                var descriptor = query.Query().Select("id").From(table).Where("id", id);
                stages.Record(SqlRequestStage.Compose, Stopwatch.GetTimestamp() - started);
                started = Stopwatch.GetTimestamp();
                // 诊断入口显式拆分内部计划与执行；不替换正式容量的终结调用路径。
                var executorRoot = (ISqlQueryPlanExecutor)query;
                var plan = new SqlQuery(executorRoot, descriptor.GetBuilder()).GetPlan();
                stages.Record(SqlRequestStage.Freeze, Stopwatch.GetTimestamp() - started);
                started = Stopwatch.GetTimestamp();
                var rows = await executorRoot.ToListAsync<int>(plan, CommandTimeoutSeconds, default);
                stages.Record(SqlRequestStage.ReadExecute, Stopwatch.GetTimestamp() - started);
                if (rows.Count != 1 || rows[0] != id)
                    throw new InvalidDataException("Primary-key read returned an unexpected row.");
            }

            async Task Execute(int request, bool write)
            {
                if (stages != null) { await ExecuteDiagnostic(request, write); return; }
                var id = (int)(unchecked((uint)request * 2654435761u) % (uint)workload.Rows) + 1;
                await using var scope = provider.CreateAsyncScope();
                if (write)
                {
                    await using var executor = scope.ServiceProvider.GetRequiredService<ISqlExecutorFactory>().Create();
                    var affected = await executor.ExecuteSqlAsync(
                        $"UPDATE {table} SET value = value + 1 WHERE id = @id", new { id }, CommandTimeoutSeconds);
                    if (affected != 1) throw new InvalidDataException("Primary-key update did not affect exactly one row.");
                    return;
                }

                await using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
                var rows = await query.Query().Select("id").From(table).Where("id", id)
                    .ToListAsync<int>(CommandTimeoutSeconds);
                if (rows.Count != 1 || rows[0] != id)
                    throw new InvalidDataException("Primary-key read returned an unexpected row.");
            }

            var initialStats = await ReadDatabaseStatsAsync(setup);
            var warmup = await SqlMixedCapacityLoad.RunAsync(TimeSpan.FromSeconds(workload.WarmupSeconds),
                concurrency, workload.ReadPercent, CommandTimeoutSeconds, Execute);
            var measureBaseline = await ReadDatabaseStatsAsync(setup);
            var commandBaseline = diagnoseStages ? await ReadCommandTimingsAsync(setup, tableName) : null;
            if (diagnoseStages)
            {
                stages = new SqlStageTimings();
                connectionOpens = new SqlConnectionOpenTimings();
            }
            SqlMixedCapacityLoad.Result measure;
            ServerActivity activity;
            var databaseWaits = diagnoseStages ? new SqlDatabaseWaitSamples() : null;
            SqlPoolCounterListener.Result connectionPool;
            // 仅在测量与收尾阶段订阅。计数是整个进程的所有池，不按 application_name 隔离。
            using (var counters = new SqlPoolCounterListener())
            {
                var measureTask = SqlMixedCapacityLoad.RunAsync(TimeSpan.FromSeconds(workload.MeasureSeconds),
                    concurrency, workload.ReadPercent, CommandTimeoutSeconds, Execute, traceMeasurement: true);
                var activityTask = SampleActivityAsync(setup, connection.ApplicationName, measureTask, databaseWaits);
                try { measure = await measureTask; }
                finally
                {
                    counters.Dispose();
                    // 测量失败时也等待独立管理连接上的采样完成，再进入表清理。
                    await activityTask;
                }
                connectionPool = counters.Snapshot();
                activity = activityTask.Result;
            }
            var finalStats = await ReadDatabaseStatsAsync(setup);
            var commandTimings = diagnoseStages
                ? (await ReadCommandTimingsAsync(setup, tableName)).Since(commandBaseline) : null;
            commandTimings?.ValidateCounts(measure.ReadAttempts, measure.WriteAttempts);

            return new Measurement(setup.PostgreSqlVersion.ToString(), warmup, measure, activity,
                Difference(initialStats, measureBaseline), Difference(measureBaseline, finalStats), connectionPool,
                stages?.Snapshot(), connectionOpens?.Snapshot(), commandTimings, databaseWaits?.Snapshot());
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP TABLE IF EXISTS {table}", setup)
            {
                CommandTimeout = CommandTimeoutSeconds
            };
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static async Task<ServerActivity> SampleActivityAsync(NpgsqlConnection connection,
        string applicationName, Task measure, SqlDatabaseWaitSamples waits = null)
    {
        const int intervalMilliseconds = 100;
        var samples = 0;
        var peakActiveSessions = 0;
        var peakLockWaiters = 0;
        while (!measure.IsCompleted)
        {
            // 正式容量沿用原两列采样；额外分类仅在显式诊断模式查询。
            var sql = waits == null ? """
                SELECT count(*)::integer,
                       count(*) FILTER (WHERE wait_event_type = 'Lock')::integer
                FROM pg_stat_activity
                WHERE datname = current_database() AND application_name = @applicationName AND state = 'active'
                """ : """
                SELECT count(*)::integer,
                       count(*) FILTER (WHERE wait_event_type = 'Lock')::integer,
                       count(*) FILTER (WHERE wait_event_type = 'IO' AND wait_event = 'WALSync')::integer,
                       count(*) FILTER (WHERE wait_event_type = 'IO' AND wait_event = 'WALWrite')::integer,
                       count(*) FILTER (WHERE wait_event_type = 'IO' AND wait_event NOT IN ('WALSync','WALWrite'))::integer,
                       count(*) FILTER (WHERE wait_event_type IS NULL)::integer,
                       count(*) FILTER (WHERE wait_event_type NOT IN ('Lock','IO'))::integer
                FROM pg_stat_activity
                WHERE datname = current_database() AND application_name = @applicationName AND state = 'active'
                """;
            await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = CommandTimeoutSeconds };
            command.Parameters.AddWithValue("applicationName", applicationName);
            await using (var reader = await command.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                if (measure.IsCompleted) break;
                samples++;
                peakActiveSessions = Math.Max(peakActiveSessions, reader.GetInt32(0));
                peakLockWaiters = Math.Max(peakLockWaiters, reader.GetInt32(1));
                waits?.Record(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
                    reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6));
            }
            await Task.WhenAny(measure, Task.Delay(intervalMilliseconds));
        }

        return new ServerActivity(intervalMilliseconds, samples,
            samples == 0 ? null : peakActiveSessions, samples == 0 ? null : peakLockWaiters);
    }

    private static async Task<DatabaseStats> ReadDatabaseStatsAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT blks_read, blks_hit, temp_bytes, tup_inserted, tup_updated, tup_deleted, deadlocks
            FROM pg_stat_database WHERE datname = current_database()
            """, connection) { CommandTimeout = CommandTimeoutSeconds };
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new DatabaseStats(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2),
            reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6));
    }

    private static async Task<SqlDatabaseCommandTimings> ReadCommandTimingsAsync(NpgsqlConnection connection,
        string tableName)
    {
        // 唯一表名隔离本次负载；管理 SQL 中仅有参数占位，不会匹配该表名。
        await using var command = new NpgsqlCommand("""
            WITH kinds(kind) AS (VALUES ('SELECT'), ('UPDATE'))
            SELECT kind, COALESCE(sum(calls),0)::bigint, COALESCE(sum(rows),0)::bigint,
                   COALESCE(sum(total_exec_time),0)::double precision,
                   COALESCE(sum(shared_blks_hit),0)::bigint, COALESCE(sum(shared_blks_read),0)::bigint,
                   COALESCE(sum(shared_blks_dirtied),0)::bigint, COALESCE(sum(shared_blks_written),0)::bigint,
                   COALESCE(sum(blk_read_time),0)::double precision, COALESCE(sum(blk_write_time),0)::double precision,
                   COALESCE(sum(wal_records),0)::bigint, COALESCE(sum(wal_bytes),0)::numeric
            FROM kinds LEFT JOIN pg_stat_statements s
              ON s.dbid = (SELECT oid FROM pg_database WHERE datname = current_database())
             AND s.toplevel AND strpos(s.query, @tableName) > 0
             AND starts_with(upper(ltrim(s.query)), kind || ' ')
            GROUP BY kind ORDER BY kind;
            SELECT stats_reset::text FROM pg_stat_statements_info;
            SELECT wal_records, wal_bytes, wal_write, wal_sync, wal_write_time, wal_sync_time, stats_reset::text
            FROM pg_stat_wal;
            """, connection) { CommandTimeout = CommandTimeoutSeconds };
        command.Parameters.AddWithValue("tableName", tableName);
        await using var reader = await command.ExecuteReaderAsync();
        async Task<SqlDatabaseStatementTiming> ReadStatement()
        {
            if (!await reader.ReadAsync()) throw new InvalidDataException("Missing database statement statistics.");
            return new(reader.GetInt64(1), reader.GetInt64(2), reader.GetDouble(3), reader.GetInt64(4),
                reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7), reader.GetDouble(8),
                reader.GetDouble(9), reader.GetInt64(10), reader.GetDecimal(11));
        }
        var read = await ReadStatement();
        var write = await ReadStatement();
        await reader.NextResultAsync();
        await reader.ReadAsync();
        var reset = reader.GetString(0);
        await reader.NextResultAsync();
        await reader.ReadAsync();
        var wal = new SqlDatabaseWalTiming(reader.GetInt64(0), reader.GetDecimal(1), reader.GetInt64(2),
            reader.GetInt64(3), reader.GetDouble(4), reader.GetDouble(5), reader.GetString(6));
        return new(read, write, wal, reset);
    }

    private static DatabaseStats Difference(DatabaseStats start, DatabaseStats end) => new(
        end.BlocksRead - start.BlocksRead, end.BlocksHit - start.BlocksHit, end.TempBytes - start.TempBytes,
        end.RowsInserted - start.RowsInserted, end.RowsUpdated - start.RowsUpdated,
        end.RowsDeleted - start.RowsDeleted, end.Deadlocks - start.Deadlocks);

    private static Workload ReadWorkload(string path, string profile, int concurrency)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var root = document.RootElement;
        if (root.GetProperty("schema").GetString() != "SqlMixedCapacityWorkload-v1" ||
            root.GetProperty("workloadStatus").GetString() != "APPROVED_EXPLORATORY" ||
            root.GetProperty("provider").GetString() != "PostgreSQL" ||
            root.GetProperty("database").GetString() != DatabaseName ||
            root.GetProperty("rows").GetInt32() != 100_000 ||
            root.GetProperty("readPercent").GetInt32() != 90 || root.GetProperty("writePercent").GetInt32() != 10 ||
            root.GetProperty("warmupSeconds").GetInt32() != 30 || root.GetProperty("measureSeconds").GetInt32() != 120 ||
            root.GetProperty("pooling").GetBoolean() != true || root.GetProperty("maxPoolSize").GetInt32() != 32 ||
            root.GetProperty("thresholdStatus").GetString() != "PENDING_APPROVAL" ||
            root.GetProperty("capacityThresholds").ValueKind != JsonValueKind.Null ||
            root.GetProperty("buildThresholds").ValueKind != JsonValueKind.Null ||
            !root.GetProperty("profiles").EnumerateArray().Any(item => item.GetString() == profile) ||
            !root.GetProperty("concurrency").EnumerateArray().Any(item => item.GetInt32() == concurrency))
            throw new InvalidDataException("Workload config differs from the approved exploratory R02 contract.");

        return new Workload(root.GetProperty("rows").GetInt32(), root.GetProperty("readPercent").GetInt32(),
            root.GetProperty("writePercent").GetInt32(), root.GetProperty("warmupSeconds").GetInt32(),
            root.GetProperty("measureSeconds").GetInt32(), root.GetProperty("maxPoolSize").GetInt32(),
            root.GetProperty("thresholdStatus").GetString()!, root.GetProperty("capacityThresholds").Clone(),
            root.GetProperty("buildThresholds").Clone());
    }

    private static bool TryConcurrency(string value, out int concurrency) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out concurrency) &&
        concurrency is 1 or 8 or 16 or 32 or 64;

    private static bool TryReadContainerLimits(string profile, out ContainerLimits limits)
    {
        limits = null;
        var expected = profile switch
        {
            "1C2G" => (Cpu: 1d, MemoryBytes: 2L * 1024 * 1024 * 1024),
            "2C4G" => (Cpu: 2d, MemoryBytes: 4L * 1024 * 1024 * 1024),
            "4C8G" => (Cpu: 4d, MemoryBytes: 8L * 1024 * 1024 * 1024),
            "8C16G" => (Cpu: 8d, MemoryBytes: 16L * 1024 * 1024 * 1024),
            _ => (Cpu: 0d, MemoryBytes: 0L)
        };
        if (expected.Cpu == 0 || !File.Exists("/sys/fs/cgroup/cpu.max") || !File.Exists("/sys/fs/cgroup/memory.max"))
            return false;

        var cpu = File.ReadAllText("/sys/fs/cgroup/cpu.max", Encoding.UTF8).Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var memoryText = File.ReadAllText("/sys/fs/cgroup/memory.max", Encoding.UTF8).Trim();
        if (cpu.Length != 2 || cpu[0] == "max" ||
            !long.TryParse(cpu[0], NumberStyles.None, CultureInfo.InvariantCulture, out var quota) ||
            !long.TryParse(cpu[1], NumberStyles.None, CultureInfo.InvariantCulture, out var period) || period == 0 ||
            !long.TryParse(memoryText, NumberStyles.None, CultureInfo.InvariantCulture, out var memoryBytes))
            return false;

        var cpus = (double)quota / period;
        limits = new ContainerLimits(cpus, memoryBytes, quota, period);
        return Math.Abs(cpus - expected.Cpu) < 0.01 && memoryBytes == expected.MemoryBytes;
    }

    private static int Reject(string reason)
    {
        Console.Error.WriteLine($"SQL mixed capacity probe rejected: {reason}");
        return 2;
    }

    internal static Dictionary<string, string> GetAssemblyHashes()
    {
        var hashes = new Dictionary<string, string>();
        foreach (var type in new[]
        {
            typeof(SqlMixedCapacityProbe), typeof(DatabaseType), typeof(ISqlQueryFactory),
            typeof(ISqlExecutorFactory), typeof(DapperCoreServiceCollectionExtensions),
            typeof(PostgreSqlServiceCollectionExtensions), typeof(global::Dapper.SqlMapper), typeof(NpgsqlConnection)
        })
        {
            var path = type.Assembly.Location;
            var fileName = Path.GetFileName(path);
            if (hashes.ContainsKey(fileName)) continue;
            using var stream = File.OpenRead(path);
            hashes[fileName] = Convert.ToHexString(SHA256.HashData(stream));
        }
        return hashes;
    }

    internal sealed record Workload(int Rows, int ReadPercent, int WritePercent, int WarmupSeconds,
        int MeasureSeconds, int MaxPoolSize, string ThresholdStatus, JsonElement CapacityThresholds,
        JsonElement BuildThresholds);
    private sealed record ContainerLimits(double CpuCount, long MemoryBytes, long CpuQuota, long CpuPeriod);
    internal sealed record DatabaseStats(long BlocksRead, long BlocksHit, long TempBytes, long RowsInserted,
        long RowsUpdated, long RowsDeleted, long Deadlocks);
    internal sealed record ServerActivity(int IntervalMilliseconds, int SampleCount, int? PeakActiveSessions,
        int? PeakLockWaitingSessions);

    /// <summary>只替换本探针的工厂；预热仍返回原生连接，测量才启用包装。</summary>
    private sealed class DiagnosticConnectionFactory(Func<SqlConnectionOpenTimings> getTimings) : ISqlDbConnectionFactoryResolver
    {
        public System.Data.IDbConnection Create(string providerKey, string connectionString)
        {
            if (!string.Equals(providerKey, PostgreSqlSqlProvider.Instance.Key, StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("The connection diagnostic only supports this PostgreSQL workload.");
            var inner = new NpgsqlConnection(connectionString);
            var timings = getTimings();
            return timings == null ? inner : new SqlTimedDbConnection(inner, timings);
        }
    }

    internal sealed record Measurement(string PostgreSqlVersion, SqlMixedCapacityLoad.Result Warmup,
        SqlMixedCapacityLoad.Result Measure,
        ServerActivity Activity, DatabaseStats WarmupDatabaseStats, DatabaseStats MeasureDatabaseStats,
        SqlPoolCounterListener.Result ConnectionPool, SqlStageTimings.StageSummary[] DiagnosticStages,
        SqlConnectionOpenTimings.Summary DiagnosticConnectionOpen, SqlDatabaseCommandTimings DiagnosticDatabaseCommands,
        SqlDatabaseWaitSamples.Summary DiagnosticDatabaseWaits)
    {
        public long? DiagnosticStopwatchFrequency => DiagnosticStages == null ? null : Stopwatch.Frequency;
        public string DiagnosticScope => DiagnosticStages == null ? null :
            "Completed phases in measure-through-drain; warmup excluded. Explicit internal plan dispatch adds a descriptor and connection-open timing wraps DbConnection; not capacity-comparable. ConnectionOpen includes all Open/OpenAsync attempts, including failures; it is a subset of Execute, not pure pool queue. Stage percentiles are nearest-rank 0.1ms bucket upper bounds through 100ms; overflow tails are null, actual maxima remain available. Database wait categories are mutually exclusive active-session observations sampled at 100ms intervals, scoped by application_name; no-wait-event does not prove CPU execution, and counts are not durations or causal request attribution. Server statement deltas exclude warmup and are scoped to the unique fixture table; total_exec_time excludes client work and implicit commit. WAL deltas are asynchronous cluster-wide statistics between snapshots, including other cluster activity; they are not per-request commit latency. Neither server time nor client residual is CPU time. Validation and owner disposal are outside stage totals; stages are not exhaustive request latency.";
    }
}
