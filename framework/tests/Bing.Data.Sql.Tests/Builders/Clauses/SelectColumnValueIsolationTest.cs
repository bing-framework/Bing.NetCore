using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Clauses;

/// <summary>投影别名变化通过替换列值实现，不能修改共享列值的其他草稿。</summary>
public sealed class SelectColumnValueIsolationTest
{
    [Fact]
    public void Clone_WhenInferredAliasChanges_ShouldKeepSourceColumns()
    {
        var source = new SelectClause(TestSqlBuilder.CreateTestClauseContext());
        source.Select<Sample>(row => row.Email, "Address");
        source.FreezeEntityAlias(typeof(Sample), "original");
        var clone = (SelectClause)source.Clone(TestSqlBuilder.CreateTestClauseContext());

        clone.RebindInferredAlias(typeof(Sample), "original", "next");

        Assert.Equal("Select [original].[Email] As [Address]", source.ToSql());
        Assert.Equal("Select [next].[Email] As [Address]", clone.ToSql());
        Assert.Equal(clone.ToSql(), clone.ToSql());
    }

    [Fact]
    public void RestoreFrom_WhenAliasChangesAfterRestore_ShouldKeepCandidateColumns()
    {
        var candidate = new SelectClause(TestSqlBuilder.CreateTestClauseContext());
        candidate.Select<Sample>(row => row.Email, "Address");
        candidate.FreezeEntityAlias(typeof(Sample), "candidate");
        var target = new SelectClause(TestSqlBuilder.CreateTestClauseContext());
        target.Select("Discarded");

        target.RestoreFrom(candidate);
        target.RebindInferredAlias(typeof(Sample), "candidate", "restored");

        Assert.Equal("Select [candidate].[Email] As [Address]", candidate.ToSql());
        Assert.Equal("Select [restored].[Email] As [Address]", target.ToSql());
        target.Clear();
        Assert.Equal("Select [candidate].[Email] As [Address]", candidate.ToSql());
        Assert.Equal("Select *", target.ToSql());
    }
}
