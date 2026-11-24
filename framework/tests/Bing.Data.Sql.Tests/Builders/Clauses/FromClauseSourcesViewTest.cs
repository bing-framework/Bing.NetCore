using System.Collections;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Clauses;

/// <summary>
/// 验证 From 多来源集合对外只读，并随所属子句状态变化。
/// </summary>
public sealed class FromClauseSourcesViewTest
{
    /// <summary>
    /// 已发布的来源视图应只读、引用稳定，并跟随 From 与 Clear 更新。
    /// </summary>
    [Fact]
    public void Sources_WhenFromAndClearAreApplied_ShouldExposeStableReadOnlyLiveView()
    {
        var clause = new FromClause(TestSqlBuilder.CreateTestClauseContext());
        var multiSource = (ISqlMultiSourceFromClause)clause;
        var sources = multiSource.Sources;

        clause.From<Sample>("s");

        Assert.Same(sources, multiSource.Sources);
        AssertReadOnly(sources);
        Assert.Single(sources);
        Assert.Equal("From [Sample] As [s]", clause.ToSql());

        multiSource.AppendRoot(typeof(Sample2), "s2");

        Assert.Same(sources, multiSource.Sources);
        Assert.Equal(2, sources.Count);
        Assert.Equal("From [Sample] As [s], [Sample2] As [s2]", clause.ToSql());

        clause.Clear();

        Assert.Same(sources, multiSource.Sources);
        Assert.Empty(sources);
        Assert.Null(clause.ToSql());
    }

    /// <summary>
    /// checkpoint 回滚后，当前 From 子句应重新暴露完整且只读的多来源状态。
    /// </summary>
    [Fact]
    public void Sources_WhenMutationCheckpointRollsBack_ShouldExposeRestoredCurrentClause()
    {
        var builder = new SqlServerBuilder().Select("Id").From("Root");
        var initialFrom = (ISqlMultiSourceFromClause)builder.FromClause;
        initialFrom.AppendRoot(typeof(Sample), "s");

        using (builder.BeginMutationCheckpoint())
        {
            builder.ClearFrom();
            builder.From("Discarded", "d");
        }

        var restoredFrom = (ISqlMultiSourceFromClause)builder.FromClause;

        AssertReadOnly(restoredFrom.Sources);
        Assert.Equal(new[] { "source_0", "source_1" },
            restoredFrom.Sources.Select(source => source.SourceId));
        Assert.Equal("From [Root], [Sample] As [s]", restoredFrom.ToSql());
        Assert.Equal("Select [Id] \r\nFrom [Root], [Sample] As [s]", builder.ToSql());
    }

    private static void AssertReadOnly(IReadOnlyList<TableSource> sources)
    {
        if (sources is IList<TableSource> genericList)
        {
            Assert.True(genericList.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => genericList.Clear());
        }

        if (sources is IList list)
        {
            Assert.True(list.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => list.Clear());
        }
    }
}
