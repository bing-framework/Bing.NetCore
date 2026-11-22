using System.Data;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>共享 CTE 定义在创建时捕获一次片段，后续只消费独立快照。</summary>
public sealed class SqlCteDefinitionTest
{
    /// <summary>SQL、逻辑参数键和可变参数值均在定义创建时固定。</summary>
    [Fact]
    public void Constructor_ShouldCaptureSqlParametersAndKeysBeforeSourceChanges()
    {
        var source = new MutableFragment();
        var definition = new SqlCteDefinition(" items ", source);
        source.Sql = "select @id + 100 as Id";
        source.Value[0] = 99;

        var rendered = definition.Render(new SqlQueryScope("q"));
        Assert.Equal("items", definition.Name);
        Assert.NotSame(source, definition.Fragment);
        Assert.Equal("items AS (select @q_id as Id)", rendered.Sql);
        Assert.Equal(SqlQueryFragmentKind.Cte, rendered.Kind);
        var parameter = Assert.Single(rendered.Parameters);
        Assert.Equal("q_id", parameter.Name);
        Assert.Equal(new[] { 7, 8 }, Assert.IsType<int[]>(parameter.Value));
        Assert.Equal(DbType.Int32, parameter.DbType);
        Assert.True(parameter.IsProviderValue);
        Assert.Equal(new SqlParameterKey("tenant", "ids"), Assert.Single(rendered.Slots));
        Assert.Equal(1, source.FreezeCalls);
    }

    /// <summary>定义不再依赖原片段；返回给调用方的参数数组不能改变后续消费。</summary>
    [Fact]
    public void FragmentAndRender_ShouldNotReadSourceAgainOrExposeMutableValues()
    {
        var source = new MutableFragment();
        var definition = new SqlCteDefinition("items", source);
        source.Fail = true;

        var exposed = definition.Fragment.Freeze();
        Assert.IsType<int[]>(Assert.Single(exposed.Parameters).Value)[0] = 99;
        Assert.Equal("select @id as Id", exposed.Sql);
        foreach (var scope in new[] { new SqlQueryScope("first"), new SqlQueryScope("second") })
        {
            var rendered = definition.Render(scope);
            Assert.Equal($"items AS (select @{scope.Name}_id as Id)", rendered.Sql);
            Assert.Equal(new[] { 7, 8 }, Assert.IsType<int[]>(Assert.Single(rendered.Parameters).Value));
        }
        Assert.Equal(1, source.FreezeCalls);
    }

    /// <summary>源片段冻结失败在定义创建时暴露，修复源后可以重新创建定义。</summary>
    [Fact]
    public void Constructor_WhenFreezeFails_ShouldThrowBeforeDefinitionCanBeShared()
    {
        var source = new MutableFragment { Fail = true };
        Assert.Throws<InvalidOperationException>(() => new SqlCteDefinition("items", source));
        source.Fail = false;
        var definition = new SqlCteDefinition("items", source);
        Assert.Equal("items AS (select @id as Id)", definition.Render().Sql);
        Assert.Equal(2, source.FreezeCalls);
    }

    /// <summary>With 消费公开 Fragment 时不能取得创建定义后的源片段版本。</summary>
    [Fact]
    public void With_WhenDefinitionIsShared_ShouldKeepEachBuilderAndDefinitionIndependent()
    {
        var source = new MutableFragment();
        var definition = new SqlCteDefinition("items", source);
        var root = new SqlQueryScope("root");
        var shared = root.Add(definition);
        source.Sql = "select @id + 100 as Id";
        var first = new TestSqlBuilder().Select("Id").From("items")
            .With(shared.Ctes[0].Name, shared.Ctes[0].Fragment);
        var second = new TestSqlBuilder().Select("Id").From("items")
            .With(shared.Ctes[0].Name, shared.Ctes[0].Fragment);
        first.Where("Id", 9);

        Assert.Empty(root.Ctes);
        Assert.Same(definition, Assert.Single(shared.CreateChild("reader").Ctes));
        Assert.Equal("With [items] \r\nAs (select @id as Id)\r\nSelect [Id] \r\nFrom [items] \r\nWhere [Id]=@_p_0", first.ToSql());
        Assert.Equal("With [items] \r\nAs (select @id as Id)\r\nSelect [Id] \r\nFrom [items]", second.ToSql());
        Assert.Equal(new[] { 7, 8 }, Assert.IsType<int[]>(first.GetParam("@id")));
        Assert.Equal(9, first.GetParam("@_p_0"));
        Assert.Equal(new[] { 7, 8 }, Assert.IsType<int[]>(second.GetParam("@id")));
        Assert.Equal("items AS (select @id as Id)", definition.Render().Sql);
        Assert.Equal(1, source.FreezeCalls);
    }

    /// <summary>不可变定义可跨线程渲染到独立作用域，各请求的参数副本互不污染。</summary>
    [Fact]
    public async Task Scope_WhenRenderedConcurrently_ShouldUseCapturedDefinitionAndIndependentParameters()
    {
        var source = new MutableFragment();
        var shared = new SqlQueryScope("shared").Add(new SqlCteDefinition("items", source));
        source.Sql = "select @id + 100 as Id";
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(index => Task.Run(() =>
        {
            var child = shared.CreateChild(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var rendered = child.Ctes[0].Render(child);
            var exposed = Assert.IsType<int[]>(Assert.Single(rendered.Parameters).Value);
            exposed[0] = index;
            Assert.Equal(new[] { 7, 8 }, Assert.IsType<int[]>(Assert.Single(rendered.Parameters).Value));
            Assert.Equal($"items AS (select @shared_{index}_id as Id)", rendered.Sql);
            return Assert.Single(rendered.Parameters).Name;
        })));
        Assert.Equal(32, results.Distinct().Count());
        Assert.Equal(1, source.FreezeCalls);
    }

    private sealed class MutableFragment : ISqlQueryFragment
    {
        internal string Sql = "select @id as Id";
        internal int[] Value = { 7, 8 };
        internal bool Fail;
        internal int FreezeCalls;

        public SqlQueryFragmentSnapshot Freeze()
        {
            Interlocked.Increment(ref FreezeCalls);
            if (Fail) throw new InvalidOperationException("source freeze failed");
            return new SqlQueryFragment(Sql, new[] { new SqlParam("@id", Value, DbType.Int32) { IsProviderValue = true } },
                    SqlQueryFragmentKind.Select)
                .WithParameterKey("id", new SqlParameterKey("tenant", "ids")).Freeze();
        }
    }
}
