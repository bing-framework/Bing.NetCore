using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Filters;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Dapper.Tests.Builders;

/// <summary>Oracle Right Join 的全局过滤 SQL 合同。</summary>
public sealed class OracleRightJoinFilterTest
{
    /// <summary>Oracle 应将左侧来源过滤追加到 On，将保留的右侧来源过滤追加到 Where。</summary>
    [Fact]
    public void ToSql_WhenBothRightJoinSourcesAreFiltered_ShouldPreserveRowsAndDraftState()
    {
        var filter = new TenantFilter();
        var builder = new OracleBuilder(new SqlBuilderServices(filters: new ISqlFilter[] { filter }));
        builder.Select("b.Id").From<LeftRow>("a")
            .RightJoin<RightRow>("b").On<LeftRow, RightRow>((left, right) => left.Id == right.Id);
        var join = builder.JoinClause;
        var where = builder.WhereClause;

        const string expectedSql = "Select \"b\".\"Id\" \r\nFrom \"left_rows\" \"a\" \r\nRight Join \"right_rows\" \"b\" " +
                                   "On \"a\".\"Id\"=\"b\".\"Id\" And \"a\".\"TenantId\"=:p_0 " +
                                   "\r\nWhere \"b\".\"TenantId\"=:p_1";

        var firstSql = builder.ToSql();
        var secondSql = builder.ToSql();

        Assert.Equal(expectedSql, firstSql);
        Assert.Equal(expectedSql, secondSql);
        Assert.Equal(2, filter.Calls);
        Assert.Same(join, builder.JoinClause);
        Assert.Same(where, builder.WhereClause);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>后续 Full Join 应在来源内部预过滤，保留双方未匹配行。</summary>
    [Fact]
    public void ToSql_WhenRightPreservedSourceFlowsIntoFullJoin_ShouldPrefilterSource()
    {
        var filter = new RightSourceFilter();
        var builder = new OracleBuilder(new SqlBuilderServices(filters: new ISqlFilter[] { filter }));
        builder.Select("b.Id").From<LeftRow>("a")
            .RightJoin<RightRow>("b").On<LeftRow, RightRow>((left, right) => left.Id == right.Id)
            .FullJoin<LastRow>("c").On<RightRow, LastRow>((right, last) => right.Id == last.Id);
        const string expected = "Select \"b\".\"Id\" \r\nFrom \"left_rows\" \"a\" \r\n" +
            "Right Join (Select * From \"right_rows\" \"b\" Where \"b\".\"TenantId\"=:p_0) \"b\" " +
            "On \"a\".\"Id\"=\"b\".\"Id\" \r\nFull Join \"last_rows\" \"c\" On \"b\".\"Id\"=\"c\".\"Id\"";
        Assert.Equal(expected, builder.ToSql());
        Assert.Empty(builder.GetParams());
    }

    [Table("left_rows")]
    private sealed class LeftRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    [Table("right_rows")]
    private sealed class RightRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    [Table("last_rows")]
    private sealed class LastRow
    {
        public int Id { get; set; }
    }

    private sealed class TenantFilter : ISqlFilter
    {
        internal int Calls { get; private set; }

        public void Filter(SqlFilterContext context)
        {
            Calls++;
            foreach (var source in context.Sources)
            {
                var value = source.EntityType == typeof(LeftRow) ? 11 : 22;
                context.AddPredicate(source, context.GetColumn(source, nameof(LeftRow.TenantId)), value);
            }
        }
    }

    private sealed class RightSourceFilter : ISqlFilter
    {
        public void Filter(SqlFilterContext context)
        {
            var source = context.Sources.Single(item => item.EntityType == typeof(RightRow));
            context.AddPredicate(source, context.GetColumn(source, nameof(RightRow.TenantId)), 22);
        }
    }
}
