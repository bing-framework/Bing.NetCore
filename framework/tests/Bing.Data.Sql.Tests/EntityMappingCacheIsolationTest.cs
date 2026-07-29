using System.Reflection;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 实体映射缓存隔离测试。
/// </summary>
public class EntityMappingCacheIsolationTest
{
    /// <summary>
    /// 测试目的：修改公开实体描述副本不应污染后续映射解析使用的静态描述缓存。
    /// </summary>
    [Fact]
    public void GetDescriptor_WhenCallerMutatesProperties_ShouldNotChangeLaterMapping()
    {
        // Arrange
        var resolver = new DefaultEntityMappingResolver();
        var descriptor = resolver.GetDescriptor(typeof(CacheSample));
        var properties = Assert.IsAssignableFrom<IList<PropertyInfo>>(descriptor.Properties);

        // Act
        properties.Clear();
        var mapping = new DefaultEntityMappingResolver().Resolve(typeof(CacheSample), null);

        // Assert
        Assert.Empty(descriptor.Properties);
        Assert.True(mapping.Columns.ContainsKey(nameof(CacheSample.Name)));
    }

    /// <summary>
    /// 测试 - 修改映射结果副本不应污染缓存。
    /// </summary>
    [Fact]
    public void Resolve_WhenCreatingTableCopy_ShouldNotChangeCachedMapping()
    {
        // Arrange
        var resolver = new DefaultEntityMappingResolver();

        // Act
        var mapping = resolver.Resolve(typeof(CacheSample), null);
        var copy = mapping.Table with { Alias = "cached" };
        var cachedMapping = resolver.Resolve(typeof(CacheSample), null);

        // Assert
        Assert.Null(mapping.Table.Alias);
        Assert.Equal("cached", copy.Alias);
        Assert.Same(mapping, cachedMapping);
        Assert.Null(cachedMapping.Table.Alias);
    }

    /// <summary>
    /// 测试 - 映射列集合不应允许外部修改。
    /// </summary>
    [Fact]
    public void Resolve_WhenColumnsExposed_ShouldRejectExternalMutation()
    {
        // Arrange
        var resolver = new DefaultEntityMappingResolver();
        var mapping = resolver.Resolve(typeof(CacheSample), null);
        var columns = (IDictionary<string, ColumnMappingMetadata>)mapping.Columns;

        // Act
        var action = () => columns.Add("Injected", new ColumnMappingMetadata
        {
            PropertyName = "Injected",
            ColumnName = "Injected"
        });

        // Assert
        Assert.Throws<NotSupportedException>(action);
        Assert.False(mapping.Columns.ContainsKey("Injected"));
    }

    /// <summary>
    /// 测试 - 未配置表路由时不同租户应复用同一映射缓存。
    /// </summary>
    [Fact]
    public void Resolve_WhenTableRouteIsNotConfigured_ShouldNotCacheTenantId()
    {
        // Arrange
        var resolver = new DefaultEntityMappingResolver();
        var tenantA = new DatabaseContext { DbKey = "default", TenantId = "tenant-a" };
        var tenantB = new DatabaseContext { DbKey = "default", TenantId = "tenant-b" };

        // Act
        var mappingA = resolver.Resolve(typeof(CacheSample), tenantA);
        var mappingB = resolver.Resolve(typeof(CacheSample), tenantB);

        // Assert
        Assert.Same(mappingA, mappingB);
        Assert.Equal(string.Empty, mappingA.TableRouteKey);
    }

    /// <summary>
    /// 测试 - 映射缓存命中不应重复访问 ORM 原始元数据提供器。
    /// </summary>
    [Fact]
    public void Resolve_WhenMappingIsCached_ShouldNotCallModelMetadataProviderAgain()
    {
        // Arrange
        var provider = new CountingEntityModelMetadataProvider();
        var resolver = new DefaultEntityMappingResolver(provider);

        // Act
        var first = resolver.Resolve(typeof(CacheSample), null);
        var metadataCallCount = provider.MetadataCallCount;
        var second = resolver.Resolve(typeof(CacheSample), null);

        // Assert
        Assert.Same(first, second);
        Assert.Equal(1, metadataCallCount);
        Assert.Equal(metadataCallCount, provider.MetadataCallCount);
    }

    /// <summary>
    /// 缓存测试实体。
    /// </summary>
    private sealed class CacheSample
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 可统计原始模型元数据访问次数的测试提供器。
    /// </summary>
    private sealed class CountingEntityModelMetadataProvider : IEntityModelMetadataProvider
    {
        /// <summary>
        /// 获取元数据的调用次数。
        /// </summary>
        public int MetadataCallCount { get; private set; }

        /// <inheritdoc />
        public EntityModelMetadata GetMetadata(Type entityType)
        {
            MetadataCallCount++;
            var properties = entityType.GetProperties().Select(property => new EntityPropertyMetadata(property));
            return new EntityModelMetadata(entityType, "cache_samples", "cache", properties);
        }

        /// <inheritdoc />
        public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));
    }
}