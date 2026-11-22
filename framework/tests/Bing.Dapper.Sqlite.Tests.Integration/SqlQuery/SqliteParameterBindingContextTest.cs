using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>真实 SQLite 命令验证生成参数与字典绑定的上下文和请求快照。</summary>
public sealed class SqliteParameterBindingContextTest
{
    [Theory]
    [InlineData(SqlExecutionMode.Compatible, false)]
    [InlineData(SqlExecutionMode.GeneratedPreferred, false)]
    [InlineData(SqlExecutionMode.GeneratedOnly, false)]
    [InlineData(SqlExecutionMode.GeneratedPreferred, true)]
    [InlineData(SqlExecutionMode.GeneratedOnly, true)]
    public async Task Bind_WhenSourceChanges_ShouldKeepOneContextAndIndependentValues(SqlExecutionMode mode, bool dictionary)
    {
        var models = new RegisteredEntityModelMetadataProvider(new EntityModelMetadata(typeof(Filter), "filters", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Filter.Value), typeof(int), true, true,
                generatedGetter: static source => ((Filter)source).Value),
            EntityPropertyMetadata.CreateGenerated(nameof(Filter.Offset), typeof(int?), true, true,
                generatedGetter: static source => ((Filter)source).Offset)
        }));
        var contexts = new CountingContexts();
        var binder = new DefaultSqlParameterBinder(new DefaultEntityMappingResolver(models),
            options: new SqlMetadataOptions { ExecutionMode = mode }, databaseContextResolver: contexts, models: models);
        var filter = new Filter { Value = 7 };
        var values = new Dictionary<string, object> { ["Value"] = 7, ["Offset"] = null };
        object source = dictionary ? values : filter;
        var first = binder.Bind(source);
        Assert.Equal(1, contexts.Calls);
        filter.Value = 20;
        filter.Offset = 2;
        values["Value"] = 20;
        values["Offset"] = 2;

        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        Assert.Equal(7L, await Execute(first));
        Assert.Equal(22L, await Execute(binder.Bind(source)));
        Assert.Equal(2, contexts.Calls);

        async Task<long> Execute(object bound)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT @Value + COALESCE(@Offset, 0)";
            Assert.IsAssignableFrom<ISqlStaticParameterSource>(bound).BindTo(command);
            Assert.Equal("SELECT @Value + COALESCE(@Offset, 0)", command.CommandText);
            Assert.Equal(new[] { "Value", "Offset" }, command.Parameters.Cast<SqliteParameter>().Select(parameter => parameter.ParameterName.TrimStart('@')));
            return (long)(await command.ExecuteScalarAsync());
        }
    }

    /// <summary>
    /// 结构化参数映射应复用一次上下文，并能通过真实 SQLite 命令执行完整 SQL。
    /// </summary>
    [Fact]
    public async Task Bind_ParameterMap_ShouldReuseContextAndExecuteCompleteSql()
    {
        var model = new EntityModelMetadata(typeof(MapRow), "map_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(MapRow.First), typeof(int), true, true,
                generatedGetter: static source => ((MapRow)source).First),
            EntityPropertyMetadata.CreateGenerated(nameof(MapRow.Second), typeof(string), true, true,
                generatedGetter: static source => ((MapRow)source).Second)
        });
        var models = new RegisteredEntityModelMetadataProvider(model);
        var contexts = new CountingContexts();
        var binder = new DefaultSqlParameterBinder(new DefaultEntityMappingResolver(models),
            options: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
            databaseContextResolver: contexts, models: models);
        var map = new SqlParameterMap<MapRow>()
            .Add("first", row => row.First)
            .Add("second", row => row.Second)
            .UseSource(new MapRow { First = 7, Second = "seven" });

        var bound = Assert.IsAssignableFrom<ISqlStaticParameterSource>(binder.Bind(map));
        Assert.Equal(1, contexts.Calls);

        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT @first || ':' || @second";
        bound.BindTo(command);

        Assert.Equal("SELECT @first || ':' || @second", command.CommandText);
        Assert.Equal(new[] { "first", "second" },
            command.Parameters.Cast<SqliteParameter>().Select(parameter => parameter.ParameterName.TrimStart('@')));
        Assert.Equal("7:seven", (string)await command.ExecuteScalarAsync());
    }

    private sealed class Filter
    {
        public int Value { get; set; }
        public int? Offset { get; set; }
    }

    private sealed class MapRow
    {
        public int First { get; set; }
        public string Second { get; set; }
    }

    private sealed class CountingContexts : ISqlDatabaseContextResolver
    {
        internal int Calls { get; private set; }
        public DatabaseContext Resolve(SqlOptions options = null) => new() { DbKey = "binding-" + ++Calls };
    }
}
