using System.Reflection;
using Bing.Data.Enums;
using Bing.Data.Sql.Configs;
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
    /// 测试目的：按不区分大小写规则匹配的数据源、映射配置和表路由键应复用同一缓存项，避免等价上下文导致缓存无限分裂。
    /// </summary>
    [Fact]
    public void Resolve_WhenEquivalentContextValuesDifferOnlyByCase_ShouldReuseCacheEntry()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(CacheSample),
            DbKey = "reporting",
            MappingProfile = "read",
            TableRouteKey = "tenant-a",
            TableName = "cache_samples_tenant_a"
        });
        var resolver = new DefaultEntityMappingResolver(options: options);
        var upperCaseContext = new DatabaseContext
        {
            DbKey = "REPORTING",
            MappingProfile = "READ",
            TenantId = "TENANT-A",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
        };
        var lowerCaseContext = new DatabaseContext
        {
            DbKey = "reporting",
            MappingProfile = "read",
            TenantId = "tenant-a",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
        };

        // Act
        var upperCaseMapping = resolver.Resolve(typeof(CacheSample), upperCaseContext);
        var lowerCaseMapping = resolver.Resolve(typeof(CacheSample), lowerCaseContext);

        // Assert
        Assert.Same(upperCaseMapping, lowerCaseMapping);
        Assert.Equal("cache_samples_tenant_a", upperCaseMapping.Table.TableName);
    }

    /// <summary>
    /// 测试目的：派生解析器根据运行时路由解析出不同架构和表名时，缓存键必须使用最终对象名，避免返回前一次路由的映射。
    /// </summary>
    [Fact]
    public void Resolve_WhenDerivedResolverChangesFinalObjectName_ShouldUseIndependentCacheEntries()
    {
        // Arrange
        var resolver = new RuntimeRouteMappingResolver { RouteSuffix = "alpha" };
        var context = new DatabaseContext { DbKey = "reporting" };

        // Act
        var alpha = resolver.Resolve(typeof(CacheSample), context);
        resolver.RouteSuffix = "beta";
        var beta = resolver.Resolve(typeof(CacheSample), context);

        // Assert
        Assert.NotSame(alpha, beta);
        Assert.Equal("route_alpha", alpha.Table.Schema);
        Assert.Equal("cache_samples_alpha", alpha.Table.TableName);
        Assert.Equal("route_beta", beta.Table.Schema);
        Assert.Equal("cache_samples_beta", beta.Table.TableName);
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
    /// 测试目的：同一实体并发首次解析时，外部模型元数据只应加载一次，避免缓存竞争放大 ORM 元数据访问成本。
    /// </summary>
    [Fact]
    public async Task Resolve_WhenSameEntityIsResolvedConcurrently_ShouldLoadModelMetadataOnce()
    {
        // Arrange
        var provider = new CountingEntityModelMetadataProvider();
        var resolver = new DefaultEntityMappingResolver(provider);

        // Act
        var mappings = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => resolver.Resolve(typeof(CacheSample), null))));

        // Assert
        Assert.All(mappings, mapping => Assert.Same(mappings[0], mapping));
        Assert.Equal(1, provider.MetadataCallCount);
    }

    /// <summary>
    /// 测试目的：首次解析与缓存命中应以数值快照报告命中、未命中、条目数和无上限容量，不暴露路由上下文。
    /// </summary>
    [Fact]
    public void Resolve_WhenMappingIsResolvedThenCached_ShouldReportMappingCacheStatistics()
    {
        // Arrange
        var resolver = new DefaultEntityMappingResolver();

        // Act
        resolver.Resolve(typeof(CacheSample), null);
        resolver.Resolve(typeof(CacheSample), null);
        var statistics = resolver.MappingCacheStatistics;

        // Assert
        Assert.Equal(1, statistics.CacheHitCount);
        Assert.Equal(1, statistics.CacheMissCount);
        Assert.Equal(0, statistics.CacheBypassCount);
        Assert.Equal(1, statistics.EntryCount);
        Assert.Null(statistics.Capacity);
        Assert.All(typeof(EntityMappingCacheStatistics).GetProperties(), property =>
            Assert.Contains(property.PropertyType, new[] { typeof(long), typeof(int), typeof(int?) }));
    }

    /// <summary>
    /// 测试目的：容量已满时，新动态路由仍应返回正确映射但不得驱逐已缓存的稳定路由。
    /// </summary>
    [Fact]
    public void Resolve_WhenMappingCacheCapacityIsReached_ShouldKeepExistingEntriesAndBypassNewRouteCaching()
    {
        // Arrange
        var resolver = new RuntimeRouteMappingResolver(new SqlMetadataOptions { EntityMappingCacheCapacity = 2 });
        var context = new DatabaseContext { DbKey = "reporting" };
        resolver.RouteSuffix = "alpha";
        var alpha = resolver.Resolve(typeof(CacheSample), context);
        resolver.RouteSuffix = "beta";
        var beta = resolver.Resolve(typeof(CacheSample), context);

        // Act
        resolver.RouteSuffix = "gamma";
        var firstGamma = resolver.Resolve(typeof(CacheSample), context);
        var secondGamma = resolver.Resolve(typeof(CacheSample), context);
        resolver.RouteSuffix = "alpha";
        var cachedAlpha = resolver.Resolve(typeof(CacheSample), context);

        // Assert
        Assert.Equal(2, resolver.MappingCacheCount);
        Assert.Equal("cache_samples_gamma", firstGamma.Table.TableName);
        Assert.Equal("cache_samples_gamma", secondGamma.Table.TableName);
        Assert.NotSame(firstGamma, secondGamma);
        Assert.Same(alpha, cachedAlpha);
        Assert.Equal("cache_samples_beta", beta.Table.TableName);
        var statistics = resolver.MappingCacheStatistics;
        Assert.Equal(1, statistics.CacheHitCount);
        Assert.Equal(4, statistics.CacheMissCount);
        Assert.Equal(2, statistics.CacheBypassCount);
        Assert.Equal(2, statistics.EntryCount);
        Assert.Equal(2, statistics.Capacity);
    }

    /// <summary>
    /// 测试目的：缓存容量为零时不应保留最终映射项，但每次解析仍必须返回正确的最终对象名。
    /// </summary>
    [Fact]
    public void Resolve_WhenMappingCacheCapacityIsZero_ShouldBypassFinalMappingCache()
    {
        // Arrange
        var resolver = new RuntimeRouteMappingResolver(new SqlMetadataOptions { EntityMappingCacheCapacity = 0 })
        {
            RouteSuffix = "uncached"
        };

        // Act
        var first = resolver.Resolve(typeof(CacheSample), null);
        var second = resolver.Resolve(typeof(CacheSample), null);

        // Assert
        Assert.Equal(0, resolver.MappingCacheCount);
        Assert.NotSame(first, second);
        Assert.Equal("route_uncached", first.Table.Schema);
        Assert.Equal("cache_samples_uncached", second.Table.TableName);
        var statistics = resolver.MappingCacheStatistics;
        Assert.Equal(0, statistics.CacheHitCount);
        Assert.Equal(2, statistics.CacheMissCount);
        Assert.Equal(2, statistics.CacheBypassCount);
        Assert.Equal(0, statistics.EntryCount);
        Assert.Equal(0, statistics.Capacity);
    }

    /// <summary>
    /// 测试目的：并发解析高基数表路由时，最终映射缓存条目数不得超过已配置容量。
    /// </summary>
    [Fact]
    public async Task Resolve_WhenDistinctRoutesAreResolvedConcurrently_ShouldNotExceedMappingCacheCapacity()
    {
        // Arrange
        const int capacity = 4;
        const int routeCount = 32;
        var options = new SqlMetadataOptions { EntityMappingCacheCapacity = capacity };
        foreach (var index in Enumerable.Range(0, routeCount))
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(CacheSample),
                TableRouteKey = $"tenant-{index}",
                TableName = $"cache_samples_{index}"
            });
        var resolver = new DefaultEntityMappingResolver(options: options);

        // Act
        var mappings = await Task.WhenAll(Enumerable.Range(0, routeCount).Select(index => Task.Run(() =>
            resolver.Resolve(typeof(CacheSample), new DatabaseContext { TenantId = $"tenant-{index}" }))));

        // Assert
        Assert.True(resolver.MappingCacheCount <= capacity);
        Assert.Equal(Enumerable.Range(0, routeCount).Select(index => $"cache_samples_{index}"),
            mappings.Select(mapping => mapping.Table.TableName));
        var statistics = resolver.MappingCacheStatistics;
        Assert.Equal(0, statistics.CacheHitCount);
        Assert.Equal(routeCount, statistics.CacheMissCount);
        Assert.Equal(routeCount - capacity, statistics.CacheBypassCount);
        Assert.True(statistics.EntryCount <= capacity);
        Assert.Equal(capacity, statistics.Capacity);
    }

    /// <summary>
    /// 测试目的：负缓存容量必须在解析器创建阶段被拒绝，避免运行期出现不明确的缓存语义。
    /// </summary>
    [Fact]
    public void Constructor_WhenMappingCacheCapacityIsNegative_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DefaultEntityMappingResolver(options: new SqlMetadataOptions { EntityMappingCacheCapacity = -1 }));

        // Assert
        Assert.Equal(nameof(SqlMetadataOptions.EntityMappingCacheCapacity), exception.ParamName);
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

    /// <summary>
    /// 根据可变运行时路由生成最终对象名的测试解析器。
    /// </summary>
    private sealed class RuntimeRouteMappingResolver : DefaultEntityMappingResolver
    {
        /// <summary>
        /// 使用指定缓存配置初始化运行时路由映射解析器。
        /// </summary>
        /// <param name="options">实体映射缓存配置。</param>
        public RuntimeRouteMappingResolver(SqlMetadataOptions options = null)
            : base(options: options)
        {
        }

        /// <summary>
        /// 当前路由后缀。
        /// </summary>
        public string RouteSuffix { get; set; }

        /// <inheritdoc />
        protected override string GetSchema(EntityModelMetadata model, EntityMappingOptions mappingOptions) =>
            $"route_{RouteSuffix}";

        /// <inheritdoc />
        protected override string GetTableName(EntityModelMetadata model, EntityMappingOptions mappingOptions) =>
            $"cache_samples_{RouteSuffix}";
    }
}