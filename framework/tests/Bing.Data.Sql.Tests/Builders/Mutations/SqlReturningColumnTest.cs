using System.Text;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>返回列的不可变表示、方言渲染与生成代码合同测试。</summary>
public sealed class SqlReturningColumnTest
{
    [Fact]
    public void AppendTo_WhenColumnIsDirect_ShouldKeepPhysicalNameQualifierAndAlias()
    {
        var column = new SqlReturningColumn("id", "t", "Key");
        var sql = new StringBuilder("Prefix:");

        column.AppendTo(sql, SqliteSqlProvider.Instance.Dialect);

        Assert.Equal("id", column.Column);
        Assert.Equal("Prefix:`t`.`id` As `Key`", sql.ToString());
    }

    [Fact]
    public void CreateComputed_ShouldCopyPartsAndUseActualDialectForEveryColumn()
    {
        var parts = new[] { "((", "id", " + ", "id", ") / ", "quantity", ")" };
        var column = SqlReturningColumn.CreateComputed(parts, "t", "Total");
        parts[1] = "changed";
        parts[2] = " - ";
        var sql = new StringBuilder();

        column.AppendTo(sql, SqliteSqlProvider.Instance.Dialect);
        Assert.Equal("((`t`.`id` + `t`.`id`) / `t`.`quantity`) As `Total`", sql.ToString());
        Assert.Null(column.Column);
        sql.Clear();
        column.AppendTo(sql, SqlServerSqlProvider.Instance.Dialect, "Deleted");
        Assert.Equal("(([Deleted].[id] + [Deleted].[id]) / [Deleted].[quantity]) As [Total]", sql.ToString());
    }

    public static TheoryData<string[]> InvalidParts => new()
    {
        null,
        new[] { "(", "id", ")" },
        new[] { "(", "id", " + ", "id" },
        new[] { "(", " ", " + ", "id", ")" },
        new[] { "(", "id", " + 1 + ", "id", ")" },
        new[] { "(", "id", " -- ", "id", ")" },
        new[] { "(", "id", " /* ", "id", ")" }
    };

    [Theory]
    [MemberData(nameof(InvalidParts))]
    public void CreateComputed_WhenShapeOrLiteralIsInvalid_ShouldReject(string[] parts)
    {
        Assert.ThrowsAny<ArgumentException>(() => SqlReturningColumn.CreateComputed(parts));
    }

    [Fact]
    public void Clone_WhenComputedColumnsMoveToAnotherProvider_ShouldRenderWithTargetDialectAndOperation()
    {
        var sqlite = SqliteSqlProvider.Instance;
        var sqlServer = SqlServerSqlProvider.Instance;
        var source = new ReturningClause(CreateContext(sqlite));
        source.AddRange(new[] { SqlReturningColumn.CreateComputed(new[] { "(", "id", " + ", "quantity", ")" }, alias: "Total") });
        source.Validate(new SqlValidationContext(sqlite, 0, false, SqlExecutionKind.Update));
        var clone = source.Clone(CreateContext(sqlServer));
        clone.Validate(new SqlValidationContext(sqlServer, 0, false, SqlExecutionKind.Delete));
        var sourceSql = new StringBuilder();
        var cloneSql = new StringBuilder();

        source.AppendTo(sourceSql);
        clone.AppendTo(cloneSql);

        Assert.Equal(" Returning (`id` + `quantity`) As `Total`", sourceSql.ToString());
        Assert.Equal(" Output ([Deleted].[id] + [Deleted].[quantity]) As [Total]", cloneSql.ToString());
    }

    [Fact]
    public void AppendTo_WhenComputedColumnFormattingFails_ShouldRestoreClauseBuffer()
    {
        var provider = SqliteSqlProvider.Instance;
        var clause = new ReturningClause(CreateContext(provider));
        clause.AddRange(new[] { SqlReturningColumn.CreateComputed(new[] { "(", "id", " + ", "invalid;", ")" }) });
        clause.Validate(new SqlValidationContext(provider, 0, false, SqlExecutionKind.Update));
        var sql = new StringBuilder("Prefix:");

        Assert.Throws<ArgumentException>(() => clause.AppendTo(sql));

        Assert.Equal("Prefix:", sql.ToString());
    }

    private static SqlMutationContext CreateContext(ISqlProvider provider) => new(provider,
        provider.ParameterManagerFactory.Create(provider.Dialect), SqlBuilderServices.CreateDefault(),
        new SqlBuilderExecutionContext(null));
}
