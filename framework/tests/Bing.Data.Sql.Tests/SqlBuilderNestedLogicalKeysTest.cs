using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>嵌套 Builder 组合后仍保留逻辑参数键的契约测试。</summary>
public sealed class SqlBuilderNestedLogicalKeysTest
{
    /// <summary>经 From 嵌套的 Builder 应保留冻结片段的逻辑键及重复渲染状态。</summary>
    [Fact]
    public void From_WhenNestedBuilderContainsKeyedFragment_ShouldPreserveKeyOnRepeatedRender()
    {
        var key = new SqlParameterKey("nested", "id");
        var child = new TestSqlBuilder().Select("*")
            .From(Fragment("select @id as Id", "id", 7, key), "leaf");
        var root = new TestSqlBuilder().Select("*").From(child, "outer");
        const string expected = "Select * \r\nFrom (Select * \r\nFrom (select @id as Id) As [leaf]) As [outer]";

        var first = root.ToTemplate();
        var second = root.ToTemplate();

        Assert.Equal(expected, first.Sql);
        Assert.Equal(expected, second.Sql);
        Assert.Equal(key, Assert.Single(first.Slots));
        Assert.Equal(key, Assert.Single(second.Slots));
        Assert.Equal(7, Assert.Single(first.Parameters).Value);
    }

    /// <summary>物理名称和值相同但逻辑键不同的直接与嵌套 CTE 参数应保持独立。</summary>
    [Fact]
    public void Cte_WhenNestedBuilderParameterCollidesWithDifferentKey_ShouldKeepBindingsIndependent()
    {
        var parentKey = new SqlParameterKey("parent", "id");
        var nestedKey = new SqlParameterKey("nested", "id");
        var parent = Fragment("select @id as Id", "id", 7, parentKey);
        var nested = new TestSqlBuilder().Select("*")
            .From(Fragment("select @id as Id", "id", 7, nestedKey), "leaf");
        var root = new TestSqlBuilder().Select("*").From("root")
            .With("parent", parent)
            .With("nested", nested);
        const string expected = "With [parent] \r\nAs (select @id as Id),\r\n[nested] \r\nAs (Select * \r\nFrom (select @_p_0 as Id) As [leaf])\r\nSelect * \r\nFrom [root]";

        var template = root.ToTemplate();
        var shape = new SqlQueryShapeTemplate(template.CreateFragment());
        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object>
        {
            [parentKey] = 11,
            [nestedKey] = 19
        });

        Assert.Equal(expected, template.Sql);
        Assert.Equal(new[] { parentKey, nestedKey }, template.Slots);
        Assert.Equal(expected, rendered.Sql);
        Assert.Equal(new object[] { 11, 19 }, rendered.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new[] { parentKey, nestedKey }, rendered.Slots);
    }

    /// <summary>嵌套 CTE 中相同逻辑键的不同物理名称应合并为一次请求绑定。</summary>
    [Fact]
    public void Cte_WhenNestedBuilderSharesLogicalKey_ShouldBindOneRequestValue()
    {
        var key = new SqlParameterKey("request", "tenant");
        var parent = Fragment("select @tenant as Id", "tenant", 1, key);
        var nested = new TestSqlBuilder().Select("*")
            .From(Fragment("select @owner as Id", "owner", 2, key), "leaf");
        var root = new TestSqlBuilder().Select("*").From("root")
            .With("parent", parent)
            .With("nested", nested);
        const string expected = "With [parent] \r\nAs (select @tenant as Id),\r\n[nested] \r\nAs (Select * \r\nFrom (select @owner as Id) As [leaf])\r\nSelect * \r\nFrom [root]";

        var template = root.ToTemplate();
        var shape = new SqlQueryShapeTemplate(template.CreateFragment());
        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 23 });

        Assert.Equal(expected, template.Sql);
        Assert.Equal(new[] { key, key }, template.Slots);
        Assert.Equal("With [parent] \r\nAs (select @tenant as Id),\r\n[nested] \r\nAs (Select * \r\nFrom (select @tenant as Id) As [leaf])\r\nSelect * \r\nFrom [root]", rendered.Sql);
        Assert.Equal(23, Assert.Single(rendered.Parameters).Value);
        Assert.Equal(key, Assert.Single(rendered.Slots));
    }

    /// <summary>嵌套 Builder 作为 Union 分支时，Clone 和重复渲染应保留逻辑键。</summary>
    [Fact]
    public void Union_WhenNestedBuilderContainsKeyedFragment_ShouldPreserveKeyAfterClone()
    {
        var key = new SqlParameterKey("union", "id");
        var branch = new TestSqlBuilder().Select("*")
            .From(Fragment("select @id as Id", "id", 7, key), "leaf");
        var root = new TestSqlBuilder().Select("*").From("root").UnionAll(branch);
        const string expected = "(Select * \r\nFrom [root] \r\n) \r\nUnion All \r\n(Select * \r\nFrom (select @id as Id) As [leaf] \r\n)";

        var clone = root.Clone();
        var first = root.ToTemplate();
        var second = clone.ToTemplate();

        Assert.Equal(expected, first.Sql);
        Assert.Equal(expected, second.Sql);
        Assert.Equal(key, Assert.Single(first.Slots));
        Assert.Equal(key, Assert.Single(second.Slots));
        Assert.Equal(7, Assert.Single(second.Parameters).Value);
    }

    /// <summary>两种组合顺序下，带键与旧式同名参数都应独立绑定。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Cte_WhenNestedAndDirectParametersMixKeyedAndUnkeyed_ShouldKeepBoth(bool nestedIsKeyed)
    {
        var key = new SqlParameterKey("request", "id");
        var keyed = Fragment("select @id as Id", "id", 7, key);
        var unkeyed = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 7, DbType.Int32) });
        var child = new TestSqlBuilder().Select("*").From(nestedIsKeyed ? keyed : unkeyed, "leaf");
        var root = new TestSqlBuilder().Select("*").From("root")
            .With("first", nestedIsKeyed ? unkeyed : keyed).With("second", child);
        var template = root.ToTemplate();
        const string expected = "With [first] \r\nAs (select @id as Id),\r\n[second] \r\nAs (Select * \r\nFrom (select @_p_0 as Id) As [leaf])\r\nSelect * \r\nFrom [root]";

        Assert.Equal(expected, template.Sql);
        Assert.Equal(nestedIsKeyed ? new[] { new SqlParameterKey("id"), key } :
            new[] { key, new SqlParameterKey("_p_0") }, template.Slots);
        var rendered = new SqlQueryShapeTemplate(template.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [key] = 19 });
        Assert.Equal(expected, rendered.Sql);
        Assert.Equal(nestedIsKeyed ? new object[] { 7, 19 } : new object[] { 19, 7 },
            rendered.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>多层嵌套仍使用最内层冻结片段声明的逻辑键。</summary>
    [Fact]
    public void From_WhenBuilderIsNestedTwice_ShouldPreserveLogicalKey()
    {
        var key = new SqlParameterKey("leaf", "id");
        var leaf = new TestSqlBuilder().Select("*").From(Fragment("select @id as Id", "id", 7, key), "value");
        var middle = new TestSqlBuilder().Select("*").From(leaf, "middle");
        var root = new TestSqlBuilder().Select("*").From(middle, "outer");
        var template = root.ToTemplate();

        Assert.Equal("Select * \r\nFrom (Select * \r\nFrom (Select * \r\nFrom (select @id as Id) As [value]) As [middle]) As [outer]", template.Sql);
        Assert.Equal(key, Assert.Single(template.Slots));
        var rendered = new SqlQueryShapeTemplate(template.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [key] = 19 });
        Assert.Equal(19, Assert.Single(rendered.Parameters).Value);
    }

    /// <summary>同键同名但默认值不同，模板应要求显式提供共享值。</summary>
    [Fact]
    public void Cte_WhenNestedBuilderSharesKeyWithDifferentDefault_ShouldRequireRequestValue()
    {
        var key = new SqlParameterKey("request", "id");
        var parent = Fragment("select @id as Id", "id", 1, key);
        var child = new TestSqlBuilder().Select("*").From(Fragment("select @id as Id", "id", 2, key), "leaf");
        var root = new TestSqlBuilder().Select("*").From("root").With("first", parent).With("second", child);
        var shape = new SqlQueryShapeTemplate(root.ToTemplate().CreateFragment());

        Assert.Throws<InvalidOperationException>(() => shape.RenderWithKeys(0, null));
        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 19 });
        Assert.Equal("With [first] \r\nAs (select @id as Id),\r\n[second] \r\nAs (Select * \r\nFrom (select @id as Id) As [leaf])\r\nSelect * \r\nFrom [root]", rendered.Sql);
        Assert.Equal(19, Assert.Single(rendered.Parameters).Value);
    }

    /// <summary>嵌套 SQL 的引号转义语义不明确时，改名失败必须回滚候选参数。</summary>
    [Fact]
    public void Union_WhenNestedRenamingHasAmbiguousEscapes_ShouldRejectAndRollback()
    {
        var key = new SqlParameterKey("first", "id");
        var first = Fragment("select @id as Id", "id", 7, key);
        var ambiguous = Fragment("select 'it\\'s @id' as Text, @id as Id", "id", 7,
            new SqlParameterKey("second", "id"));
        var child = new TestSqlBuilder().Select("*").From(ambiguous, "leaf");
        var root = new TestSqlBuilder().Select("*").From("first").With("first", first).UnionAll(child);

        Assert.Throws<InvalidOperationException>(() => root.ToSql());
        Assert.Empty(root.GetParams());
        root.ClearUnionBuilders().AddParam("id", 7);
        var recovered = root.ToTemplate();
        Assert.Equal("With [first] \r\nAs (select @_p_0 as Id)\r\nSelect * \r\nFrom [first]", recovered.Sql);
        Assert.Equal(new[] { new SqlParameterKey("id"), key }, recovered.Slots);
    }

    /// <summary>Where In 子查询也应隔离同名的不同逻辑键。</summary>
    [Fact]
    public void In_WhenNestedBuilderHasDifferentKey_ShouldPreserveBothBindings()
    {
        var outerKey = new SqlParameterKey("outer", "id");
        var innerKey = new SqlParameterKey("inner", "id");
        var outer = Fragment("select @id as Id", "id", 7, outerKey);
        var inner = Fragment("select @id as Id", "id", 7, innerKey);
        var child = new TestSqlBuilder().Select("Id").From(inner, "leaf");
        var root = new TestSqlBuilder().Select("*").From(outer, "source").In("source.Id", child);

        var template = root.ToTemplate();
        Assert.Equal("Select * \r\nFrom (select @id as Id) As [source] \r\nWhere [source].[Id] In (Select [Id] \r\nFrom (select @_p_0 as Id) As [leaf])", template.Sql);
        Assert.Equal(new[] { outerKey, innerKey }, template.Slots);
        var rendered = new SqlQueryShapeTemplate(template.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [outerKey] = 11, [innerKey] = 19 });
        Assert.Equal(new object[] { 11, 19 }, rendered.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>只读复用同一嵌套 Builder 时，各查询独立绑定自己的请求值。</summary>
    [Fact]
    public void From_WhenNestedBuilderIsReusedConcurrently_ShouldKeepRequestsIndependent()
    {
        var key = new SqlParameterKey("request", "id");
        var child = new TestSqlBuilder().Select("*").From(Fragment("select @id as Id", "id", 7, key), "leaf");
        var results = new SqlQueryFragment[64];

        Parallel.For(0, results.Length, index =>
        {
            var root = new TestSqlBuilder().Select("*").From(child, "outer");
            var shape = new SqlQueryShapeTemplate(root.ToTemplate().CreateFragment());
            results[index] = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = index });
        });

        foreach (var (result, index) in results.Select((result, index) => (result, index)))
        {
            Assert.Equal("Select * \r\nFrom (Select * \r\nFrom (select @id as Id) As [leaf]) As [outer]", result.Sql);
            Assert.Equal(key, Assert.Single(result.Slots));
            Assert.Equal(index, Assert.Single(result.Parameters).Value);
        }
    }

    private static SqlQueryFragment Fragment(string sql, string name, int value, SqlParameterKey key) =>
        new SqlQueryFragment(sql, new[] { new SqlParam(name, value, DbType.Int32) })
            .WithParameterKey(name, key);
}
