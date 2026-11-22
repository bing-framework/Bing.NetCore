using System.Data;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>查询形状中逻辑参数身份、共享与隔离的直接测试。</summary>
public sealed class SqlQueryShapeLogicalParametersTest
{
    private static SqlQueryFragment Fragment(string sql, string name, object value, SqlParameterKey key) =>
        new SqlQueryFragment(sql, new[] { new SqlParam(name, value, DbType.Int32) }).WithParameterKey(name, key);

    /// <summary>同名但不同作用域的参数应使用不同物理名称和值。</summary>
    [Fact]
    public void RenderWithKeys_WhenSameNameHasDifferentKeys_ShouldKeepValuesIndependent()
    {
        var firstKey = new SqlParameterKey("first", "id");
        var secondKey = new SqlParameterKey("second", "id");
        var template = new SqlQueryShapeTemplate(Fragment("SELECT @id", "@id", 1, firstKey),
            new[] { new SqlQueryOptionalFragment(0, Fragment("UNION ALL SELECT @id", "@id", 2, secondKey)) });
        var values = new Dictionary<SqlParameterKey, object> { [firstKey] = 7, [secondKey] = 9 };

        var required = template.RenderWithKeys(0, values);
        var combined = template.RenderWithKeys(1, values);
        var again = template.RenderWithKeys(1, new Dictionary<SqlParameterKey, object> { [firstKey] = 10, [secondKey] = 20 });

        Assert.Equal("SELECT @id", required.Sql);
        Assert.Equal("SELECT @id UNION ALL SELECT @id_1", combined.Sql);
        Assert.Equal(combined.Sql, again.Sql);
        Assert.Equal(new object[] { 7, 9 }, combined.Parameters.Select(item => item.Value));
        Assert.Equal(new object[] { 10, 20 }, again.Parameters.Select(item => item.Value));
        Assert.Equal(new[] { firstKey, secondKey }, combined.Slots);
        Assert.Equal(2, template.CachedShapeCount);
    }

    /// <summary>相同逻辑参数可跨不同 SQL 名称共享一次绑定。</summary>
    [Fact]
    public void RenderWithKeys_WhenDifferentNamesShareKey_ShouldBindOnce()
    {
        var key = new SqlParameterKey("request", "tenant");
        var template = new SqlQueryShapeTemplate(Fragment("SELECT @tenant", "@tenant", 1, key),
            new[] { new SqlQueryOptionalFragment(0, Fragment("UNION ALL SELECT :owner", ":owner", 2, key)) });

        var error = Assert.Throws<InvalidOperationException>(() => template.RenderWithKeys(1, null));
        Assert.Equal("同一逻辑参数的默认值或转换状态不一致，请使用逻辑键绑定本次请求值：request/tenant", error.Message);
        var result = template.RenderWithKeys(1, new Dictionary<SqlParameterKey, object> { [key] = 7 });

        Assert.Equal("SELECT @tenant UNION ALL SELECT @tenant", result.Sql);
        Assert.Equal(7, Assert.Single(result.Parameters).Value);
        Assert.Equal(key, Assert.Single(result.Slots));
    }

    /// <summary>显式逻辑槽与旧式同名槽可以分别提供请求值。</summary>
    [Fact]
    public void RenderWithKeys_WhenKeyedAndLegacyNamesCollide_ShouldBindBoth()
    {
        var key = new SqlParameterKey("request", "id");
        var template = new SqlQueryShapeTemplate(Fragment("SELECT @id", "@id", 0, key),
            new[] { new SqlQueryOptionalFragment(0, "UNION ALL SELECT @id", new[] { new SqlParam("@id", 0, DbType.Int32) }) });

        var result = template.RenderWithKeys(1, new Dictionary<SqlParameterKey, object> { [key] = 3 },
            new Dictionary<string, object> { ["id"] = 4 });

        Assert.Equal("SELECT @id UNION ALL SELECT @id_1", result.Sql);
        Assert.Equal(new object[] { 3, 4 }, result.Parameters.Select(item => item.Value));
    }

    /// <summary>同一逻辑键的绑定合同不一致时应在请求绑定前失败。</summary>
    [Fact]
    public void RenderWithKeys_WhenSharedKeyHasDifferentTypes_ShouldReject()
    {
        var key = new SqlParameterKey("request", "id");
        var required = Fragment("SELECT @id", "@id", 1, key);
        var optional = new SqlQueryFragment("UNION ALL SELECT @other", new[] { new SqlParam("@other", "x", DbType.String) })
            .WithParameterKey("@other", key);
        var template = new SqlQueryShapeTemplate(required, new[] { new SqlQueryOptionalFragment(0, optional) });

        Assert.Throws<InvalidOperationException>(() => template.RenderWithKeys(1, new Dictionary<SqlParameterKey, object> { [key] = 7 }));
        Assert.Equal("SELECT @id", template.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 7 }).Sql);
    }

    /// <summary>片段冻结、作用域化和模板保存都应保留逻辑参数身份。</summary>
    [Fact]
    public void Fragment_WhenFrozenOrScoped_ShouldPreserveLogicalKey()
    {
        var key = new SqlParameterKey("request", "id");
        var original = new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 1) });
        var keyed = original.WithParameterKey("@id", key);
        var frozen = keyed.Freeze();
        var scoped = frozen.Render(new SqlQueryScope("sub"));
        var template = new SqlQueryShapeTemplate(keyed);

        Assert.Equal(new SqlParameterKey("id"), Assert.Single(original.Slots));
        Assert.Equal(key, Assert.Single(keyed.Slots));
        Assert.Equal(key, Assert.Single(frozen.Slots));
        Assert.Equal("SELECT @sub_id", scoped.Sql);
        Assert.Equal(key, Assert.Single(scoped.Slots));
        Assert.Equal(key, Assert.Single(template.Required.Slots));
    }

    /// <summary>CTE 和组合模板保存身份，随后按同一个键绑定时可以共享参数。</summary>
    [Fact]
    public void Fragment_WhenUsedInCteAndTemplate_ShouldPreserveKeysForLaterBinding()
    {
        var key = new SqlParameterKey("request", "id");
        var first = Fragment("SELECT @id", "@id", 1, key);
        var second = Fragment("SELECT @id", "@id", 1, key);
        var cte = new SqlCteDefinition("items", first).Render();
        var template = SqlQueryTemplate.Compose(" UNION ALL ", first, second);
        var combined = new SqlQueryShapeTemplate(template.CreateFragment());

        Assert.Equal("items AS (SELECT @id)", cte.Sql);
        Assert.Equal(key, Assert.Single(cte.Slots));
        Assert.Equal(new[] { key, key }, template.Slots);
        var rendered = combined.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = 7 });
        Assert.Equal("SELECT @fragment_0_id UNION ALL SELECT @fragment_0_id", rendered.Sql);
        Assert.Equal(7, Assert.Single(rendered.Parameters).Value);
    }

    /// <summary>不同请求并发渲染同一个键控形状时不能串用参数值。</summary>
    [Fact]
    public void RenderWithKeys_WhenUsedConcurrently_ShouldIsolateRequestValues()
    {
        var first = new SqlParameterKey("first", "id");
        var second = new SqlParameterKey("second", "id");
        var template = new SqlQueryShapeTemplate(Fragment("SELECT @id", "@id", 0, first),
            new[] { new SqlQueryOptionalFragment(0, Fragment("UNION ALL SELECT @id", "@id", 0, second)) });
        var results = new SqlQueryFragment[128];

        Parallel.For(0, results.Length, index => results[index] = template.RenderWithKeys(1,
            new Dictionary<SqlParameterKey, object> { [first] = index, [second] = index + 1000 }));

        for (var index = 0; index < results.Length; index++)
        {
            Assert.Equal("SELECT @id UNION ALL SELECT @id_1", results[index].Sql);
            Assert.Equal(new object[] { index, index + 1000 }, results[index].Parameters.Select(item => item.Value));
        }
        Assert.Equal(1, template.CachedShapeCount);
    }

    /// <summary>同一片段中连续的参数名称碰撞不应互相覆盖或改写字符串与注释。</summary>
    [Fact]
    public void RenderWithKeys_WhenRenamedNamesAlsoExist_ShouldRewriteEachTokenOnce()
    {
        var first = new SqlParameterKey("first", "id");
        var second = new SqlParameterKey("second", "id");
        var third = new SqlParameterKey("third", "id");
        var fragment = new SqlQueryFragment("UNION ALL SELECT @id, @id_1, '@id' /* @id_1 */",
                new[] { new SqlParam("@id", 0), new SqlParam("@id_1", 0) })
            .WithParameterKey("id", second).WithParameterKey("id_1", third);
        var template = new SqlQueryShapeTemplate(Fragment("SELECT @id, 0, ''", "@id", 0, first),
            new[] { new SqlQueryOptionalFragment(0, fragment) });

        var result = template.RenderWithKeys(1, new Dictionary<SqlParameterKey, object>
            { [first] = 1, [second] = 2, [third] = 3 });

        Assert.Equal("SELECT @id, 0, '' UNION ALL SELECT @id_1, @id_1_1, '@id' /* @id_1 */", result.Sql);
        Assert.Equal(new object[] { 1, 2, 3 }, result.Parameters.Select(item => item.Value));
    }

    /// <summary>无前缀声明的实际 SQL 标记使用不同大小写时，应统一为可绑定的名称。</summary>
    [Fact]
    public void RenderWithKeys_WhenFirstTokenCaseDiffers_ShouldUseOneBoundToken()
    {
        var key = new SqlParameterKey("request", "id");
        var first = Fragment("SELECT :ID", "id", 0, key);
        var second = Fragment("UNION ALL SELECT :other", ":other", 0, key);
        var template = new SqlQueryShapeTemplate(first, new[] { new SqlQueryOptionalFragment(0, second) });

        var result = template.RenderWithKeys(1, new Dictionary<SqlParameterKey, object> { [key] = 7 });

        Assert.Equal("SELECT :id UNION ALL SELECT :id", result.Sql);
        Assert.Equal(7, Assert.Single(result.Parameters).Value);
    }

    /// <summary>完整结构键的命中和不同路由未命中均只复用参数布局。</summary>
    [Fact]
    public void RenderWithKeys_WhenShapeKeyChanges_ShouldIsolateCachedLayoutsAndRequestValues()
    {
        var key = new SqlParameterKey("request", "id");
        var template = new SqlQueryShapeTemplate(Fragment("SELECT @id", "@id", 0, key));
        var shape = new SqlQueryShapeKey("query", "sqlite", "v1", "one", 0, "");
        var first = template.RenderWithKeys(shape, new Dictionary<SqlParameterKey, object> { [key] = 1 }, null, null);
        var again = template.RenderWithKeys(shape, new Dictionary<SqlParameterKey, object> { [key] = 2 }, null, null);
        var other = template.RenderWithKeys(shape with { RouteKey = "two" },
            new Dictionary<SqlParameterKey, object> { [key] = 3 }, null, null);

        Assert.Equal("SELECT @id", first.Sql);
        Assert.Same(first.Sql, again.Sql);
        Assert.Equal("SELECT @id", other.Sql);
        Assert.Equal(1, Assert.Single(first.Parameters).Value);
        Assert.Equal(2, Assert.Single(again.Parameters).Value);
        Assert.Equal(3, Assert.Single(other.Parameters).Value);
        Assert.Equal(2, template.CachedShapeCount);
    }

    /// <summary>显式身份不接受旧式名称参数来覆盖冲突的默认值。</summary>
    [Fact]
    public void Render_WhenKeyedDefaultsConflict_ShouldRequireValueByLogicalKey()
    {
        var key = new SqlParameterKey("request", "id");
        var template = new SqlQueryShapeTemplate(Fragment("SELECT @id", "@id", 1, key),
            new[] { new SqlQueryOptionalFragment(0, Fragment("UNION ALL SELECT @other", "@other", 2, key)) });

        Assert.Throws<InvalidOperationException>(() => template.Render(1, new Dictionary<string, object> { ["id"] = 7 }));
        var result = template.RenderWithKeys(1, new Dictionary<SqlParameterKey, object> { [key] = 7 });
        Assert.Equal("SELECT @id UNION ALL SELECT @id", result.Sql);
        Assert.Equal(7, Assert.Single(result.Parameters).Value);
    }

    /// <summary>为片段设置参数键不会改变之前创建的片段或已冻结的快照。</summary>
    [Fact]
    public void WithParameterKey_WhenReassigned_ShouldKeepEarlierFragmentsUnchanged()
    {
        var original = new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 0) });
        var firstKey = new SqlParameterKey("one", "id");
        var secondKey = new SqlParameterKey("two", "id");
        var first = original.WithParameterKey("id", firstKey);
        var snapshot = first.Freeze();
        var second = first.WithParameterKey("@id", secondKey);

        Assert.Equal(new SqlParameterKey("id"), Assert.Single(original.Slots));
        Assert.Equal(firstKey, Assert.Single(first.Slots));
        Assert.Equal(firstKey, Assert.Single(snapshot.Slots));
        Assert.Equal(secondKey, Assert.Single(second.Slots));
        Assert.Throws<ArgumentException>(() => original.WithParameterKey("missing", firstKey));
        Assert.Throws<ArgumentException>(() => original.WithParameterKey("id", default));
    }

    /// <summary>键的作用域和名称采用其值语义，大小写不同的键代表不同身份。</summary>
    [Fact]
    public void RenderWithKeys_WhenKeysDifferOnlyByCase_ShouldKeepThemIndependent()
    {
        var lower = new SqlParameterKey("request", "id");
        var upper = new SqlParameterKey("request", "ID");
        var template = new SqlQueryShapeTemplate(Fragment("SELECT @id", "@id", 0, lower),
            new[] { new SqlQueryOptionalFragment(0, Fragment("UNION ALL SELECT @id", "@id", 0, upper)) });

        var result = template.RenderWithKeys(1, new Dictionary<SqlParameterKey, object> { [lower] = 1, [upper] = 2 });

        Assert.Equal("SELECT @id UNION ALL SELECT @id_1", result.Sql);
        Assert.Equal(new object[] { 1, 2 }, result.Parameters.Select(item => item.Value));
    }

    /// <summary>需要改写参数且方言的反斜杠转义模式未知时拒绝执行该形状。</summary>
    [Fact]
    public void RenderWithKeys_WhenRewriteHasAmbiguousEscaping_ShouldRejectSelectedShape()
    {
        var first = new SqlParameterKey("first", "id");
        var second = new SqlParameterKey("second", "id");
        var template = new SqlQueryShapeTemplate(Fragment("SELECT @id", "@id", 0, first),
            new[] { new SqlQueryOptionalFragment(0, Fragment("UNION ALL SELECT @id, '\\'@id'", "@id", 0, second)) });

        Assert.Equal("SELECT @id", template.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [first] = 1 }).Sql);
        Assert.Throws<InvalidOperationException>(() => template.RenderWithKeys(1,
            new Dictionary<SqlParameterKey, object> { [first] = 1, [second] = 2 }));
    }

    /// <summary>输出作用域只调整物理名称并继续保留逻辑键与参数快照。</summary>
    [Fact]
    public void RenderWithKeys_WhenOutputIsScoped_ShouldKeepIdentityAndSnapshot()
    {
        var key = new SqlParameterKey("request", "payload");
        var input = new byte[] { 1, 2 };
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @payload",
            new[] { new SqlParam("@payload", Array.Empty<byte>(), DbType.Binary) }).WithParameterKey("payload", key));
        var result = template.RenderWithKeys(0, new Dictionary<SqlParameterKey, object> { [key] = input },
            scope: new SqlQueryScope("sub"));
        input[0] = 9;

        Assert.Equal("SELECT @sub_payload", result.Sql);
        Assert.Equal(key, Assert.Single(result.Slots));
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Assert.Single(result.Parameters).Value));
    }
}
