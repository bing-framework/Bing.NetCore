using System.Collections.Concurrent;
using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 查询形状模板与有界缓存的职责测试。
/// </summary>
public sealed class SqlQueryShapeTemplateTest
{
    /// <summary>
    /// 测试目的：可选片段按 OptionalMask 选择，且运行时值只绑定到当前渲染结果。
    /// </summary>
    [Fact]
    public void Render_ShouldSelectOptionalFragmentsAndBindRequestValues()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select * from users where tenant_id=@tenant",
                new[] { new SqlParam("@tenant", 0, DbType.Int32) }),
            new[]
            {
                new SqlQueryOptionalFragment(0,
                    new SqlQueryFragment("and name=@name", new[] { new SqlParam("@name", "template") })),
                new SqlQueryOptionalFragment(1,
                    new SqlQueryFragment("and status=@status", new[] { new SqlParam("@status", "template") }))
            });

        var rendered = template.Render(1, new Dictionary<string, object>
        {
            ["tenant"] = 7,
            ["name"] = "alice"
        });

        Assert.Equal("select * from users where tenant_id=@tenant and name=@name", rendered.Sql);
        Assert.Equal(new[] { "@tenant", "@name" }, rendered.Parameters.Select(item => item.Name));
        Assert.Equal(7, rendered.Parameters[0].Value);
        Assert.Equal("alice", rendered.Parameters[1].Value);
        Assert.DoesNotContain("status", rendered.Sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 测试目的：重复参数槽只保留一个绑定项，并保持必选、可选片段的稳定顺序。
    /// </summary>
    [Fact]
    public void Render_ShouldKeepStableParameterOrderAndReuseRepeatedSlot()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select * from orders where tenant_id=@tenant",
                new[] { new SqlParam("@tenant", 1, DbType.Int32) }),
            new[]
            {
                new SqlQueryOptionalFragment(0,
                    new SqlQueryFragment("and state=@state",
                        new[] { new SqlParam("@state", "new", DbType.String) })),
                new SqlQueryOptionalFragment(1,
                    new SqlQueryFragment("and owner_tenant=@tenant",
                        new[] { new SqlParam("@tenant", 2, DbType.Int32) }))
            });

        var rendered = template.Render(3, new Dictionary<string, object>
        {
            ["tenant"] = 9,
            ["state"] = "paid"
        });

        Assert.Equal("select * from orders where tenant_id=@tenant and state=@state and owner_tenant=@tenant", rendered.Sql);
        Assert.Equal(new[] { "@tenant", "@state" }, rendered.Parameters.Select(item => item.Name));
        Assert.Equal(9, rendered.Parameters[0].Value);
        Assert.Equal("paid", rendered.Parameters[1].Value);
    }

    /// <summary>共享槽的不同拼写统一到首个参数标记，字符串和注释内容不变。</summary>
    [Theory]
    [InlineData("@id", "@ID", "@id")]
    [InlineData("@id", ":id", "@id")]
    [InlineData("id", ":ID", ":id")]
    public void Render_WhenRepeatedSlotSpellingDiffers_ShouldUseFirstSqlToken(string firstName, string repeatedName, string token)
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select " + token + ", ':ID' /* @ID */", new[] { new SqlParam(firstName, 1, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "where " + repeatedName + " = " + token,
                new[] { new SqlParam(repeatedName, 1, DbType.Int32) }) });

        var withoutOptional = template.Render();
        var rendered = template.Render(1, new Dictionary<string, object> { ["id"] = 7 });

        Assert.Equal("select " + token + ", ':ID' /* @ID */", withoutOptional.Sql);
        Assert.Equal("select " + token + ", ':ID' /* @ID */ where " + token + " = " + token, rendered.Sql);
        Assert.Equal(7, Assert.Single(rendered.Parameters).Value);
    }

    /// <summary>缺少方言转义模式时，不应把可能位于字符串中的参数文本改写成 SQL。</summary>
    [Fact]
    public void Render_WhenTokenRewriteHasAmbiguousEscapedQuote_ShouldRejectSelectedShape()
    {
        const string required = "select E'can\\'t :id', @id";
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment(required, new[] { new SqlParam("@id", 1, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "where :id = @id", new[] { new SqlParam(":id", 1, DbType.Int32) }) });

        Assert.Equal(required, template.Render().Sql);
        Assert.Throws<InvalidOperationException>(() => template.Render(1));
    }

    /// <summary>同名参数的绑定配置不一致时，启用冲突片段必须在绑定前拒绝。</summary>
    [Theory]
    [InlineData("DbType")]
    [InlineData("Direction")]
    [InlineData("Size")]
    [InlineData("Precision")]
    [InlineData("Scale")]
    [InlineData("EntityType")]
    [InlineData("PropertyName")]
    [InlineData("ColumnName")]
    [InlineData("DatabaseType")]
    [InlineData("ProviderTypeName")]
    [InlineData("Source")]
    [InlineData("MetadataLevel")]
    [InlineData("StorageKind")]
    [InlineData("ConverterKind")]
    [InlineData("CustomConverterName")]
    public void Render_WhenRepeatedParameterContractDiffers_ShouldRejectSelectedShape(string difference)
    {
        var first = new SqlParam("@id", 1, DbType.Int32, ParameterDirection.Input, 10, 6, 2);
        var second = new SqlParam("@id", 1,
            difference == "DbType" ? DbType.String : DbType.Int32,
            difference == "Direction" ? ParameterDirection.Output : ParameterDirection.Input,
            difference == "Size" ? 20 : 10,
            difference == "Precision" ? (byte)7 : (byte)6,
            difference == "Scale" ? (byte)3 : (byte)2)
        {
            EntityType = difference == "EntityType" ? typeof(SqlQueryShapeTemplateTest) : null,
            PropertyName = difference == "PropertyName" ? "Id" : null,
            ColumnName = difference == "ColumnName" ? "id" : null,
            DatabaseType = difference == "DatabaseType" ? DatabaseType.Sqlite : null,
            ProviderTypeName = difference == "ProviderTypeName" ? "INTEGER" : null,
            Source = difference == "Source" ? SqlParameterSource.Lambda : SqlParameterSource.Unknown,
            MetadataLevel = difference == "MetadataLevel" ? SqlParameterMetadataLevel.Full : SqlParameterMetadataLevel.None,
            StorageKind = difference == "StorageKind" ? ColumnStorageKind.Number : ColumnStorageKind.Default,
            ConverterKind = difference == "ConverterKind" ? FieldValueConverterKind.EnumToValue : FieldValueConverterKind.None,
            CustomConverterName = difference == "CustomConverterName" ? "custom" : null
        };
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("select @id", new[] { first }),
            new[] { new SqlQueryOptionalFragment(0, "where @id > 0", new[] { second }) });

        var required = template.Render();
        var error = Assert.Throws<InvalidOperationException>(() =>
            template.Render(1, new Dictionary<string, object> { ["id"] = 7 }));

        Assert.Equal("select @id", required.Sql);
        Assert.Equal(1, Assert.Single(required.Parameters).Value);
        Assert.Contains("id", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, template.CachedShapeCount);
    }

    /// <summary>共享槽默认值不同时，必须提供请求值；失败不能污染后续缓存使用。</summary>
    [Fact]
    public void Render_WhenRepeatedDefaultsDiffer_ShouldRequireRequestValue()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select @id", new[] { new SqlParam("@id", 1, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "where @id > 0", new[] { new SqlParam("@id", 2, DbType.Int32) }) });

        Assert.Throws<InvalidOperationException>(() => template.Render(1));
        var rendered = template.Render(1, new Dictionary<string, object> { ["id"] = 7 });
        Assert.Equal("select @id where @id > 0", rendered.Sql);
        Assert.Equal(7, Assert.Single(rendered.Parameters).Value);
        Assert.Throws<InvalidOperationException>(() => template.Render(1));
        Assert.Equal(1, template.CachedShapeCount);
    }

    /// <summary>数组默认值按内容比较，快照副本相同内容时仍可共享。</summary>
    [Fact]
    public void Render_WhenRepeatedArrayDefaultsMatch_ShouldReuseParameter()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select @payload", new[] { new SqlParam("@payload", new byte[] { 1, 2 }) }),
            new[] { new SqlQueryOptionalFragment(0, "where @payload is not null",
                new[] { new SqlParam("@payload", new byte[] { 1, 2 }) }) });

        var rendered = template.Render(1);
        Assert.Equal("select @payload where @payload is not null", rendered.Sql);
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(Assert.Single(rendered.Parameters).Value));
    }

    /// <summary>字典及其中的数组作为默认值时，快照应按内容判断能否共享。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Render_WhenRepeatedDictionaryDefaultsAreCompared_ShouldRespectContents(bool changed)
    {
        var first = new Dictionary<string, object>
        {
            ["payload"] = new object[] { new Dictionary<string, object> { ["ids"] = new[] { 1, 2 } } }
        };
        var second = new Dictionary<string, object>
        {
            ["payload"] = new object[] { new Dictionary<string, object> { ["ids"] = new[] { 1, changed ? 3 : 2 } } }
        };
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("select @value", new[] { new SqlParam("@value", first) }),
            new[] { new SqlQueryOptionalFragment(0, "where @value is not null", new[] { new SqlParam("@value", second) }) });

        if (changed)
        {
            Assert.Throws<InvalidOperationException>(() => template.Render(1));
            var overridden = template.Render(1, new Dictionary<string, object> { ["value"] = null });
            Assert.Equal("select @value where @value is not null", overridden.Sql);
            Assert.Null(Assert.Single(overridden.Parameters).Value);
        }
        else
        {
            var rendered = template.Render(1);
            Assert.Equal("select @value where @value is not null", rendered.Sql);
            Assert.Single(rendered.Parameters);
        }
    }

    /// <summary>数组内容及已转换状态存在差异时不能静默采用第一个默认参数。</summary>
    [Theory]
    [InlineData("Array")]
    [InlineData("ProviderState")]
    [InlineData("OriginalValue")]
    public void Render_WhenRepeatedDefaultStateDiffers_ShouldRequireRequestValue(string difference)
    {
        var first = new SqlParam("@value", new byte[] { 1, 2 }) { OriginalValue = "original" };
        var second = new SqlParam("@value", difference == "Array" ? new byte[] { 1, 3 } : new byte[] { 1, 2 })
        {
            OriginalValue = difference == "OriginalValue" ? "other" : "original",
            IsProviderValue = difference == "ProviderState"
        };
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("select @value", new[] { first }),
            new[] { new SqlQueryOptionalFragment(0, "where @value is not null", new[] { second }) });

        Assert.Throws<InvalidOperationException>(() => template.Render(1));
        var rendered = template.Render(1, new Dictionary<string, object> { ["value"] = new byte[] { 4, 5 } });
        Assert.Equal("select @value where @value is not null", rendered.Sql);
        Assert.Equal(new byte[] { 4, 5 }, Assert.IsType<byte[]>(Assert.Single(rendered.Parameters).Value));
    }

    /// <summary>
    /// 测试目的：渲染结果之间及集合参数之间保持深隔离，模板不保存请求值。
    /// </summary>
    [Fact]
    public void Render_ShouldIsolateRequestParametersAndCloneCollectionValues()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select * from users where id in (@ids)",
                new[] { new SqlParam("@ids", new[] { 1, 2 }, DbType.Int32) }));
        var firstIds = new[] { 3, 4 };
        var secondIds = new[] { 5, 6 };

        var first = template.Render(0, new Dictionary<string, object> { ["ids"] = firstIds });
        var second = template.Render(0, new Dictionary<string, object> { ["ids"] = secondIds });
        firstIds[0] = 99;
        ((int[])first.Parameters[0].Value)[1] = 88;

        Assert.Equal(new[] { 3, 4 }, Assert.IsType<int[]>(first.Parameters[0].Value));
        Assert.Equal(new[] { 5, 6 }, Assert.IsType<int[]>(second.Parameters[0].Value));
        Assert.Equal(new[] { 1, 2 }, Assert.IsType<int[]>(template.Render().Parameters[0].Value));
    }

    /// <summary>
    /// 同一形状复用 SQL，绑定值和作用域仍属于各自的渲染结果。
    /// </summary>
    [Fact]
    public void Render_WhenShapeRepeats_ShouldReuseSqlAndIsolateRequestValues()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select * from users where tenant_id=@tenant", new[] { new SqlParam("@tenant", 0) }),
            new[] { new SqlQueryOptionalFragment(0, "and id=@id", new[] { new SqlParam("@id", 0) }) });
        var shape = new SqlQueryShapeKey("users", "sqlite", "v1", "tenant-a", 1, string.Empty);
        var first = template.Render(shape, new Dictionary<string, object> { ["tenant"] = 1, ["id"] = 11 }, null);
        var second = template.Render(shape, new Dictionary<string, object> { ["tenant"] = 2, ["id"] = 22 }, null);
        var scoped = template.Render(shape, new Dictionary<string, object> { ["tenant"] = 3, ["id"] = 33 }, new SqlQueryScope("child"));

        Assert.Equal("select * from users where tenant_id=@tenant and id=@id", first.Sql);
        Assert.Same(first.Sql, second.Sql);
        Assert.Equal(new object[] { 1, 11 }, first.Parameters.Select(item => item.Value));
        Assert.Equal(new object[] { 2, 22 }, second.Parameters.Select(item => item.Value));
        Assert.Equal("select * from users where tenant_id=@child_tenant and id=@child_id", scoped.Sql);
        Assert.Equal(new object[] { 3, 33 }, scoped.Parameters.Select(item => item.Value));
    }

    /// <summary>
    /// Provider、映射、路由和集合形状不同的键不共享同一个缓存条目。
    /// </summary>
    [Fact]
    public void Render_WhenShapeKeyChanges_ShouldIsolateCachedSql()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select * from users where tenant_id=@tenant", new[] { new SqlParam("@tenant", 0) }),
            new[] { new SqlQueryOptionalFragment(0, "and id=@id", new[] { new SqlParam("@id", 0) }) });
        var shape = new SqlQueryShapeKey("users", "sqlite", "v1", "tenant-a", 1, "ids:1");
        var first = template.Render(shape, null, null);
        var alternatives = new[]
        {
            shape with { QueryId = "other" }, shape with { ProviderKey = "postgres" },
            shape with { MappingVersion = "v2" }, shape with { RouteKey = "tenant-b" },
            shape with { CollectionShape = "ids:2" }
        };

        foreach (var alternative in alternatives)
        {
            var rendered = template.Render(alternative, null, null);
            Assert.Equal("select * from users where tenant_id=@tenant and id=@id", rendered.Sql);
            Assert.NotSame(first.Sql, rendered.Sql);
            Assert.Same(rendered.Sql, template.Render(alternative, null, null).Sql);
        }
    }

    /// <summary>
    /// 并发请求共享不可变 SQL，但各自持有独立参数。
    /// </summary>
    [Fact]
    public void Render_WhenCalledConcurrently_ShouldIsolateBoundValues()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select * from users where tenant_id=@tenant", new[] { new SqlParam("@tenant", 0) }),
            new[] { new SqlQueryOptionalFragment(0, "and id=@id", new[] { new SqlParam("@id", 0) }) });
        var shape = new SqlQueryShapeKey("users", "sqlite", "v1", "route", 1, string.Empty);
        var results = new SqlQueryFragment[512];
        Parallel.For(0, results.Length, index =>
            results[index] = template.Render(shape, new Dictionary<string, object>
            {
                ["tenant"] = index,
                ["id"] = index + 1000
            }, null));

        for (var index = 0; index < results.Length; index++)
        {
            Assert.Equal("select * from users where tenant_id=@tenant and id=@id", results[index].Sql);
            Assert.Same(results[0].Sql, results[index].Sql);
            Assert.Equal(new object[] { index, index + 1000 }, results[index].Parameters.Select(item => item.Value));
        }
    }

    /// <summary>
    /// 可复用模板遇到大量路由键时，缓存条目数仍有上限。
    /// </summary>
    [Fact]
    public void Render_WhenManyShapeKeysAreUsed_ShouldBoundTemplateCache()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select * from users where tenant_id=@tenant", new[] { new SqlParam("@tenant", 0) }),
            new[] { new SqlQueryOptionalFragment(0, "and id=@id", new[] { new SqlParam("@id", 0) }) });
        var shape = new SqlQueryShapeKey("users", "sqlite", "v1", "route-0", 1, string.Empty);
        var first = template.Render(shape, null, null);
        for (var index = 1; index < 300; index++)
            template.Render(shape with { RouteKey = "route-" + index }, null, null);

        Assert.Equal(256, template.CachedShapeCount);
        var rebuilt = template.Render(shape, null, null);
        Assert.Equal("select * from users where tenant_id=@tenant and id=@id", rebuilt.Sql);
        Assert.NotSame(first.Sql, rebuilt.Sql);
        Assert.Equal(256, template.CachedShapeCount);
    }

    /// <summary>
    /// 新形状不应重复复制模板中的大默认值；每次渲染仍独立快照本次参数。
    /// </summary>
    [Fact]
    public void Render_WhenShapeChanges_ShouldNotCopyLargeTemplateDefaultForTheCache()
    {
        var template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("select * from users where payload=@payload",
                new[] { new SqlParam("@payload", new byte[1024 * 1024]) }),
            new[] { new SqlQueryOptionalFragment(0, "and id=@id", new[] { new SqlParam("@id", 0) }) });
        var values = new Dictionary<string, object> { ["payload"] = new byte[] { 1 }, ["id"] = 3 };
        var shape = new SqlQueryShapeKey("users", "sqlite", "v1", "route-1", 1, string.Empty);
        template.Render(shape, values, null);

        var beforeWarm = GC.GetAllocatedBytesForCurrentThread();
        var warm = template.Render(shape, values, null);
        var warmAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeWarm;
        var beforeCold = GC.GetAllocatedBytesForCurrentThread();
        var cold = template.Render(shape with { RouteKey = "route-2" }, values, null);
        var coldAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeCold;

        Assert.Equal("select * from users where payload=@payload and id=@id", cold.Sql);
        Assert.Equal(new byte[] { 1 }, Assert.IsType<byte[]>(warm.Parameters[0].Value));
        Assert.Equal(new byte[] { 1 }, Assert.IsType<byte[]>(cold.Parameters[0].Value));
        Assert.True(warmAllocated < 128 * 1024, $"绑定新值时不应复制大默认值，实际分配 {warmAllocated} B。");
        Assert.True(coldAllocated <= warmAllocated + 128 * 1024,
            $"新形状额外分配过多：热命中 {warmAllocated} B，未命中 {coldAllocated} B。");
    }

    /// <summary>覆盖参数值时保留映射元数据，清除已转换状态和原值。</summary>
    [Fact]
    public void Render_WhenValueIsOverridden_ShouldPreserveMetadataAndResetConversionState()
    {
        var parameter = new SqlParam("@value", "provider-value", DbType.String, ParameterDirection.Input, 40, 6, 2)
        {
            OriginalValue = "original",
            IsProviderValue = true,
            EntityType = typeof(SqlQueryShapeTemplateTest),
            PropertyName = "Property",
            ColumnName = "Column",
            ProviderTypeName = "TEXT"
        };
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("select @value", new[] { parameter }));
        var original = Assert.Single(template.Render().Parameters);
        var overridden = Assert.Single(template.Render(0, new Dictionary<string, object> { ["value"] = "request" }).Parameters);

        Assert.Equal("provider-value", original.Value);
        Assert.Equal("original", original.OriginalValue);
        Assert.True(original.IsProviderValue);
        Assert.Equal("request", overridden.Value);
        Assert.Null(overridden.OriginalValue);
        Assert.False(overridden.IsProviderValue);
        Assert.Equal((original.DbType, original.Direction, original.Size, original.Precision, original.Scale,
                original.EntityType, original.PropertyName, original.ColumnName, original.ProviderTypeName),
            (overridden.DbType, overridden.Direction, overridden.Size, overridden.Precision, overridden.Scale,
                overridden.EntityType, overridden.PropertyName, overridden.ColumnName, overridden.ProviderTypeName));
    }

    /// <summary>
    /// 测试目的：查询形状缓存应在并发下共享同一个值，并受容量上限约束。
    /// </summary>
    [Fact]
    public async Task Cache_ShouldShareConcurrentCreationAndEvictWithinCapacity()
    {
        var cache = new SqlQueryShapeCache<SqlQueryShapeKey, string>(2);
        var factoryCalls = 0;
        var key = new SqlQueryShapeKey("users", "sqlite", "v1", "default", 1, "ids:2");

        var values = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            cache.GetOrAdd(key, () =>
            {
                Interlocked.Increment(ref factoryCalls);
                Thread.Sleep(1);
                return "select 1";
            }))));

        cache.GetOrAdd(key with { OptionalMask = 2 }, () => "select 2");
        cache.GetOrAdd(key with { OptionalMask = 4 }, () => "select 4");

        Assert.All(values, value => Assert.Equal("select 1", value));
        Assert.Equal(1, factoryCalls);
        Assert.True(cache.Count <= 2);
        Assert.True(cache.HitCount > 0);
        Assert.True(cache.EvictionCount > 0);
    }

    /// <summary>
    /// 测试目的：失败的形状生成不能污染缓存，后续请求仍可重新生成。
    /// </summary>
    [Fact]
    public void Cache_ShouldRemoveFailedFactoryResult()
    {
        var cache = new SqlQueryShapeCache<SqlQueryShapeKey, string>(2);
        var key = new SqlQueryShapeKey("users", "sqlite", "v1", "default", 0, string.Empty);
        var calls = 0;

        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(key, () =>
        {
            calls++;
            throw new InvalidOperationException("generation failed");
        }));

        Assert.Equal("select 1", cache.GetOrAdd(key, () =>
        {
            calls++;
            return "select 1";
        }));
        Assert.Equal(2, calls);
        Assert.Equal(1, cache.Count);
    }

    /// <summary>
    /// 工厂失败后重建的键不能被失败条目的旧淘汰记录删除。
    /// </summary>
    [Fact]
    public void Cache_WhenFailedKeyIsRecreated_ShouldEvictOldestLiveEntry()
    {
        var cache = new SqlQueryShapeCache<string, object>(2);
        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("a", () => throw new InvalidOperationException()));
        var older = cache.GetOrAdd("b", () => new object());
        var recreated = cache.GetOrAdd("a", () => new object());

        cache.GetOrAdd("c", () => new object());

        Assert.Same(recreated, cache.GetOrAdd("a", () => new object()));
        Assert.NotSame(older, cache.GetOrAdd("b", () => new object()));
        Assert.Equal(2, cache.Count);
    }

    /// <summary>
    /// 反复生成失败时，缓存及淘汰记录都不能随失败次数增长。
    /// </summary>
    [Fact]
    public void Cache_WhenFactoryRepeatedlyFails_ShouldNotRetainEvictionRecords()
    {
        var cache = new SqlQueryShapeCache<int, object>(2);
        for (var index = 0; index < 1000; index++)
            Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(index, () => throw new InvalidOperationException()));

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.TrackedCount);
        cache.GetOrAdd(0, () => new object());
        Assert.Equal(1, cache.TrackedCount);
    }

    /// <summary>
    /// 不限制容量的缓存不需要维护淘汰记录。
    /// </summary>
    [Fact]
    public void Cache_WhenCapacityIsUnlimited_ShouldNotTrackEvictions()
    {
        var cache = new SqlQueryShapeCache<int, object>(null);
        for (var index = 0; index < 100; index++)
            cache.GetOrAdd(index, () => new object());

        Assert.Equal(100, cache.Count);
        Assert.Equal(0, cache.TrackedCount);
    }

    /// <summary>缓存必须支持值类型和 null 结果，并在命中时复用工厂结果。</summary>
    [Fact]
    public void Cache_WhenValueIsDefaultOrNull_ShouldReuseFactoryResult()
    {
        var valueCache = new SqlQueryShapeCache<int, int>();
        var referenceCache = new SqlQueryShapeCache<int, object>();
        var calls = 0;

        Assert.Equal(0, valueCache.GetOrAdd(1, () => { calls++; return 0; }));
        Assert.Equal(0, valueCache.GetOrAdd(1, () => { calls++; return 1; }));
        Assert.Null(referenceCache.GetOrAdd(1, () => { calls++; return null; }));
        Assert.Null(referenceCache.GetOrAdd(1, () => { calls++; return new object(); }));
        Assert.Equal(2, calls);
    }

    /// <summary>并发创建大量不同形状时，缓存应保留容量内的有效条目并清理失败结果。</summary>
    [Fact]
    public void Cache_WhenManyKeysAreCreatedConcurrently_ShouldRetainBoundedLiveEntries()
    {
        const int capacity = 32;
        const int total = 1024;
        for (var round = 0; round < 12; round++)
        {
            var cache = new SqlQueryShapeCache<int, int>(capacity);
            Parallel.For(0, total, index =>
            {
                if (index % 8 == 0)
                    Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(index, () => throw new InvalidOperationException()));
                else
                    Assert.Equal(index, cache.GetOrAdd(index, () => index));
            });

            Assert.Equal(capacity, cache.Count);
            Assert.Equal(capacity, cache.TrackedCount);
            Assert.Equal(total - total / 8 - capacity, cache.EvictionCount);
        }
    }

    /// <summary>绑定大数组时保留独立快照，避免在一次渲染中重复复制和逐项装箱。</summary>
    [Fact]
    public void Render_WhenBindingLargeByteArray_ShouldTakeOneIsolatedSnapshot()
    {
        var template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @payload",
            new[] { new SqlParam("@payload", Array.Empty<byte>(), DbType.Binary) }));
        var payload = new byte[1024 * 1024];
        payload[0] = 23;
        var values = new Dictionary<string, object> { ["payload"] = payload };
        template.Render(0, values);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = template.Render(0, values);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal("SELECT @payload", result.Sql);
        Assert.InRange(allocated, payload.Length, payload.Length + 128 * 1024);
        payload[0] = 99;
        var snapshot = Assert.IsType<byte[]>(Assert.Single(result.Parameters).Value);
        Assert.Equal(23, snapshot[0]);
        snapshot[0] = 88;
        Assert.Equal(23, Assert.IsType<byte[]>(Assert.Single(result.Parameters).Value)[0]);
    }

    /// <summary>
    /// 测试目的：不同 Provider、路由和集合形状必须得到互不相等的缓存键。
    /// </summary>
    [Fact]
    public void ShapeKey_ShouldSeparateProviderRouteAndCollectionShape()
    {
        var sqlite = new SqlQueryShapeKey("users", "sqlite", "v1", "tenant_a", 3, "ids:1");
        var sqlServer = sqlite with { ProviderKey = "sqlserver" };
        var anotherRoute = sqlite with { RouteKey = "tenant_b" };
        var anotherCollection = sqlite with { CollectionShape = "ids:10" };

        Assert.NotEqual(sqlite, sqlServer);
        Assert.NotEqual(sqlite, anotherRoute);
        Assert.NotEqual(sqlite, anotherCollection);
    }
}
