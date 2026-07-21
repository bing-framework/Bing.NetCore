using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 实体映射缓存隔离测试。
/// </summary>
public class EntityMappingCacheIsolationTest
{
    /// <summary>
    /// 测试 - 修改映射结果副本不应污染缓存。
    /// </summary>
    [Fact]
    public void Resolve_WhenCreatingTableReferenceCopy_ShouldNotChangeCachedMapping()
    {
        // Arrange
        var resolver = new DefaultEntityMappingResolver();

        // Act
        var mapping = resolver.Resolve(typeof(CacheSample), null);
        var copy = mapping.TableReference.WithAlias("cached");
        var cachedMapping = resolver.Resolve(typeof(CacheSample), null);

        // Assert
        Assert.Null(mapping.TableReference.Alias);
        Assert.Equal("cached", copy.Alias);
        Assert.Same(mapping, cachedMapping);
        Assert.Null(cachedMapping.TableReference.Alias);
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
    /// 缓存测试实体。
    /// </summary>
    private sealed class CacheSample
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }
}