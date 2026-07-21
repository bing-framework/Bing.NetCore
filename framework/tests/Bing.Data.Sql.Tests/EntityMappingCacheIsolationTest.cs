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
        var tableNameCallCount = provider.TableNameCallCount;
        var schemaCallCount = provider.SchemaCallCount;
        var columnNameCallCount = provider.ColumnNameCallCount;
        var second = resolver.Resolve(typeof(CacheSample), null);

        // Assert
        Assert.Same(first, second);
        Assert.Equal(1, tableNameCallCount);
        Assert.Equal(1, schemaCallCount);
        Assert.Equal(1, columnNameCallCount);
        Assert.Equal(tableNameCallCount, provider.TableNameCallCount);
        Assert.Equal(schemaCallCount, provider.SchemaCallCount);
        Assert.Equal(columnNameCallCount, provider.ColumnNameCallCount);
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
        /// 获取表名的调用次数。
        /// </summary>
        public int TableNameCallCount { get; private set; }

        /// <summary>
        /// 获取架构的调用次数。
        /// </summary>
        public int SchemaCallCount { get; private set; }

        /// <summary>
        /// 获取列名的调用次数。
        /// </summary>
        public int ColumnNameCallCount { get; private set; }

        /// <inheritdoc />
        public string GetTableName(Type entityType)
        {
            TableNameCallCount++;
            return "cache_samples";
        }

        /// <inheritdoc />
        public string GetSchema(Type entityType)
        {
            SchemaCallCount++;
            return "cache";
        }

        /// <inheritdoc />
        public string GetColumnName(Type entityType, string propertyName)
        {
            ColumnNameCallCount++;
            return propertyName;
        }
    }
}