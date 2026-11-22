using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>查询形状模板按参数槽位绑定值的职责测试。</summary>
public sealed class SqlQueryShapeValueBindingTest
{
    /// <summary>无参数形状接受空 span 和 default span。</summary>
    [Fact]
    public void RenderValues_WhenShapeHasNoParameters_ShouldAcceptEmptySpan()
    {
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT 1"));
        var shape = new SqlQueryShapeKey("constant", "sqlite", "v1", "default", 0, "");

        var empty = template.RenderValues(0, ReadOnlySpan<object>.Empty);
        var defaultValues = template.RenderValues(shape, default);

        Assert.Equal("SELECT 1", empty.Sql);
        Assert.Empty(empty.Parameters);
        Assert.Same(empty.Sql, defaultValues.Sql);
        Assert.Empty(defaultValues.Parameters);
    }

    /// <summary>位置绑定沿用模板对非法可选掩码的校验。</summary>
    [Fact]
    public void RenderValues_WhenOptionalMaskIsUnsupported_ShouldMatchRenderException()
    {
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @id",
                new[] { new SqlParam("@id", 0, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "WHERE @id > 0",
                new[] { new SqlParam("@id", 0, DbType.Int32) }) });

        var renderError = Assert.Throws<ArgumentOutOfRangeException>(() => template.Render(2));
        var valuesError = Assert.Throws<ArgumentOutOfRangeException>(() =>
            template.RenderValues(2, new object[] { 1 }));

        Assert.Equal(renderError.ParamName, valuesError.ParamName);
        Assert.Equal(renderError.ActualValue, valuesError.ActualValue);
    }

    /// <summary>掩码只为启用片段创建槽位，重复参数只占首次出现的位置。</summary>
    [Fact]
    public void RenderValues_ByOptionalMask_ShouldBindEnabledSlotsInFirstOccurrenceOrder()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT * FROM orders WHERE tenant=@tenant",
                new[] { new SqlParam("@tenant", 0, DbType.Int32) }),
            new[]
            {
                new SqlQueryOptionalFragment(0, "AND state=@state AND owner_tenant=@tenant",
                    new[] { new SqlParam("@state", "", DbType.String), new SqlParam("@tenant", 0, DbType.Int32) }),
                new SqlQueryOptionalFragment(1, "AND owner=@owner",
                    new[] { new SqlParam("@owner", 0, DbType.Int32) })
            }, " ", new SqlQueryFragment("ORDER BY @sort",
                new[] { new SqlParam("@sort", "", DbType.String) }));

        var oneCondition = template.RenderValues(1, new object[] { 7, "paid", "created_at" });
        var bothConditions = template.RenderValues(3, new object[] { 8, "new", 19, "updated_at" });

        Assert.Equal("SELECT * FROM orders WHERE tenant=@tenant AND state=@state AND owner_tenant=@tenant ORDER BY @sort",
            oneCondition.Sql);
        Assert.Equal(new[] { "@tenant", "@state", "@sort" }, oneCondition.Parameters.Select(item => item.Name));
        Assert.Equal(new object[] { 7, "paid", "created_at" }, oneCondition.Parameters.Select(item => item.Value));
        Assert.Equal("SELECT * FROM orders WHERE tenant=@tenant AND state=@state AND owner_tenant=@tenant AND owner=@owner ORDER BY @sort",
            bothConditions.Sql);
        Assert.Equal(new[] { "@tenant", "@state", "@owner", "@sort" }, bothConditions.Parameters.Select(item => item.Name));
        Assert.Equal(new object[] { 8, "new", 19, "updated_at" }, bothConditions.Parameters.Select(item => item.Value));
    }

    /// <summary>结构键重载应按其掩码选择 SQL 和对应参数槽位。</summary>
    [Fact]
    public void RenderValues_ByShapeKey_ShouldUseShapeMaskForSqlAndSlots()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT @tenant", new[] { new SqlParam("@tenant", 0, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "WHERE @owner=@ownerValue",
                new[] { new SqlParam("@owner", 0, DbType.Int32), new SqlParam("@ownerValue", 0, DbType.Int32) }) });
        var shape = new SqlQueryShapeKey("users", "sqlite", "mapping-v1", "route-a", 1, "");

        var rendered = template.RenderValues(shape, new object[] { 5, 8, 8 });

        Assert.Equal("SELECT @tenant WHERE @owner=@ownerValue", rendered.Sql);
        Assert.Equal(new[] { "@tenant", "@owner", "@ownerValue" }, rendered.Parameters.Select(item => item.Name));
        Assert.Equal(new object[] { 5, 8, 8 }, rendered.Parameters.Select(item => item.Value));
    }

    /// <summary>相同逻辑键即使名称不同也只占一个参数槽。</summary>
    [Fact]
    public void RenderValues_WhenLogicalKeyIsShared_ShouldBindOneSlot()
    {
        var key = new SqlParameterKey("request", "id");
        var required = new SqlQueryFragment("SELECT @first", new[] { new SqlParam("@first", 0, DbType.Int32) })
            .WithParameterKey("first", key);
        var optional = new SqlQueryFragment("UNION ALL SELECT :second",
                new[] { new SqlParam(":second", 0, DbType.Int32) })
            .WithParameterKey("second", key);
        var template = new SqlQueryShapeTemplate(required, new[] { new SqlQueryOptionalFragment(0, optional) });

        var rendered = template.RenderValues(1, new object[] { 42 });

        Assert.Equal("SELECT @first UNION ALL SELECT @first", rendered.Sql);
        Assert.Equal(new object[] { 42 }, rendered.Parameters.Select(item => item.Value));
        Assert.Equal(new[] { key }, rendered.Slots);
    }

    /// <summary>名称相同但逻辑键不同的参数分别占槽并独立绑定。</summary>
    [Fact]
    public void RenderValues_WhenNamesMatchButKeysDiffer_ShouldBindIndependentSlots()
    {
        var firstKey = new SqlParameterKey("first", "id");
        var secondKey = new SqlParameterKey("second", "id");
        var required = new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 0, DbType.Int32) })
            .WithParameterKey("id", firstKey);
        var optional = new SqlQueryFragment("UNION ALL SELECT @id",
                new[] { new SqlParam("@id", 0, DbType.Int32) })
            .WithParameterKey("id", secondKey);
        var template = new SqlQueryShapeTemplate(required, new[] { new SqlQueryOptionalFragment(0, optional) });

        var rendered = template.RenderValues(1, new object[] { 7, 9 });

        Assert.Equal("SELECT @id UNION ALL SELECT @id_1", rendered.Sql);
        Assert.Equal(new object[] { 7, 9 }, rendered.Parameters.Select(item => item.Value));
        Assert.Equal(new[] { firstKey, secondKey }, rendered.Slots);
    }

    /// <summary>覆盖已转换参数值时保留映射元数据并重置转换状态，null 仍是显式值。</summary>
    [Fact]
    public void RenderValues_WhenOverridingProviderValue_ShouldPreserveMetadataAndBindNull()
    {
        var parameter = new SqlParam("@value", "provider-value", DbType.String, ParameterDirection.Input,
            40, 6, 2)
        {
            OriginalValue = "original",
            IsProviderValue = true,
            EntityType = typeof(SqlQueryShapeValueBindingTest),
            PropertyName = "Property",
            ColumnName = "Column",
            ProviderTypeName = "TEXT"
        };
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @value", new[] { parameter }));

        var original = Assert.Single(template.RenderValues(0, new object[] { "request" }).Parameters);
        var nullValue = Assert.Single(template.RenderValues(0, new object[] { null }).Parameters);

        Assert.Equal("request", original.Value);
        Assert.Null(original.OriginalValue);
        Assert.False(original.IsProviderValue);
        Assert.Equal((DbType.String, ParameterDirection.Input, 40, (byte)6, (byte)2,
                typeof(SqlQueryShapeValueBindingTest), "Property", "Column", "TEXT"),
            (original.DbType, original.Direction, original.Size, original.Precision, original.Scale,
                original.EntityType, original.PropertyName, original.ColumnName, original.ProviderTypeName));
        Assert.Null(nullValue.Value);
        Assert.Null(nullValue.OriginalValue);
        Assert.False(nullValue.IsProviderValue);
    }

    /// <summary>可变数组输入与渲染结果都应各自快照。</summary>
    [Fact]
    public void RenderValues_WhenBindingArray_ShouldTakeIndependentSnapshots()
    {
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @ids",
            new[] { new SqlParam("@ids", Array.Empty<int>(), DbType.Object) }));
        var values = new[] { 1, 2, 3 };

        var first = Assert.Single(template.RenderValues(0, new object[] { values }).Parameters);
        values[0] = 10;
        Assert.Equal(new[] { 1, 2, 3 }, Assert.IsType<int[]>(first.Value));
        Assert.Equal(new[] { 10, 2, 3 }, Assert.IsType<int[]>(template.RenderValues(0, new object[] { values })
            .Parameters[0].Value));
        Assert.NotSame(first.Value, template.RenderValues(0, new object[] { values }).Parameters[0].Value);
    }

    /// <summary>槽位数量不匹配必须拒绝，之后仍能命中已编译形状。</summary>
    [Fact]
    public void RenderValues_WhenValueCountDiffers_ShouldRejectAndReuseCachedShape()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT @tenant", new[] { new SqlParam("@tenant", 0, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "WHERE @id=@id2",
                new[] { new SqlParam("@id", 0, DbType.Int32), new SqlParam("@id2", 0, DbType.Int32) }) });
        var shape = new SqlQueryShapeKey("users", "sqlite", "v1", "default", 1, "");
        var first = template.RenderValues(shape, new object[] { 1, 2, 3 });
        var dictionaryHit = template.Render(shape, new Dictionary<string, object>
        {
            ["tenant"] = 4,
            ["id"] = 5,
            ["id2"] = 6
        }, null);

        Assert.Throws<ArgumentException>(() => template.RenderValues(shape, new object[] { 1, 2 }));
        Assert.Throws<ArgumentException>(() => template.RenderValues(shape, new object[] { 1, 2, 3, 4 }));
        var hit = template.RenderValues(shape, new object[] { 4, 5, 6 });

        Assert.Same(first.Sql, hit.Sql);
        Assert.Same(first.Sql, dictionaryHit.Sql);
        Assert.Equal(new object[] { 4, 5, 6 }, hit.Parameters.Select(item => item.Value));
        Assert.Equal(1, template.CachedShapeCount);
    }

    /// <summary>参数作用域在位置绑定后改写名称，值顺序保持不变。</summary>
    [Fact]
    public void RenderValues_WhenScopeIsApplied_ShouldRetainBoundValues()
    {
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @id, @tenant",
            new[] { new SqlParam("@id", 0, DbType.Int32), new SqlParam("@tenant", 0, DbType.Int32) }));

        var shape = new SqlQueryShapeKey("users", "sqlite", "v1", "default", 0, "");
        var rendered = template.RenderValues(shape, new object[] { 17, 23 }, new SqlQueryScope("child"));

        Assert.Equal("SELECT @child_id, @child_tenant", rendered.Sql);
        Assert.Equal(new[] { "child_id", "child_tenant" }, rendered.Parameters.Select(item => item.Name));
        Assert.Equal(new object[] { 17, 23 }, rendered.Parameters.Select(item => item.Value));
    }

    /// <summary>并发渲染同一形状时共享 SQL，参数对象和值仍彼此隔离。</summary>
    [Fact]
    public void RenderValues_WhenCalledConcurrently_ShouldIsolateParameterValues()
    {
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @tenant, @id",
            new[] { new SqlParam("@tenant", 0, DbType.Int32), new SqlParam("@id", 0, DbType.Int32) }));
        var shape = new SqlQueryShapeKey("users", "sqlite", "v1", "route-a", 0, "");
        var results = new SqlQueryFragment[256];

        Parallel.For(0, results.Length, index =>
            results[index] = template.RenderValues(shape, new object[] { index, index + 1000 }));

        for (var index = 0; index < results.Length; index++)
        {
            Assert.Equal("SELECT @tenant, @id", results[index].Sql);
            Assert.Same(results[0].Sql, results[index].Sql);
            Assert.Equal(new object[] { index, index + 1000 }, results[index].Parameters.Select(item => item.Value));
            Assert.NotSame(results[0].Parameters[0], results[index].Parameters[0]);
        }
    }
}
