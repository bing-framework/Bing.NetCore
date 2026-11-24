using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Tests.Mutations;

/// <summary>生成式批量插入直接构造命令时的参数、映射和所有权边界。</summary>
public sealed class GeneratedBatchInsertCommandTest
{
    private const string Head = "Insert Into `batch_rows` (`Id`, `Name`, `Version`) Values ";
    private const string FirstRow = "(@_p_0, @_p_1, @_p_2)";
    private const string SecondRow = ", (@_p_3, @_p_4, @_p_5)";
    private const string ThirdRow = ", (@_p_6, @_p_7, @_p_8)";
    private const string FourthRow = ", (@_p_9, @_p_10, @_p_11)";
    private const string NameSql = "Insert Into `batch_rows` (`Name`) Values (@_p_0), (@_p_1), (@_p_2)";

    /// <summary>每个参数只做一次方言转换，并保留列元数据和独立快照。</summary>
    [Theory]
    [InlineData(1, Head + FirstRow)]
    [InlineData(2, Head + FirstRow + SecondRow)]
    [InlineData(3, Head + FirstRow + SecondRow + ThirdRow)]
    [InlineData(4, Head + FirstRow + SecondRow + ThirdRow + FourthRow)]
    public void InsertCombined_WithGeneratedSql_ShouldConvertOnceAndFreezeParameters(int count, string expected)
    {
        var dialect = new TrackingDialect();
        var fixture = new Fixture(new TestProvider(dialect));
        var rows = Rows(count);
        var values = rows.SelectMany(row => new object[] { row.Id, "converted:" + row.Name, row.Version }).ToArray();

        var command = fixture.Builder.InsertCombined(rows);

        Assert.Equal(expected, command.Sql);
        Assert.Equal(values, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(count * 3, dialect.ConvertCount);
        Assert.Equal(0, dialect.FormatCount);
        Assert.Equal(count * 3, fixture.GetterCalls);
        Assert.Equal(1, fixture.SqlCalls);
        Assert.True(command.IsGeneratedExecution);
        Assert.False(command.HasReturning);
        Assert.False(command.ValidateAffectedRows);
        Assert.Equal(SqlOperationKind.InsertValues, command.OperationKind);
        Assert.Equal(Enumerable.Range(0, count * 3).Select(index => "@_p_" + index),
            command.Parameters.Select(parameter => parameter.Name));
        Assert.All(command.Parameters.Where(parameter => parameter.PropertyName == "Name"), parameter =>
        {
            Assert.Equal(DbType.AnsiString, parameter.DbType);
            Assert.Equal(64, parameter.Size);
            Assert.True(parameter.IsProviderValue);
            Assert.Equal(SqlParameterSource.SqlBuilder, parameter.Source);
        });
        var exposed = command.Parameters.First();
        exposed.Value = "changed";
        rows[0].Name = "changed";
        Assert.Equal(values, command.CreateParameters().Select(parameter => parameter.Value));
    }

    /// <summary>列筛选后的参数顺序按行展开，不能继续使用完整列的序号。</summary>
    [Fact]
    public void InsertCombined_WithSingleColumn_ShouldUseFilteredGeneratedShape()
    {
        var dialect = new TrackingDialect();
        var fixture = new Fixture(new TestProvider(dialect));

        var command = fixture.Builder.InsertCombined(Rows(3),
            new SqlInsertOptions { IncludeProperties = new[] { "Name" } });

        Assert.Equal(NameSql, command.Sql);
        Assert.Equal(new object[] { "converted:row-1", "converted:row-2", "converted:row-3" },
            command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(3, dialect.ConvertCount);
        Assert.Equal(3, fixture.GetterCalls);
        Assert.Equal(1, fixture.SqlCalls);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>参数超限必须先于读取实体和构造批量 SQL，避免无效批次的大量分配。</summary>
    [Fact]
    public void InsertCombined_WhenParameterLimitIsExceeded_ShouldRejectBeforeSqlAndGetters()
    {
        var fixture = new Fixture(new TestProvider(profile: new SqlProviderProfile
        {
            Mutation = new SqlProviderMutationCapabilities
            {
                GeneratedSqlDialect = SqliteSqlProvider.Instance.Dialect,
                SupportsMultiRowValues = true
            },
            Limits = new SqlProviderLimits { MaxParameterCount = 8 }
        }));

        var exception = Assert.Throws<InvalidOperationException>(() => fixture.Builder.InsertCombined(Rows(3)));

        Assert.Equal("Provider bing.sqlite 的参数数量不能超过 8。", exception.Message);
        Assert.Equal(0, fixture.SqlCalls);
        Assert.Equal(0, fixture.GetterCalls);
    }

    /// <summary>上限包含临界值；Provider 限制针对实际筛选列计算。</summary>
    [Fact]
    public void InsertCombined_WhenFilteredCountEqualsLimit_ShouldSucceed()
    {
        var fixture = new Fixture(new TestProvider(profile: new SqlProviderProfile
        {
            Mutation = new SqlProviderMutationCapabilities
            {
                GeneratedSqlDialect = SqliteSqlProvider.Instance.Dialect,
                SupportsMultiRowValues = true
            },
            Limits = new SqlProviderLimits { MaxParameterCount = 3 }
        }));

        var command = fixture.Builder.InsertCombined(Rows(3),
            new SqlInsertOptions { IncludeProperties = new[] { "Name" } });

        Assert.Equal(NameSql, command.Sql);
        Assert.Equal(3, command.Parameters.Count);
    }

    /// <summary>生成 SQL 不能绕过 Provider 的多行能力声明。</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("incomplete")]
    [InlineData("unsupported")]
    public void InsertCombined_WhenMultiRowProfileIsUnavailable_ShouldRejectBeforeGeneration(string state)
    {
        var provider = new TestProvider(profile: state switch
        {
            "incomplete" => new SqlProviderProfile { Mutation = null },
            "unsupported" => new SqlProviderProfile(),
            _ => null
        }, useDefaultProfile: false);
        var fixture = new Fixture(provider);

        Assert.Throws<NotSupportedException>(() => fixture.Builder.InsertCombined(Rows(3)));
        Assert.Equal(0, fixture.SqlCalls);
        Assert.Equal(0, fixture.GetterCalls);
    }

    /// <summary>最终表名或列名变化时回退动态渲染，保留静态绑定。</summary>
    [Theory]
    [InlineData("mapped_rows", null,
        "Insert Into `mapped_rows` (`Id`, `Name`, `Version`) Values (@_p_0, @_p_1, @_p_2), (@_p_3, @_p_4, @_p_5), (@_p_6, @_p_7, @_p_8)")]
    [InlineData(null, "display_name",
        "Insert Into `batch_rows` (`Id`, `display_name`, `Version`) Values (@_p_0, @_p_1, @_p_2), (@_p_3, @_p_4, @_p_5), (@_p_6, @_p_7, @_p_8)")]
    public void InsertCombined_WhenFinalMappingChanges_ShouldRenderMappedSql(string table, string column, string expected)
    {
        var fixture = new Fixture(table: table, nameColumn: column);

        var command = fixture.Builder.InsertCombined(Rows(3));

        Assert.Equal(expected, command.Sql);
        Assert.Equal(0, fixture.SqlCalls);
        Assert.Equal(new object[] { 1, "row-1", 11, 2, "row-2", 12, 3, "row-3", 13 },
            command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>共享计划只缓存形状，不得在并发批次间复用请求参数。</summary>
    [Fact]
    public void InsertCombined_WhenConcurrent_ShouldIsolateBatchParameters()
    {
        var fixture = new Fixture();
        Parallel.For(0, 32, index =>
        {
            var rows = new[]
            {
                new Row { Id = index * 2, Name = "first-" + index, Version = index },
                new Row { Id = index * 2 + 1, Name = "second-" + index, Version = index + 1 }
            };
            var command = fixture.Builder.InsertCombined(rows);

            Assert.Equal(Head + FirstRow + SecondRow, command.Sql);
            Assert.Equal(new object[] { index * 2, "first-" + index, index, index * 2 + 1, "second-" + index, index + 1 },
                command.Parameters.Select(parameter => parameter.Value));
        });
    }

    private static Row[] Rows(int count) => Enumerable.Range(1, count)
        .Select(index => new Row { Id = index, Name = "row-" + index, Version = index + 10 }).ToArray();

    private sealed class Row
    {
        public int Id { get; set; }
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

        internal Fixture(ISqlProvider provider = null, string table = null, string nameColumn = null)
        {
            object Read(object source, string property)
            {
                Interlocked.Increment(ref _getterCalls);
                var row = (Row)source;
                return property switch { "Id" => row.Id, "Name" => row.Name, _ => row.Version };
            }
            var model = new EntityModelMetadata(typeof(Row), "batch_rows", null, new[]
            {
                EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true, isKey: true,
                    generatedGetter: source => Read(source, "Id")),
                EntityPropertyMetadata.CreateGenerated("Name", typeof(string), true, true,
                    generatedGetter: source => Read(source, "Name")),
                EntityPropertyMetadata.CreateGenerated("Version", typeof(int), true, true,
                    generatedGetter: source => Read(source, "Version"))
            }, () => new Row())
            {
                GeneratedMutationShapeSql = (_, operation, property, rows) =>
                {
                    Interlocked.Increment(ref _sqlCalls);
                    return operation != SqlOperationKind.InsertValues ? null : (property, rows) switch
                    {
                        (null, 1) => Head + FirstRow,
                        (null, 2) => Head + FirstRow + SecondRow,
                        (null, 3) => Head + FirstRow + SecondRow + ThirdRow,
                        (null, 4) => Head + FirstRow + SecondRow + ThirdRow + FourthRow,
                        ("Name", 3) => NameSql,
                        _ => null
                    };
                }
            };
            var options = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
            var mapping = new EntityMappingOptions { EntityType = typeof(Row), TableName = table ?? "batch_rows" };
            mapping.Columns["Name"] = new ColumnMappingOptions
            {
                PropertyName = "Name", ColumnName = nameColumn, DbType = DbType.AnsiString, Size = 64
            };
            options.EntityMappings.Add(mapping);
            Builder = new DefaultSqlEntityMutationCommandBuilder(provider ?? new TestProvider(), new SqlBuilderServices(
                metadataOptions: options, entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model)));
        }
    }

    private sealed class TrackingDialect : DialectBase
    {
        private int _convertCount;
        private int _formatCount;
        internal int ConvertCount => _convertCount;
        internal int FormatCount => _formatCount;
        public override char OpeningIdentifier => '`';
        public override char ClosingIdentifier => '`';
        public override string SafeName(string name)
        {
            Interlocked.Increment(ref _formatCount);
            return base.SafeName(name);
        }
        public override object GetParamValue(object value)
        {
            Interlocked.Increment(ref _convertCount);
            return value is string text ? "converted:" + text : base.GetParamValue(value);
        }
    }

    private sealed class TestProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        private static readonly SqliteSqlProvider Inner = SqliteSqlProvider.Instance;
        private readonly IDialect _dialect;
        private readonly SqlProviderProfile _profile;

        internal TestProvider(IDialect dialect = null, SqlProviderProfile profile = null, bool useDefaultProfile = true)
        {
            _dialect = dialect ?? Inner.Dialect;
            _profile = profile ?? (useDefaultProfile
                ? dialect == null ? Inner.Profile : WithGeneratedSqlDialect(Inner.Profile, _dialect)
                : null);
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

        private static SqlProviderProfile WithGeneratedSqlDialect(SqlProviderProfile source, IDialect dialect) => new()
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
            Limits = source.Limits
        };
    }
}
