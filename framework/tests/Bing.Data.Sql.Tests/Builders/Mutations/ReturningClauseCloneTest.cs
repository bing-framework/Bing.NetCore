using System.Text;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;
using MutationSqlExecutionKind = Bing.Data.Sql.SqlExecutionKind;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Returning 子句克隆行为测试。
/// </summary>
public sealed class ReturningClauseCloneTest
{
    /// <summary>
    /// 提供具有不同引用符号的 Returning Provider 和预期完整 SQL。
    /// </summary>
    public static TheoryData<ISqlProvider, string> ProviderCases => new()
    {
        {
            SqliteSqlProvider.Instance,
            " Returning `t`.`id` As `EntityId`, `t`.`display_name` As `Display`, " +
            "`t`.`id` As `DuplicateId`"
        },
        {
            PostgreSqlSqlProvider.Instance,
            " Returning \"t\".\"id\" As \"EntityId\", \"t\".\"display_name\" As \"Display\", " +
            "\"t\".\"id\" As \"DuplicateId\""
        }
    };

    /// <summary>
    /// 测试目的：克隆应保留 Provider 格式化的限定符、别名、顺序与重复列。
    /// </summary>
    [Theory]
    [MemberData(nameof(ProviderCases))]
    public void Clone_ShouldPreserveProviderQualifiedAliasedDuplicateColumns(
        ISqlProvider provider, string expectedSql)
    {
        var source = new ReturningClause(CreateContext(provider));
        source.AddRange(new[]
        {
            new SqlReturningColumn("id", "t", "EntityId"),
            new SqlReturningColumn("display_name", "t", "Display"),
            new SqlReturningColumn("id", "t", "DuplicateId")
        });
        source.Validate(CreateValidationContext(provider, MutationSqlExecutionKind.Update));

        var clone = Assert.IsType<ReturningClause>(source.Clone(CreateContext(provider)));
        clone.Validate(CreateValidationContext(provider, MutationSqlExecutionKind.Update));

        Assert.Equal(expectedSql, ToSql(clone));
    }

    /// <summary>
    /// 测试目的：来源与克隆的清空、追加操作应分别维护各自的投影列。
    /// </summary>
    [Fact]
    public void Clone_ShouldIsolateSourceAndCloneClearAddOperations()
    {
        var provider = SqliteSqlProvider.Instance;
        var source = new ReturningClause(CreateContext(provider));
        source.AddRange(new[] { new SqlReturningColumn("id", alias: "Id") });
        source.Validate(CreateValidationContext(provider, MutationSqlExecutionKind.Update));
        var clone = Assert.IsType<ReturningClause>(source.Clone(CreateContext(provider)));
        clone.Validate(CreateValidationContext(provider, MutationSqlExecutionKind.Update));

        source.Clear();
        clone.AddRange(new[] { new SqlReturningColumn("display_name", "t", "Display") });
        source.AddRange(new[] { new SqlReturningColumn("created_at", alias: "Created") });

        Assert.Equal(" Returning `id` As `Id`, `t`.`display_name` As `Display`", ToSql(clone));
        Assert.Equal(" Returning `created_at` As `Created`", ToSql(source));

        clone.Clear();

        Assert.True(clone.IsEmpty);
        Assert.Equal(" Returning `created_at` As `Created`", ToSql(source));
    }

    /// <summary>
    /// 测试目的：SQL Server 来源与克隆应各自使用最后验证的 Inserted 或 Deleted 限定符。
    /// </summary>
    [Fact]
    public void Clone_ShouldKeepSqlServerInsertAndDeleteValidationStateIndependent()
    {
        var provider = SqlServerSqlProvider.Instance;
        var source = new ReturningClause(CreateContext(provider));
        source.AddRange(new[] { new SqlReturningColumn("id", alias: "Id") });
        source.Validate(CreateValidationContext(provider, MutationSqlExecutionKind.Insert));

        var clone = Assert.IsType<ReturningClause>(source.Clone(CreateContext(provider)));
        clone.Validate(CreateValidationContext(provider, MutationSqlExecutionKind.Delete));

        Assert.Equal(" Output [Inserted].[id] As [Id]", ToSql(source));
        Assert.Equal(" Output [Deleted].[id] As [Id]", ToSql(clone));
    }

    /// <summary>
    /// 测试目的：克隆中的列格式化失败时，应回滚已写入的关键字和前序列。
    /// </summary>
    [Fact]
    public void Clone_WhenLaterColumnFormattingFails_ShouldKeepCallerBufferUnchanged()
    {
        var provider = SqliteSqlProvider.Instance;
        var source = new ReturningClause(CreateContext(provider));
        source.AddRange(new[]
        {
            new SqlReturningColumn("id"),
            new SqlReturningColumn("invalid;")
        });
        source.Validate(CreateValidationContext(provider, MutationSqlExecutionKind.Update));
        var clone = source.Clone(CreateContext(provider));
        clone.Validate(CreateValidationContext(provider, MutationSqlExecutionKind.Update));
        var result = new StringBuilder("Prefix:");

        Assert.Throws<ArgumentException>(() => clone.AppendTo(result));

        Assert.Equal("Prefix:", result.ToString());
    }

    private static SqlMutationContext CreateContext(ISqlProvider provider)
    {
        IParameterManager parameterManager = provider.ParameterManagerFactory.Create(provider.Dialect);
        return new SqlMutationContext(provider, parameterManager, SqlBuilderServices.CreateDefault(),
            new SqlBuilderExecutionContext(null));
    }

    private static SqlValidationContext CreateValidationContext(
        ISqlProvider provider, MutationSqlExecutionKind executionKind) =>
        new(provider, 0, false, executionKind);

    private static string ToSql(ISqlContent content)
    {
        var builder = new StringBuilder();
        content.AppendTo(builder);
        return builder.ToString();
    }
}
