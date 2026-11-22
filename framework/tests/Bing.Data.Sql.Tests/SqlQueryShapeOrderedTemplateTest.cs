using System.Data;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>按声明顺序组合固定片段和可选片段的直接测试。</summary>
public sealed class SqlQueryShapeOrderedTemplateTest
{
    /// <summary>条件位与声明顺序无关，固定分组始终位于 WHERE 和 HAVING 之间。</summary>
    [Theory]
    [InlineData(0, "SELECT category, COUNT(*) AS total FROM items WHERE enabled=@enabled GROUP BY category ORDER BY category")]
    [InlineData(1, "SELECT category, COUNT(*) AS total FROM items WHERE enabled=@enabled GROUP BY category HAVING COUNT(*)>=@minimumCount ORDER BY category")]
    [InlineData(2, "SELECT category, COUNT(*) AS total FROM items WHERE enabled=@enabled AND id>=@minimumId GROUP BY category ORDER BY category")]
    [InlineData(3, "SELECT category, COUNT(*) AS total FROM items WHERE enabled=@enabled AND id>=@minimumId GROUP BY category HAVING COUNT(*)>=@minimumCount ORDER BY category")]
    public void Render_WhenFixedAndOptionalPartsInterleave_ShouldPreserveDeclarationOrder(int mask, string sql)
    {
        var template = CreateTemplate();
        var rendered = template.Render(mask, new Dictionary<string, object>
        {
            ["enabled"] = 1, ["minimumId"] = 3, ["minimumCount"] = 2
        });

        Assert.Equal(sql, rendered.Sql);
        var expectedNames = new List<string> { "@enabled" };
        var expectedValues = new List<object> { 1 };
        if ((mask & 2) != 0) { expectedNames.Add("@minimumId"); expectedValues.Add(3); }
        if ((mask & 1) != 0) { expectedNames.Add("@minimumCount"); expectedValues.Add(2); }
        Assert.Equal(expectedNames, rendered.Parameters.Select(p => p.Name));
        Assert.Equal(expectedValues, rendered.Parameters.Select(p => p.Value));
        Assert.Equal(3, template.SupportedMask);
        Assert.Equal(new[] { 1, 0 }, template.OptionalFragments.Select(part => part.Bit));
    }

    /// <summary>同一条件位可以控制多处片段，逻辑键仍按身份共享或隔离参数。</summary>
    [Theory]
    [InlineData(0, "SELECT @id UNION ALL SELECT @id", new[] { 7 })]
    [InlineData(1, "SELECT @id UNION ALL SELECT @id_1 UNION ALL SELECT @id UNION ALL SELECT @id_1", new[] { 7, 9 })]
    public void RenderWithKeys_WhenBitIsReused_ShouldSelectAllPartsAndPreserveKeys(int mask, string sql, int[] values)
    {
        var first = new SqlParameterKey("request", "id");
        var second = new SqlParameterKey("audit", "id");
        var required = Fragment("SELECT @id", "@id", first);
        var template = SqlQueryShapeTemplate.CreateOrdered(required, new (SqlQueryFragment Fragment, int? OptionalBit)[]
        {
            (Fragment("UNION ALL SELECT @id", "@id", second), 0),
            (Fragment("UNION ALL SELECT :owner", ":owner", first), null),
            (Fragment("UNION ALL SELECT @id", "@id", second), 0)
        });

        var result = template.RenderWithKeys(mask, new Dictionary<SqlParameterKey, object> { [first] = 7, [second] = 9 });

        Assert.Equal(sql, result.Sql);
        Assert.Equal(values.Cast<object>(), result.Parameters.Select(p => p.Value));
        Assert.Equal(mask == 0 ? new[] { first } : new[] { first, second }, result.Slots);
        Assert.Equal(new[] { 0, 0 }, template.OptionalFragments.Select(part => part.Bit));
    }

    /// <summary>构造后修改输入不会改变模板，未启用的相关片段不影响结果。</summary>
    [Fact]
    public void Render_WhenInputsChange_ShouldUseSnapshotAndSelectedCorrelation()
    {
        var parameter = new SqlParam("@limit", 3, DbType.Int32);
        var fixedPart = new SqlQueryFragment("LIMIT @limit", new[] { parameter });
        var correlated = new SqlQueryFragment("WHERE id=outer_id", Array.Empty<SqlParam>(), SqlQueryFragmentKind.Raw,
            SqlQueryCorrelationKind.Correlated);
        var parts = new (SqlQueryFragment Fragment, int? OptionalBit)[] { (correlated, 0), (fixedPart, null) };
        var template = SqlQueryShapeTemplate.CreateOrdered(new SqlQueryFragment("SELECT id FROM items"), parts);
        parameter.Value = 8;
        fixedPart.Parameters[0].Value = 9;
        parts[1] = (new SqlQueryFragment("LIMIT 100"), null);

        var plain = template.Render(0);
        var filtered = template.Render(1);

        Assert.Equal("SELECT id FROM items LIMIT @limit", plain.Sql);
        Assert.Equal("SELECT id FROM items WHERE id=outer_id LIMIT @limit", filtered.Sql);
        Assert.Equal(3, Assert.Single(plain.Parameters).Value);
        Assert.Equal(SqlQueryCorrelationKind.Independent, plain.Correlation);
        Assert.Equal(SqlQueryCorrelationKind.Correlated, filtered.Correlation);
    }

    /// <summary>并发请求共享缓存布局，参数、形状键和掩码各自隔离。</summary>
    [Fact]
    public void Render_WhenConcurrentRequestsReuseShapes_ShouldKeepValuesAndKeysIndependent()
    {
        var template = CreateTemplate();
        var results = new SqlQueryFragment[128];
        Parallel.For(0, results.Length, index =>
        {
            var key = new SqlQueryShapeKey("grouped", "sqlite", "v1", "route-" + (index % 2), index & 3, "");
            results[index] = template.Render(key, new Dictionary<string, object>
            {
                ["enabled"] = index, ["minimumId"] = index + 1, ["minimumCount"] = index + 2
            }, null);
        });

        for (var index = 0; index < results.Length; index++)
        {
            var expectedSql = "SELECT category, COUNT(*) AS total FROM items WHERE enabled=@enabled" +
                ((index & 2) != 0 ? " AND id>=@minimumId" : "") + " GROUP BY category" +
                ((index & 1) != 0 ? " HAVING COUNT(*)>=@minimumCount" : "") + " ORDER BY category";
            var expectedValues = new List<object> { index };
            if ((index & 2) != 0) expectedValues.Add(index + 1);
            if ((index & 1) != 0) expectedValues.Add(index + 2);
            Assert.Equal(expectedSql, results[index].Sql);
            Assert.Equal(expectedValues, results[index].Parameters.Select(p => p.Value));
        }
        Assert.Equal(4, template.CachedShapeCount);

        var first = new SqlQueryShapeKey("grouped", "sqlite", "v1", "another-route", 0, "");
        var miss = template.Render(first, new Dictionary<string, object> { ["enabled"] = 7 }, null);
        var hit = template.Render(first, new Dictionary<string, object> { ["enabled"] = 8 }, null);
        Assert.Same(miss.Sql, hit.Sql);
        Assert.Equal(7, Assert.Single(miss.Parameters).Value);
        Assert.Equal(8, Assert.Single(hit.Parameters).Value);
        Assert.Equal(5, template.CachedShapeCount);
    }

    /// <summary>旧构造方式继续按条件位排序，尾部保持在所有可选片段之后。</summary>
    [Fact]
    public void Render_WhenUsingExistingConstructor_ShouldRetainBitOrdering()
    {
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT 1"), new[]
        {
            new SqlQueryOptionalFragment(1, new SqlQueryFragment("UNION ALL SELECT 2")),
            new SqlQueryOptionalFragment(0, new SqlQueryFragment("UNION ALL SELECT 3"))
        }, " ", new SqlQueryFragment("ORDER BY 1"));

        Assert.Equal("SELECT 1 UNION ALL SELECT 3 UNION ALL SELECT 2 ORDER BY 1", template.Render(3).Sql);
    }

    /// <summary>分隔符只放在实际启用的片段之间。</summary>
    [Theory]
    [InlineData(0, "SELECT 1\nUNION ALL SELECT 3")]
    [InlineData(1, "SELECT 1\nUNION ALL SELECT 2\nUNION ALL SELECT 3")]
    public void Render_WhenPartIsDisabled_ShouldJoinOnlySelectedParts(int mask, string sql)
    {
        var template = SqlQueryShapeTemplate.CreateOrdered(new SqlQueryFragment("SELECT 1"),
            new (SqlQueryFragment Fragment, int? OptionalBit)[]
            {
                (new SqlQueryFragment("UNION ALL SELECT 2"), 0),
                (new SqlQueryFragment("UNION ALL SELECT 3"), null)
            }, "\n");

        Assert.Equal(sql, template.Render(mask).Sql);
    }

    /// <summary>只有启用冲突片段时才要求显式提供共享槽的请求值。</summary>
    [Fact]
    public void Render_WhenInterleavedPartHasConflictingDefault_ShouldRequireRequestValueOnlyWhenEnabled()
    {
        var template = SqlQueryShapeTemplate.CreateOrdered(
            new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 1, DbType.Int32) }),
            new (SqlQueryFragment Fragment, int? OptionalBit)[]
            {
                (new SqlQueryFragment("UNION ALL SELECT @id", new[] { new SqlParam("@id", 2, DbType.Int32) }), 0),
                (new SqlQueryFragment("UNION ALL SELECT @id", new[] { new SqlParam("@id", 1, DbType.Int32) }), null)
            });

        var withoutOptional = template.Render(0);
        Assert.Equal("SELECT @id UNION ALL SELECT @id", withoutOptional.Sql);
        Assert.Equal(1, Assert.Single(withoutOptional.Parameters).Value);
        Assert.Throws<InvalidOperationException>(() => template.Render(1));
        var bound = template.Render(1, new Dictionary<string, object> { ["id"] = 7 });
        Assert.Equal("SELECT @id UNION ALL SELECT @id UNION ALL SELECT @id", bound.Sql);
        Assert.Equal(7, Assert.Single(bound.Parameters).Value);
    }

    /// <summary>条件位范围和请求掩码沿用已有模板合同。</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public void CreateOrdered_WhenBitIsOutsideSupportedRange_ShouldReject(int bit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SqlQueryShapeTemplate.CreateOrdered(
            new SqlQueryFragment("SELECT 1"),
            new (SqlQueryFragment Fragment, int? OptionalBit)[] { (new SqlQueryFragment("WHERE 1=1"), bit) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTemplate().Render(4));
    }

    private static SqlQueryShapeTemplate CreateTemplate() => SqlQueryShapeTemplate.CreateOrdered(
        new SqlQueryFragment("SELECT category, COUNT(*) AS total FROM items WHERE enabled=@enabled",
            new[] { new SqlParam("@enabled", 0, DbType.Int32) }),
        new (SqlQueryFragment Fragment, int? OptionalBit)[]
        {
            (new SqlQueryFragment("AND id>=@minimumId", new[] { new SqlParam("@minimumId", 0, DbType.Int32) }), 1),
            (new SqlQueryFragment("GROUP BY category"), null),
            (new SqlQueryFragment("HAVING COUNT(*)>=@minimumCount", new[] { new SqlParam("@minimumCount", 0, DbType.Int32) }), 0),
            (new SqlQueryFragment("ORDER BY category"), null)
        });

    private static SqlQueryFragment Fragment(string sql, string name, SqlParameterKey key) =>
        new SqlQueryFragment(sql, new[] { new SqlParam(name, 0, DbType.Int32) }).WithParameterKey(name, key);
}
