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
    /// 测试目的：首次属性名带空白时，兼容模式与严格生成模式都应使用规范化名称创建 Getter。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetValue_WhenFirstPropertyNameHasWhitespace_ShouldNormalizeBeforeCreatingGetter(bool generatedOnly)
    {
        var calls = 0;
        IEntityModelMetadataProvider models = generatedOnly
            ? new RegisteredEntityModelMetadataProvider(new EntityModelMetadata(typeof(GetterSource), "rows", null,
                new[]
                {
                    EntityPropertyMetadata.CreateGenerated(nameof(GetterSource.Name), typeof(string), true, true,
                        generatedGetter: source =>
                        {
                            calls++;
                            return ((GetterSource)source).Name;
                        })
                }))
            : null;
        var cache = new SqlMutationPlanCache(models: models, generatedOnly: generatedOnly);
        var source = new GetterSource { Name = "first" };

        var first = cache.GetValue(source, new ColumnMappingMetadata { PropertyName = " Name " });
        var second = cache.GetValue(source, new ColumnMappingMetadata { PropertyName = "Name" });

        Assert.Equal("first", first);
        Assert.Equal("first", second);
        Assert.Equal(1, cache.GetterCacheMissCount);
        Assert.Equal(1, cache.GetterCacheHitCount);
        Assert.Equal(1, cache.GetterCount);
        Assert.Equal(generatedOnly ? 2 : 0, calls);
    }

    /// <summary>
    /// 测试目的：属性 Getter 对一个源实例抛错后，缓存中的委托仍可供后续实例复用。
    /// </summary>
    [Fact]
    public void GetValue_WhenPropertyGetterThrows_ShouldKeepGetterForLaterSources()
    {
        var cache = new SqlMutationPlanCache();
        var column = new ColumnMappingMetadata { PropertyName = nameof(ThrowingGetterSource.Name) };
        var failingSource = new ThrowingGetterSource { NameValue = "unused", ThrowOnGet = true };
        var successfulSource = new ThrowingGetterSource { NameValue = "recovered" };

        var exception = Assert.Throws<InvalidOperationException>(() => cache.GetValue(failingSource, column));
        var getterCountAfterFailure = cache.GetterCount;
        var value = cache.GetValue(successfulSource, column);

        Assert.Equal("getter failed", exception.Message);
        Assert.Equal("recovered", value);
        Assert.Equal(1, getterCountAfterFailure);
        Assert.Equal(1, cache.GetterCount);
        Assert.Equal(1, cache.GetterCacheMissCount);
        Assert.Equal(1, cache.GetterCacheHitCount);
    }

    /// <summary>
    /// 测试目的：生成 Getter 创建失败时应移除失败项，并允许相同缓存键再次创建。
    /// </summary>
    [Fact]
    public void GetValue_WhenGeneratedGetterCreationFails_ShouldRetryOnNextCall()
    {
        var model = new EntityModelMetadata(typeof(GetterSource), "rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(GetterSource.Name), typeof(string), true, true,
                generatedGetter: source => ((GetterSource)source).Name)
        });
        var models = new RetryingGetterMetadataProvider(model);
        var cache = new SqlMutationPlanCache(models: models, generatedOnly: true);
        var source = new GetterSource { Name = "retried" };
        var column = new ColumnMappingMetadata { PropertyName = nameof(GetterSource.Name) };

        Assert.Throws<NotSupportedException>(() => cache.GetValue(source, column));
        Assert.Equal(0, cache.GetterCount);

        var value = cache.GetValue(source, column);

        Assert.Equal("retried", value);
        Assert.Equal(2, models.SourceMetadataLookupCount);
        Assert.Equal(2, cache.GetterCacheMissCount);
        Assert.Equal(0, cache.GetterCacheHitCount);
        Assert.Equal(1, cache.GetterCount);
    }

    /// <summary>
    /// 测试目的：来源为生成模型登记类型的派生实例时，应继续复用基类生成 Getter。
    /// </summary>
    [Fact]
    public void GetValue_WhenDerivedSourceHasGeneratedBaseModel_ShouldUseGeneratedGetter()
    {
        var calls = 0;
        var model = new EntityModelMetadata(typeof(BaseGeneratedSource), "generated_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(BaseGeneratedSource.Name), typeof(string), true, true,
                generatedGetter: source =>
                {
                    calls++;
                    return ((BaseGeneratedSource)source).Name;
                })
        });
        var cache = new SqlMutationPlanCache(
            models: new RegisteredEntityModelMetadataProvider(model), generatedOnly: true);
        var source = new DerivedGeneratedSource { Name = "generated" };

        var value = cache.GetValue(source, new ColumnMappingMetadata { PropertyName = nameof(BaseGeneratedSource.Name) });

        Assert.Equal("generated", value);
        Assert.Equal(1, calls);
        Assert.Equal(1, cache.GetterCacheMissCount);
        Assert.Equal(1, cache.GetterCount);
    }

    /// <summary>
    /// 测试目的：派生类型已有不带 Getter 的模型时，仍应继续查找可用的基类生成 Getter。
    /// </summary>
    [Fact]
    public void GetValue_WhenDerivedModelLacksGeneratedGetter_ShouldPreferGeneratedBaseGetter()
    {
        var calls = 0;
        var baseModel = new EntityModelMetadata(typeof(BaseGeneratedSource), "generated_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(BaseGeneratedSource.Name), typeof(string), true, true,
                generatedGetter: source =>
                {
                    calls++;
                    return ((BaseGeneratedSource)source).Name;
                })
        });
        var derivedModel = new EntityModelMetadata(typeof(DerivedGeneratedSource), "generated_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(BaseGeneratedSource.Name), typeof(string), true, true)
        });
        var cache = new SqlMutationPlanCache(
            models: new RegisteredEntityModelMetadataProvider(baseModel, derivedModel), generatedOnly: true);
        var source = new DerivedGeneratedSource { Name = "generated" };

        var value = cache.GetValue(source, new ColumnMappingMetadata { PropertyName = nameof(BaseGeneratedSource.Name) });

        Assert.Equal("generated", value);
        Assert.Equal(1, calls);
        Assert.Equal(1, cache.GetterCacheMissCount);
        Assert.Equal(1, cache.GetterCount);
    }

    /// <summary>
    /// 测试目的：仅登记接口生成模型时，派生实例也应复用接口生成 Getter。
    /// </summary>
    [Fact]
    public void GetValue_WhenOnlyInterfaceHasGeneratedModel_ShouldUseInterfaceGeneratedGetter()
    {
        var calls = 0;
        var model = new EntityModelMetadata(typeof(IGeneratedNameSource), "generated_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(IGeneratedNameSource.Name), typeof(string), true, false,
                generatedGetter: source =>
                {
                    calls++;
                    return ((IGeneratedNameSource)source).Name;
                })
        });
        var cache = new SqlMutationPlanCache(
            models: new RegisteredEntityModelMetadataProvider(model), generatedOnly: true);
        var source = new InterfaceGeneratedSource { Name = "generated-interface" };

        var value = cache.GetValue(source, new ColumnMappingMetadata { PropertyName = nameof(IGeneratedNameSource.Name) });

        Assert.Equal("generated-interface", value);
        Assert.Equal(1, calls);
        Assert.Equal(1, cache.GetterCacheMissCount);
        Assert.Equal(1, cache.GetterCount);
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
        var equivalent = cache.GetOrAdd(CreateMapping(database: " database ", schema: " schema ", table: " table ",
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
    /// 验证仅大小写不同的物理表名会生成独立且完整的 SQL。
    /// </summary>
    [Fact]
    public void Builder_WhenPhysicalTableNameDiffersOnlyByCase_ShouldRenderIndependentCompleteSql()
    {
        var resolver = new CaseSensitiveMutationMappingResolver { TableName = "Orders" };
        var services = new SqlBuilderServices(entityMappingResolver: resolver);
        var builder = new DefaultSqlEntityMutationCommandBuilder(new KeyedMutationSqlProvider("test"), services);
        var upper = builder.Insert(new MutationCacheEntity { Id = 1, Name = "upper" });

        resolver.TableName = "orders";
        var lower = builder.Insert(new MutationCacheEntity { Id = 2, Name = "lower" });

        Assert.Equal("Insert Into [Orders] ([Id], [Name]) Values (@_p_0, @_p_1)", upper.Sql);
        Assert.Equal("Insert Into [orders] ([Id], [Name]) Values (@_p_0, @_p_1)", lower.Sql);
        Assert.Equal(new object[] { 1, "upper" }, upper.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 2, "lower" }, lower.Parameters.Select(parameter => parameter.Value));

        resolver.TableName = "Orders";
        var upperUpdate = builder.Update(new MutationCacheEntity { Id = 3, Name = "upper-update" });
        var upperDelete = builder.Delete(new MutationCacheEntity { Id = 4 });
        resolver.TableName = "orders";
        var lowerUpdate = builder.Update(new MutationCacheEntity { Id = 5, Name = "lower-update" });
        var lowerDelete = builder.Delete(new MutationCacheEntity { Id = 6 });

        Assert.Equal("Update [Orders] Set [Name] = @_p_0 Where [Id] = @_p_1", upperUpdate.Sql);
        Assert.Equal("Delete From [Orders] Where [Id] = @_p_0", upperDelete.Sql);
        Assert.Equal("Update [orders] Set [Name] = @_p_0 Where [Id] = @_p_1", lowerUpdate.Sql);
        Assert.Equal("Delete From [orders] Where [Id] = @_p_0", lowerDelete.Sql);
        Assert.Equal(new object[] { "upper-update", 3 }, upperUpdate.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 4 }, upperDelete.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { "lower-update", 5 }, lowerUpdate.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 6 }, lowerDelete.Parameters.Select(parameter => parameter.Value));
    }
    /// <summary>
    /// 创建直接缓存测试使用的映射。
    /// </summary>
    /// <param name="entityType">映射对应的实体类型。</param>
    /// <param name="database">映射所属的数据库名称。</param>
    /// <param name="schema">映射所属的架构名称。</param>
    /// <param name="table">映射对应的表名称。</param>
    /// <param name="profile">映射使用的配置标识。</param>
    /// <param name="route">映射使用的路由标识。</param>
    /// <returns>返回包含指定实体、表和路由信息的测试映射。</returns>
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
        /// 获取或设置名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置版本。
        /// </summary>
        public string Version { get; set; }
    }

    /// <summary>
    /// 第二个 Getter 测试源。
    /// </summary>
    private sealed class OtherGetterSource
    {
        /// <summary>
        /// 获取或设置名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 对指定源实例抛出属性 Getter 异常的测试源。
    /// </summary>
    private sealed class ThrowingGetterSource
    {
        /// <summary>
        /// 获取或设置是否抛出异常。
        /// </summary>
        public bool ThrowOnGet { get; set; }

        /// <summary>
        /// 获取或设置正常读取时返回的名称。
        /// </summary>
        public string NameValue { get; set; }

        /// <summary>
        /// 获取名称。
        /// </summary>
        public string Name => ThrowOnGet ? throw new InvalidOperationException("getter failed") : NameValue;
    }

    /// <summary>
    /// 首次不返回目标模型、后续返回模型的测试元数据提供器。
    /// </summary>
    private sealed class RetryingGetterMetadataProvider : IEntityModelMetadataProvider
    {
        private readonly EntityModelMetadata _model;
        private int _sourceMetadataLookupCount;

        /// <summary>
        /// 初始化一个 <see cref="RetryingGetterMetadataProvider"/> 类型的实例。
        /// </summary>
        /// <param name="model">重试时返回的生成模型。</param>
        public RetryingGetterMetadataProvider(EntityModelMetadata model) => _model = model;

        /// <summary>
        /// 获取目标源类型的模型查询次数。
        /// </summary>
        public int SourceMetadataLookupCount => _sourceMetadataLookupCount;

        /// <inheritdoc />
        public EntityModelMetadata GetMetadata(Type entityType)
        {
            if (entityType != typeof(GetterSource))
                return null;
            return Interlocked.Increment(ref _sourceMetadataLookupCount) == 1 ? null : _model;
        }

        /// <inheritdoc />
        public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));
    }

    /// <summary>
    /// 生成 Getter 基类测试源。
    /// </summary>
    private class BaseGeneratedSource
    {
        /// <summary>
        /// 获取或设置名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 生成 Getter 派生测试源。
    /// </summary>
    private sealed class DerivedGeneratedSource : BaseGeneratedSource
    {
    }

    /// <summary>
    /// 生成 Getter 接口测试契约。
    /// </summary>
    private interface IGeneratedNameSource
    {
        /// <summary>
        /// 获取名称。
        /// </summary>
        string Name { get; }
    }

    /// <summary>
    /// 仅通过接口提供生成 Getter 的测试源。
    /// </summary>
    private sealed class InterfaceGeneratedSource : IGeneratedNameSource
    {
        /// <summary>
        /// 获取或设置名称。
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
        /// 获取或设置主键。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 仅改变 Provider key 的测试 Provider，复用现有 SQL Server 方言和能力档案。
    /// </summary>
    private sealed class KeyedMutationSqlProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <summary>
        /// 初始化一个 <see cref="KeyedMutationSqlProvider"/> 类型的实例。
        /// </summary>
        /// <param name="key">Provider 路由标识。</param>
        public KeyedMutationSqlProvider(string key) => Key = key;

        /// <inheritdoc />
        public string Key { get; }

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.SqlServer;

        /// <inheritdoc />
        public IDialect Dialect => TestMutationSqlProvider.Instance.Dialect;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory => TestMutationSqlProvider.Instance.ClauseFactory;

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => TestMutationSqlProvider.Instance.TableReferenceParser;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer => TestMutationSqlProvider.Instance.PaginationRenderer;

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => TestMutationSqlProvider.Instance.ParameterManagerFactory;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => TestMutationSqlProvider.Instance.ParamLiteralsResolver;

        /// <inheritdoc />
        public SqlProviderProfile Profile => TestMutationSqlProvider.Instance.Profile;
    }

    /// <summary>
    /// 返回可变表名以验证大小写敏感缓存键的映射解析器。
    /// </summary>
    private sealed class CaseSensitiveMutationMappingResolver : DefaultEntityMappingResolver
    {
        /// <summary>
        /// 获取或设置当前解析器返回的表名。
        /// </summary>
        public string TableName { get; set; }

        /// <inheritdoc />
        protected override string GetTableName(EntityModelMetadata model, EntityMappingOptions mappingOptions) =>
            TableName;
    }
}
