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

/// <summary>生成式配对删除的条件顺序、并发语义及动态回退。</summary>
public sealed class GeneratedPairedDeleteCommandTest
{
    private const string PairedSql = "Delete From `paired_rows` Where ((`Id` = @_p_0 And `TenantId` = @_p_1 And `Version` = @_p_2) Or (`Id` = @_p_3 And `TenantId` = @_p_4 And `Version` = @_p_5))";

    /// <summary>多列条件以实体为单位配对，完整生成 SQL 直接连接冻结参数和静态绑定。</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void DeleteCombined_WithGeneratedPairs_ShouldSkipFormattingAndPreserveConditionOrder(bool composite, bool concurrency)
    {
        var fixture = new Fixture(composite: composite, concurrency: concurrency);
        var rows = Rows();

        var command = fixture.Builder.DeleteCombined(rows);

        Assert.Equal(ExpectedSql(composite, concurrency), command.Sql);
        Assert.Equal(composite && concurrency ? new object[] { 1, 10, 7, 2, 20, 8 } :
            composite ? new object[] { 1, 10, 2, 20 } : new object[] { 1, 7, 2, 8 },
            command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(command.Parameters.Count, fixture.GetterCalls);
        Assert.Equal(command.Parameters.Count, fixture.Dialect.ConvertCount);
        Assert.Equal(0, fixture.Dialect.FormatCount);
        Assert.Equal(1, fixture.SqlCalls);
        Assert.Equal(concurrency, command.ValidateAffectedRows);
        Assert.True(command.IsGeneratedExecution);
        Assert.False(command.HasReturning);
        Assert.Equal(SqlOperationKind.Delete, command.OperationKind);
        Assert.All(command.Parameters, parameter =>
        {
            Assert.Equal(DbType.Int32, parameter.DbType);
            Assert.Equal(SqlParameterSource.SqlBuilder, parameter.Source);
            Assert.True(parameter.IsProviderValue);
        });
        Assert.Equal(Enumerable.Range(0, command.Parameters.Count).Select(index => "@_p_" + index),
            command.Parameters.Select(parameter => parameter.Name));
        var original = command.Parameters.Select(parameter => parameter.Value).ToArray();
        command.Parameters.First().Value = -1;
        rows[0].Id = -2;
        Assert.Equal(original, command.CreateParameters().Select(parameter => parameter.Value));
    }

    /// <summary>原始令牌覆盖当前值，忽略冲突只改变校验标记，不改变条件。</summary>
    [Theory]
    [InlineData(SqlConcurrencyConflictBehavior.Throw)]
    [InlineData(SqlConcurrencyConflictBehavior.ReturnAffectedRows)]
    public void DeleteCombined_WithOriginalVersion_ShouldUseOriginalForEachEntity(SqlConcurrencyConflictBehavior behavior)
    {
        var fixture = new Fixture();
        var options = new SqlDeleteOptions<Row> { ConcurrencyConflictBehavior = behavior }.Original(row => row.Version, 42);

        var command = fixture.Builder.DeleteCombined(Rows(), options, SqlBatchDeleteStrategy.CompositePredicate);

        Assert.Equal(PairedSql, command.Sql);
        Assert.Equal(new object[] { 1, 10, 42, 2, 20, 42 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(4, fixture.GetterCalls);
        Assert.Equal(6, fixture.Dialect.ConvertCount);
        Assert.Equal(0, fixture.Dialect.FormatCount);
        Assert.Equal(behavior == SqlConcurrencyConflictBehavior.Throw, command.ValidateAffectedRows);
    }

    /// <summary>参数上限按每行所有键和令牌计算，并在 SQL 展开及 Getter 前拒绝。</summary>
    [Fact]
    public void DeleteCombined_WhenPairedParametersExceedLimit_ShouldRejectBeforeGenerationAndGetters()
    {
        var fixture = new Fixture(maximum: 5);

        var exception = Assert.Throws<InvalidOperationException>(() => fixture.Builder.DeleteCombined(Rows()));

        Assert.Equal("Provider bing.sqlite 的参数数量不能超过 5。", exception.Message);
        Assert.Equal(0, fixture.SqlCalls);
        Assert.Equal(0, fixture.GetterCalls);
    }

    /// <summary>临界上限和重复实体不应改变参数数量或按行配对。</summary>
    [Fact]
    public void DeleteCombined_WhenParametersEqualLimit_ShouldPreserveDuplicatePairs()
    {
        var fixture = new Fixture(maximum: 6);
        var row = Rows()[0];

        var command = fixture.Builder.DeleteCombined(new[] { row, row });

        Assert.Equal(PairedSql, command.Sql);
        Assert.Equal(new object[] { 1, 10, 7, 1, 10, 7 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(0, fixture.Dialect.FormatCount);
    }

    /// <summary>生成 SQL 不能绕过空主键、空并发令牌的既有拒绝语义。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DeleteCombined_WhenConditionIsNull_ShouldReject(bool key)
    {
        var fixture = new Fixture();
        var rows = Rows();
        if (key) rows[1].TenantId = null;
        else rows[1].Version = null;

        var exception = Assert.Throws<InvalidOperationException>(() => fixture.Builder.DeleteCombined(rows));

        Assert.Equal($"实体 Row 的条件列 {(key ? "TenantId" : "Version")} 不能为空。", exception.Message);
    }

    /// <summary>缺少相应生成形状或最终表名变化时仍有完整的动态删除路径。</summary>
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "mapped_pairs")]
    public void DeleteCombined_WhenShapeCannotBeUsed_ShouldRenderFinalMapping(bool generated, string table)
    {
        var fixture = new Fixture(generated: generated, table: table);

        var command = fixture.Builder.DeleteCombined(Rows());

        Assert.Equal(table == null ? PairedSql : PairedSql.Replace("`paired_rows`", "`mapped_pairs`"), command.Sql);
        Assert.Equal(table == null ? 1 : 0, fixture.SqlCalls);
        Assert.True(fixture.Dialect.FormatCount > 0);
        Assert.True(command.ValidateAffectedRows);
    }

    /// <summary>用户的数据边界必须覆盖整个 OR 组，不能被生成式快路径跳过。</summary>
    [Fact]
    public void DeleteCombined_WithDataBoundary_ShouldKeepWholeGroupBoundary()
    {
        var fixture = new Fixture(filters: new ISqlFilter[] { new TenantBoundary() });

        var command = fixture.Builder.DeleteCombined(Rows());

        Assert.Equal(PairedSql + " And `TenantId`=@_p_6", command.Sql);
        Assert.Equal(new object[] { 1, 10, 7, 2, 20, 8, 10 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(0, fixture.SqlCalls);
    }

    /// <summary>不适用的显式 IN 策略应在生成或读取实体前拒绝。</summary>
    [Fact]
    public void DeleteCombined_WithExplicitIn_ShouldRejectPairedModel()
    {
        var fixture = new Fixture();

        Assert.Throws<NotSupportedException>(() => fixture.Builder.DeleteCombined(Rows(), strategy: SqlBatchDeleteStrategy.InPredicate));
        Assert.Equal(0, fixture.SqlCalls);
        Assert.Equal(0, fixture.GetterCalls);
    }

    /// <summary>共享构建服务可并行创建命令，每个批次保持独立的参数快照。</summary>
    [Fact]
    public void DeleteCombined_WhenConcurrent_ShouldIsolatePairedBatches()
    {
        var fixture = new Fixture();
        Parallel.For(0, 32, index =>
        {
            var rows = Rows();
            rows[0].TenantId = index;
            var command = fixture.Builder.DeleteCombined(rows);

            Assert.Equal(PairedSql, command.Sql);
            Assert.Equal(new object[] { 1, index, 7, 2, 20, 8 }, command.Parameters.Select(parameter => parameter.Value));
        });
    }

    private static Row[] Rows() => new[]
    {
        new Row { Id = 1, TenantId = 10, Version = 7 }, new Row { Id = 2, TenantId = 20, Version = 8 }
    };

    private static string ExpectedSql(bool composite, bool concurrency)
    {
        if (composite && concurrency) return PairedSql;
        var column = composite ? "TenantId" : "Version";
        return $"Delete From `paired_rows` Where ((`Id` = @_p_0 And `{column}` = @_p_1) Or (`Id` = @_p_2 And `{column}` = @_p_3))";
    }

    private sealed class Row
    {
        public int? Id { get; set; }
        public int? TenantId { get; set; }
        public int? Version { get; set; }
    }

    private sealed class Fixture
    {
        private int _getterCalls;
        private int _sqlCalls;
        internal int GetterCalls => _getterCalls;
        internal int SqlCalls => _sqlCalls;
        internal TrackingDialect Dialect { get; } = new();
        internal DefaultSqlEntityMutationCommandBuilder Builder { get; }

        internal Fixture(bool composite = true, bool concurrency = true, int? maximum = null,
            bool generated = true, string table = null, IEnumerable<ISqlFilter> filters = null)
        {
            object Read(object instance, string name)
            {
                Interlocked.Increment(ref _getterCalls);
                var row = (Row)instance;
                return name switch { "Id" => row.Id, "TenantId" => row.TenantId, _ => row.Version };
            }
            var model = new EntityModelMetadata(typeof(Row), "paired_rows", null, new[]
            {
                EntityPropertyMetadata.CreateGenerated("Id", typeof(int?), true, true, isKey: true, generatedGetter: row => Read(row, "Id")),
                EntityPropertyMetadata.CreateGenerated("TenantId", typeof(int?), true, true, isKey: composite, generatedGetter: row => Read(row, "TenantId")),
                EntityPropertyMetadata.CreateGenerated("Version", typeof(int?), true, true, isConcurrencyToken: concurrency, generatedGetter: row => Read(row, "Version"))
            }, () => new Row())
            {
                GeneratedMutationShapeSql = (_, operation, property, rows) =>
                {
                    Interlocked.Increment(ref _sqlCalls);
                    return generated && operation == SqlOperationKind.Delete && property == null && rows == 2
                        ? ExpectedSql(composite, concurrency) : null;
                }
            };
            var options = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
            var mapping = new EntityMappingOptions { EntityType = typeof(Row), TableName = table ?? "paired_rows" };
            foreach (var name in new[] { "Id", "TenantId", "Version" })
                mapping.Columns[name] = new ColumnMappingOptions { PropertyName = name, DbType = DbType.Int32 };
            options.EntityMappings.Add(mapping);
            Builder = new DefaultSqlEntityMutationCommandBuilder(new TestProvider(Dialect, maximum), new SqlBuilderServices(
                metadataOptions: options, entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model), filters: filters));
        }
    }

    private sealed class TenantBoundary : ISqlFilter, ISqlDataBoundaryContributor
    {
        public void Filter(SqlFilterContext context) { }
        public bool ShouldApply(SqlDataBoundaryContext context) => context.EntityType == typeof(Row) && context.Operation == SqlDataBoundaryOperation.Delete;
        public void Apply(SqlDataBoundaryContext context) => context.AddEquals(nameof(Row.TenantId), 10);
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
