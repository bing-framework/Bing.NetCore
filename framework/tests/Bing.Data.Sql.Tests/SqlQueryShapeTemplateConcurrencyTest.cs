using System.Data;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 验证同一查询形状并发渲染时，可变参数仍按请求隔离。
/// </summary>
public sealed class SqlQueryShapeTemplateConcurrencyTest
{
    /// <summary>共享形状中默认值冲突不应让其他请求的显式绑定失败或串值。</summary>
    [Fact]
    public void Render_WhenConflictingDefaultsAreUsedConcurrently_ShouldIsolateEachRequest()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select @id", new[] { new SqlParam("@id", 1, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "where @id > 0", new[] { new SqlParam("@id", 2, DbType.Int32) }) });
        var results = new SqlQueryFragment[128];

        Parallel.For(0, results.Length, index =>
        {
            if (index % 2 == 0)
                Assert.Throws<InvalidOperationException>(() => template.Render(1));
            else
                results[index] = template.Render(1, new Dictionary<string, object> { ["id"] = index });
        });

        for (var index = 1; index < results.Length; index += 2)
        {
            Assert.Equal("select @id where @id > 0", results[index].Sql);
            Assert.Equal(index, Assert.Single(results[index].Parameters).Value);
        }
        Assert.Equal(1, template.CachedShapeCount);
    }

    /// <summary>多个请求共享形状和作用域时，改名结果与本次参数值均保持隔离。</summary>
    [Fact]
    public void Render_WhenSharedScopeIsUsedConcurrently_ShouldKeepSqlAndValuesIsolated()
    {
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment(
            "select '@id', @id, @_id /* @id */", new[] { new SqlParam("@id", -1), new SqlParam("@_id", -1) }));
        var scope = new SqlQueryScope("shared");
        var results = new SqlQueryFragment[128];

        Parallel.For(0, results.Length, index =>
            results[index] = template.Render(0,
                new Dictionary<string, object> { ["id"] = index, ["_id"] = index + 1000 }, scope));

        for (var index = 0; index < results.Length; index++)
        {
            Assert.Equal("select '@id', @shared_id, @shared_id_1 /* @id */", results[index].Sql);
            var parameters = results[index].Parameters;
            Assert.Equal(new[] { "shared_id", "shared_id_1" }, parameters.Select(parameter => parameter.Name));
            Assert.Equal(new object[] { index, index + 1000 }, parameters.Select(parameter => parameter.Value));
        }
    }

    /// <summary>
    /// 并发请求共享形状缓存时，二进制参数快照不与其他请求或调用方共享。
    /// </summary>
    [Fact]
    public void Render_WhenSameShapeIsRenderedConcurrently_ShouldIsolateMutableParameterSnapshots()
    {
        const int requestCount = 512;
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select * from uploads where tenant_id=@tenant and payload=@payload",
                new[]
                {
                    new SqlParam("@tenant", 0, DbType.Int32),
                    new SqlParam("@payload", Array.Empty<byte>(), DbType.Binary)
                }));
        var shape = new SqlQueryShapeKey("uploads", "sqlite", "mapping-v1", "route-a", 0, string.Empty);
        var inputs = new byte[requestCount][];
        var rendered = new SqlQueryFragment[requestCount];

        for (var index = 0; index < requestCount; index++)
            inputs[index] = new[] { (byte)index, (byte)(index >> 1), (byte)(255 - index % 256), (byte)17 };

        Parallel.For(0, requestCount, new ParallelOptions { MaxDegreeOfParallelism = 16 }, index =>
        {
            rendered[index] = template.Render(shape, new Dictionary<string, object>
            {
                ["tenant"] = index,
                ["payload"] = inputs[index]
            }, null);
        });

        for (var index = 0; index < requestCount; index++)
        {
            Assert.Equal("select * from uploads where tenant_id=@tenant and payload=@payload", rendered[index].Sql);
            Assert.Same(rendered[0].Sql, rendered[index].Sql);
            Assert.Equal(index, rendered[index].Parameters[0].Value);
            Assert.Equal(inputs[index], Assert.IsType<byte[]>(rendered[index].Parameters[1].Value));
        }

        foreach (var input in inputs)
            Array.Fill(input, (byte)0);

        for (var index = 0; index < requestCount; index++)
            Assert.Equal(new[] { (byte)index, (byte)(index >> 1), (byte)(255 - index % 256), (byte)17 },
                Assert.IsType<byte[]>(rendered[index].Parameters[1].Value));

        var firstPayload = Assert.IsType<byte[]>(rendered[0].Parameters[1].Value);
        firstPayload[0] = 99;
        Assert.Equal((byte)1, Assert.IsType<byte[]>(rendered[1].Parameters[1].Value)[0]);
    }
}
