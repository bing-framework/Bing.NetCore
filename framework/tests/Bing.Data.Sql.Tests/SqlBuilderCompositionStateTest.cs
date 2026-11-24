using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>组合集合仅暴露只读视图，修改继续通过现有 Fluent 操作完成。</summary>
public sealed class SqlBuilderCompositionStateTest
{
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("postgresql")]
    [InlineData("sqlite")]
    public void CompositionViews_ShouldRejectCollectionMutation(string provider)
    {
        SqlBuilderBase builder = provider switch
        {
            "mysql" => new MySqlBuilder(),
            "postgresql" => new PostgreSqlBuilder(),
            "sqlite" => new SqliteBuilder(),
            _ => new SqlServerBuilder()
        };
        var ctes = Assert.IsAssignableFrom<ICollection<BuilderItem>>(((ICteAccessor)builder).CteItems);
        var unions = Assert.IsAssignableFrom<ICollection<BuilderItem>>(((IUnionAccessor)builder).UnionItems);

        Assert.True(ctes.IsReadOnly);
        Assert.True(unions.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => ctes.Add(new BuilderItem("child", Child())));
        Assert.Throws<NotSupportedException>(() => unions.Clear());
        Assert.Empty(builder.CteItems);
        Assert.False(builder.IsUnion);
    }

    /// <summary>持有只读视图的诊断调用者在 Clear 和重新追加后仍看到当前草稿。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompositionView_ShouldFollowClearAndSubsequentAppend(bool cte)
    {
        var builder = Root();
        Compose(builder, cte, "first");
        IReadOnlyList<BuilderItem> view = cte ? builder.CteItems : builder.UnionItems;

        if (cte) builder.ClearCte(); else builder.ClearUnionBuilders();
        Assert.Empty(view);
        Compose(builder, cte, "second");

        Assert.Same(view, cte ? builder.CteItems : builder.UnionItems);
        Assert.Equal(cte ? "second" : "Union All", Assert.Single(view).Name);
        Assert.Equal(Expected(cte, "second"), builder.ToSql());
    }

    /// <summary>失败的原子变更同时恢复集合内容及调用者已经持有的只读视图。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompositionRollback_ShouldRestorePublishedView(bool cte)
    {
        var builder = Root();
        Compose(builder, cte, "first");
        IReadOnlyList<BuilderItem> view = cte ? builder.CteItems : builder.UnionItems;

        using (var checkpoint = builder.BeginMutationCheckpoint())
        {
            Assert.NotNull(checkpoint);
            if (cte) builder.ClearCte(); else builder.ClearUnionBuilders();
            Compose(builder, cte, "discarded");
        }

        Assert.Same(view, cte ? builder.CteItems : builder.UnionItems);
        Assert.Single(view);
        Assert.Equal(cte ? "first" : "Union All", view[0].Name);
        Assert.Equal("Select [Id] \r\nFrom [first]", view[0].Builder.ToSql());
        Assert.Equal(Expected(cte, "first"), builder.ToSql());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FluentComposition_AfterPreview_ShouldRefreshQuerySql(bool cte)
    {
        var query = new SqlQuery(new Mock<ISqlQueryPlanExecutor>().Object, Root());
        Assert.Equal("Select [Id] \r\nFrom [Rows]", query.ToSql());

        if (cte) query.With("first", Child("first")); else query.UnionAll(Child("first"));

        Assert.Equal(Expected(cte, "first"), query.ToSql());
        Assert.Equal(Expected(cte, "first"), query.GetPlan().CommandText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Clone_WhenCompositionChanges_ShouldKeepSourceViewAndSql(bool cte)
    {
        var builder = Root();
        Compose(builder, cte, "first");
        IReadOnlyList<BuilderItem> sourceView = cte ? builder.CteItems : builder.UnionItems;
        var clone = (SqlServerBuilder)builder.Clone();

        if (cte) clone.ClearCte(); else clone.ClearUnionBuilders();
        Compose(clone, cte, "second");

        Assert.NotSame(sourceView, cte ? clone.CteItems : clone.UnionItems);
        Assert.Single(sourceView);
        Assert.Equal(Expected(cte, "first"), builder.ToSql());
        Assert.Equal(Expected(cte, "second"), clone.ToSql());
    }

    /// <summary>访问器是扩展入口，空项不能进入集合；普通 Fluent 写法仍负责子查询克隆。</summary>
    [Fact]
    public void Accessors_ShouldAppendPreparedItemsAndRejectNull()
    {
        var builder = Root();
        ICteAccessor ctes = builder;
        IUnionAccessor unions = builder;
        var cte = new BuilderItem("shared", Child());
        var union = new BuilderItem("Union", Child());

        ctes.AddCte(cte);
        unions.AddUnion(union);

        Assert.Same(cte, Assert.Single(ctes.CteItems));
        Assert.Same(union, Assert.Single(unions.UnionItems));
        Assert.Throws<ArgumentNullException>(() => ctes.AddCte(null));
        Assert.Throws<ArgumentNullException>(() => unions.AddUnion(null));
        Assert.Single(ctes.CteItems);
        Assert.Single(unions.UnionItems);
    }

    /// <summary>第三方访问器只需实现读取与追加，Fluent 不再要求它暴露可变 List。</summary>
    [Fact]
    public void FluentComposition_ShouldUseThirdPartyAccessorAppendContracts()
    {
        var items = new List<BuilderItem>();
        var source = new Mock<ISqlBuilder>();
        var ctes = source.As<ICteAccessor>();
        ctes.SetupGet(accessor => accessor.CteItems).Returns(items.AsReadOnly());
        ctes.Setup(accessor => accessor.AddCte(It.IsAny<BuilderItem>())).Callback<BuilderItem>(items.Add);
        var unions = source.As<IUnionAccessor>();
        unions.SetupGet(accessor => accessor.UnionItems).Returns(items.AsReadOnly());
        unions.Setup(accessor => accessor.AddUnion(It.IsAny<BuilderItem>())).Callback<BuilderItem>(items.Add);
        var child = Child();

        source.Object.With("shared", child).UnionAll(child);

        Assert.Equal(new[] { "shared", "Union All" }, items.Select(item => item.Name));
        Assert.All(items, item => Assert.NotSame(child, item.Builder));
        Assert.All(items, item => Assert.Equal("Select [Id] \r\nFrom [Child]", item.Builder.ToSql()));
        ctes.Verify(accessor => accessor.AddCte(It.IsAny<BuilderItem>()), Times.Once);
        unions.Verify(accessor => accessor.AddUnion(It.IsAny<BuilderItem>()), Times.Once);
    }

    private static SqlServerBuilder Root()
    {
        var builder = new SqlServerBuilder();
        builder.Select("Id").From("Rows");
        return builder;
    }

    private static ISqlBuilder Child(string table = "Child") =>
        new SqlServerBuilder().Select("Id").From(table);

    private static void Compose(SqlServerBuilder builder, bool cte, string table)
    {
        if (cte) builder.With(table, Child(table)); else builder.UnionAll(Child(table));
    }

    private static string Expected(bool cte, string table) => cte
        ? $"With [{table}] \r\nAs (Select [Id] \r\nFrom [{table}])\r\nSelect [Id] \r\nFrom [Rows]"
        : $"(Select [Id] \r\nFrom [Rows] \r\n) \r\nUnion All \r\n(Select [Id] \r\nFrom [{table}] \r\n)";
}
