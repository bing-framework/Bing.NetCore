using System.Text;
using System.Text.Json;
using Bing.Data.Sql.Benchmarks;
using Npgsql;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: <workload.json> <new report.json>");
    return 2;
}

// 独立集成入口不声明硬件档位，不生成正式容量收据。
if (!string.Equals(Environment.GetEnvironmentVariable("RUN_POSTGRESQL_INTEGRATION_TESTS"), "true",
        StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("RUN_POSTGRESQL_INTEGRATION_TESTS=true is required.");
    return 2;
}
var connectionText = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSqlConnection");
if (string.IsNullOrWhiteSpace(connectionText)) return 2;
var connection = new NpgsqlConnectionStringBuilder(connectionText);
if (connection.Database != "bing_sql_aot_test")
{
    Console.Error.WriteLine("Use the dedicated bing_sql_aot_test database.");
    return 2;
}

var output = Path.GetFullPath(args[1]);
if (File.Exists(output)) throw new IOException("Report already exists.");
connection.Pooling = true;
connection.MaxPoolSize = 2;
connection.Timeout = 5;
connection.CommandTimeout = 30;
connection.ApplicationName = $"sql_mixed_integration_{Guid.NewGuid():N}";
var cases = new List<string>();
await RejectsUnmatchedCgroupBeforeConnecting();
await ExecutesReadsWritesAndCleansFixture(false);
await ExecutesReadsWritesAndCleansFixture(true);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await using (var report = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    await JsonSerializer.SerializeAsync(report, new
    {
        schema = "SqlMixedCapacityIntegration-v1", status = "PASS", cases,
        productionCapacityVerified = false,
        assemblySha256 = SqlMixedCapacityProbe.GetAssemblyHashes()
    }, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine($"SQL_MIXED_POSTGRESQL_INTEGRATION_PASS ({cases.Count})");
return 0;

async Task RejectsUnmatchedCgroupBeforeConnecting()
{
    // 本地 Windows 不具备 Linux cgroup；非法端口确保不会依赖数据库来拒绝请求。
    var previous = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSqlConnection");
    var originalError = Console.Error;
    using var error = new StringWriter();
    var rejectedReport = output + ".rejected.json";
    try
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__PostgreSqlConnection",
            "Host=127.0.0.1;Port=1;Database=bing_sql_aot_test;Username=unused");
        Console.SetError(error);
        var code = await SqlMixedCapacityProbe.RunAsync(new[]
        {
            "--mixed-capacity-probe", args[0], "8C16G", "8", rejectedReport
        });
        Check(code == 2 && error.ToString().Contains("active cgroup limits") && !File.Exists(rejectedReport),
            "正式入口必须在连接数据库前拒绝未匹配的 cgroup，且不产生收据。");
        cases.Add(nameof(RejectsUnmatchedCgroupBeforeConnecting));
    }
    finally
    {
        Console.SetError(originalError);
        Environment.SetEnvironmentVariable("ConnectionStrings__PostgreSqlConnection", previous);
    }
}

async Task ExecutesReadsWritesAndCleansFixture(bool diagnoseStages)
{
    var management = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Pooling = false };
    await using var database = new NpgsqlConnection(management.ConnectionString);
    await database.OpenAsync();
    var before = await FixtureTables(database);
    using var jsonNull = JsonDocument.Parse("null");
    // 短时回归使用 2000 行/1 秒预热/3 秒测量，不能冒充已批准的 100K/30/120 容量负载。
    var workload = new SqlMixedCapacityProbe.Workload(2000, 90, 10, 1, 3, 2,
        "NOT_APPLICABLE_INTEGRATION", jsonNull.RootElement.Clone(), jsonNull.RootElement.Clone());
    var concurrency = diagnoseStages ? 1 : 8;
    var result = await SqlMixedCapacityProbe.MeasureAsync(connection, workload, concurrency, diagnoseStages);
    if (diagnoseStages)
    {
        var timings = result.DiagnosticStages;
        Check(timings is { Length: 4 } && timings[0].Count == result.Measure.Attempted &&
            timings[1].Count == result.Measure.ReadAttempts && timings[2].Count == result.Measure.ReadAttempts &&
            timings[3].Count == result.Measure.WriteAttempts && timings.All(x => x.MeanMilliseconds >= 0),
            "诊断阶段只记录正式测量、不包含预热，读写与规划计数必须对应真实请求。");
        var opens = result.DiagnosticConnectionOpen;
        Check(timings.All(x => x.LatencyBucketWidthMilliseconds == 0.1 && x.LatencyHistogramMaxMilliseconds == 100 &&
            x.LatencyHistogramOverflowCount >= 0 && x.LatencyHistogramOverflowCount <= x.Count &&
            (x.P99Milliseconds == null || x.P99Milliseconds >= x.P50Milliseconds)),
            "真实诊断应提供独立阶段的有界分位数及溢出计数。");
        var waits = result.DiagnosticDatabaseWaits;
        Check(waits != null && waits.IntervalMilliseconds == 100 && waits.SampleCount == result.Activity.SampleCount &&
            waits.ActiveSessionObservations == waits.LockObservations + waits.WalSyncObservations +
                waits.WalWriteObservations + waits.OtherIoObservations + waits.NoWaitEventObservations + waits.OtherWaitObservations,
            "真实等待类别必须与同一管理采样计数对应，互斥合计为活动会话观测数。");
        Check(opens != null && opens.Count == result.Measure.Attempted && opens.Failed == 0 &&
            opens.TotalTicks >= 0 && opens.TotalTicks <= timings[2].TotalTicks + timings[3].TotalTicks,
            "连接打开只记录测量和收尾，恰好每个请求一次且包含在执行阶段内。");
        var commands = result.DiagnosticDatabaseCommands;
        Check(commands != null && commands.Read.Calls == result.Measure.ReadAttempts &&
            commands.Write.Calls == result.Measure.WriteAttempts &&
            commands.Read.Rows == result.Measure.ReadAttempts && commands.Write.Rows == result.Measure.WriteAttempts &&
            commands.Read.TotalExecutionMilliseconds > 0 && commands.Write.TotalExecutionMilliseconds > 0 &&
            commands.Write.WalRecords > 0 && commands.Write.WalBytes > 0 &&
            commands.Wal.Syncs > 0 && commands.Wal.SyncMilliseconds > 0,
            "服务端唯一表统计应排除预热、匹配读写次数，并观察持久化WAL；不强行匹配异步WAL次数。");
    }
    else Check(result.DiagnosticStages == null && result.DiagnosticConnectionOpen == null &&
        result.DiagnosticDatabaseCommands == null && result.DiagnosticDatabaseWaits == null,
        "普通负载不启用阶段计时或连接包装。");
    Check(result.Warmup.Failed == 0 && result.Measure.Failed == 0 &&
        result.Measure.ReadAttempts > 0 && result.Measure.WriteAttempts > 0 &&
        result.Measure.Attempted == result.Measure.Successful && result.Measure.PeakInFlight <= 8,
        "真实 DI 查询和写入均须成功，计数及并发必须一致。");
    Check(result.Measure.WriteAttempts == result.Measure.Attempted / 10 &&
        result.Measure.ReadAttempts + result.Measure.WriteAttempts == result.Measure.Attempted,
        "真实负载应保持每十次一次写入的确定调度。");
    Check(result.Activity.SampleCount > 0 && result.Activity.PeakActiveSessions <= 2 &&
        result.ConnectionPool.BusyConnections.SampleCount > 0,
        "服务端采样与实际 Npgsql 池计数须取得样本。");
    var after = await FixtureTables(database);
    Check(before.SequenceEqual(after), "测量结束必须删除本次独立测试表。");
    // 保存完整测量事实，格式与正式容量收据明确区分。
    var measurementPath = output + (diagnoseStages ? ".stages.json" : ".measurement.json");
    await using var measurement = new FileStream(measurementPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    await JsonSerializer.SerializeAsync(measurement, new
    {
        schema = "SqlMixedCapacityIntegrationMeasurement-v1", workload, concurrency, diagnoseStages,
        maxPoolSize = 2, result, fixtureTablesBefore = before, fixtureTablesAfter = after,
        productionCapacityVerified = false
    }, new JsonSerializerOptions { WriteIndented = true });
    cases.Add($"{nameof(ExecutesReadsWritesAndCleansFixture)}(diagnoseStages={diagnoseStages})");
}

static async Task<string[]> FixtureTables(NpgsqlConnection connection)
{
    await using var command = new NpgsqlCommand("""
        SELECT tablename FROM pg_tables
        WHERE schemaname = 'public' AND starts_with(tablename, 'sql_mixed_')
        ORDER BY tablename
        """, connection);
    await using var reader = await command.ExecuteReaderAsync();
    var names = new List<string>();
    while (await reader.ReadAsync()) names.Add(reader.GetString(0));
    return names.ToArray();
}

static void Check(bool valid, string message)
{
    if (!valid) throw new InvalidOperationException(message);
}
