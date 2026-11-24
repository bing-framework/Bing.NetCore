using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Filters;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Dapper.Tests.Builders;

/// <summary>Oracle Full Join 来源过滤的 SQL 合同。</summary>
public sealed class OracleFullJoinFilterTest
{
    /// <summary>两侧来源过滤应进入各自派生表，重复渲染不污染 Builder 草稿。</summary>
    [Fact]
    public void ToSql_WhenBothFullJoinSourcesAreFiltered_ShouldWrapSourcesAndPreserveDraftState()
    {
        var filter = new BothSourcesFilter();
        var builder = new OracleBuilder(new SqlBuilderServices(filters: new ISqlFilter[] { filter }));
        builder.Select("a.Id,b.Id").From<LeftRow>("a")
            .FullJoin<RightRow>("b").On<LeftRow, RightRow>((left, right) => left.Id == right.Id);
        var join = builder.JoinClause;
        var where = builder.WhereClause;

        const string expectedSql = "Select \"a\".\"Id\",\"b\".\"Id\" \r\nFrom " +
                                   "(Select * From \"left_rows\" \"a\" Where \"a\".\"TenantId\"=:p_0) \"a\" " +
                                   "\r\nFull Join (Select * From \"right_rows\" \"b\" Where \"b\".\"TenantId\"=:p_1) \"b\" " +
                                   "On \"a\".\"Id\"=\"b\".\"Id\"";

        var firstSql = builder.ToSql();
        var secondSql = builder.ToSql();

        Assert.Equal(expectedSql, firstSql);
        Assert.Equal(expectedSql, secondSql);
        Assert.Equal(2, filter.Calls);
        Assert.Same(join, builder.JoinClause);
        Assert.Same(where, builder.WhereClause);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>同一来源的多个谓词应在同一派生表中以 And 合并。</summary>
    [Fact]
    public void ToSql_WhenOneFullJoinSourceHasMultiplePredicates_ShouldCombineThemInOneDerivedTable()
    {
        var filter = new LeftPredicatesFilter();
        var builder = new OracleBuilder(new SqlBuilderServices(filters: new ISqlFilter[] { filter }));
        builder.Select("a.Id,b.Id").From<LeftRow>("a")
            .FullJoin<RightRow>("b").On<LeftRow, RightRow>((left, right) => left.Id == right.Id);
        var join = builder.JoinClause;
        var where = builder.WhereClause;

        const string expectedSql = "Select \"a\".\"Id\",\"b\".\"Id\" \r\nFrom " +
                                   "(Select * From \"left_rows\" \"a\" Where \"a\".\"TenantId\"=:p_0 " +
                                   "And \"a\".\"IsDeleted\"=:p_1) \"a\" " +
                                   "\r\nFull Join \"right_rows\" \"b\" On \"a\".\"Id\"=\"b\".\"Id\"";

        var firstSql = builder.ToSql();
        var secondSql = builder.ToSql();

        Assert.Equal(expectedSql, firstSql);
        Assert.Equal(expectedSql, secondSql);
        Assert.Equal(2, filter.Calls);
        Assert.Same(join, builder.JoinClause);
        Assert.Same(where, builder.WhereClause);
        Assert.Empty(builder.GetParams());
    }

    [Table("left_rows")]
    private sealed class LeftRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public bool IsDeleted { get; set; }
    }

    [Table("right_rows")]
    private sealed class RightRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    private sealed class BothSourcesFilter : ISqlFilter
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

    private sealed class LeftPredicatesFilter : ISqlFilter
    {
        internal int Calls { get; private set; }

        public void Filter(SqlFilterContext context)
        {
            Calls++;
            var source = context.Sources.Single(item => item.EntityType == typeof(LeftRow));
            context.AddPredicate(source, context.GetColumn(source, nameof(LeftRow.TenantId)), 11);
            context.AddPredicate(source, context.GetColumn(source, nameof(LeftRow.IsDeleted)), false);
        }
    }
}
