using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Tests.Mutations;

/// <summary>生成式 IN 删除的参数快照、生成准入和 Builder 回退边界。</summary>
public sealed class GeneratedBatchDeleteCommandTest
{
    private const string DeleteSql = "Delete From `batch_rows` Where `Id` In (@_p_0,@_p_1,@_p_2)";

    /// <summary>生成 SQL 直接连接静态参数绑定，不重复格式化表名或条件。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeleteCombined_WithGeneratedSql_ShouldSkipFormattingAndFreezeParameters(bool explicitIn)
    {
        var dialect = new TrackingDialect();
        var fixture = new Fixture(dialect: dialect);
        var rows = Rows(3);

        var command = fixture.Builder.DeleteCombined(rows, strategy: explicitIn
            ? SqlBatchDeleteStrategy.InPredicate : SqlBatchDeleteStrategy.Auto);

        Assert.Equal(DeleteSql, command.Sql);
        Assert.Equal(new object[] { 1, 2, 3 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(3, dialect.ConvertCount);
        Assert.Equal(0, dialect.FormatCount);
        Assert.Equal(3, fixture.GetterCalls);
        Assert.Equal(1, fixture.SqlCalls);
        Assert.True(command.IsGeneratedExecution);
        Assert.False(command.HasReturning);
        Assert.False(command.ValidateAffectedRows);
        Assert.Equal(SqlOperationKind.Delete, command.OperationKind);
        Assert.Equal(new[] { "@_p_0", "@_p_1", "@_p_2" }, command.Parameters.Select(parameter => parameter.Name));
        Assert.All(command.Parameters, parameter =>
        {
            Assert.Equal(DbType.Int32, parameter.DbType);
            Assert.Equal("Id", parameter.PropertyName);
            Assert.Equal(SqlParameterSource.SqlBuilder, parameter.Source);
            Assert.True(parameter.IsProviderValue);
        });
        command.Parameters.First().Value = -1;
        rows[0].Id = -2;
        Assert.Equal(new object[] { 1, 2, 3 }, command.CreateParameters().Select(parameter => parameter.Value));
    }

    /// <summary>超限批次必须在生成 SQL 或读取实体前拒绝。</summary>
    [Fact]
    public void DeleteCombined_WhenParameterLimitIsExceeded_ShouldRejectBeforeSqlAndGetters()
    {
        var fixture = new Fixture(maximum: 2);

        var exception = Assert.Throws<InvalidOperationException>(() => fixture.Builder.DeleteCombined(Rows(3)));

        Assert.Equal("Provider bing.sqlite 的参数数量不能超过 2。", exception.Message);
        Assert.Equal(0, fixture.SqlCalls);
        Assert.Equal(0, fixture.GetterCalls);
    }

    /// <summary>上限包含临界值；重复键不应被去重或改变参数次序。</summary>
    [Fact]
    public void DeleteCombined_WhenCountEqualsLimit_ShouldPreserveRepeatedKeys()
    {
        var fixture = new Fixture(maximum: 3);
        var command = fixture.Builder.DeleteCombined(new[] { new Row { Id = 8 }, new Row { Id = 8 }, new Row { Id = 9 } });

        Assert.Equal(DeleteSql, command.Sql);
        Assert.Equal(new object[] { 8, 8, 9 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(1, fixture.SqlCalls);
    }

    /// <summary>键值不可为空，即使完整 SQL 已由生成器提供也不能放过。</summary>
    [Fact]
    public void DeleteCombined_WhenKeyIsNull_ShouldReject()
    {
        var fixture = new Fixture();

        var exception = Assert.Throws<InvalidOperationException>(() => fixture.Builder.DeleteCombined(
            new[] { new Row { Id = 1 }, new Row(), new Row { Id = 3 } }));

        Assert.Equal("实体 Row 的条件列 Id 不能为空。", exception.Message);
    }

    /// <summary>映射覆盖必须使用最终表列名，不可复用旧的 IN 文本。</summary>
    [Theory]
    [InlineData("mapped_rows", null, "Delete From `mapped_rows` Where `Id` In (@_p_0,@_p_1,@_p_2)")]
    [InlineData(null, "row_key", "Delete From `batch_rows` Where `row_key` In (@_p_0,@_p_1,@_p_2)")]
    public void DeleteCombined_WhenFinalMappingChanges_ShouldRenderMappedSql(string table, string column, string expected)
    {
        var fixture = new Fixture(table: table, keyColumn: column);

        var command = fixture.Builder.DeleteCombined(Rows(3));

        Assert.Equal(expected, command.Sql);
        Assert.Equal(0, fixture.SqlCalls);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>未提供配对生成形状时保留 Builder；显式配对策略的单主键也不能替换为 IN。</summary>
    [Theory]
    [InlineData(false, false, SqlBatchDeleteStrategy.CompositePredicate,
        "Delete From `batch_rows` Where ((`Id` = @_p_0) Or (`Id` = @_p_1))")]
    [InlineData(true, false, SqlBatchDeleteStrategy.Auto,
        "Delete From `batch_rows` Where ((`Id` = @_p_0 And `Version` = @_p_1) Or (`Id` = @_p_2 And `Version` = @_p_3))")]
    [InlineData(false, true, SqlBatchDeleteStrategy.Auto,
        "Delete From `batch_rows` Where ((`Id` = @_p_0 And `Version` = @_p_1) Or (`Id` = @_p_2 And `Version` = @_p_3))")]
    public void DeleteCombined_WithPairedConditions_ShouldKeepBuilder(bool composite, bool concurrency,
        SqlBatchDeleteStrategy strategy, string expected)
    {
        var fixture = new Fixture(composite: composite, concurrency: concurrency);

        var command = fixture.Builder.DeleteCombined(Rows(2), strategy: strategy);

        Assert.Equal(expected, command.Sql);
        Assert.Equal(composite || concurrency ? 1 : 0, fixture.SqlCalls);
        Assert.Equal(concurrency, command.ValidateAffectedRows);
        Assert.Equal(composite || concurrency ? new object[] { 1, 11, 2, 12 } : new object[] { 1, 2 },
            command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>删除数据边界仍应由 Builder 追加；它与 Purge 的操作语义不同。</summary>
    [Fact]
    public void DeleteCombined_WithDataBoundary_ShouldApplyDeleteBoundary()
    {
        var fixture = new Fixture(filters: new ISqlFilter[] { new NameBoundaryFilter() });

        var command = fixture.Builder.DeleteCombined(Rows(3));

        Assert.Equal("Delete From `batch_rows` Where `Id` In (@_p_0,@_p_1,@_p_2) And `Name`=@_p_3", command.Sql);
        Assert.Equal(new object[] { 1, 2, 3, "allowed" }, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(0, fixture.SqlCalls);
    }

    /// <summary>一个实体仍保留既有 IN SQL 语义，不误用默认的等值删除常量。</summary>
    [Fact]
    public void DeleteCombined_WithSingleEntity_ShouldKeepInPredicate()
    {
        var fixture = new Fixture();

        var command = fixture.Builder.DeleteCombined(Rows(1));

        Assert.Equal("Delete From `batch_rows` Where `Id` In (@_p_0)", command.Sql);
        Assert.Equal(0, fixture.SqlCalls);
    }

    /// <summary>同一个 Builder 服务并发创建的命令各自持有冻结参数。</summary>
    [Fact]
    public void DeleteCombined_WhenConcurrent_ShouldIsolateBatchParameters()
    {
        var fixture = new Fixture();
        Parallel.For(0, 32, index =>
        {
            var rows = new[] { new Row { Id = index * 3 }, new Row { Id = index * 3 + 1 }, new Row { Id = index * 3 + 2 } };
            var command = fixture.Builder.DeleteCombined(rows);

            Assert.Equal(DeleteSql, command.Sql);
            Assert.Equal(rows.Select(row => (object)row.Id), command.Parameters.Select(parameter => parameter.Value));
        });
    }

    private static Row[] Rows(int count) => Enumerable.Range(1, count)
        .Select(index => new Row { Id = index, Version = index + 10 }).ToArray();

    private sealed class Row
    {
        public int? Id { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
    }

    private sealed class Fixture
    {
        private int _getterCalls;
        private int _sqlCalls;
        internal int GetterCalls => _getterCalls;
        internal int SqlCalls => _sqlCalls;
        internal DefaultSqlEntityMutationCommandBuilder Builder { get; }

        internal Fixture(IDialect dialect = null, int? maximum = null, string table = null, string keyColumn = null,
            bool composite = false, bool concurrency = false, IEnumerable<ISqlFilter> filters = null)
        {
            object Read(object source, string property)
            {
                Interlocked.Increment(ref _getterCalls);
                var row = (Row)source;
                return property switch { "Id" => row.Id, "Name" => row.Name, _ => row.Version };
            }
            var model = new EntityModelMetadata(typeof(Row), "batch_rows", null, new[]
            {
                EntityPropertyMetadata.CreateGenerated("Id", typeof(int?), true, true, isKey: true,
                    generatedGetter: source => Read(source, "Id")),
                EntityPropertyMetadata.CreateGenerated("Name", typeof(string), true, true,
                    generatedGetter: source => Read(source, "Name")),
                EntityPropertyMetadata.CreateGenerated("Version", typeof(int), true, true, isKey: composite,
                    isConcurrencyToken: concurrency, generatedGetter: source => Read(source, "Version"))
            }, () => new Row())
            {
                GeneratedMutationShapeSql = (_, operation, property, rows) =>
                {
                    Interlocked.Increment(ref _sqlCalls);
                    return operation == SqlOperationKind.Delete && property == null && rows == 3 ? DeleteSql : null;
                }
            };
            var options = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
            var mapping = new EntityMappingOptions { EntityType = typeof(Row), TableName = table ?? "batch_rows" };
            mapping.Columns["Id"] = new ColumnMappingOptions { PropertyName = "Id", ColumnName = keyColumn, DbType = DbType.Int32 };
            options.EntityMappings.Add(mapping);
            Builder = new DefaultSqlEntityMutationCommandBuilder(new TestProvider(dialect, maximum), new SqlBuilderServices(
                metadataOptions: options, entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model), filters: filters));
        }
    }

    private sealed class NameBoundaryFilter : ISqlFilter, ISqlDataBoundaryContributor
    {
        public void Filter(SqlFilterContext context) { }
        public bool ShouldApply(SqlDataBoundaryContext context) => context.EntityType == typeof(Row) &&
            context.Operation == SqlDataBoundaryOperation.Delete;
        public void Apply(SqlDataBoundaryContext context) => context.AddEquals(nameof(Row.Name), "allowed");
    }

    private sealed class TrackingDialect : DialectBase
    {
        internal int ConvertCount { get; private set; }
        internal int FormatCount { get; private set; }
        public override char OpeningIdentifier => '`';
        public override char ClosingIdentifier => '`';
        public override string SafeName(string name) { FormatCount++; return base.SafeName(name); }
        public override object GetParamValue(object value) { ConvertCount++; return base.GetParamValue(value); }
    }

    private sealed class TestProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        private static readonly SqliteSqlProvider Inner = SqliteSqlProvider.Instance;
        private readonly IDialect _dialect;
        private readonly SqlProviderProfile _profile;

        internal TestProvider(IDialect dialect, int? maximum)
        {
            _dialect = dialect ?? Inner.Dialect;
            _profile = WithGeneratedSqlDialect(Inner.Profile, _dialect, maximum);
        }

        public string Key => Inner.Key;
        public DatabaseType DatabaseType => Inner.DatabaseType;
        public IDialect Dialect => _dialect;
        public ISqlClauseFactory ClauseFactory => Inner.ClauseFactory;
        public ISqlTableReferenceParser TableReferenceParser => Inner.TableReferenceParser;
        public ISqlPaginationRenderer PaginationRenderer => Inner.PaginationRenderer;
        public IParameterManagerFactory ParameterManagerFactory => Inner.ParameterManagerFactory;
        public IParamLiteralsResolver ParamLiteralsResolver => Inner.ParamLiteralsResolver;
        public SqlProviderProfile Profile => _profile;

        private static SqlProviderProfile WithGeneratedSqlDialect(SqlProviderProfile source, IDialect dialect,
            int? maximum) => new()
        {
            Query = source.Query,
            Mutation = new SqlProviderMutationCapabilities
            {
                GeneratedSqlDialect = dialect,
                SupportsMultiRowValues = source.Mutation.SupportsMultiRowValues,
                MultiRowValuesFailureReason = source.Mutation.MultiRowValuesFailureReason,
                SupportsUpdateFrom = source.Mutation.SupportsUpdateFrom,
                UpdateFromFailureReason = source.Mutation.UpdateFromFailureReason,
                SupportsDeleteUsing = source.Mutation.SupportsDeleteUsing,
                DeleteUsingFailureReason = source.Mutation.DeleteUsingFailureReason,
                SupportsReturning = source.Mutation.SupportsReturning,
                ReturningFailureReason = source.Mutation.ReturningFailureReason
            },
            Execution = source.Execution,
            Transaction = source.Transaction,
            Procedure = source.Procedure,
            Limits = maximum is { } count
                ? new SqlProviderLimits { MaxParameterCount = count }
                : source.Limits
        };
    }
}
