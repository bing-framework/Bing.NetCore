using System.Data.Common;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>直接应用生成查询模板后，通过真实 SQLite 验证形状、参数和执行资源。</summary>
public sealed class SqliteAutomaticTemplateIntegrationTest
{
    private static readonly Type[] RowTypes = { typeof(Row) };
    private static readonly string[] RowNames = { "row" };
    private static readonly string[] RowAliases = { "r" };
    private static readonly SqlGeneratedColumn IdColumn = new(0, nameof(Row.Id), null, typeof(int));
    private static readonly SqlGeneratedColumn NameColumn = new(0, nameof(Row.Name), null, typeof(string));
    private static readonly SqlGeneratedColumn[] AggregateColumns =
        { new(0, nameof(Row.Id), nameof(Summary.Total), typeof(long), SqlAggregateFunction.Count) };
    private static readonly SqlGeneratedQueryTemplate AggregateTemplate = new(new[]
    {
        new SqlGeneratedQueryTemplate.Operation(
            static (query, arguments) => SqlGeneratedQueryBridge.OptionalCompare(query, (bool)arguments[0], false,
                RowTypes, RowNames, RowAliases, IdColumn, null, Operator.Greater, arguments[1]),
            static arguments => arguments[1], static _ => Operator.Greater, valueArgument: 1, conditionArgument: 0),
        new SqlGeneratedQueryTemplate.Operation(
            static (query, _) => SqlGeneratedQueryBridge.Project(query, "Select", RowTypes, RowNames, RowAliases, AggregateColumns))
    }, replacesDefaultProjection: true);
    private static readonly SqlGeneratedQueryTemplate Template = new(new[]
    {
        new SqlGeneratedQueryTemplate.Operation(
            static (query, arguments) => SqlGeneratedQueryBridge.Compare(query, false, RowTypes, RowNames,
                RowAliases, IdColumn, null, Operator.Greater, arguments[0]),
            static arguments => arguments[0], static _ => Operator.Greater, valueArgument: 0),
        new SqlGeneratedQueryTemplate.Operation(
            static (query, arguments) => SqlGeneratedQueryBridge.OptionalCompare(query, (bool)arguments[0], false,
                RowTypes, RowNames, RowAliases, IdColumn, null, Operator.In, arguments[1]),
            static arguments => arguments[1], static _ => Operator.In, valueArgument: 1, conditionArgument: 0),
        new SqlGeneratedQueryTemplate.Operation(
            static (query, _) => SqlGeneratedQueryBridge.Project(query, "Select", RowTypes, RowNames,
                RowAliases, new[] { IdColumn, NameColumn })),
        new SqlGeneratedQueryTemplate.Operation(
            static (query, _) => SqlGeneratedQueryBridge.Project(query, "OrderBy", RowTypes, RowNames,
                RowAliases, new[] { IdColumn }))
    }, replacesDefaultProjection: true);

    /// <summary>同步和异步终结都应覆盖 mask、集合形状及同形状请求值。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedTemplate_WhenValuesAndMaskChange_ShouldRenderAndExecute(bool asynchronous)
    {
        using var host = new TestHost();

        var many = await Read(host, 1, true, new[] { 2, 3 }, asynchronous);
        var manySql = "Select `r`.`Id`,`r`.`Name` \r\nFrom `automatic_template_rows` As `r` \r\n" +
                      "Where `r`.`Id`>@_p_0 And `r`.`Id` In (@_p_1,@_p_2) \r\nOrder By `r`.`Id`";
        Assert.Equal(manySql, many.Sql);
        Assert.Equal(new[] { new Row { Id = 2, Name = "two" }, new Row { Id = 3, Name = "three" } }, many.Rows);

        var sameShape = await Read(host, 2, true, new[] { 3, 4 }, asynchronous);
        Assert.Equal(many.Sql, sameShape.Sql);
        Assert.Equal(new[] { new Row { Id = 3, Name = "three" }, new Row { Id = 4, Name = "four" } }, sameShape.Rows);

        var one = await Read(host, 0, true, new[] { 4 }, asynchronous);
        Assert.Equal("Select `r`.`Id`,`r`.`Name` \r\nFrom `automatic_template_rows` As `r` \r\n" +
                     "Where `r`.`Id`>@_p_0 And `r`.`Id` In (@_p_1) \r\nOrder By `r`.`Id`", one.Sql);
        Assert.Equal(new[] { new Row { Id = 4, Name = "four" } }, one.Rows);

        var empty = await Read(host, 0, true, Array.Empty<int>(), asynchronous);
        Assert.Equal("Select `r`.`Id`,`r`.`Name` \r\nFrom `automatic_template_rows` As `r` \r\n" +
                     "Where `r`.`Id`>@_p_0 And 1 = 0 \r\nOrder By `r`.`Id`", empty.Sql);
        Assert.Empty(empty.Rows);

        var disabled = await Read(host, 2, false, new[] { 1 }, asynchronous);
        Assert.Equal("Select `r`.`Id`,`r`.`Name` \r\nFrom `automatic_template_rows` As `r` \r\n" +
                     "Where `r`.`Id`>@_p_0 \r\nOrder By `r`.`Id`", disabled.Sql);
        Assert.Equal(new[] { new Row { Id = 3, Name = "three" }, new Row { Id = 4, Name = "four" } }, disabled.Rows);
    }

    /// <summary>生成式模板查询可以读取共享事务内的数据，且不接管事务提交。</summary>
    [Fact]
    public async Task GeneratedTemplate_WhenUsingSharedTransaction_ShouldReadUncommittedRowsAndRespectRollback()
    {
        using var host = new TestHost();
        using (var transaction = host.Services.GetRequiredService<ISqlTransactionScopeFactory>().Begin())
        {
            using (var executor = transaction.CreateExecutor())
            {
                executor.ExecuteSql("Insert Into automatic_template_rows (Id, Name) Values (5, 'five')");
                using var root = transaction.CreateQuery();
                var query = CreateQuery(root, 4, false, Array.Empty<int>());
                Assert.Equal("Select `r`.`Id`,`r`.`Name` \r\nFrom `automatic_template_rows` As `r` \r\n" +
                             "Where `r`.`Id`>@_p_0 \r\nOrder By `r`.`Id`", query.ToSql());
                Assert.Equal(new[] { new Row { Id = 5, Name = "five" } },
                    SqlGeneratedExecution.ToBoundList<Row>(query, ReadRow, BindParameters, null));
            }
            transaction.Rollback();
        }

        var rolledBack = await Read(host, 4, false, Array.Empty<int>(), asynchronous: false);
        Assert.Empty(rolledBack.Rows);
    }

    /// <summary>模板直接进入统一 IR，Count/Page 不重新拼接条件或污染源查询。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedTemplate_WhenPaged_ShouldUseFrozenIrAndKeepSource(bool asynchronous)
    {
        using var host = new TestHost();
        using var root = host.Services.GetRequiredService<ISqlQueryFactory>().Create();
        var query = CreateQuery(root, 0, true, new[] { 1, 2, 3, 4 });
        var original = query.ToSql();
        Assert.True(query.GeneratedCore.TryGeneratedTemplatePlan(typeof(Row), out var source, out _));
        Assert.Equal("Select Count(*) \r\nFrom `automatic_template_rows` As `r` \r\nWhere `r`.`Id`>@_p_0 And `r`.`Id` In (@_p_1,@_p_2,@_p_3,@_p_4)",
            SqlBuilderRuntimeBridge.CreateCountPlan(source).CommandText);
        Assert.Equal(original + " \r\nLimit @_p_6 OFFSET @_p_5",
            SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 1)).CommandText);
        var result = asynchronous
            ? await SqlGeneratedExecution.ToPageAsync<Row>(query, new Pager(2, 1), BindParameters, null, default)
            : SqlGeneratedExecution.ToPage<Row>(query, new Pager(2, 1), BindParameters, null);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(new[] { new Row { Id = 2, Name = "two" } }, result.Data);
        Assert.Equal(original, query.ToSql());
        Assert.Equal(original, query.Clone().ToSql());
    }

    /// <summary>严格生成模式下聚合模板复用 SQL，并通过 SQLite 返回每次请求的实际结果。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedAggregateTemplate_WhenWarm_ShouldValidateModelsAndExecuteCurrentValues(bool asynchronous)
    {
        using var host = new TestHost();
        async Task<(string Sql, long Total)> Execute(int minimum)
        {
            using var root = host.Services.GetRequiredService<ISqlQueryFactory>().Create();
            var query = root.From<Row>("r");
            SqlGeneratedQueryTemplate.Apply(query, AggregateTemplate, 0, new object[] { true, minimum });
            SqlGeneratedQueryTemplate.Apply(query, AggregateTemplate, 1, Array.Empty<object>());
            Assert.True(query.GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out var plan, out _));
            Func<IReadOnlyDictionary<string, int>, Func<DbDataReader, Summary>> reader = ordinals => data =>
                new Summary { Total = data.GetInt64(ordinals[nameof(Summary.Total)]) };
            var rows = asynchronous
                ? await SqlGeneratedExecution.ToBoundListAsync(query, reader, BindParameters, null, default)
                : SqlGeneratedExecution.ToBoundList(query, reader, BindParameters, null);
            return (plan.CommandText, Assert.Single(rows).Total);
        }

        var first = await Execute(1);
        var second = await Execute(2);
        Assert.Equal("Select Count(`r`.`Id`) As `Total` \r\nFrom `automatic_template_rows` As `r` \r\nWhere `r`.`Id`>@_p_0", first.Sql);
        Assert.Same(first.Sql, second.Sql);
        Assert.Equal(3L, first.Total);
        Assert.Equal(2L, second.Total);
    }

    /// <summary>同一已执行查询复用快照时，仍遵守能力提供器的当前配置。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedTemplate_WhenTableCapabilitiesChange_ShouldValidateBeforeExecutingAgain(bool asynchronous)
    {
        var provider = new MutableNameCapabilities { AllowSchema = true };
        using var host = new TestHost(new DefaultSqlTableReferenceValidator(provider));
        using var root = host.Services.GetRequiredService<ISqlQueryFactory>().Create();
        var query = CreateQuery(root, 2, false, Array.Empty<int>(), schema: "main");
        Assert.Equal("Select `r`.`Id`,`r`.`Name` \r\nFrom `automatic_template_rows` As `r` \r\n" +
                     "Where `r`.`Id`>@_p_0 \r\nOrder By `r`.`Id`", query.ToSql());

        async Task<List<Row>> Execute() => asynchronous
            ? await SqlGeneratedExecution.ToBoundListAsync<Row>(query, ReadRow, BindParameters, null, default)
            : SqlGeneratedExecution.ToBoundList<Row>(query, ReadRow, BindParameters, null);

        var expected = new[] { new Row { Id = 3, Name = "three" }, new Row { Id = 4, Name = "four" } };
        Assert.Equal(expected, await Execute());
        provider.AllowSchema = false;
        await Assert.ThrowsAsync<NotSupportedException>(Execute);
        provider.AllowSchema = true;
        Assert.Equal(expected, await Execute());
    }

    private static async Task<(string Sql, List<Row> Rows)> Read(TestHost host, int minimumId, bool filterIds,
        int[] ids, bool asynchronous)
    {
        using var root = host.Services.GetRequiredService<ISqlQueryFactory>().Create();
        var query = CreateQuery(root, minimumId, filterIds, ids);
        var sql = query.ToSql();
        var rows = asynchronous
            ? await SqlGeneratedExecution.ToBoundListAsync<Row>(query, ReadRow, BindParameters, null, default)
            : SqlGeneratedExecution.ToBoundList<Row>(query, ReadRow, BindParameters, null);
        return (sql, rows);
    }

    private static SqlLambdaQuery CreateQuery(ISqlQuery root, int minimumId, bool filterIds, int[] ids, string schema = null)
    {
        var query = root.From<Row>("r", schema);
        SqlGeneratedQueryTemplate.Apply(query, Template, 0, new object[] { minimumId });
        SqlGeneratedQueryTemplate.Apply(query, Template, 1, new object[] { filterIds, ids });
        SqlGeneratedQueryTemplate.Apply(query, Template, 2, Array.Empty<object>());
        SqlGeneratedQueryTemplate.Apply(query, Template, 3, Array.Empty<object>());
        return query;
    }

    private static Func<DbDataReader, Row> ReadRow(IReadOnlyDictionary<string, int> ordinals) => reader =>
        new Row
        {
            Id = reader.GetInt32(ordinals[nameof(Row.Id)]),
            Name = reader.GetString(ordinals[nameof(Row.Name)])
        };

    private static void BindParameters(DbCommand command, IEnumerable<SqlParam> parameters) =>
        SqlGeneratedExecution.BindParameters(command, parameters);

    public sealed record Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public sealed class Summary { public long Total { get; set; } }

    private sealed class MutableNameCapabilities : ISqlObjectNameCapabilityProvider
    {
        internal bool AllowSchema { get; set; }

        public SqlObjectNameCapabilities GetCapabilities(DatabaseType? databaseType) =>
            new() { SupportsSchema = AllowSchema, MaximumNameParts = AllowSchema ? 2 : 1 };
    }

    private sealed class TestHost : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(),
            "bing-sqlite-automatic-template-" + Guid.NewGuid().ToString("N") + ".db");

        internal ServiceProvider Services { get; }

        internal TestHost(ISqlTableReferenceValidator tableReferenceValidator = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSqlCore();
            if (tableReferenceValidator != null)
                services.AddSingleton(tableReferenceValidator);
            services.ConfigureSqlMetadata(options => options.ExecutionMode = SqlExecutionMode.GeneratedOnly);
            services.AddSqlGeneratedModels(new[]
            {
                new EntityModelMetadata(typeof(Row), "automatic_template_rows", null, new[]
                {
                    EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true,
                        generatedGetter: static row => ((Row)row).Id,
                        generatedSetter: static (row, value) => ((Row)row).Id = (int)value),
                    EntityPropertyMetadata.CreateGenerated(nameof(Row.Name), typeof(string), true, true,
                        generatedGetter: static row => ((Row)row).Name,
                        generatedSetter: static (row, value) => ((Row)row).Name = (string)value)
                }, static () => new Row()),
                new EntityModelMetadata(typeof(Summary), null, null, new[]
                {
                    EntityPropertyMetadata.CreateGenerated(nameof(Summary.Total), typeof(long), true, true,
                        generatedGetter: static summary => ((Summary)summary).Total,
                        generatedSetter: static (summary, value) => ((Summary)summary).Total = (long)value)
                }, static () => new Summary())
            });
            var connectionString = new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString();
            services.AddSqlDataSource("default", DatabaseType.Sqlite, connectionString);
            services.AddSqliteProvider();
            Services = services.BuildServiceProvider();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "Create Table automatic_template_rows (Id Integer Primary Key, Name Text Not Null);" +
                                  "Insert Into automatic_template_rows Values (1, 'one'), (2, 'two'), (3, 'three'), (4, 'four');";
            command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            Services.Dispose();
            SqliteConnection.ClearAllPools();
            if (File.Exists(_path)) File.Delete(_path);
        }
    }
}
