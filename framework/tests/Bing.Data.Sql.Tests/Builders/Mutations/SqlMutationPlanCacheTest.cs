using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Mutation Plan、属性 Getter 和 resolver 分区缓存的直接职责测试。
/// </summary>
public sealed class SqlMutationPlanCacheTest
{
    /// <summary>
    /// 测试目的：同一类型同一属性重复读取应命中 Getter，大小写和空白只影响规范化前输入。
    /// </summary>
    [Fact]
    public void GetValue_WhenEquivalentPropertyNameIsRepeated_ShouldHitGetterCache()
    {
        var cache = new SqlMutationPlanCache();
        var source = new GetterSource { Name = "first" };

        var first = cache.GetValue(source, new ColumnMappingMetadata { PropertyName = "Name" });
        var second = cache.GetValue(source, new ColumnMappingMetadata { PropertyName = " name " });

        Assert.Equal("first", first);
        Assert.Equal("first", second);
        Assert.Equal(1, cache.GetterCacheMissCount);
        Assert.Equal(1, cache.GetterCacheHitCount);
        Assert.Equal(1, cache.GetterCount);
    }

    /// <summary>
    /// 测试目的：不同源类型或不同属性必须使用独立 Getter，避免同名属性跨类型串用。
    /// </summary>
    [Fact]
    public void GetValue_WhenSourceTypeOrPropertyDiffers_ShouldIsolateGetterEntries()
    {
        var cache = new SqlMutationPlanCache();
        var first = new GetterSource { Name = "first", Version = "v1" };
        var second = new OtherGetterSource { Name = "second" };

        var firstName = cache.GetValue(first, new ColumnMappingMetadata { PropertyName = "Name" });
        var firstVersion = cache.GetValue(first, new ColumnMappingMetadata { PropertyName = "Version" });
        var secondName = cache.GetValue(second, new ColumnMappingMetadata { PropertyName = "Name" });

        Assert.Equal("first", firstName);
        Assert.Equal("v1", firstVersion);
        Assert.Equal("second", secondName);
        Assert.Equal(3, cache.GetterCacheMissCount);
        Assert.Equal(0, cache.GetterCacheHitCount);
        Assert.Equal(3, cache.GetterCount);
    }

    /// <summary>
    /// 测试目的：Getter 容量为零时每次访问都应旁路且不保留编译委托。
    /// </summary>
    [Fact]
    public void GetValue_WhenGetterCapacityIsZero_ShouldBypassCache()
    {
        var cache = new SqlMutationPlanCache(getterCacheCapacity: 0);
        var source = new GetterSource { Name = "first" };
        var column = new ColumnMappingMetadata { PropertyName = "Name" };

        Assert.Equal("first", cache.GetValue(source, column));
        Assert.Equal("first", cache.GetValue(source, column));

        Assert.Equal(0, cache.GetterCount);
        Assert.Equal(2, cache.GetterCacheMissCount);
        Assert.Equal(0, cache.GetterCacheHitCount);
        Assert.Equal(2, cache.GetterCacheBypassCount);
        Assert.Equal(0, cache.GetterCacheEvictionCount);
    }

    /// <summary>
    /// 测试目的：同一 Getter 并发首次读取时应发布一个缓存项并返回正确值。
    /// </summary>
    [Fact]
    public async Task GetValue_WhenSameGetterIsReadConcurrently_ShouldPublishOneEntry()
    {
        var cache = new SqlMutationPlanCache();
        var source = new GetterSource { Name = "concurrent" };
        var values = new object[32];
        using var ready = new CountdownEvent(values.Length);
        using var start = new ManualResetEventSlim();

        var tasks = Enumerable.Range(0, values.Length).Select(index => Task.Factory.StartNew(() =>
        {
            ready.Signal();
            start.Wait();
            values[index] = cache.GetValue(source, new ColumnMappingMetadata { PropertyName = "Name" });
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        Assert.True(SpinWait.SpinUntil(() => ready.CurrentCount == 0, TimeSpan.FromSeconds(5)));
        start.Set();
        await Task.WhenAll(tasks);

        Assert.All(values, value => Assert.Equal("concurrent", value));
        Assert.Equal(1, cache.GetterCacheMissCount);
        Assert.Equal(values.Length - 1, cache.GetterCacheHitCount);
        Assert.Equal(1, cache.GetterCount);
    }

    /// <summary>
    /// 测试目的：Getter 负容量应在缓存创建阶段被拒绝。
    /// </summary>
    [Fact]
    public void Constructor_WhenGetterCapacityIsNegative_ShouldThrowArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SqlMutationPlanCache(getterCacheCapacity: -1));

        Assert.Equal("MutationGetterCacheCapacity", exception.ParamName);
    }

    /// <summary>
    /// 测试目的：同一 resolver 应复用同一缓存分区，不同 resolver 不得共享计划。
    /// </summary>
    [Fact]
    public void Get_WhenResolverDiffers_ShouldKeepMutationCachePartitionsIsolated()
    {
        var firstResolver = new DefaultEntityMappingResolver();
        var secondResolver = new DefaultEntityMappingResolver();
        var firstOptions = new SqlMetadataOptions
        {
            MutationPlanCacheCapacity = 1,
            MutationGetterCacheCapacity = 1
        };
        var laterOptions = new SqlMetadataOptions
        {
            MutationPlanCacheCapacity = 2,
            MutationGetterCacheCapacity = 2
        };

        var first = SqlMutationPlanCaches.Get(firstResolver, firstOptions);
        var sameResolver = SqlMutationPlanCaches.Get(firstResolver, laterOptions);
        var second = SqlMutationPlanCaches.Get(secondResolver, laterOptions);

        Assert.Same(first, sameResolver);
        Assert.NotSame(first, second);

        var mapping = CreateMapping();
        var insertKey = SqlMutationPlanCacheKey.Create(mapping, "test", SqlMutationOperation.Insert, null, null);
        var updateKey = SqlMutationPlanCacheKey.Create(mapping, "test", SqlMutationOperation.Update, null, null);
        first.GetOrAdd(insertKey, () => SqlMutationPlan.Create(mapping, SqlMutationOperation.Insert, null, null));
        first.GetOrAdd(updateKey, () => SqlMutationPlan.Create(mapping, SqlMutationOperation.Update, null, null));
        first.GetValue(new GetterSource { Name = "first", Version = "v1" },
            new ColumnMappingMetadata { PropertyName = nameof(GetterSource.Name) });
        first.GetValue(new GetterSource { Name = "first", Version = "v1" },
            new ColumnMappingMetadata { PropertyName = nameof(GetterSource.Version) });
        second.GetValue(new GetterSource { Name = "second", Version = "v2" },
            new ColumnMappingMetadata { PropertyName = nameof(GetterSource.Name) });
        second.GetValue(new GetterSource { Name = "second", Version = "v2" },
            new ColumnMappingMetadata { PropertyName = nameof(GetterSource.Version) });

        Assert.Equal(1, first.PlanCount);
        Assert.Equal(0, second.PlanCount);
        Assert.Equal(1, first.GetterCount);
        Assert.Equal(2, first.GetterCacheMissCount);
        Assert.Equal(1, first.GetterCacheEvictionCount);
        Assert.Equal(2, second.GetterCount);
        Assert.Equal(2, second.GetterCacheMissCount);
        Assert.Equal(0, second.GetterCacheEvictionCount);
    }

    /// <summary>
    /// 测试目的：Mutation Plan 的每个键维度都必须通过真实缓存产生独立计划，等价输入应命中。
    /// </summary>
    [Fact]
    public void GetOrAdd_WhenEachPlanDimensionChanges_ShouldMissAndKeepEquivalentPlanHit()
    {
        var cache = new SqlMutationPlanCache();
        var baselineMapping = CreateMapping(database: " database ", schema: " schema ", table: " table ",
            profile: " read ", route: " tenant-a ");
        var variations = new[]
        {
            (CreateMapping(typeof(OtherGetterSource), database: " database ", schema: " schema ", table: " table ", profile: " read ", route: " tenant-a "), "test", SqlMutationOperation.Insert, (IReadOnlyCollection<string>)null, (IReadOnlyCollection<string>)null),
            (baselineMapping, "other-provider", SqlMutationOperation.Insert, (IReadOnlyCollection<string>)null, (IReadOnlyCollection<string>)null),
            (CreateMapping(database: " other_database ", schema: " schema ", table: " table ", profile: " read ", route: " tenant-a "), "test", SqlMutationOperation.Insert, (IReadOnlyCollection<string>)null, (IReadOnlyCollection<string>)null),
            (CreateMapping(database: " database ", schema: " other_schema ", table: " table ", profile: " read ", route: " tenant-a "), "test", SqlMutationOperation.Insert, (IReadOnlyCollection<string>)null, (IReadOnlyCollection<string>)null),
            (CreateMapping(database: " database ", schema: " schema ", table: " other_table ", profile: " read ", route: " tenant-a "), "test", SqlMutationOperation.Insert, (IReadOnlyCollection<string>)null, (IReadOnlyCollection<string>)null),
            (CreateMapping(database: " database ", schema: " schema ", table: " table ", profile: " write ", route: " tenant-a "), "test", SqlMutationOperation.Insert, (IReadOnlyCollection<string>)null, (IReadOnlyCollection<string>)null),
            (CreateMapping(database: " database ", schema: " schema ", table: " table ", profile: " read ", route: " other_route "), "test", SqlMutationOperation.Insert, (IReadOnlyCollection<string>)null, (IReadOnlyCollection<string>)null),
            (baselineMapping, "test", SqlMutationOperation.Update, (IReadOnlyCollection<string>)null, (IReadOnlyCollection<string>)null),
            (baselineMapping, "test", SqlMutationOperation.Insert, (IReadOnlyCollection<string>)new[] { nameof(GetterSource.Name) }, (IReadOnlyCollection<string>)null),
            (baselineMapping, "test", SqlMutationOperation.Insert, (IReadOnlyCollection<string>)null, (IReadOnlyCollection<string>)new[] { nameof(GetterSource.Name) })
        };

        var baseline = cache.GetOrAdd(baselineMapping, "test", SqlMutationOperation.Insert, null, null);
        var expectedMissCount = 1L;
        foreach (var variation in variations)
        {
            cache.GetOrAdd(variation.Item1, variation.Item2, variation.Item3, variation.Item4, variation.Item5);
            Assert.Equal(++expectedMissCount, cache.PlanCacheMissCount);
        }
        var equivalent = cache.GetOrAdd(CreateMapping(database: " DATABASE ", schema: " SCHEMA ", table: " TABLE ",
            profile: " READ ", route: " TENANT-A "), " TEST ", SqlMutationOperation.Insert, null, null);

        Assert.Same(baseline, equivalent);
        Assert.Equal(11, cache.PlanCacheMissCount);
        Assert.Equal(1, cache.PlanCacheHitCount);
        Assert.Equal(11, cache.PlanCount);
    }

    /// <summary>
    /// 测试目的：不同 Provider key 和最终表路由必须经真实 Builder 生成独立完整 SQL。
    /// </summary>
    [Fact]
    public void Builder_WhenProviderAndRouteDiffer_ShouldRenderIndependentCompleteSql()
    {
        var metadata = new SqlMetadataOptions();
        metadata.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MutationCacheEntity),
            DbKey = "alpha",
            DatabaseType = DatabaseType.SqlServer,
            TableName = "mutation_alpha"
        });
        metadata.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MutationCacheEntity),
            DbKey = "beta",
            DatabaseType = DatabaseType.SqlServer,
            TableName = "mutation_beta"
        });
        var resolver = new DefaultEntityMappingResolver(options: metadata);
        var alphaOptions = new SqlOptions().SetDatabaseContext(new DatabaseContext
        {
            DbKey = "alpha",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
        });
        var betaOptions = new SqlOptions().SetDatabaseContext(new DatabaseContext
        {
            DbKey = "beta",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
        });
        var alphaBuilder = new DefaultSqlEntityMutationCommandBuilder(new KeyedMutationSqlProvider("test.alpha"),
            new SqlBuilderServices(entityMappingResolver: resolver, metadataOptions: metadata, options: alphaOptions));
        var betaBuilder = new DefaultSqlEntityMutationCommandBuilder(new KeyedMutationSqlProvider("test.beta"),
            new SqlBuilderServices(entityMappingResolver: resolver, metadataOptions: metadata, options: betaOptions));

        var alpha = alphaBuilder.Insert(new MutationCacheEntity { Id = 1, Name = "alpha" });
        var beta = betaBuilder.Insert(new MutationCacheEntity { Id = 1, Name = "beta" });

        Assert.Equal("Insert Into [mutation_alpha] ([Id], [Name]) Values (@_p_0, @_p_1)", alpha.Sql);
        Assert.Equal("Insert Into [mutation_beta] ([Id], [Name]) Values (@_p_0, @_p_1)", beta.Sql);
        Assert.Equal(new object[] { 1, "alpha" }, alpha.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 1, "beta" }, beta.Parameters.Select(parameter => parameter.Value));
        Assert.Equal("test.alpha", alpha.ProviderKey);
        Assert.Equal("test.beta", beta.ProviderKey);
    }

    /// <summary>
    /// 创建直接缓存测试使用的映射。
    /// </summary>
    private static EntityMappingMetadata CreateMapping(Type entityType = null, string database = null,
        string schema = null, string table = "getter_sources", string profile = null, string route = null) => new()
    {
        EntityType = entityType ?? typeof(GetterSource),
        MappingProfile = profile,
        TableRouteKey = route,
        Table = new SqlTableReference { Database = database, Schema = schema, TableName = table },
        Columns = new Dictionary<string, ColumnMappingMetadata>(StringComparer.Ordinal)
        {
            [nameof(GetterSource.Name)] = new ColumnMappingMetadata
            {
                PropertyName = nameof(GetterSource.Name),
                ColumnName = "Name",
                CanInsert = true,
                CanUpdate = true
            }
        }
    };

    /// <summary>
    /// 主 Getter 测试源。
    /// </summary>
    private sealed class GetterSource
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 版本。
        /// </summary>
        public string Version { get; set; }
    }

    /// <summary>
    /// 第二个 Getter 测试源。
    /// </summary>
    private sealed class OtherGetterSource
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 经真实 Mutation Builder 渲染的测试实体。
    /// </summary>
    [Table("mutation_cache_entities")]
    private sealed class MutationCacheEntity
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 仅改变 Provider key 的测试 Provider，复用现有 SQL Server 方言和能力档案。
    /// </summary>
    private sealed class KeyedMutationSqlProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        public KeyedMutationSqlProvider(string key) => Key = key;

        public string Key { get; }

        public DatabaseType DatabaseType => DatabaseType.SqlServer;

        public IDialect Dialect => TestMutationSqlProvider.Instance.Dialect;

        public ISqlClauseFactory ClauseFactory => TestMutationSqlProvider.Instance.ClauseFactory;

        public ISqlTableReferenceParser TableReferenceParser => TestMutationSqlProvider.Instance.TableReferenceParser;

        public ISqlPaginationRenderer PaginationRenderer => TestMutationSqlProvider.Instance.PaginationRenderer;

        public IParameterManagerFactory ParameterManagerFactory => TestMutationSqlProvider.Instance.ParameterManagerFactory;

        public IParamLiteralsResolver ParamLiteralsResolver => TestMutationSqlProvider.Instance.ParamLiteralsResolver;

        public SqlProviderProfile Profile => TestMutationSqlProvider.Instance.Profile;
    }
}
