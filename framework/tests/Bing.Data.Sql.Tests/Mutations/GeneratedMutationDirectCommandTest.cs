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

/// <summary>完整生成 SQL 直接构造实体命令时的行为与安全边界。</summary>
public sealed class GeneratedMutationDirectCommandTest
{
    private const string InsertSql = "Insert Into `direct_rows` (`Id`, `Name`, `Version`) Values (@_p_0, @_p_1, @_p_2)";
    private const string UpdateSql = "Update `direct_rows` Set `Name` = @_p_0, `Version` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3";
    private const string DeleteSql = "Delete From `direct_rows` Where `Id` = @_p_0 And `Version` = @_p_1";
    private const string ReturningSql = " Returning `Id` As `Id`, `Name` As `Name`, `Version` As `Version`";

    /// <summary>已有完整 SQL 时不再拼接条件或格式化列名。</summary>
    [Theory]
    [InlineData("Update", false)]
    [InlineData("Update", true)]
    [InlineData("Delete", false)]
    [InlineData("Delete", true)]
    public void GeneratedSql_WhenConditionsAreKnown_ShouldSkipConditionFormatting(string operation, bool returning)
    {
        var dialect = new TrackingDialect();
        var builder = CreateBuilder(new TestProvider(dialect));
        var command = Build(builder, operation, returning, new Row { Id = 7, Name = "value", Version = 6 });

        Assert.Equal(GetSql(operation, returning), command.Sql);
        Assert.Equal(operation == "Update" ? new object[] { "value", 6, 7, 4 } : new object[] { 7, 4 },
            command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(0, dialect.FormatCount);
        Assert.True(command.ValidateAffectedRows);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>三种写入及 Returning 都必须遵循 Provider 参数上限。</summary>
    [Theory]
    [InlineData("Insert", false)]
    [InlineData("Insert", true)]
    [InlineData("Update", false)]
    [InlineData("Update", true)]
    [InlineData("Delete", false)]
    [InlineData("Delete", true)]
    public void GeneratedSql_WhenParameterLimitIsExceeded_ShouldThrow(string operation, bool returning)
    {
        var provider = new TestProvider(profile: new SqlProviderProfile
        {
            Mutation = new SqlProviderMutationCapabilities
            {
                GeneratedSqlDialect = SqliteSqlProvider.Instance.Dialect,
                SupportsReturning = true
            },
            Limits = new SqlProviderLimits { MaxParameterCount = 1 }
        });

        var exception = Assert.Throws<InvalidOperationException>(() => Build(CreateBuilder(provider), operation,
            returning, new Row { Id = 7, Name = "value", Version = 6 }));

        Assert.Equal("Provider bing.sqlite 的参数数量不能超过 1。", exception.Message);
    }

    /// <summary>即使登记了 Returning SQL，能力档案缺失、不完整或明确关闭时仍拒绝。</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("incomplete")]
    [InlineData("unsupported")]
    public void GeneratedReturning_WhenProfileDoesNotSupportReturning_ShouldThrow(string profileState)
    {
        var provider = new TestProvider(profile: profileState switch
        {
            "incomplete" => new SqlProviderProfile { Mutation = null },
            "unsupported" => new SqlProviderProfile(),
            _ => null
        }, useDefaultProfile: false);

        Assert.Throws<NotSupportedException>(() => CreateBuilder(provider).InsertReturning(
            new Row { Id = 7, Name = "value", Version = 6 }));
    }

    /// <summary>条件为空时必须在创建可执行命令之前失败，显式并发原值为空也不能放行。</summary>
    [Theory]
    [InlineData("Update", false, true)]
    [InlineData("Update", true, true)]
    [InlineData("Delete", false, true)]
    [InlineData("Delete", true, true)]
    [InlineData("Update", false, false)]
    [InlineData("Update", true, false)]
    [InlineData("Delete", false, false)]
    [InlineData("Delete", true, false)]
    public void GeneratedSql_WhenConditionIsNull_ShouldThrow(string operation, bool returning, bool nullKey)
    {
        var builder = CreateBuilder();
        var row = new Row { Id = nullKey ? null : 7, Name = "value", Version = 6 };
        var update = new SqlUpdateOptions<Row>().Original(entity => entity.Version, (int?)null);
        var delete = new SqlDeleteOptions<Row>().Original(entity => entity.Version, (int?)null);

        var exception = Assert.Throws<InvalidOperationException>(() => operation == "Update"
            ? returning ? builder.UpdateReturning(row, update) : builder.Update(row, update)
            : returning ? builder.DeleteReturning(row, delete) : builder.Purge(row, delete));

        Assert.Equal($"实体 Row 的条件列 {(nullKey ? "Id" : "Version")} 不能为空。", exception.Message);
    }

    /// <summary>保留参数管理器的方言转换以及最终列元数据，外部快照不能改写冻结命令。</summary>
    [Theory]
    [InlineData("Insert", false)]
    [InlineData("Insert", true)]
    [InlineData("Update", false)]
    [InlineData("Update", true)]
    [InlineData("Delete", false)]
    [InlineData("Delete", true)]
    public void GeneratedSql_ShouldPreserveParameterConversionMetadataAndSnapshots(string operation, bool returning)
    {
        var dialect = new TrackingDialect(convertStrings: true);
        var builder = CreateBuilder(new TestProvider(dialect));
        var row = new Row { Id = 7, Name = "value", Version = 6 };
        var command = Build(builder, operation, returning, row);
        var values = operation switch
        {
            "Insert" => new object[] { 7, "converted:value", 6 },
            "Update" => new object[] { "converted:value", 6, 7, 4 },
            _ => new object[] { 7, 4 }
        };

        Assert.Equal(GetSql(operation, returning), command.Sql);
        Assert.Equal(values, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(values.Length, dialect.ConvertCount);
        Assert.All(command.Parameters, parameter => Assert.True(parameter.IsProviderValue));
        if (operation != "Delete")
        {
            var name = Assert.Single(command.Parameters, parameter => parameter.PropertyName == "Name");
            Assert.Equal(DbType.AnsiString, name.DbType);
            Assert.Equal(64, name.Size);
            Assert.Equal("value", name.OriginalValue);
            Assert.Equal(SqlParameterSource.SqlBuilder, name.Source);
        }
        var exposed = command.Parameters.First();
        exposed.Value = "changed";
        exposed.PropertyName = "changed";
        row.Name = "changed";
        Assert.Equal(values, command.CreateParameters().Select(parameter => parameter.Value));
        Assert.Equal("@_p_0", command.CreateParameters().First().Name);
        Assert.NotEqual("changed", command.CreateParameters().First().PropertyName);
        Assert.Equal(returning, command.HasReturning);
        Assert.Equal(operation != "Insert", command.ValidateAffectedRows);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>共享映射计划不共享命令参数；默认并发校验也可以显式关闭。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedUpdate_WhenConcurrent_ShouldIsolateValuesAndHonorConflictBehavior(bool returning)
    {
        var builder = CreateBuilder();
        Parallel.For(0, 32, index =>
        {
            var row = new Row { Id = index, Name = $"request-{index}", Version = index + 1 };
            var options = new SqlUpdateOptions<Row>
            {
                ConcurrencyConflictBehavior = SqlConcurrencyConflictBehavior.ReturnAffectedRows
            }.Original(entity => entity.Version, index);
            var command = returning ? builder.UpdateReturning(row, options) : builder.Update(row, options);

            Assert.Equal(GetSql("Update", returning), command.Sql);
            Assert.Equal(new object[] { $"request-{index}", index + 1, index, index },
                command.Parameters.Select(parameter => parameter.Value));
            Assert.False(command.ValidateAffectedRows);
        });
    }

    private static SqlWriteCommand Build(DefaultSqlEntityMutationCommandBuilder builder, string operation,
        bool returning, Row row) => operation switch
    {
        "Insert" => returning ? builder.InsertReturning(row) : builder.Insert(row),
        "Update" => returning
            ? builder.UpdateReturning(row, new SqlUpdateOptions<Row>().Original(entity => entity.Version, 4))
            : builder.Update(row, new SqlUpdateOptions<Row>().Original(entity => entity.Version, 4)),
        _ => returning
            ? builder.DeleteReturning(row, new SqlDeleteOptions<Row>().Original(entity => entity.Version, 4))
            : builder.Purge(row, new SqlDeleteOptions<Row>().Original(entity => entity.Version, 4))
    };

    private static string GetSql(string operation, bool returning) => (operation switch
    {
        "Insert" => InsertSql,
        "Update" => UpdateSql,
        _ => DeleteSql
    }) + (returning ? ReturningSql : string.Empty);

    private static DefaultSqlEntityMutationCommandBuilder CreateBuilder(ISqlProvider provider = null)
    {
        var model = new EntityModelMetadata(typeof(Row), "direct_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated("Id", typeof(int?), true, true, isKey: true,
                generatedGetter: entity => ((Row)entity).Id),
            EntityPropertyMetadata.CreateGenerated("Name", typeof(string), true, true,
                generatedGetter: entity => ((Row)entity).Name),
            EntityPropertyMetadata.CreateGenerated("Version", typeof(int?), true, true, isConcurrencyToken: true,
                generatedGetter: entity => ((Row)entity).Version)
        }, () => new Row())
        {
            GeneratedMutationSql = static (_, operation) => GetSql(OperationName(operation), false),
            GeneratedMutationReturningSql = static (_, operation) => GetSql(OperationName(operation), true)
        };
        var metadata = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        var mapping = new EntityMappingOptions { EntityType = typeof(Row), TableName = "direct_rows" };
        mapping.Columns["Name"] = new ColumnMappingOptions { PropertyName = "Name", DbType = DbType.AnsiString, Size = 64 };
        metadata.EntityMappings.Add(mapping);
        return new DefaultSqlEntityMutationCommandBuilder(provider ?? new TestProvider(), new SqlBuilderServices(
            metadataOptions: metadata, entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model)));
    }

    private static string OperationName(SqlOperationKind operation) => operation switch
    {
        SqlOperationKind.InsertValues => "Insert",
        SqlOperationKind.Update => "Update",
        _ => "Delete"
    };

    private sealed class Row
    {
        public int? Id { get; set; }
        public string Name { get; set; }
        public int? Version { get; set; }
    }

    private sealed class TrackingDialect(bool convertStrings = false) : DialectBase
    {
        private int _formatCount;
        private int _convertCount;
        public int FormatCount => _formatCount;
        public int ConvertCount => _convertCount;
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
            return convertStrings && value is string text ? "converted:" + text : base.GetParamValue(value);
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
