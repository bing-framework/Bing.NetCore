using System.Data;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>查询形状中固定尾部片段的直接测试。</summary>
public sealed class SqlQueryShapeSuffixTemplateTest
{
    /// <summary>可选条件应始终出现在排序和分页之前，参数按 SQL 顺序绑定。</summary>
    [Theory]
    [InlineData(0, "SELECT id FROM users WHERE tenant=@tenant ORDER BY id LIMIT @limit")]
    [InlineData(1, "SELECT id FROM users WHERE tenant=@tenant AND name=@name ORDER BY id LIMIT @limit")]
    [InlineData(2, "SELECT id FROM users WHERE tenant=@tenant AND state=@state ORDER BY id LIMIT @limit")]
    [InlineData(3, "SELECT id FROM users WHERE tenant=@tenant AND name=@name AND state=@state ORDER BY id LIMIT @limit")]
    public void Render_WhenOptionalConditionsChange_ShouldKeepSuffixLast(int mask, string sql)
    {
        var template = CreateTemplate();
        var values = new Dictionary<string, object>
        {
            ["tenant"] = 7,
            ["name"] = "alice",
            ["state"] = 2,
            ["limit"] = 10
        };

        var rendered = template.Render(mask, values);

        Assert.Equal(sql, rendered.Sql);
        var expected = new List<string> { "@tenant" };
        if ((mask & 1) != 0) expected.Add("@name");
        if ((mask & 2) != 0) expected.Add("@state");
        expected.Add("@limit");
        Assert.Equal(expected, rendered.Parameters.Select(item => item.Name));
        Assert.Equal(7, rendered.Parameters[0].Value);
        Assert.Equal(10, rendered.Parameters[^1].Value);
    }

    /// <summary>尾部与前面片段的逻辑参数可以共享，也可以独立绑定同名参数。</summary>
    [Fact]
    public void RenderWithKeys_WhenSuffixSharesKey_ShouldPreserveLogicalParameterIdentity()
    {
        var first = new SqlParameterKey("first", "id");
        var second = new SqlParameterKey("second", "id");
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 0, DbType.Int32) }).WithParameterKey("id", first),
            new[] { new SqlQueryOptionalFragment(0, new SqlQueryFragment("UNION ALL SELECT @id",
                new[] { new SqlParam("@id", 0, DbType.Int32) }).WithParameterKey("id", second)) },
            " ",
            new SqlQueryFragment("UNION ALL SELECT :owner", new[] { new SqlParam(":owner", 0, DbType.Int32) })
                .WithParameterKey("owner", first));

        var result = template.RenderWithKeys(1, new Dictionary<SqlParameterKey, object> { [first] = 7, [second] = 9 });

        Assert.Equal("SELECT @id UNION ALL SELECT @id_1 UNION ALL SELECT @id", result.Sql);
        Assert.Equal(new object[] { 7, 9 }, result.Parameters.Select(item => item.Value));
        Assert.Equal(new[] { first, second }, result.Slots);
    }

    /// <summary>尾部的同名槽仍要求绑定合同一致且冲突默认值由请求显式指定。</summary>
    [Fact]
    public void Render_WhenSuffixSharesParameter_ShouldRespectBindingContract()
    {
        var required = new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 1, DbType.Int32) });
        var suffix = new SqlQueryFragment("WHERE @id > 0", new[] { new SqlParam("@id", 2, DbType.Int32) });
        var template = new SqlQueryShapeTemplate(required, Array.Empty<SqlQueryOptionalFragment>(), " ", suffix);

        Assert.Throws<InvalidOperationException>(() => template.Render());
        var result = template.Render(0, new Dictionary<string, object> { ["id"] = 7 });
        Assert.Equal("SELECT @id WHERE @id > 0", result.Sql);
        Assert.Equal(7, Assert.Single(result.Parameters).Value);

        var incompatible = new SqlQueryShapeTemplate(required, Array.Empty<SqlQueryOptionalFragment>(), " ",
            new SqlQueryFragment("WHERE @id > 0", new[] { new SqlParam("@id", "x", DbType.String) }));
        Assert.Throws<InvalidOperationException>(() => incompatible.Render());
    }

    /// <summary>创建模板后修改来源参数不会改变尾部快照，相关性也参与最终合成。</summary>
    [Fact]
    public void Render_WhenSuffixIsCorrelated_ShouldUseSnapshotAndPropagateCorrelation()
    {
        var parameter = new SqlParam("@limit", 3, DbType.Int32);
        var suffix = new SqlQueryFragment("LIMIT @limit", new[] { parameter }, SqlQueryFragmentKind.Raw,
            SqlQueryCorrelationKind.Correlated);
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT id FROM users"),
            Array.Empty<SqlQueryOptionalFragment>(), " ", suffix);
        parameter.Value = 8;
        suffix.Parameters[0].Value = 9;

        var result = template.Render();

        Assert.Equal("SELECT id FROM users LIMIT @limit", result.Sql);
        Assert.Equal(3, Assert.Single(result.Parameters).Value);
        Assert.Equal(SqlQueryCorrelationKind.Correlated, result.Correlation);
    }

    /// <summary>共享模板在并发条件和参数变化时只复用结构，不串用请求值。</summary>
    [Fact]
    public void Render_WhenUsedConcurrently_ShouldIsolateSuffixAndOptionalValues()
    {
        var template = CreateTemplate();
        var results = new SqlQueryFragment[256];
        Parallel.For(0, results.Length, index => results[index] = template.Render(index & 3,
            new Dictionary<string, object>
            {
                ["tenant"] = index,
                ["name"] = "user-" + index,
                ["state"] = index + 1,
                ["limit"] = index + 2
            }));

        for (var index = 0; index < results.Length; index++)
        {
            var mask = index & 3;
            var expectedSql = "SELECT id FROM users WHERE tenant=@tenant" +
                ((mask & 1) != 0 ? " AND name=@name" : "") +
                ((mask & 2) != 0 ? " AND state=@state" : "") + " ORDER BY id LIMIT @limit";
            Assert.Equal(expectedSql, results[index].Sql);
            var expected = new List<object> { index };
            if ((mask & 1) != 0) expected.Add("user-" + index);
            if ((mask & 2) != 0) expected.Add(index + 1);
            expected.Add(index + 2);
            Assert.Equal(expected, results[index].Parameters.Select(item => item.Value));
        }
        Assert.Equal(4, template.CachedShapeCount);
    }

    /// <summary>完整结构键重复命中及路由变化未命中时，尾部参数都采用各自的请求值。</summary>
    [Fact]
    public void Render_WhenShapeKeyChanges_ShouldCacheOnlyItsLayout()
    {
        var template = CreateTemplate();
        var shape = new SqlQueryShapeKey("users", "sqlite", "v1", "first", 0, "");
        var first = template.Render(shape, new Dictionary<string, object> { ["tenant"] = 1, ["limit"] = 2 }, null);
        var hit = template.Render(shape, new Dictionary<string, object> { ["tenant"] = 3, ["limit"] = 4 }, null);
        var miss = template.Render(shape with { RouteKey = "second" },
            new Dictionary<string, object> { ["tenant"] = 5, ["limit"] = 6 }, null);

        Assert.Equal("SELECT id FROM users WHERE tenant=@tenant ORDER BY id LIMIT @limit", first.Sql);
        Assert.Same(first.Sql, hit.Sql);
        Assert.Equal(first.Sql, miss.Sql);
        Assert.Equal(new object[] { 1, 2 }, first.Parameters.Select(item => item.Value));
        Assert.Equal(new object[] { 3, 4 }, hit.Parameters.Select(item => item.Value));
        Assert.Equal(new object[] { 5, 6 }, miss.Parameters.Select(item => item.Value));
        Assert.Equal(2, template.CachedShapeCount);
    }

    private static SqlQueryShapeTemplate CreateTemplate() => new(
        new SqlQueryFragment("SELECT id FROM users WHERE tenant=@tenant",
            new[] { new SqlParam("@tenant", 0, DbType.Int32) }),
        new[]
        {
            new SqlQueryOptionalFragment(0, "AND name=@name", new[] { new SqlParam("@name", "", DbType.String) }),
            new SqlQueryOptionalFragment(1, "AND state=@state", new[] { new SqlParam("@state", 0, DbType.Int32) })
        }, " ", new SqlQueryFragment("ORDER BY id LIMIT @limit", new[] { new SqlParam("@limit", 0, DbType.Int32) }));
}
