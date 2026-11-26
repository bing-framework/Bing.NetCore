using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Metadata;
using Moq;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>验证生成参数查找使用登记模型，完整查找仍保留约定回退。</summary>
public sealed class CompositeEntityModelMetadataProviderRegisteredTest
{
    [Fact]
    public void GetRegisteredMetadata_WhenUnregistered_ShouldSkipConventionFallback()
    {
        var provider = new CompositeEntityModelMetadataProvider();

        Assert.Null(provider.GetRegisteredMetadata(typeof(Unregistered)));
        Assert.NotNull(provider.GetMetadata(typeof(Unregistered)));
    }

    [Fact]
    public void GetRegisteredMetadata_ShouldPreferExplicitModelToGlobalModel()
    {
        var global = Model(typeof(Registered), "global");
        var registered = Model(typeof(Registered), "local");
        GeneratedEntityModelMetadataProvider.Register(global);
        var provider = new CompositeEntityModelMetadataProvider(new[]
        {
            new RegisteredEntityModelMetadataProvider(registered)
        });

        Assert.Same(registered, provider.GetRegisteredMetadata(typeof(Registered)));
    }

    [Fact]
    public void GetRegisteredMetadata_WhenGeneratedOnly_ShouldNotUseGlobalRegistration()
    {
        var global = Model(typeof(GlobalOnly), "global");
        GeneratedEntityModelMetadataProvider.Register(global);
        var compatible = new CompositeEntityModelMetadataProvider();
        var strict = new CompositeEntityModelMetadataProvider(null, generatedOnly: true);

        Assert.Same(global, compatible.GetRegisteredMetadata(typeof(GlobalOnly)));
        Assert.Null(strict.GetRegisteredMetadata(typeof(GlobalOnly)));
    }

    /// <summary>内置注解和约定回退可以复用不可变模型。</summary>
    [Theory]
    [InlineData(typeof(ConventionFallback), "ConventionFallback")]
    [InlineData(typeof(AnnotatedFallback), "annotated_fallback")]
    public void GetMetadata_WhenUsingBuiltInFallback_ShouldReuseMetadata(Type type, string tableName)
    {
        var provider = new CompositeEntityModelMetadataProvider();
        var first = provider.GetMetadata(type);
        Assert.Equal(tableName, first.TableName);
        Assert.Same(first, provider.GetMetadata(type));
        Assert.Null(provider.GetRegisteredMetadata(type));
    }

    /// <summary>回退已缓存时，动态自定义提供器仍须逐次具有更高优先级。</summary>
    [Fact]
    public void GetMetadata_WhenCustomProviderChanges_ShouldPreferCurrentMetadata()
    {
        EntityModelMetadata current = null;
        var custom = new Mock<IEntityModelMetadataProvider>();
        custom.Setup(provider => provider.GetMetadata(typeof(DynamicFallback))).Returns(() => current);
        var provider = new CompositeEntityModelMetadataProvider(new[] { custom.Object });
        var fallback = provider.GetMetadata(typeof(DynamicFallback));
        current = Model(typeof(DynamicFallback), "custom");
        Assert.Same(current, provider.GetMetadata(typeof(DynamicFallback)));
        current = null;
        Assert.Same(fallback, provider.GetMetadata(typeof(DynamicFallback)));
    }

    /// <summary>晚注册的生成模型仍可覆盖已经缓存的内置回退。</summary>
    [Fact]
    public void GetMetadata_WhenGeneratedModelRegistersAfterFallback_ShouldPreferGeneratedModel()
    {
        var provider = new CompositeEntityModelMetadataProvider();
        var fallback = provider.GetMetadata(typeof(LateGeneratedFallback));
        Assert.Same(fallback, provider.GetMetadata(typeof(LateGeneratedFallback)));
        var generated = Model(typeof(LateGeneratedFallback), "generated");
        GeneratedEntityModelMetadataProvider.Register(generated);
        Assert.Same(generated, provider.GetMetadata(typeof(LateGeneratedFallback)));
        Assert.Same(generated, provider.GetRegisteredMetadata(typeof(LateGeneratedFallback)));
    }

    /// <summary>并发首次读取应返回同一稳定的内置模型。</summary>
    [Fact]
    public void GetMetadata_WhenFallbackIsReadConcurrently_ShouldShareMetadata()
    {
        var provider = new CompositeEntityModelMetadataProvider();
        var models = new EntityModelMetadata[64];
        Parallel.For(0, models.Length, index => models[index] = provider.GetMetadata(typeof(ConcurrentFallback)));
        Assert.All(models, model => Assert.Same(models[0], model));
    }

    /// <summary>严格生成模式不应因为回退缓存而使用约定模型。</summary>
    [Fact]
    public void GetMetadata_WhenGeneratedOnly_ShouldNotUseBuiltInFallback()
    {
        var provider = new CompositeEntityModelMetadataProvider(null, generatedOnly: true);
        Assert.Null(provider.GetMetadata(typeof(StrictFallback)));
        Assert.Null(provider.GetMetadata(typeof(StrictFallback)));
    }

    private static EntityModelMetadata Model(Type type, string name) =>
        new(type, name, null, Array.Empty<EntityPropertyMetadata>());

    private sealed class Unregistered { public int Value { get; set; } }
    private sealed class Registered { }
    private sealed class GlobalOnly { }
    private sealed class ConventionFallback { public int Id { get; set; } }
    [Table("annotated_fallback")]
    private sealed class AnnotatedFallback { public int Id { get; set; } }
    private sealed class DynamicFallback { public int Id { get; set; } }
    private sealed class LateGeneratedFallback { public int Id { get; set; } }
    private sealed class ConcurrentFallback { public int Id { get; set; } }
    private sealed class StrictFallback { }
}
