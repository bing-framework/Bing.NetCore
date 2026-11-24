using System.ComponentModel;
using System.Reflection;
using System.Text;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>Returning 计算列的一元负号、片段校验与方言格式化测试。</summary>
public sealed class SqlReturningArithmeticColumnTest
{
    [Fact]
    public void CreateComputed_WhenUnaryMinusIsUsed_ShouldRenderWithProviderQualifier()
    {
        var parts = new[] { "(-", "amount", ")" };
        var column = SqlReturningColumn.CreateComputed(parts, "row", "NegativeAmount");
        parts[1] = "changed";
        parts[0] = "";

        var sqlite = new StringBuilder();
        column.AppendTo(sqlite, SqliteSqlProvider.Instance.Dialect);
        Assert.Equal("(-`row`.`amount`) As `NegativeAmount`", sqlite.ToString());

        var postgresql = new StringBuilder();
        column.AppendTo(postgresql, PostgreSqlSqlProvider.Instance.Dialect);
        Assert.Equal("(-\"row\".\"amount\") As \"NegativeAmount\"", postgresql.ToString());

        var sqlServer = new StringBuilder();
        column.AppendTo(sqlServer, SqlServerSqlProvider.Instance.Dialect, "Deleted");
        Assert.Equal("(-[Deleted].[amount]) As [NegativeAmount]", sqlServer.ToString());
        Assert.Null(column.Column);
    }

    [Fact]
    public void CreateComputed_WhenUnaryAndBinaryOperatorsAreNested_ShouldKeepEveryTextAndColumnSlot()
    {
        var column = SqlReturningColumn.CreateComputed(
            new[] { "(( - ( - ", "amount", ")) + (-", "other", "))" }, "t", "Total");
        var sql = new StringBuilder();

        column.AppendTo(sql, SqliteSqlProvider.Instance.Dialect);

        Assert.Equal("(( - ( - `t`.`amount`)) + (-`t`.`other`)) As `Total`", sql.ToString());
    }

    [Fact]
    public void CreateArithmetic_WhenUnaryMinusIsUsed_ShouldCopyPartsAndRenderQualifiedColumn()
    {
        var parts = new[] { "(-", "amount", ")" };
        var column = SqlReturningColumn.CreateArithmetic(parts, "row", "NegativeAmount");
        parts[0] = "changed";
        parts[1] = "changed";

        var sql = new StringBuilder();
        column.AppendTo(sql, SqliteSqlProvider.Instance.Dialect);

        Assert.Equal("(-`row`.`amount`) As `NegativeAmount`", sql.ToString());
        Assert.Null(column.Column);
        Assert.Equal("row", column.Qualifier);
        Assert.Equal("NegativeAmount", column.Alias);
    }

    [Fact]
    public void CreateArithmetic_ShouldBeHiddenFromIntelliSense()
    {
        var method = typeof(SqlReturningColumn).GetMethod(nameof(SqlReturningColumn.CreateArithmetic));

        Assert.NotNull(method);
        Assert.Equal(EditorBrowsableState.Never,
            method.GetCustomAttribute<EditorBrowsableAttribute>()?.State);
    }

    public static TheoryData<string[]> InvalidParts => new()
    {
        null,
        Array.Empty<string>(),
        new[] { "1 + 2" },
        new string[] { null, "id", " + ", "other", "" },
        new string[] { "", null, " + ", "other", "" },
        new[] { "(", "", " + ", "id", ")" },
        new[] { "", "id", " ", "other", "" },
        new[] { " + ", "id", " + ", "other", "" },
        new[] { "(", "id", " + ", "other", "" },
        new[] { "", "id", " + ", "other", ")" },
        new[] { "", "id", " -- ", "other", "" },
        new[] { "", "id", " * / ", "other", "" },
        new[] { "", "id", " + * ", "other", "" }
    };

    [Theory]
    [MemberData(nameof(InvalidParts))]
    public void CreateFactories_WhenExpressionHasNoValidArithmeticShape_ShouldReject(string[] parts)
    {
        Assert.ThrowsAny<ArgumentException>(() => SqlReturningColumn.CreateComputed(parts));
        Assert.ThrowsAny<ArgumentException>(() => SqlReturningColumn.CreateArithmetic(parts));
    }
}
