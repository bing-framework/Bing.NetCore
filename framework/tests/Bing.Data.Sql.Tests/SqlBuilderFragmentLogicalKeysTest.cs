using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>Builder 组合片段后仍保留逻辑参数键的契约测试。</summary>
public sealed class SqlBuilderFragmentLogicalKeysTest
{
    /// <summary>派生表经模板冻结和参数作用域化后仍可按逻辑键覆盖。</summary>
    [Fact]
    public void From_WhenFragmentHasLogicalKey_ShouldPreserveKeyThroughScopedTemplate()
    {
        var key = new SqlParameterKey("request", "id");
        var fragment = new SqlQueryFragment("select @ID as Id", new[]
            { new SqlParam("id", 7, DbType.Int32) }).WithParameterKey("id", key);
        var builder = new TestSqlBuilder().Select("*").From(fragment, "items");
        var template = builder.ToTemplate(new SqlQueryScope("root"));
        var shape = new SqlQueryShapeTemplate(template.CreateFragment());

        Assert.Equal("Select * \r\nFrom (select @root_id as Id) As [items]", template.Sql);
        Assert.Equal(key, Assert.Single(template.CreateFragment().Slots));

        var first = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 11 });
        var second = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 19 });

        Assert.Equal("Select * \r\nFrom (select @root_id as Id) As [items]", first.Sql);
        Assert.Equal(first.Sql, second.Sql);
        Assert.Equal(11, Assert.Single(first.Parameters).Value);
        Assert.Equal(19, Assert.Single(second.Parameters).Value);
        Assert.Equal(key, Assert.Single(first.Slots));
    }

    /// <summary>CTE 和 Union 中同名同值但身份不同的参数必须可分别绑定。</summary>
    [Fact]
    public void CteAndUnion_WhenSameNameHasDifferentLogicalKeys_ShouldKeepIndependentBindings()
    {
        var cteKey = new SqlParameterKey("cte", "id");
        var unionKey = new SqlParameterKey("union", "id");
        var cte = new SqlQueryFragment("select @id as Id", new[]
            { new SqlParam("id", 7, DbType.Int32) }).WithParameterKey("id", cteKey);
        var union = new SqlQueryFragment("select @id as Id", new[]
            { new SqlParam("id", 7, DbType.Int32) }).WithParameterKey("id", unionKey);
        var builder = new TestSqlBuilder().Select("*").From("active_users").With("active_users", cte).UnionAll(union);
        var template = builder.ToTemplate();
        var shape = new SqlQueryShapeTemplate(template.CreateFragment());
        const string expected = "With [active_users] \r\nAs (select @id as Id)\r\n(Select * \r\nFrom [active_users] \r\n) \r\nUnion All \r\n(select @_p_0 as Id \r\n)";

        Assert.Equal(expected, template.Sql);
        Assert.Equal(new[] { cteKey, unionKey }, template.CreateFragment().Slots);

        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object>
        {
            [cteKey] = 11,
            [unionKey] = 19
        });

        Assert.Equal(expected, rendered.Sql);
        Assert.Equal(new object[] { 11, 19 }, rendered.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new[] { cteKey, unionKey }, rendered.Slots);
        Assert.Equal(expected, builder.ToSql());
        var clone = builder.Clone();
        Assert.Equal(expected, clone.ToSql());
        Assert.Equal(new[] { cteKey, unionKey }, clone.ToTemplate().Slots);
    }

    /// <summary>同一键的不同物理名称经 Builder 冻结后仍要求显式请求值并共享绑定。</summary>
    [Fact]
    public void CteAndUnion_WhenDifferentNamesShareLogicalKey_ShouldRequireAndReuseKeyBinding()
    {
        var key = new SqlParameterKey("request", "tenant");
        var cte = new SqlQueryFragment("select @tenant as Id", new[]
            { new SqlParam("tenant", 1, DbType.Int32) }).WithParameterKey("tenant", key);
        var union = new SqlQueryFragment("select :owner as Id", new[]
            { new SqlParam(":owner", 2, DbType.Int32) }).WithParameterKey(":owner", key);
        var builder = new TestSqlBuilder().Select("*").From("active_users").With("active_users", cte).UnionAll(union);
        var template = builder.ToTemplate();
        var shape = new SqlQueryShapeTemplate(template.CreateFragment());
        const string expected = "With [active_users] \r\nAs (select @tenant as Id)\r\n(Select * \r\nFrom [active_users] \r\n) \r\nUnion All \r\n(select @tenant as Id \r\n)";

        Assert.Throws<InvalidOperationException>(() => shape.RenderWithKeys(0, null));

        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 23 });

        Assert.Equal(expected, rendered.Sql);
        Assert.Equal(23, Assert.Single(rendered.Parameters).Value);
        Assert.Equal(key, Assert.Single(rendered.Slots));
    }

    /// <summary>同名同值的带键与旧式参数在两种组合顺序中保持独立。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CteAndUnion_WhenKeyedAndUnkeyedNamesCollide_ShouldKeepIndependentBindings(bool keyedCte)
    {
        var key = new SqlParameterKey("request", "id");
        var keyed = new SqlQueryFragment("select @id as Id", new[]
            { new SqlParam("id", 7, DbType.Int32) }).WithParameterKey("id", key);
        var unkeyed = new SqlQueryFragment("select @id as Id", new[]
            { new SqlParam("id", 7, DbType.Int32) });
        var builder = new TestSqlBuilder().Select("*").From("active_users");
        if (keyedCte)
            builder.With("active_users", keyed).UnionAll(unkeyed);
        else
            builder.With("active_users", unkeyed).UnionAll(keyed);

        var template = builder.ToTemplate();
        var shape = new SqlQueryShapeTemplate(template.CreateFragment());
        const string expected = "With [active_users] \r\nAs (select @id as Id)\r\n(Select * \r\nFrom [active_users] \r\n) \r\nUnion All \r\n(select @_p_0 as Id \r\n)";

        Assert.Equal(expected, template.Sql);
        Assert.Equal(2, template.Parameters.Count);
        var expectedSlots = keyedCte
            ? new[] { key, new SqlParameterKey("_p_0") }
            : new[] { new SqlParameterKey("id"), key };
        Assert.Equal(expectedSlots, template.CreateFragment().Slots);

        var rendered = shape.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 19 });

        Assert.Equal(expected, rendered.Sql);
        Assert.Equal(keyedCte ? new object[] { 19, 7 } : new object[] { 7, 19 },
            rendered.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(expectedSlots, rendered.Slots);
    }

    /// <summary>清空参数后重新添加的普通参数不能继承已清除片段的逻辑键。</summary>
    [Fact]
    public void ClearParams_WhenNameIsReused_ShouldDiscardPreviousLogicalKey()
    {
        var key = new SqlParameterKey("request", "id");
        var fragment = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 7) })
            .WithParameterKey("id", key);
        var builder = new TestSqlBuilder().Select("*").From(fragment, "items");

        builder.ClearParams().AddParam("id", 13);

        var template = builder.ToTemplate();
        Assert.Equal("Select * \r\nFrom (select @id as Id) As [items]", template.Sql);
        Assert.Equal(new SqlParameterKey("id"), Assert.Single(template.Slots));
        Assert.Equal(13, Assert.Single(template.Parameters).Value);
    }

    /// <summary>通过实体属性显式覆盖参数后，该参数恢复为普通名称绑定。</summary>
    [Fact]
    public void AddParam_WhenTypedParameterOverwritesFragment_ShouldDiscardPreviousLogicalKey()
    {
        var key = new SqlParameterKey("request", "id");
        var fragment = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 7) })
            .WithParameterKey("id", key);
        var builder = new TestSqlBuilder().Select("*").From(fragment, "items");

        builder.AddParam<ISqlBuilder, ParameterEntity>("id", entity => entity.Id, 13);

        var template = builder.ToTemplate();
        Assert.Equal("Select * \r\nFrom (select @id as Id) As [items]", template.Sql);
        Assert.Equal(new SqlParameterKey("id"), Assert.Single(template.Slots));
        Assert.Equal(13, Assert.Single(template.Parameters).Value);
    }

    /// <summary>已替换的缓存参数不应在模板中保留失效的片段键。</summary>
    [Fact]
    public void ToTemplate_WhenCachedNameIsReplaced_ShouldExcludeUnusedLogicalKey()
    {
        var key = new SqlParameterKey("request", "id");
        var fragment = new SqlQueryFragment("select @id as Id", new[]
            { new SqlParam("id", 7, DbType.Int32) }).WithParameterKey("id", key);
        var builder = new TestSqlBuilder().Select("*").From("items").AppendWhere("[Id]=@id").With("items", fragment);
        Assert.Equal("With [items] \r\nAs (select @id as Id)\r\nSelect * \r\nFrom [items] \r\nWhere [Id]=@id", builder.ToSql());

        builder.AddParam("id", 8);
        var template = builder.ToTemplate();

        Assert.Equal("With [items] \r\nAs (select @_p_0 as Id)\r\nSelect * \r\nFrom [items] \r\nWhere [Id]=@id", template.Sql);
        Assert.Equal(new[] { new SqlParameterKey("id"), key }, template.Slots);
        var rendered = new SqlQueryShapeTemplate(template.CreateFragment()).RenderWithKeys(0,
            new Dictionary<SqlParameterKey, object> { [key] = 21 });
        Assert.Equal(new object[] { 8, 21 }, rendered.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>过滤器触发独立渲染时，模板 SQL、参数与逻辑键应来自同一快照。</summary>
    [Fact]
    public void ToTemplate_WhenFilterRequiresSnapshot_ShouldKeepRenderedParametersAndKeys()
    {
        var key = new SqlParameterKey("request", "id");
        var fragment = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 7) })
            .WithParameterKey("id", key);
        var builder = new SqlServerBuilder(new SqlBuilderServices(filters: new ISqlFilter[] { new NoOpFilter() }))
            .Select("*").From("items").With("items", fragment);

        var template = builder.ToTemplate();

        Assert.Equal("With [items] \r\nAs (select @id as Id)\r\nSelect * \r\nFrom [items]", template.Sql);
        Assert.Equal(key, Assert.Single(template.Slots));
        Assert.Equal(7, Assert.Single(template.Parameters).Value);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>失败的渲染不应将候选逻辑键留在可继续使用的草稿中。</summary>
    [Fact]
    public void CteAndUnion_WhenRenderFails_ShouldRestoreParametersAndLogicalKeys()
    {
        var key = new SqlParameterKey("request", "id");
        var cte = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 7) })
            .WithParameterKey("id", key);
        var invalid = new SqlQueryFragment("select 'it\\'s @ID' as text, @ID as Id",
            new[] { new SqlParam("id", 8) });
        var builder = new TestSqlBuilder().Select("*").From("items").With("items", cte).UnionAll(invalid);

        Assert.Throws<InvalidOperationException>(() => builder.ToSql());
        Assert.Empty(builder.GetParams());
        builder.ClearUnionBuilders().AddParam("id", 7);
        var template = builder.ToTemplate();

        Assert.Equal("With [items] \r\nAs (select @_p_0 as Id)\r\nSelect * \r\nFrom [items]", template.Sql);
        Assert.Equal(new[] { new SqlParameterKey("id"), key }, template.Slots);
    }

    /// <summary>草稿修改回滚时恢复原片段的参数和逻辑键。</summary>
    [Fact]
    public void MutationCheckpoint_WhenRolledBack_ShouldRestoreLogicalKey()
    {
        var key = new SqlParameterKey("request", "id");
        var fragment = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 7) })
            .WithParameterKey("id", key);
        var builder = new SqlServerBuilder();
        builder.Select("*").From(fragment, "items");

        using (var checkpoint = builder.BeginMutationCheckpoint())
        {
            Assert.NotNull(checkpoint);
            builder.ClearSqlParams();
            builder.AddParam("id", 13);
        }

        var template = builder.ToTemplate();
        Assert.Equal("Select * \r\nFrom (select @id as Id) As [items]", template.Sql);
        Assert.Equal(key, Assert.Single(template.Slots));
        Assert.Equal(7, Assert.Single(template.Parameters).Value);
    }

    /// <summary>独立 Builder 并发冻结共享片段时，请求键值不能相互覆盖。</summary>
    [Fact]
    public void ToTemplate_WhenKeyedFragmentIsUsedConcurrently_ShouldIsolateBindings()
    {
        var key = new SqlParameterKey("request", "id");
        var fragment = new SqlQueryFragment("select @id as Id", new[] { new SqlParam("id", 0) })
            .WithParameterKey("id", key);
        var results = new (string Sql, object Value)[128];
        Parallel.For(0, results.Length, index =>
        {
            var builder = new TestSqlBuilder().Select("*").From(fragment, "items");
            var template = new SqlQueryShapeTemplate(builder.ToTemplate().CreateFragment());
            var rendered = template.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = index });
            results[index] = (rendered.Sql, Assert.Single(rendered.Parameters).Value);
        });

        for (var index = 0; index < results.Length; index++)
        {
            Assert.Equal("Select * \r\nFrom (select @id as Id) As [items]", results[index].Sql);
            Assert.Equal(index, results[index].Value);
        }
    }

    private sealed class NoOpFilter : ISqlFilter
    {
        public void Filter(SqlFilterContext context) { }
    }

    private sealed class ParameterEntity { public int Id { get; set; } }
}
