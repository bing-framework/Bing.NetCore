using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Aot.PackageModels;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

SQLitePCL.Batteries_V2.Init();
var path = Path.Combine(Path.GetTempPath(), "bing-raw-only-" + Guid.NewGuid().ToString("N") + ".db");
try
{
    using (var connection = new SqliteConnection("Data Source=" + path))
    {
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE package_rows(Id INTEGER, Name TEXT); INSERT INTO package_rows VALUES(1,'raw');";
        command.ExecuteNonQuery();
    }
    var services = new ServiceCollection();
    services.AddSqlGeneratedModels(PackageModels.CreateModels());
    services.AddSqlDataSource("default", DatabaseType.Sqlite, "Data Source=" + path);
    services.AddSqliteProvider();
    var converterDependency = GenericConverterProbe.Register(services);
    using var provider = services.BuildServiceProvider();
    GenericConverterProbe.Verify(provider, converterDependency);
    using var scope = provider.CreateScope();
    using var query = scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().Create();
    var rows = await query.Sql("SELECT Name, Id FROM package_rows WHERE Id>=@Minimum", new RawFilter { Minimum = 1 }).ToListAsync<RawRow>();
    if (rows.Count != 1 || rows[0].Id != 1 || rows[0].Name != "raw") throw new Exception("raw-only reader/binder");
    if (AppContext.TryGetSwitch("Bing.Data.Sql.ReflectionFallbackEnabled", out var enabled) && enabled)
        throw new Exception("strict package props not applied");
    Console.WriteLine("RAW_ONLY_PACKAGE_NATIVE_PASS");
    if (args.Contains("--perf"))
    {
        // 固定输入与停止条件：256 行、100 次预热、1000 次无事务查询。
        using (var connection = new SqliteConnection("Data Source=" + path))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "WITH RECURSIVE ids(Id) AS (SELECT 2 UNION ALL SELECT Id+1 FROM ids WHERE Id<256) INSERT INTO package_rows SELECT Id, 'raw' FROM ids";
            command.ExecuteNonQuery();
        }
        var filter = new RawFilter { Minimum = 1 };
        const string sql = "SELECT Name, Id FROM package_rows WHERE Id>=@Minimum";
        for (var iteration = 0; iteration < 100; iteration++)
            if (query.Sql(sql, filter).ToList<RawRow>().Count != 256) throw new Exception("perf warmup");
        var allocated = GC.GetTotalAllocatedBytes(true);
        var timer = Stopwatch.StartNew();
        var checksum = 0;
        for (var iteration = 0; iteration < 1000; iteration++) checksum += query.Sql(sql, filter).ToList<RawRow>().Count;
        timer.Stop();
        allocated = GC.GetTotalAllocatedBytes(true) - allocated;
        if (checksum != 256000) throw new Exception("perf checksum");
        Console.WriteLine(FormattableString.Invariant($"NATIVE_PERF {{\"iterations\":1000,\"rows\":256,\"warmup\":100,\"elapsedMs\":{timer.Elapsed.TotalMilliseconds},\"bytesPerQuery\":{allocated / 1000.0},\"peakWorkingSetBytes\":{Process.GetCurrentProcess().PeakWorkingSet64}}}"));
    }
}
finally { SqliteConnection.ClearAllPools(); if (File.Exists(path)) File.Delete(path); }
