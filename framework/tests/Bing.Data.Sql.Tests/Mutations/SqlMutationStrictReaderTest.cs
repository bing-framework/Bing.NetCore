using Bing.Data.KeyGeneration;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Mutations;

/// <summary>修改宿主开关的测试独占执行，避免影响其他兼容模式测试。</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlMutationRuntimeFeatureCollection
{
    public const string Name = "Sql mutation runtime feature";
}

/// <summary>严格宿主使用映射计划读取，兼容宿主保留按来源类型查找的边界。</summary>
[Collection(SqlMutationRuntimeFeatureCollection.Name)]
public sealed class SqlMutationStrictReaderTest
{
    /// <summary>缺少计划读取器时，严格宿主不能通过另一种已注册来源绕过映射合同。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BatchGetValue_WhenSourceDiffersFromMapping_ShouldRespectHostFallback(bool fallback)
    {
        WithFallback(fallback, () =>
        {
            var services = CreateServices();
            var builder = new DefaultSqlEntityMutationCommandBuilder(TestMutationSqlProvider.Instance, services);
            var context = builder.CreateUpdateRenderContext(new[] { new Row { Id = 1, Name = "mapped" } },
                new SqlUpdateOptions());
            var column = Assert.Single(context.UpdateColumns);
            var source = new OtherRow { Name = "other" };

            if (fallback)
                Assert.Equal("other", context.GetValue(source, column));
            else
            {
                var error = Assert.Throws<NotSupportedException>(() => context.GetValue(source, column));
                Assert.Equal($"类型 {typeof(Row).FullName} 的属性 Name 没有匹配当前映射的生成读取器。", error.Message);
            }

            var cache = SqlMutationPlanCaches.Get(services.EntityMappingResolver, services.MetadataOptions,
                services.EntityModelMetadataProvider);
            Assert.Equal(fallback ? 1 : 0, cache.GetterCount);
            Assert.Equal(fallback ? 1 : 0, cache.GetterCacheMissCount);
        });
    }

    /// <summary>严格宿主仍使用登记基类的静态读取器，读取隐藏成员时遵循映射类型。</summary>
    [Fact]
    public void InsertUpdateAndBatch_WithHostFallbackDisabled_ShouldReadMappedBaseMembers()
    {
        WithFallback(false, () =>
        {
            var services = CreateServices();
            var builder = new DefaultSqlEntityMutationCommandBuilder(TestMutationSqlProvider.Instance, services);
            Row source = new DerivedRow { Name = "hidden" };
            source.Id = 7;
            source.Name = "mapped";

            var insert = builder.Insert(source);
            var update = builder.Update(source);
            Assert.Equal("Insert Into [rows] ([Id], [Name]) Values (@_p_0, @_p_1)", insert.Sql);
            Assert.Equal(new object[] { 7, "mapped" }, insert.Parameters.Select(item => item.Value));
            Assert.Equal("Update [rows] Set [Name] = @_p_0 Where [Id] = @_p_1", update.Sql);
            Assert.Equal(new object[] { "mapped", 7 }, update.Parameters.Select(item => item.Value));
            var context = builder.CreateUpdateRenderContext(new[] { source }, new SqlUpdateOptions());
            Assert.Equal("mapped", context.GetValue(source, Assert.Single(context.UpdateColumns)));
            Assert.Equal(0, builder.GetterCacheCount);
        });
    }

    /// <summary>带键插入也遵循映射类型的生成读取器，不进入运行时 Getter 缓存。</summary>
    [Fact]
    public void KeyInsert_WithHostFallbackDisabled_ShouldReadMappedBaseMembers()
    {
        WithFallback(false, () =>
        {
            var services = CreateServices();
            Row source = new DerivedRow { Name = "hidden" };
            source.Id = 8;
            source.Name = "mapped";
            var policy = EntityKeyPolicy.CreateGenerated<Row, int>(nameof(Row.Id),
                EntityKeyGenerationKind.Assigned, row => row.Id, (row, value) => row.Id = value);
            var command = new SqlKeyInsertCommandBuilder(
                new SqlInsertBuilder(TestMutationSqlProvider.Instance, services), policy).Build(source);

            Assert.Equal("Insert Into [rows] ([Id], [Name]) Values (@_p_0, @_p_1)", command.Sql);
            Assert.Equal(new object[] { 8, "mapped" }, command.Parameters.Select(item => item.Value));
            var cache = SqlMutationPlanCaches.Get(services.EntityMappingResolver, services.MetadataOptions,
                services.EntityModelMetadataProvider);
            Assert.Equal(0, cache.GetterCount);
            Assert.Equal(0, cache.GetterCacheMissCount);
        });
    }

    private static SqlBuilderServices CreateServices()
    {
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true, isKey: true,
                generatedGetter: source => ((Row)source).Id),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Name), typeof(string), true, true,
                generatedGetter: source => ((Row)source).Name)
        });
        var other = new EntityModelMetadata(typeof(OtherRow), "other_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(OtherRow.Name), typeof(string), true, true,
                generatedGetter: source => ((OtherRow)source).Name)
        });
        return new SqlBuilderServices(metadataOptions: new SqlMetadataOptions
        {
            ExecutionMode = SqlExecutionMode.GeneratedOnly
        }, entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model, other));
    }

    private static void WithFallback(bool enabled, Action test)
    {
        var hadValue = AppContext.TryGetSwitch(SqlRuntimeFeatures.ReflectionFallbackSwitch, out var previous);
        AppContext.SetSwitch(SqlRuntimeFeatures.ReflectionFallbackSwitch, enabled);
        try { test(); }
        finally { AppContext.SetSwitch(SqlRuntimeFeatures.ReflectionFallbackSwitch, hadValue ? previous : true); }
    }

    private class Row { public int Id { get; set; } public string Name { get; set; } }
    private sealed class DerivedRow : Row { public new string Name { get; set; } }
    private sealed class OtherRow { public string Name { get; set; } }
}
