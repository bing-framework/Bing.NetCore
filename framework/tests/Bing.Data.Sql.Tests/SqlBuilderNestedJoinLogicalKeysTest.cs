using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>类型化派生表连接的逻辑参数键及失败回滚契约。</summary>
public sealed class SqlBuilderNestedJoinLogicalKeysTest
{
    /// <summary>连接试算失败不能发布逻辑键；重试成功后保留独立的请求参数。</summary>
    [Fact]
    public void Join_WhenPredicateFailsThenRetried_ShouldCommitOnlySuccessfulLogicalKeys()
    {
        var key = new SqlParameterKey("nested", "id");
        var fragment = new SqlQueryFragment("select @id as IntValue", new[] { new SqlParam("id", 7) })
            .WithParameterKey("id", key);
        var child = new TestSqlBuilder().Select("IntValue").From(fragment, "leaf");
        var subquery = new SqlSubquery<Sample>(child, "summary", new[] { nameof(Sample.IntValue) },
            "test.sqlserver", null, null, null, null, null);
        var builder = new TestSqlBuilder();
        builder.Select("owner.IntValue").From<Sample>("owner");
        var from = (FromClause)builder.FromClause;
        var join = (JoinClause)builder.JoinClause;
        Expression<Func<Sample, Sample, bool>> invalid = (owner, summary) => owner.StringValue == summary.StringValue;
        Expression<Func<Sample, Sample, bool>> valid = (owner, summary) => owner.IntValue == summary.IntValue;

        Assert.Throws<NotSupportedException>(() => join.Join(from, subquery, invalid));
        builder.AppendWhere("[owner].[IntValue]=@id").AddParam("id", 7);
        var beforeRetry = builder.ToTemplate();
        Assert.Equal("Select [owner].[IntValue] \r\nFrom [Sample] As [owner] \r\nWhere [owner].[IntValue]=@id", beforeRetry.Sql);
        Assert.Equal(new SqlParameterKey("id"), Assert.Single(beforeRetry.Slots));

        join.Join(from, subquery, valid);
        var result = builder.ToTemplate();
        Assert.Equal("Select [owner].[IntValue] \r\nFrom [Sample] As [owner] \r\nJoin (Select [IntValue] \r\nFrom (select @_p_0 as IntValue) As [leaf]) As [summary] On [owner].[IntValue]=[summary].[IntValue] \r\nWhere [owner].[IntValue]=@id", result.Sql);
        Assert.Equal(new[] { new SqlParameterKey("id"), key }, result.Slots);
        var rendered = new SqlQueryShapeTemplate(result.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [key] = 19 });
        Assert.Equal(new object[] { 7, 19 }, rendered.Parameters.Select(parameter => parameter.Value));
    }
}
