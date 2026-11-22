using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Builders.Core;
using Moq;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>生成元数据与既有映射、版本及访问器缓存的合同。</summary>
public sealed class GeneratedMappingContractTest
{
    /// <summary>旧第三方映射可在 JIT 回退；严格列解析必须依赖模型而不枚举属性。</summary>
    [Fact]
    public void EntityResolver_WhenCustomMappingHasNoModel_ShouldRejectStrictFallbackAndKeepJitCompatibility()
    {
        var empty = new Mock<IEntityMappingResolver>();
        var compatible = new EntityResolver(empty.Object);
        Assert.Equal("Name", compatible.GetColumns<Model>(false));
        var strict = new EntityResolver(empty.Object, options: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });
        Assert.Throws<NotSupportedException>(() => strict.GetColumns<Model>(false));
        var models = new RegisteredEntityModelMetadataProvider(CreateModel(value => ((Model)value).Name));
        var generated = new EntityResolver(entityModelMetadataProvider: models,
            options: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });
        Assert.Equal("Name", generated.GetColumns<Model>(false));
    }

    [Fact]
    public void SharedResolver_ShouldPartitionModelAndStrictGetterCaches()
    {
        var models = new RegisteredEntityModelMetadataProvider(CreateModel(value => ((Model)value).Name));
        var resolver = new DefaultEntityMappingResolver(models);
        var compatible = new SqlMetadataOptions();
        var strict = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        var legacy = SqlMutationPlanCaches.Get(resolver, compatible);
        var generated = SqlMutationPlanCaches.Get(resolver, strict, models);

        Assert.Same(generated, SqlMutationPlanCaches.Get(resolver, strict, models));
        Assert.NotSame(legacy, generated);
        Assert.NotSame(generated, SqlMutationPlanCaches.Get(resolver, strict,
            new RegisteredEntityModelMetadataProvider(CreateModel(value => ((Model)value).Name))));
        var column = new ColumnMappingMetadata { PropertyName = "Name" };
        Assert.Equal("cached", legacy.GetValue(new { Name = "cached" }, column));
        Assert.Throws<NotSupportedException>(() => generated.GetValue(new { Name = "cached" }, column));
        Assert.Equal(1, legacy.GetterCacheMissCount);
        Assert.Equal(0, generated.GetterCount);
    }

    [Fact]
    public void GeneratedGetter_ShouldBypassReflectionAndIsolateSources()
    {
        var calls = 0;
        var registry = new RegisteredEntityModelMetadataProvider(CreateModel(value =>
        {
            calls++;
            return ((Model)value).Name + "-generated";
        }));
        var cache = new SqlMutationPlanCache(models: registry, generatedOnly: true);
        var column = new ColumnMappingMetadata { PropertyName = nameof(Model.Name) };
        Assert.Equal("one-generated", cache.GetValue(new Model { Name = "one" }, column));
        Assert.Equal("two-generated", cache.GetValue(new Model { Name = "two" }, column));
        Assert.Equal(2, calls);
        Assert.Equal(1, cache.GetterCacheMissCount);
        Assert.Equal(1, cache.GetterCacheHitCount);
        Assert.Throws<NotSupportedException>(() => cache.GetValue(new { Name = "other" }, column));
        Assert.Equal(1, cache.GetterCount);
    }

    [Fact]
    public void GeneratedMapping_ShouldUseRuntimeOverridesAndKeepVersionSnapshots()
    {
        var models = new RegisteredEntityModelMetadataProvider(CreateModel(value => ((Model)value).Name));
        var options = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        options.EntityMappings.Add(CreateMapping("old_table", "old_name"));
        var resolver = new VersionedEntityMappingResolver(options: options, entityModelMetadataProvider: models);
        var oldSnapshot = resolver.CaptureSnapshot();
        var oldMapping = oldSnapshot.Resolve(typeof(Model), new DatabaseContext());
        Assert.Same(oldMapping, oldSnapshot.Resolve(typeof(Model), new DatabaseContext()));
        resolver.PublishMappings(new[] { CreateMapping("new_table", "new_name") });
        var current = resolver.Resolve(typeof(Model), new DatabaseContext());
        Assert.Equal("new_table", current.Table.TableName);
        Assert.Equal("new_name", current.Columns[nameof(Model.Name)].ColumnName);
        Assert.Equal("old_table", oldMapping.Table.TableName);
        Assert.Equal("old_name", oldMapping.Columns[nameof(Model.Name)].ColumnName);
        Assert.Same(models.GetMetadata<Model>(), current.Model);
        Assert.Throws<NotSupportedException>(() => resolver.Resolve(typeof(Unregistered), new DatabaseContext()));
    }

    [Fact]
    public void GeneratedOnlyResolver_ShouldRejectUnknownModelWithoutConventionFallback()
    {
        var resolver = new DefaultEntityMappingResolver(options: new SqlMetadataOptions
        {
            ExecutionMode = SqlExecutionMode.GeneratedOnly
        });
        var error = Assert.Throws<NotSupportedException>(() => resolver.Resolve(typeof(Model), new DatabaseContext()));
        Assert.Contains(typeof(Model).FullName, error.Message);
    }

    private static EntityModelMetadata CreateModel(Func<object, object> getter) => new(typeof(Model), "models", null,
        new[] { EntityPropertyMetadata.CreateGenerated(nameof(Model.Name), typeof(string), true, true,
            generatedGetter: getter, generatedSetter: (source, value) => ((Model)source).Name = (string)value) },
        () => new Model());

    private static EntityMappingOptions CreateMapping(string table, string name)
    {
        var mapping = new EntityMappingOptions { EntityType = typeof(Model), TableName = table };
        mapping.Columns[nameof(Model.Name)] = new ColumnMappingOptions { ColumnName = name };
        return mapping;
    }

    public sealed class Model { public string Name { get; set; } }
    public sealed class Unregistered { public string Name { get; set; } }
}
