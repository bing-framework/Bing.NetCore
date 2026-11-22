using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>真实 SQLite 结果类型变化、只读成员及发送前能力检查的回归测试。</summary>
public sealed class SqliteGeneratedReaderContractTest
{
    /// <summary>Single 必须先转换第二行，再报告基数错误；First 只物化首行。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Terminal_WhenSecondRowCannotConvert_ShouldPreserveConversionError(bool asynchronous, bool orDefault)
    {
        using var host = Host(SqlExecutionMode.GeneratedOnly);
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var query = Query(host, connection);
        const string sql = "SELECT 1 AS Id UNION ALL SELECT 'invalid'";
        if (asynchronous)
        {
            await Assert.ThrowsAsync<FormatException>(() => orDefault
                ? query.Sql(sql).ToEntityAsync<Row>() : query.Sql(sql).SingleAsync<Row>());
            Assert.Equal(1, (await query.Sql(sql).FirstAsync<Row>()).Id);
        }
        else
        {
            Assert.Throws<FormatException>(() => orDefault
                ? query.Sql(sql).ToEntity<Row>() : query.Sql(sql).Single<Row>());
            Assert.Equal(1, query.Sql(sql).First<Row>().Id);
        }
        Assert.Equal(1, query.ErrorCount);
    }

    [Theory]
    [InlineData(SqlExecutionMode.GeneratedOnly, false, 256)]
    [InlineData(SqlExecutionMode.GeneratedOnly, true, -1)]
    [InlineData(SqlExecutionMode.GeneratedPreferred, false, -1)]
    [InlineData(SqlExecutionMode.GeneratedPreferred, true, 256)]
    public async Task NullableEnumOverflow_ShouldFailOnceAndReleaseLease(SqlExecutionMode mode, bool asynchronous, int value)
    {
        using var host = Host(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var query = Query(host, connection);
        var command = query.Sql(value == 256 ? "SELECT 256 AS Value" : "SELECT -1 AS Value");
        if (asynchronous) await Assert.ThrowsAsync<OverflowException>(() => command.SingleAsync<EnumRow>());
        else Assert.Throws<OverflowException>(() => command.Single<EnumRow>());
        Assert.Equal(1, query.BeforeCount);
        Assert.Equal(1, query.ErrorCount);
        Assert.Equal(9, query.Sql("SELECT 9").Single<int>());
    }

    [Theory]
    [InlineData(SqlExecutionMode.GeneratedOnly)]
    [InlineData(SqlExecutionMode.GeneratedPreferred)]
    public void NullableEnums_ShouldKeepUnnamedValuesAndNullDefaults(SqlExecutionMode mode)
    {
        using var host = Host(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var query = Query(host, connection);
        Assert.Equal((ByteState)255, query.Sql("SELECT 255 AS Value").Single<EnumRow>().Value);
        Assert.Null(query.Sql("SELECT NULL AS Value").Single<EnumRow>().Value);
        Assert.Null(query.Sql("SELECT NULL").Single<ByteState?>());
    }

    [Theory]
    [InlineData(SqlExecutionMode.GeneratedOnly, false)]
    [InlineData(SqlExecutionMode.GeneratedOnly, true)]
    [InlineData(SqlExecutionMode.GeneratedPreferred, false)]
    [InlineData(SqlExecutionMode.GeneratedPreferred, true)]
    public async Task MixedStorageTypes_ShouldKeepConversionsNullDefaultsAndComputedMembers(SqlExecutionMode mode, bool asynchronous)
    {
        using var host = Host(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var query = Query(host, connection);
        const string sql = "SELECT 1 AS Id, 'first' AS Name, 90 AS Computed UNION ALL SELECT '2', NULL, 91 UNION ALL SELECT 2.5, 'third', 92";
        var command = query.Sql(sql);
        var rows = asynchronous ? await command.ToListAsync<Row>() : command.ToList<Row>();
        Assert.Equal(new[] { 1, 2, 2 }, rows.Select(row => row.Id));
        Assert.Equal(new[] { "first", "initial", "third" }, rows.Select(row => row.Name));
        Assert.Equal(new[] { 2, 4, 4 }, rows.Select(row => row.Computed));
        Assert.Equal(1, query.BeforeCount);
        Assert.Equal(9, query.Sql("SELECT 9").Single<int>());
    }

    [Theory]
    [InlineData(SqlExecutionMode.GeneratedOnly, false)]
    [InlineData(SqlExecutionMode.GeneratedOnly, true)]
    [InlineData(SqlExecutionMode.GeneratedPreferred, false)]
    [InlineData(SqlExecutionMode.GeneratedPreferred, true)]
    public async Task PrivateSetter_ShouldSelectCompatibleOrRejectBeforeIo(SqlExecutionMode mode, bool asynchronous)
    {
        using var host = Host(mode);
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var query = Query(host, connection);
        var command = query.Sql("SELECT 42 AS Id");
        if (mode == SqlExecutionMode.GeneratedOnly)
        {
            if (asynchronous) await Assert.ThrowsAsync<NotSupportedException>(() => command.SingleAsync<PrivateRow>());
            else Assert.Throws<NotSupportedException>(() => command.Single<PrivateRow>());
            Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
            Assert.Equal(0, query.BeforeCount);
        }
        else
        {
            var row = asynchronous ? await command.SingleAsync<PrivateRow>() : command.Single<PrivateRow>();
            Assert.Equal(42, row.Id);
            Assert.Equal(1, query.BeforeCount);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IgnoredPrivateSetter_ShouldNotDisableGeneratedReader(bool excludeInFinalMapping)
    {
        using var host = Host(SqlExecutionMode.GeneratedOnly, excludeInFinalMapping);
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var query = Query(host, connection);
        Assert.Equal(7, query.Sql("SELECT 7 AS Id, 19 AS Hidden").Single<IgnoredRow>().Id);
        Assert.Equal(1, query.BeforeCount);
    }

    private static RecordingQuery Query(IServiceProvider host, SqliteConnection connection) => new(host,
        new SqlOptions<SqliteSqlQuery> { DatabaseType = DatabaseType.Sqlite, Connection = connection });

    private static ServiceProvider Host(SqlExecutionMode mode, bool excludeInFinalMapping = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSqlCore();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        services.AddSqlGeneratedModels(new[]
        {
            new EntityModelMetadata(typeof(EnumRow), "enum_rows", null, new[]
            {
                EntityPropertyMetadata.CreateGenerated("Value", typeof(ByteState?), true, true,
                    generatedSetter: (row, value) => ((EnumRow)row).Value = (ByteState?)value)
            }, () => new EnumRow()),
            new EntityModelMetadata(typeof(Row), "rows", null, new[]
            {
                EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true, generatedSetter: (row, value) => ((Row)row).Id = (int)value)
                    .WithGeneratedReader((row, reader, ordinal) =>
                    {
                        if (reader.GetFieldType(ordinal) != typeof(long)) return false;
                        ((Row)row).Id = checked((int)reader.GetInt64(ordinal));
                        return true;
                    }),
                EntityPropertyMetadata.CreateGenerated("Name", typeof(string), true, true, generatedSetter: (row, value) => ((Row)row).Name = (string)value),
                EntityPropertyMetadata.CreateGenerated("Computed", typeof(int), true, false)
            }, () => new Row()),
            new EntityModelMetadata(typeof(PrivateRow), "private_rows", null, new[]
            {
                PrivateMember("Id", false)
            }, () => new PrivateRow()),
            new EntityModelMetadata(typeof(IgnoredRow), "ignored_rows", null, new[]
            {
                EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true, generatedSetter: (row, value) => ((IgnoredRow)row).Id = (int)value),
                PrivateMember("Hidden", !excludeInFinalMapping)
            }, () => new IgnoredRow())
        });
        if (excludeInFinalMapping)
            services.AddSingleton<IEntityMappingResolver>(provider => new ExcludingResolver(
                new DefaultEntityMappingResolver(provider.GetRequiredService<IEntityModelMetadataProvider>())));
        return services.BuildServiceProvider();
    }

    private sealed class ExcludingResolver(IEntityMappingResolver inner) : IEntityMappingResolver
    {
        public EntityDescriptor GetDescriptor(Type type) => inner.GetDescriptor(type);
        public EntityMappingMetadata Resolve(Type type, DatabaseContext context)
        {
            var mapping = inner.Resolve(type, context);
            if (type != typeof(IgnoredRow)) return mapping;
            return new EntityMappingMetadata
            {
                EntityType = mapping.EntityType, Model = mapping.Model, Table = mapping.Table,
                Columns = mapping.Columns.Where(pair => pair.Key != "Hidden").ToDictionary(pair => pair.Key, pair => pair.Value)
            };
        }
    }

    private static EntityPropertyMetadata PrivateMember(string name, bool ignored) => EntityPropertyMetadata.CreateGenerated(
        name, typeof(int), true, false, null, ignored, false, DatabaseGeneratedOption.None, false, false, null, null, null, null, true);

    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "initial";
        public int Computed => Id * 2;
    }
    public sealed class PrivateRow { public int Id { get; private set; } }
    public sealed class IgnoredRow { public int Id { get; set; } public int Hidden { get; private set; } }
    public enum ByteState : byte { One = 1 }
    public sealed class EnumRow { public ByteState? Value { get; set; } }

    private sealed class RecordingQuery : SqliteSqlQuery
    {
        public RecordingQuery(IServiceProvider provider, SqlOptions<SqliteSqlQuery> options) : base(provider, options) { }
        public int BeforeCount { get; private set; }
        public int ErrorCount { get; private set; }
        protected override bool ExecuteBefore() { BeforeCount++; return base.ExecuteBefore(); }
        protected override void ExecuteError(DiagnosticsMessage message, Exception exception) => ErrorCount++;
    }
}
