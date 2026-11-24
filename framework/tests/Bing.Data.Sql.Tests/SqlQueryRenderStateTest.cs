using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Tests;

/// <summary>渲染工作区与草稿变更检查点的直接职责测试。</summary>
public sealed class SqlQueryRenderStateTest
{
    [Fact]
    public void NestedRender_ShouldCloneEachOwnedChildOnceAndKeepSourceReusable()
    {
        var leaf = new CountedChild();
        leaf.Select("Id").From("Leaf");
        var child = new CountedChild();
        child.Select("Id").From("Middle").With("leaf", leaf);
        var root = new SqlServerBuilder();
        root.Select("Id").From("Root").With("nested", child);
        const string expected = "With [nested] \r\nAs (With [leaf] \r\nAs (Select [Id] \r\nFrom [Leaf])\r\nSelect [Id] \r\nFrom [Middle])\r\nSelect [Id] \r\nFrom [Root]";

        for (var iteration = 0; iteration < 2; iteration++)
        {
            CountedChild.Clones = 0;
            Assert.Equal(expected, root.ToSql());
            Assert.Equal(2, CountedChild.Clones);
            Assert.Empty(root.GetParams());
        }
        Assert.Equal("Select [Id] \r\nFrom [Leaf]", leaf.ToSql());
    }

    private sealed class CountedChild : SqlServerBuilder
    {
        internal static int Clones;
        public override ISqlBuilder Clone() { Clones++; return base.Clone(); }
        protected override SqlBuilderBase CreateBuilder(Bing.Data.Sql.Builders.Params.IParameterManager parameters) => new CountedChild();
    }

    private sealed class SelfCloningBuilder : SqlServerBuilder
    {
        internal int CloneCalls;
        public override ISqlBuilder Clone() { CloneCalls++; return this; }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(32)]
    [InlineData(128)]
    public void WideNestedRender_ShouldPreserveCompleteSqlAndCloneEachChildOnce(int width)
    {
        var root = new SqlServerBuilder(new SqlBuilderServices(filters: new ISqlFilter[] { new NoOpFilter() }));
        root.Select("Id").From("Root");
        var definitions = new string[width];

        for (var index = 0; index < width; index++)
        {
            var name = $"cte_{index:D3}";
            var table = $"Child_{index:D3}";
            var child = new CountedChild().Select("Id").From(table);
            root.With(name, child);
            definitions[index] = $"[{name}] \r\nAs (Select [Id] \r\nFrom [{table}])";
        }

        var expected = $"With {string.Join(",\r\n", definitions)}\r\nSelect [Id] \r\nFrom [Root]";
        for (var iteration = 0; iteration < 2; iteration++)
        {
            CountedChild.Clones = 0;
            Assert.Equal(expected, root.ToSql());
            Assert.Equal(width, CountedChild.Clones);
        }
    }

    [Fact]
    public void RenderSnapshot_ShouldCloneLateAddedBuilderAndKeepSourceReusable()
    {
        var root = new SqlServerBuilder(new SqlBuilderServices(filters: new ISqlFilter[] { new NoOpFilter() }));
        root.Select("Id").From("Root");
        var initial = new CountedChild().Select("Id").From("Initial");
        root.With("initial", initial);

        var late = new CountedChild().Select("Id").From("Late");
        root.AddCte(new BuilderItem("late", late));
        const string expected = "With [initial] \r\nAs (Select [Id] \r\nFrom [Initial]),\r\n[late] \r\nAs (Select [Id] \r\nFrom [Late])\r\nSelect [Id] \r\nFrom [Root]";

        CountedChild.Clones = 0;
        Assert.Equal(expected, root.ToSql());
        Assert.Equal(2, CountedChild.Clones);
        Assert.Equal("Select [Id] \r\nFrom [Late]", late.ToSql());
    }

    [Fact]
    public void Clone_ShouldMarkOnlyTheNestedCloneRelationship()
    {
        var root = new SqlServerBuilder();
        root.Select("Id").From("Root").With("initial",
            new SqlServerBuilder().Select("Id").From("Initial"));

        var clone = (SqlServerBuilder)root.Clone();
        clone.AddCte(new BuilderItem("late", new SqlServerBuilder().Select("Id").From("Late")));

        Assert.False(root.CteItems[0].IsOwnedNestedClone);
        Assert.True(clone.CteItems[0].IsOwnedNestedClone);
        Assert.False(clone.CteItems[1].IsOwnedNestedClone);
    }

    [Fact]
    public void CloneReturningSelf_ShouldKeepExternalCloneBoundaryDuringSnapshotRender()
    {
        var child = new SelfCloningBuilder().Select("Id").From("Child");
        var root = new SqlServerBuilder(new SqlBuilderServices(filters: new ISqlFilter[] { new NoOpFilter() }));
        root.Select("Id").From("Root").With("self", child);

        var clone = (SqlServerBuilder)root.Clone();
        Assert.False(clone.CteItems[0].IsOwnedNestedClone);

        child.CloneCalls = 0;
        Assert.Equal("With [self] \r\nAs (Select [Id] \r\nFrom [Child])\r\nSelect [Id] \r\nFrom [Root]", root.ToSql());
        Assert.Equal(2, child.CloneCalls);
    }

    private sealed class NoOpFilter : ISqlFilter
    {
        public void Filter(SqlFilterContext context) { }
    }

    [Fact]
    public void NestedRender_WithCollidingParameters_ShouldKeepRepeatedSqlAndSourceParameters()
    {
        var leaf = new SqlServerBuilder().Select("Id").From("Leaf").Where("LeafId", 7);
        var child = new SqlServerBuilder().Select("Id").From("Middle").Where("MiddleId", 6).With("leaf", leaf);
        var root = new SqlServerBuilder().Select("Id").From("Root").Where("RootId", 5).With("nested", child);
        const string expected = "With [nested] \r\nAs (With [leaf] \r\nAs (Select [Id] \r\nFrom [Leaf] \r\nWhere [LeafId]=@_p_2)\r\nSelect [Id] \r\nFrom [Middle] \r\nWhere [MiddleId]=@_p_1)\r\nSelect [Id] \r\nFrom [Root] \r\nWhere [RootId]=@_p_0";
        Assert.Equal(expected, root.ToSql());
        Assert.Equal(new object[] { 5, 6, 7 }, root.GetParams().Values);
        Assert.Equal(expected, root.ToSql());
        Assert.Equal(new object[] { 5, 6, 7 }, root.GetParams().Values);
        Assert.Equal(new object[] { 6 }, child.GetParams().Values);
        Assert.Equal(new object[] { 7 }, leaf.GetParams().Values);
    }

    [Fact]
    public void MutationCheckpoint_ShouldCopyOnlyTouchedClausesAndKeepNestedBuilder()
    {
        var builder = new SqlServerBuilder();
        builder.Select("Id").From("Rows").Where("Id", 1);
        var select = builder.SelectClause;
        var where = builder.WhereClause;
        var child = new SqlServerBuilder().Select("Id").From("Other");
        builder.Union(child);
        var nested = builder.UnionItems.Single().Builder;

        using (var checkpoint = builder.BeginMutationCheckpoint())
        {
            Assert.NotNull(checkpoint);
            builder.Where("Enabled", true);
            checkpoint.Complete();
        }

        Assert.Same(select, builder.SelectClause);
        Assert.NotSame(where, builder.WhereClause);
        Assert.Same(nested, builder.UnionItems.Single().Builder);
        Assert.Equal("Where [Id]=@_p_0 And [Enabled]=@_p_1", builder.WhereClause.ToSql());
        Assert.Equal(new object[] { 1, true }, builder.GetParams().Values);
    }

    [Fact]
    public void MutationCheckpoint_WhenUncompleted_ShouldRestoreClausesParametersPagingAndAliases()
    {
        var builder = new SqlServerBuilder();
        builder.Select("Id").From("Rows").Where("Id", 1);
        using (builder.BeginMutationCheckpoint())
        {
            builder.ClearFrom();
            builder.From("Other", "o").Where("Name", "discard").Take(5);
        }
        Assert.Equal("Select [Id] \r\nFrom [Rows] \r\nWhere [Id]=@_p_0", builder.ToSql());
        Assert.Equal(new object[] { 1 }, builder.GetParams().Values);
        Assert.Equal(20, builder.Pager.PageSize);
    }

    [Fact]
    public void DerivedBuilder_ShouldRetainFullCloneContract()
    {
        Assert.Null(new ExtendedBuilder().BeginMutationCheckpoint());
    }

    [Theory]
    [InlineData("SqlServer", "[", "]", "@")]
    [InlineData("MySql", "`", "`", "@")]
    [InlineData("PostgreSql", "\"", "\"", "@")]
    [InlineData("Sqlite", "`", "`", "@")]
    public void BuiltInProvider_ShouldEnableCheckpointAndRollback(string provider, string open, string close, string prefix)
    {
        SqlBuilderBase builder = provider switch
        {
            "MySql" => new MySqlBuilder(),
            "PostgreSql" => new PostgreSqlBuilder(),
            "Sqlite" => new SqliteBuilder(),
            _ => new SqlServerBuilder()
        };
        builder.Select("Id").From("Rows").Where("Id", 1);
        using (var checkpoint = builder.BeginMutationCheckpoint())
        {
            Assert.NotNull(checkpoint);
            builder.Where("Id", 2);
        }
        Assert.Equal($"Select {open}Id{close} \r\nFrom {open}Rows{close} \r\nWhere {open}Id{close}={prefix}_p_0", builder.ToSql());
        Assert.Equal(new object[] { 1 }, builder.GetParams().Values);
    }

    [Fact]
    public void FailedRender_ShouldDiscardCandidateWithoutMutatingPublishedParameterManager()
    {
        var builder = new FailingRenderBuilder();
        builder.Select("Id").From("Rows").Where("Id", 1);
        builder.With("valid", new SqlServerBuilder().Select("Id").From("Other").Where("Id", 2));
        var published = builder.ParameterManager;
        var output = new StringBuilder("prefix");

        Assert.Throws<InvalidOperationException>(() => builder.AppendTo(output));
        Assert.Same(published, builder.ParameterManager);
        Assert.Equal("prefix", output.ToString());
        Assert.Equal(new object[] { 1 }, published.GetParams().Values);
        builder.Fail = false;
        builder.ClearCte();
        Assert.Equal("Select [Id] \r\nFrom [Rows] \r\nWhere [Id]=@_p_0", builder.ToSql());
        Assert.Equal(new object[] { 1 }, builder.GetParams().Values);
    }

    private sealed class ExtendedBuilder : SqlServerBuilder { }

    [Fact]
    public void FailedPagingRender_ShouldDiscardGeneratedOffsetNameAndAllowRetry()
    {
        var builder = new FailingPagingBuilder();
        builder.Select("Id").From("Rows").OrderBy("Id").Take(5);
        var published = builder.ParameterManager;
        Assert.Throws<InvalidOperationException>(() => builder.ToSql());
        Assert.Same(published, builder.ParameterManager);
        Assert.Equal(new object[] { 5 }, builder.GetParams().Values);
        builder.Fail = false;
        Assert.Equal("Select `Id` \r\nFrom `Rows` \r\nOrder By `Id` \r\nLimit @_p_0 OFFSET @_p_1", builder.ToSql());
        Assert.Equal(new object[] { 5, 0 }, builder.GetParams().Values);
        Assert.Equal("Select `Id` \r\nFrom `Rows` \r\nOrder By `Id` \r\nLimit @_p_0 OFFSET @_p_1", builder.ToSql());
    }

    private sealed class FailingPagingBuilder : SqliteBuilder
    {
        public bool Fail { get; set; } = true;
        protected override void CreateSql(StringBuilder result)
        {
            base.CreateSql(result);
            if (Fail) throw new InvalidOperationException("after-pagination");
        }
    }

    private sealed class FailingRenderBuilder : SqlServerBuilder
    {
        public bool Fail { get; set; } = true;
        protected override void CreateSql(StringBuilder result)
        {
            base.CreateSql(result);
            if (Fail) throw new InvalidOperationException("render-failure-after-parameter-merge");
        }
    }
}
