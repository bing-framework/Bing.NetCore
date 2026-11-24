using System.Text;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Kind = Bing.Data.Sql.SqlExecutionKind;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>生成 Returning 文本与可变子句生命周期的直接合同测试。</summary>
public sealed class ReturningClauseGeneratedSqlTest
{
    private static readonly SqlReturningColumn[] Columns =
    {
        new("row_id", alias: "Id"), new("display_name", alias: "Caption"), new("row_id", alias: "Again")
    };

    public static TheoryData<ISqlProvider, Kind, string> ProviderCases => new()
    {
        { SqliteSqlProvider.Instance, Kind.Insert, " Returning `row_id` As `Id`, `display_name` As `Caption`, `row_id` As `Again`" },
        { SqliteSqlProvider.Instance, Kind.Update, " Returning `row_id` As `Id`, `display_name` As `Caption`, `row_id` As `Again`" },
        { SqliteSqlProvider.Instance, Kind.Delete, " Returning `row_id` As `Id`, `display_name` As `Caption`, `row_id` As `Again`" },
        { PostgreSqlSqlProvider.Instance, Kind.Insert, " Returning \"row_id\" As \"Id\", \"display_name\" As \"Caption\", \"row_id\" As \"Again\"" },
        { PostgreSqlSqlProvider.Instance, Kind.Update, " Returning \"row_id\" As \"Id\", \"display_name\" As \"Caption\", \"row_id\" As \"Again\"" },
        { PostgreSqlSqlProvider.Instance, Kind.Delete, " Returning \"row_id\" As \"Id\", \"display_name\" As \"Caption\", \"row_id\" As \"Again\"" },
        { SqlServerSqlProvider.Instance, Kind.Insert, " Output [Inserted].[row_id] As [Id], [Inserted].[display_name] As [Caption], [Inserted].[row_id] As [Again]" },
        { SqlServerSqlProvider.Instance, Kind.Update, " Output [Inserted].[row_id] As [Id], [Inserted].[display_name] As [Caption], [Inserted].[row_id] As [Again]" },
        { SqlServerSqlProvider.Instance, Kind.Delete, " Output [Deleted].[row_id] As [Id], [Deleted].[display_name] As [Caption], [Deleted].[row_id] As [Again]" }
    };

    /// <summary>生成工厂必须真正参与渲染，不能仅靠相同的动态输出通过测试。</summary>
    [Theory]
    [MemberData(nameof(ProviderCases))]
    public void TryAddGenerated_ShouldRenderCompleteProviderSql(ISqlProvider provider, Kind kind, string expected)
    {
        var clause = new ReturningClause(Context(provider));
        var calls = 0;
        Assert.True(clause.TryAddGenerated(Columns, (key, operation) =>
        {
            calls++;
            Assert.Equal(provider.Key, key);
            Assert.Equal(kind, operation);
            return expected;
        }));
        clause.Validate(new SqlValidationContext(provider, 0, false, kind));

        Assert.Equal(expected, Render(clause));
        Assert.Equal(expected, Render(clause));
        Assert.Equal(2, calls);
    }

    /// <summary>追加普通列时，恢复完整结构渲染，不能丢失旧列或复用过时文本。</summary>
    [Fact]
    public void AddRange_AfterGenerated_ShouldKeepAllColumnsAndInvalidateGeneratedSql()
    {
        var clause = new ReturningClause(Context(SqliteSqlProvider.Instance));
        var calls = 0;
        Assert.True(clause.TryAddGenerated(Columns, (_, _) => { calls++; return "unused"; }));
        clause.AddRange(new[] { new SqlReturningColumn("extra", alias: "Extra") });
        clause.Validate(new SqlValidationContext(SqliteSqlProvider.Instance, 0, false, Kind.Update));

        Assert.Equal(" Returning `row_id` As `Id`, `display_name` As `Caption`, `row_id` As `Again`, `extra` As `Extra`", Render(clause));
        Assert.Equal(0, calls);
    }

    /// <summary>已有子句拒绝替换，调用方仍可追加原来的结构列。</summary>
    [Fact]
    public void TryAddGenerated_WhenAlreadyConfigured_ShouldKeepExistingColumns()
    {
        var clause = new ReturningClause(Context(SqliteSqlProvider.Instance));
        clause.AddRange(new[] { new SqlReturningColumn("existing") });

        Assert.False(clause.TryAddGenerated(Columns, (_, _) => "unexpected"));
        clause.AddRange(Columns);
        clause.Validate(new SqlValidationContext(SqliteSqlProvider.Instance, 0, false, Kind.Update));
        Assert.Equal(" Returning `existing`, `row_id` As `Id`, `display_name` As `Caption`, `row_id` As `Again`", Render(clause));
    }

    /// <summary>清空会同时丢弃生成 SQL，后续普通投影没有旧内容。</summary>
    [Fact]
    public void Clear_AfterGenerated_ShouldRemoveSqlAndColumns()
    {
        var clause = new ReturningClause(Context(SqliteSqlProvider.Instance));
        Assert.True(clause.TryAddGenerated(Columns, (_, _) => "unexpected"));
        clause.Clear();
        Assert.True(clause.IsEmpty);
        clause.AddRange(new[] { new SqlReturningColumn("fresh") });
        clause.Validate(new SqlValidationContext(SqliteSqlProvider.Instance, 0, false, Kind.Update));
        Assert.Equal(" Returning `fresh`", Render(clause));
    }

    /// <summary>克隆保留生成工厂，但操作类型与集合变更分别独立。</summary>
    [Fact]
    public void Clone_ShouldKeepGeneratedSqlAndIndependentOperationState()
    {
        var provider = SqlServerSqlProvider.Instance;
        var clause = new ReturningClause(Context(provider));
        var calls = 0;
        Assert.True(clause.TryAddGenerated(new[] { new SqlReturningColumn("row_id", alias: "Id") }, (_, kind) =>
        {
            calls++;
            return kind == Kind.Delete ? " Output [Deleted].[row_id] As [Id]" : " Output [Inserted].[row_id] As [Id]";
        }));
        clause.Validate(new SqlValidationContext(provider, 0, false, Kind.Update));
        var clone = clause.Clone(Context(provider));
        clone.Validate(new SqlValidationContext(provider, 0, false, Kind.Delete));
        Assert.Equal(" Output [Inserted].[row_id] As [Id]", Render(clause));
        Assert.Equal(" Output [Deleted].[row_id] As [Id]", Render(clone));
        Assert.Equal(2, calls);
        clone.Clear();
        Assert.Equal(" Output [Inserted].[row_id] As [Id]", Render(clause));
    }

    /// <summary>工厂不覆盖当前 Provider 时仍使用同一结构列，不产生空子句。</summary>
    [Fact]
    public void AppendTo_WhenFactoryReturnsNull_ShouldRenderStructuredColumns()
    {
        var provider = SqliteSqlProvider.Instance;
        var clause = new ReturningClause(Context(provider));
        Assert.True(clause.TryAddGenerated(new[] { new SqlReturningColumn("row_id", alias: "Id") }, (_, _) => null));
        clause.Validate(new SqlValidationContext(provider, 0, false, Kind.Update));
        Assert.Equal(" Returning `row_id` As `Id`", Render(clause));
    }

    /// <summary>生成工厂异常时应保留调用者缓冲区。</summary>
    [Fact]
    public void AppendTo_WhenFactoryThrows_ShouldKeepCallerBuffer()
    {
        var provider = SqliteSqlProvider.Instance;
        var clause = new ReturningClause(Context(provider));
        Assert.True(clause.TryAddGenerated(Columns, (_, _) => throw new InvalidOperationException("test")));
        clause.Validate(new SqlValidationContext(provider, 0, false, Kind.Update));
        var buffer = new StringBuilder("prefix");
        Assert.Throws<InvalidOperationException>(() => clause.AppendTo(buffer));
        Assert.Equal("prefix", buffer.ToString());
    }

    /// <summary>使用自定义命名服务时保留原格式化入口。</summary>
    [Fact]
    public void TryAddGenerated_WithCustomFormatter_ShouldLeaveClauseEmpty()
    {
        var provider = SqliteSqlProvider.Instance;
        var services = new SqlBuilderServices(objectNameFormatter: new CustomFormatter());
        var context = new SqlMutationContext(provider, provider.ParameterManagerFactory.Create(provider.Dialect), services,
            new SqlBuilderExecutionContext(null));
        var clause = new ReturningClause(context);
        Assert.False(clause.TryAddGenerated(Columns, (_, _) => "unexpected"));
        Assert.True(clause.IsEmpty);
    }

    /// <summary>克隆到另一内置 Provider 时，由目标 Provider 选择文本。</summary>
    [Fact]
    public void Clone_WhenProviderChanges_ShouldSelectTargetSql()
    {
        var clause = new ReturningClause(Context(SqliteSqlProvider.Instance));
        Assert.True(clause.TryAddGenerated(new[] { new SqlReturningColumn("row_id", alias: "Id") }, (key, _) =>
            key == "bing.postgresql" ? " Returning \"row_id\" As \"Id\"" : " Returning `row_id` As `Id`"));
        var clone = clause.Clone(Context(PostgreSqlSqlProvider.Instance));
        clone.Validate(new SqlValidationContext(PostgreSqlSqlProvider.Instance, 0, false, Kind.Update));
        Assert.Equal(" Returning \"row_id\" As \"Id\"", Render(clone));
    }

    /// <summary>目标服务不满足生成合同后，克隆只保留结构列。</summary>
    [Fact]
    public void Clone_WhenTargetServicesChange_ShouldRenderStructuredColumns()
    {
        var provider = SqliteSqlProvider.Instance;
        var clause = new ReturningClause(Context(provider));
        Assert.True(clause.TryAddGenerated(new[] { new SqlReturningColumn("row_id", alias: "Id") }, (_, _) => "unexpected"));
        var services = new SqlBuilderServices(objectNameFormatter: new CustomFormatter());
        var context = new SqlMutationContext(provider, provider.ParameterManagerFactory.Create(provider.Dialect), services,
            new SqlBuilderExecutionContext(null));
        var clone = clause.Clone(context);
        clone.Validate(new SqlValidationContext(provider, 0, false, Kind.Update));
        Assert.Equal(" Returning `row_id` As `Id`", Render(clone));
    }

    /// <summary>自定义方言拒绝静态文本后，结构列仍按自定义标识符规则完整渲染。</summary>
    [Fact]
    public void TryAddGenerated_WhenDialectIsCustomized_ShouldRejectAndRenderStructuredColumns()
    {
        var provider = new CustomDialectProvider(SqliteSqlProvider.Instance);
        var clause = new ReturningClause(Context(provider));

        Assert.False(clause.TryAddGenerated(Columns, (_, _) => " STATIC RETURNING SQL"));
        clause.AddRange(Columns);
        clause.Validate(new SqlValidationContext(provider, 0, false, Kind.Update));

        Assert.Equal(" Returning <row_id> As <Id>, <display_name> As <Caption>, <row_id> As <Again>",
            Render(clause));
    }

    /// <summary>克隆到自定义方言时不携带按内置方言生成的 Returning 工厂。</summary>
    [Fact]
    public void Clone_WhenDialectIsCustomized_ShouldDiscardGeneratedSqlFactory()
    {
        var sourceProvider = SqliteSqlProvider.Instance;
        var clause = new ReturningClause(Context(sourceProvider));
        var calls = 0;
        Assert.True(clause.TryAddGenerated(new[] { new SqlReturningColumn("row_id", alias: "Id") },
            (_, _) => { calls++; return " STATIC RETURNING SQL"; }));

        var targetProvider = new CustomDialectProvider(sourceProvider);
        var clone = clause.Clone(Context(targetProvider));
        clone.Validate(new SqlValidationContext(targetProvider, 0, false, Kind.Update));

        Assert.Equal(" Returning <row_id> As <Id>", Render(clone));
        Assert.Equal(0, calls);
    }

    private static SqlMutationContext Context(ISqlProvider provider) => new(provider,
        provider.ParameterManagerFactory.Create(provider.Dialect), SqlBuilderServices.CreateDefault(), new SqlBuilderExecutionContext(null));

    private static string Render(ISqlContent content)
    {
        var buffer = new StringBuilder();
        content.AppendTo(buffer);
        return buffer.ToString();
    }

    private sealed class CustomFormatter : Bing.Data.Sql.Metadata.ISqlObjectNameFormatter
    {
        public string Format(SqlTableReference reference, IDialect dialect, Bing.Data.Enums.DatabaseType? databaseType) => throw new NotSupportedException();
    }

    private sealed class CustomDialectProvider(ISqlProvider inner) : ISqlProvider, ISqlProviderProfileProvider
    {
        public string Key => inner.Key;
        public Bing.Data.Enums.DatabaseType DatabaseType => inner.DatabaseType;
        public IDialect Dialect { get; } = AngleDialect.Instance;
        public ISqlClauseFactory ClauseFactory => inner.ClauseFactory;
        public ISqlTableReferenceParser TableReferenceParser => inner.TableReferenceParser;
        public ISqlPaginationRenderer PaginationRenderer => inner.PaginationRenderer;
        public IParameterManagerFactory ParameterManagerFactory => inner.ParameterManagerFactory;
        public IParamLiteralsResolver ParamLiteralsResolver => inner.ParamLiteralsResolver;
        public SqlProviderProfile Profile => ((ISqlProviderProfileProvider)inner).Profile;
    }

    private sealed class AngleDialect : DialectBase
    {
        public static AngleDialect Instance { get; } = new();
        public override char OpeningIdentifier => '<';
        public override char ClosingIdentifier => '>';
    }
}
