using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>
/// 验证宿主私有实体模型元数据提供器的只读与隔离行为。
/// </summary>
public sealed class RegisteredEntityModelMetadataProviderTest
{
    /// <summary>
    /// 验证提供器使用输入快照并在实例间隔离同一实体类型的注册值。
    /// </summary>
    [Fact]
    public void Constructor_WhenModelsAreProvided_ShouldSnapshotAndIsolateRegistrations()
    {
        var first = CreateModel(typeof(SampleEntity), "first_table");
        var second = CreateModel(typeof(SampleEntity), "second_table");
        var unknown = CreateModel(typeof(OtherEntity), "other_table");
        var firstInput = new[] { first };
        var firstProvider = new RegisteredEntityModelMetadataProvider(firstInput);
        var secondProvider = new RegisteredEntityModelMetadataProvider(new[] { second, unknown });

        firstInput[0] = second;

        Assert.Same(first, firstProvider.GetMetadata(typeof(SampleEntity)));
        Assert.Same(first, firstProvider.GetMetadata<SampleEntity>());
        Assert.Same(second, secondProvider.GetMetadata(typeof(SampleEntity)));
        Assert.Same(unknown, secondProvider.GetMetadata<OtherEntity>());
        Assert.Null(firstProvider.GetMetadata(typeof(OtherEntity)));
        Assert.Null(new RegisteredEntityModelMetadataProvider(Array.Empty<EntityModelMetadata>())
            .GetMetadata(typeof(SampleEntity)));
    }

    /// <summary>
    /// 验证相同实体类型被重复登记时拒绝构造并报告类型名。
    /// </summary>
    [Fact]
    public void Constructor_WhenEntityTypeIsDuplicated_ShouldThrowWithTypeName()
    {
        var first = CreateModel(typeof(SampleEntity), "first_table");
        var second = CreateModel(typeof(SampleEntity), "second_table");

        var exception = Assert.Throws<ArgumentException>(() =>
            new RegisteredEntityModelMetadataProvider(new[] { first, second }));

        Assert.Contains(typeof(SampleEntity).FullName!, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证空类型参数抛出参数异常，未登记类型返回空值。
    /// </summary>
    [Fact]
    public void GetMetadata_WhenTypeIsNullOrUnknown_ShouldValidateOrReturnNull()
    {
        var provider = new RegisteredEntityModelMetadataProvider(new[]
        {
            CreateModel(typeof(SampleEntity), "sample_table")
        });

        Assert.Throws<ArgumentNullException>(() => provider.GetMetadata((Type)null!));
        Assert.Null(provider.GetMetadata(typeof(OtherEntity)));
        Assert.Null(provider.GetMetadata<OtherEntity>());
    }

    /// <summary>
    /// 验证构造完成后的只读元数据索引可被多线程并发读取。
    /// </summary>
    [Fact]
    public void GetMetadata_WhenReadConcurrently_ShouldReturnStableModel()
    {
        var model = CreateModel(typeof(SampleEntity), "sample_table");
        var provider = new RegisteredEntityModelMetadataProvider(new[] { model });
        var results = new ConcurrentBag<EntityModelMetadata>();

        Parallel.For(0, 2048, _ => results.Add(provider.GetMetadata(typeof(SampleEntity))!));

        Assert.Equal(2048, results.Count);
        Assert.All(results, result => Assert.Same(model, result));
    }

    private static EntityModelMetadata CreateModel(Type entityType, string tableName) =>
        new(entityType, tableName, string.Empty, Array.Empty<EntityPropertyMetadata>());

    private sealed class SampleEntity { }

    private sealed class OtherEntity { }
}
