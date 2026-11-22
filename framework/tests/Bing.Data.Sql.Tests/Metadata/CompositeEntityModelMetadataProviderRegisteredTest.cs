using Bing.Data.Sql.Metadata;

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

    private static EntityModelMetadata Model(Type type, string name) =>
        new(type, name, null, Array.Empty<EntityPropertyMetadata>());

    private sealed class Unregistered { public int Value { get; set; } }
    private sealed class Registered { }
    private sealed class GlobalOnly { }
}
