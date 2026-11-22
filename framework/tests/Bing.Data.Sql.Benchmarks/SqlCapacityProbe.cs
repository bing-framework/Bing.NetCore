using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper;
using Bing.Dapper.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>以专用 PostgreSQL 库比较运行时 Builder 和共享模板的同一组并发读请求。</summary>
internal static class SqlCapacityProbe
{
    private const string Database = "bing_sql_aot_test";
    private const int PageSize = 8;

    internal static async Task<int> RunAsync(string[] args)
    {
        var observeServer = args.Length == 7 && args[6] == "--observe-server";
        if ((args.Length != 6 && !observeServer) || args[1] is not ("runtime" or "template") ||
            !TryPositiveInt(args[2], out var rows) || !TryPositiveInt(args[3], out var requests) ||
            !TryPositiveInt(args[4], out var concurrency) || concurrency > requests)
        {
            Console.Error.WriteLine("Usage: --capacity-probe <runtime|template> <rows> <requests> <concurrency> <report.json> [--observe-server] (concurrency <= requests).");
            return 2;
        }

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSqlConnection");
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_POSTGRESQL_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(connectionString))
            return RejectDatabase();

        try
        {
            var configuration = new NpgsqlConnectionStringBuilder(connectionString);
            if (configuration.Database != Database)
                return RejectDatabase();

            var path = args[1];
            var warmupRequests = Math.Max(64, concurrency);
            var (warmup, measure, databaseVersion, serverActivity) = await MeasureAsync(configuration, path, rows,
                requests, concurrency, warmupRequests, observeServer);
            var report = new
            {
                probe = "SqlQueryCapacityProbe-v2",
                path,
                provider = "PostgreSQL",
                databaseVersion,
                npgsqlVersion = typeof(NpgsqlConnection).Assembly.GetName().Version.ToString(),
                runtime = RuntimeInformation.FrameworkDescription,
                os = RuntimeInformation.OSDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                processors = Environment.ProcessorCount,
                assemblySha256 = GetAssemblyHashes(),
                rows,
                requests,
                concurrency,
                maxQueuedRequests = 0, // 闭环客户端不维护额外的入站请求队列。
                pooling = configuration.Pooling,
                maxPoolSize = configuration.MaxPoolSize,
                warmupRequests,
                warmupFailed = warmup.Failed,
                warmupFirstFailureType = warmup.FirstFailureType,
                serverActivity,
                measure
            };
            var output = Path.GetFullPath(args[5]);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            await using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return warmup.Failed == 0 && measure.Failed == 0 ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"SQL capacity probe could not finish: {error.GetType().Name}.\n{error.StackTrace}");
            return 2;
        }
    }

    /// <summary>记录固定读负载使用的 SQL 程序集，供验收时与候选构建逐一比对。</summary>
    private static Dictionary<string, string> GetAssemblyHashes()
    {
        var hashes = new Dictionary<string, string>();
        foreach (var type in new[]
        {
            typeof(SqlCapacityProbe), typeof(DatabaseType), typeof(ISqlQueryFactory),
            typeof(DapperCoreServiceCollectionExtensions), typeof(PostgreSqlServiceCollectionExtensions),
            typeof(global::Dapper.SqlMapper), typeof(NpgsqlConnection)
        })
        {
            var path = type.Assembly.Location;
            using var stream = File.OpenRead(path);
            hashes.Add(Path.GetFileName(path), Convert.ToHexString(SHA256.HashData(stream)));
        }
        return hashes;
    }

    private static async Task<(SqlCapacityLoad.Result Warmup, SqlCapacityLoad.Result Measure,
        string DatabaseVersion, ServerActivity Activity)> MeasureAsync(NpgsqlConnectionStringBuilder configuration,
        string path, int rows, int requests, int concurrency, int warmupRequests, bool observeServer)
    {
        // 管理连接不占用被测池。每次运行只建删自己命名的表，测试进程可以并行执行。
        var setupConfiguration = new NpgsqlConnectionStringBuilder(configuration.ConnectionString) { Pooling = false };
        await using var setup = new NpgsqlConnection(setupConfiguration.ConnectionString);
        await setup.OpenAsync();
        var fixtureName = $"sql_capacity_{Guid.NewGuid():N}";
        var table = $"public.{fixtureName}";
        await using (var transaction = await setup.BeginTransactionAsync())
        {
            await using var seed = new NpgsqlCommand($"""
                CREATE TABLE {table} (id integer PRIMARY KEY);
                INSERT INTO {table} (id) SELECT generate_series(1, @rows);
                ANALYZE {table};
                """, setup, transaction);
            seed.Parameters.AddWithValue("rows", rows);
            await seed.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }

        try
        {
            if (observeServer)
                configuration.ApplicationName = fixtureName;
            var services = new ServiceCollection();
            services.AddSqlDataSource("default", DatabaseType.PgSql, configuration.ConnectionString);
            services.AddPostgreSqlProvider();
            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var template = path == "template" ? CreateTemplate(table) : null;

            async Task Execute(int request)
            {
                var minimum = (int)(unchecked((uint)request * 2654435761u) % (uint)rows) + 1;
                var exact = (request & 1) == 0;
                var pageEnd = minimum + Math.Min(PageSize - 1, rows - minimum);
                await using var scope = provider.CreateAsyncScope();
                await using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
                var result = template == null
                    ? await query.Query().Select("id").From(table).Where("id", minimum, Operator.GreaterEqual)
                        .Where("id", pageEnd, Operator.LessEqual).WhereIf("id", minimum, exact).OrderBy("id").ToListAsync<int>()
                    : await query.Sql(template, exact ? 1 : 0, new object[] { minimum, pageEnd })
                        .ToListAsync<int>();
                CheckResults(result, minimum, exact ? 1 : pageEnd - minimum + 1);
            }

            var warmup = await SqlCapacityLoad.RunAsync(warmupRequests, concurrency, Execute);
            var measure = SqlCapacityLoad.RunAsync(requests, concurrency, Execute);
            ServerActivity activity = null;
            if (observeServer)
            {
                var observation = ObserveServerAsync(setup, fixtureName, measure);
                // 等负载和采样都结束后再清理测试表。
                await Task.WhenAll(measure, observation);
                activity = await observation;
            }
            return (warmup, await measure, setup.PostgreSqlVersion.ToString(), activity);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand($"DROP TABLE {table}", setup);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    /// <summary>经独立管理连接采样同一次探针的活跃会话及其中等待数据库锁的会话。</summary>
    private static async Task<ServerActivity> ObserveServerAsync(NpgsqlConnection setup, string applicationName, Task measure)
    {
        const int intervalMilliseconds = 10;
        var sampleCount = 0;
        var peakActive = 0;
        var peakLockWaiting = 0;
        await using var sample = new NpgsqlCommand("""
            SELECT count(*)::integer, (count(*) FILTER (WHERE wait_event_type = 'Lock'))::integer
            FROM pg_stat_activity
            WHERE datname = current_database() AND application_name = @applicationName AND state = 'active'
            """, setup);
        sample.Parameters.AddWithValue("applicationName", applicationName);

        while (!measure.IsCompleted)
        {
            await using (var reader = await sample.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                if (measure.IsCompleted)
                    break;
                sampleCount++;
                peakActive = Math.Max(peakActive, reader.GetInt32(0));
                peakLockWaiting = Math.Max(peakLockWaiting, reader.GetInt32(1));
            }
            await Task.WhenAny(measure, Task.Delay(intervalMilliseconds));
        }
        return new ServerActivity(intervalMilliseconds, sampleCount, sampleCount == 0 ? null : peakActive,
            sampleCount == 0 ? null : peakLockWaiting);
    }

    private sealed record ServerActivity(int IntervalMilliseconds, int SampleCount, int? PeakActiveSessions,
        int? PeakLockWaitingSessions);

    private static SqlQueryShapeTemplate CreateTemplate(string table) => new(
        new SqlQueryFragment($"SELECT id FROM {table} WHERE id >= @minimum AND id <= @maximum",
            new[] { new SqlParam("@minimum", 0, DbType.Int32), new SqlParam("@maximum", 0, DbType.Int32) }),
        new[] { new SqlQueryOptionalFragment(0, "AND id = @minimum", new[] { new SqlParam("@minimum", 0, DbType.Int32) }) },
        " ", new SqlQueryFragment("ORDER BY id"));

    private static void CheckResults(List<int> result, int first, int count)
    {
        if (result.Count != count)
            throw new InvalidDataException("Unexpected result count.");
        for (var index = 0; index < result.Count; index++)
        {
            if (result[index] != first + index)
                throw new InvalidDataException("Unexpected result row.");
        }
    }

    private static bool TryPositiveInt(string value, out int parsed) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) && parsed > 0;

    private static int RejectDatabase()
    {
        Console.Error.WriteLine("SQL capacity probe requires RUN_POSTGRESQL_INTEGRATION_TESTS=true and a dedicated bing_sql_aot_test database.");
        return 2;
    }
}
