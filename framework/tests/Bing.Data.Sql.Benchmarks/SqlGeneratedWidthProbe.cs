using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Bing.Uow;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>比较不同结果列宽下 SQLite 兼容映射与手工生成 Reader 的固定采样。</summary>
internal static class SqlGeneratedWidthProbe
{
    private const int Warmup = 32;
    private const int Samples = 5;
    private const int Iterations = 100;

    internal static int Run(string output, string identityOutput)
    {
        var results = new List<object>();
        foreach (var mode in new[] { SqlExecutionMode.Compatible, SqlExecutionMode.GeneratedOnly })
        foreach (var rows in new[] { 1, 32, 256 })
        foreach (var columns in new[] { 2, 8, 32 })
        {
            using var workload = CreateWorkload(mode, rows, columns);
            workload.Setup();

            for (var iteration = 0; iteration < Warmup; iteration++)
                workload.ReadAndValidate();

            for (var sample = 0; sample < Samples; sample++)
            {
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var timer = Stopwatch.StartNew();
                var rowChecksum = 0;
                for (var iteration = 0; iteration < Iterations; iteration++)
                    rowChecksum += workload.ReadAndValidate();
                timer.Stop();
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                if (rowChecksum != rows * Iterations)
                    throw new InvalidOperationException("采样行数校验失败。");

                results.Add(new
                {
                    mode = mode.ToString(),
                    rows,
                    columns,
                    operation = "NoTransaction",
                    sample,
                    iterations = Iterations,
                    rowsPerOperation = rows,
                    lastColumnValue = workload.ExpectedLastColumnValue,
                    nsPerOperation = timer.Elapsed.TotalNanoseconds / Iterations,
                    bytesPerOperation = (double)allocated / Iterations
                });
            }
        }

        if (results.Count != 90)
            throw new InvalidOperationException($"期望 90 条采样记录，实际得到 {results.Count} 条。");

        var identity = CreateIdentity();
        WriteJson(output, new
        {
            workload = "SqlGeneratedWidthProbe-v1",
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            warmup = Warmup,
            samples = Samples,
            iterations = Iterations,
            groups = 18,
            sampleRecords = results.Count,
            columnWidths = new[] { 2, 8, 32 },
            rowCounts = new[] { 1, 32, 256 },
            modes = new[] { SqlExecutionMode.Compatible.ToString(), SqlExecutionMode.GeneratedOnly.ToString() },
            operation = "NoTransaction",
            mapping = "Strongly typed DTOs with manual per-property WithGeneratedReader delegates",
            comparison = "Current candidate only; no historical width-matched baseline is available",
            results
        });
        WriteJson(identityOutput, new
        {
            capturedAt = DateTimeOffset.UtcNow,
            commandLine = Environment.CommandLine,
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            probe = "SqlGeneratedWidthProbe-v1",
            exitCode = 0,
            before = "Not available: no historical width-matched before run was collected.",
            binaries = identity
        });
        return 0;
    }

    private static WidthWorkload CreateWorkload(SqlExecutionMode mode, int rows, int columns) => columns switch
    {
        2 => new WidthWorkload<Width2Row>(mode, rows, 2, static row => row.C02, Width2Metadata()),
        8 => new WidthWorkload<Width8Row>(mode, rows, 8, static row => row.C08, Width8Metadata()),
        32 => new WidthWorkload<Width32Row>(mode, rows, 32, static row => row.C32, Width32Metadata()),
        _ => throw new ArgumentOutOfRangeException(nameof(columns), columns, "未支持的列宽。")
    };

    private static EntityModelMetadata Width2Metadata() => new(typeof(Width2Row), "rows", null, new[]
    {
        IntColumn(nameof(Width2Row.C01), static (row, value) => ((Width2Row)row).C01 = (int)value,
            static (row, reader, ordinal) =>
            {
                if (reader.GetFieldType(ordinal) != typeof(long)) return false;
                ((Width2Row)row).C01 = checked((int)reader.GetInt64(ordinal)); return true;
            }),
        IntColumn(nameof(Width2Row.C02), static (row, value) => ((Width2Row)row).C02 = (int)value,
            static (row, reader, ordinal) =>
            {
                if (reader.GetFieldType(ordinal) != typeof(long)) return false;
                ((Width2Row)row).C02 = checked((int)reader.GetInt64(ordinal)); return true;
            })
    }, static () => new Width2Row());

    private static EntityModelMetadata Width8Metadata() => new(typeof(Width8Row), "rows", null, new[]
    {
        IntColumn(nameof(Width8Row.C01), static (row, value) => ((Width8Row)row).C01 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width8Row)row).C01 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width8Row.C02), static (row, value) => ((Width8Row)row).C02 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width8Row)row).C02 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width8Row.C03), static (row, value) => ((Width8Row)row).C03 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width8Row)row).C03 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width8Row.C04), static (row, value) => ((Width8Row)row).C04 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width8Row)row).C04 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width8Row.C05), static (row, value) => ((Width8Row)row).C05 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width8Row)row).C05 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width8Row.C06), static (row, value) => ((Width8Row)row).C06 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width8Row)row).C06 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width8Row.C07), static (row, value) => ((Width8Row)row).C07 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width8Row)row).C07 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width8Row.C08), static (row, value) => ((Width8Row)row).C08 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width8Row)row).C08 = checked((int)reader.GetInt64(ordinal)); return true; })
    }, static () => new Width8Row());

    private static EntityModelMetadata Width32Metadata() => new(typeof(Width32Row), "rows", null, new[]
    {
        IntColumn(nameof(Width32Row.C01), static (row, value) => ((Width32Row)row).C01 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C01 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C02), static (row, value) => ((Width32Row)row).C02 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C02 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C03), static (row, value) => ((Width32Row)row).C03 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C03 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C04), static (row, value) => ((Width32Row)row).C04 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C04 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C05), static (row, value) => ((Width32Row)row).C05 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C05 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C06), static (row, value) => ((Width32Row)row).C06 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C06 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C07), static (row, value) => ((Width32Row)row).C07 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C07 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C08), static (row, value) => ((Width32Row)row).C08 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C08 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C09), static (row, value) => ((Width32Row)row).C09 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C09 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C10), static (row, value) => ((Width32Row)row).C10 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C10 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C11), static (row, value) => ((Width32Row)row).C11 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C11 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C12), static (row, value) => ((Width32Row)row).C12 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C12 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C13), static (row, value) => ((Width32Row)row).C13 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C13 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C14), static (row, value) => ((Width32Row)row).C14 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C14 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C15), static (row, value) => ((Width32Row)row).C15 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C15 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C16), static (row, value) => ((Width32Row)row).C16 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C16 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C17), static (row, value) => ((Width32Row)row).C17 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C17 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C18), static (row, value) => ((Width32Row)row).C18 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C18 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C19), static (row, value) => ((Width32Row)row).C19 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C19 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C20), static (row, value) => ((Width32Row)row).C20 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C20 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C21), static (row, value) => ((Width32Row)row).C21 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C21 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C22), static (row, value) => ((Width32Row)row).C22 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C22 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C23), static (row, value) => ((Width32Row)row).C23 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C23 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C24), static (row, value) => ((Width32Row)row).C24 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C24 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C25), static (row, value) => ((Width32Row)row).C25 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C25 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C26), static (row, value) => ((Width32Row)row).C26 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C26 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C27), static (row, value) => ((Width32Row)row).C27 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C27 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C28), static (row, value) => ((Width32Row)row).C28 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C28 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C29), static (row, value) => ((Width32Row)row).C29 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C29 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C30), static (row, value) => ((Width32Row)row).C30 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C30 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C31), static (row, value) => ((Width32Row)row).C31 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C31 = checked((int)reader.GetInt64(ordinal)); return true; }),
        IntColumn(nameof(Width32Row.C32), static (row, value) => ((Width32Row)row).C32 = (int)value,
            static (row, reader, ordinal) => { if (reader.GetFieldType(ordinal) != typeof(long)) return false; ((Width32Row)row).C32 = checked((int)reader.GetInt64(ordinal)); return true; })
    }, static () => new Width32Row());

    private static EntityPropertyMetadata IntColumn(string name, Action<object, object> setter,
        Func<object, DbDataReader, int, bool> reader) =>
        EntityPropertyMetadata.CreateGenerated(name, typeof(int), true, true, generatedSetter: setter)
            .WithGeneratedReader(reader);

    private static void WriteJson(string path, object value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
    }

    private static object[] CreateIdentity() => new[]
    {
        AssemblyIdentity(typeof(SqlGeneratedWidthProbe).Assembly.Location),
        AssemblyIdentity(typeof(SqlMetadataOptions).Assembly.Location),
        AssemblyIdentity(FindAssemblyPath("Bing.Dapper.Core")),
        AssemblyIdentity(FindAssemblyPath("Bing.Dapper.Sqlite"))
    };

    private static string FindAssemblyPath(string name)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, name, StringComparison.Ordinal));
        return assembly?.Location ?? throw new InvalidOperationException($"运行时未加载程序集 {name}。");
    }

    private static object AssemblyIdentity(string path) => new
    {
        fileName = Path.GetFileName(path),
        path,
        sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()
    };

    private readonly record struct QueryResult(int Count, int LastColumnValue);

    private abstract class WidthWorkload : IDisposable
    {
        protected WidthWorkload(SqlExecutionMode mode, int rows, int columns)
        {
            Mode = mode;
            Rows = rows;
            Columns = columns;
            ExpectedLastColumnValue = rows * 1000 + columns;
        }

        protected SqlExecutionMode Mode { get; }
        protected int Rows { get; }
        protected int Columns { get; }
        internal int ExpectedLastColumnValue { get; }
        internal abstract void Setup();
        internal abstract int ReadAndValidate();
        public abstract void Dispose();
    }

    private sealed class WidthWorkload<TRow> : WidthWorkload where TRow : class
    {
        private readonly Func<TRow, int> _lastColumn;
        private readonly EntityModelMetadata _metadata;
        private string _databasePath;
        private ServiceProvider _provider;
        private IServiceScope _scope;
        private ISqlQueryFactory _queries;
        private string _sql;

        internal WidthWorkload(SqlExecutionMode mode, int rows, int columns, Func<TRow, int> lastColumn,
            EntityModelMetadata metadata) : base(mode, rows, columns)
        {
            _lastColumn = lastColumn;
            _metadata = metadata;
        }

        internal override void Setup()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), "bing-width-bench-" + Guid.NewGuid().ToString("N") + ".db");
            var connectionString = "Data Source=" + _databasePath;
            CreateDatabase(connectionString);

            var services = new ServiceCollection();
            services.AddUnitOfWorkScopes();
            services.ConfigureSqlMetadata(options => options.ExecutionMode = Mode);
            services.AddSqlGeneratedModels(new[] { _metadata });
            services.AddSqlDataSource("default", DatabaseType.Sqlite, connectionString);
            services.AddSqliteProvider();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();
            _queries = _scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>();
            _sql = $"SELECT {string.Join(", ", Enumerable.Range(1, Columns).Select(index => "C" + index.ToString("D2")))} FROM rows ORDER BY rowid";
        }

        internal override int ReadAndValidate()
        {
            using var query = _queries.Create();
            var rows = query.Sql(_sql).ToList<TRow>();
            if (rows.Count != Rows)
                throw new InvalidOperationException($"行数不匹配：期望 {Rows}，实际 {rows.Count}。");
            var lastColumnValue = _lastColumn(rows[rows.Count - 1]);
            if (lastColumnValue != ExpectedLastColumnValue)
                throw new InvalidOperationException($"末列值不匹配：期望 {ExpectedLastColumnValue}，实际 {lastColumnValue}。");
            return rows.Count;
        }

        public override void Dispose()
        {
            _scope?.Dispose();
            _provider?.Dispose();
            SqliteConnection.ClearAllPools();
            if (File.Exists(_databasePath)) File.Delete(_databasePath);
        }

        private void CreateDatabase(string connectionString)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            var schema = string.Join(", ", Enumerable.Range(1, Columns).Select(index => "C" + index.ToString("D2") + " INTEGER NOT NULL"));
            var inserts = new StringBuilder();
            inserts.Append("CREATE TABLE rows (").Append(schema).AppendLine(");");
            for (var row = 1; row <= Rows; row++)
            {
                var values = string.Join(",", Enumerable.Range(1, Columns).Select(column => (row * 1000 + column).ToString()));
                inserts.Append("INSERT INTO rows VALUES (").Append(values).AppendLine(");");
            }
            command.CommandText = inserts.ToString();
            command.ExecuteNonQuery();
        }
    }

    public sealed class Width2Row { public int C01 { get; set; } public int C02 { get; set; } }
    public sealed class Width8Row
    {
        public int C01 { get; set; } public int C02 { get; set; } public int C03 { get; set; } public int C04 { get; set; }
        public int C05 { get; set; } public int C06 { get; set; } public int C07 { get; set; } public int C08 { get; set; }
    }
    public sealed class Width32Row
    {
        public int C01 { get; set; } public int C02 { get; set; } public int C03 { get; set; } public int C04 { get; set; }
        public int C05 { get; set; } public int C06 { get; set; } public int C07 { get; set; } public int C08 { get; set; }
        public int C09 { get; set; } public int C10 { get; set; } public int C11 { get; set; } public int C12 { get; set; }
        public int C13 { get; set; } public int C14 { get; set; } public int C15 { get; set; } public int C16 { get; set; }
        public int C17 { get; set; } public int C18 { get; set; } public int C19 { get; set; } public int C20 { get; set; }
        public int C21 { get; set; } public int C22 { get; set; } public int C23 { get; set; } public int C24 { get; set; }
        public int C25 { get; set; } public int C26 { get; set; } public int C27 { get; set; } public int C28 { get; set; }
        public int C29 { get; set; } public int C30 { get; set; } public int C31 { get; set; } public int C32 { get; set; }
    }
}
