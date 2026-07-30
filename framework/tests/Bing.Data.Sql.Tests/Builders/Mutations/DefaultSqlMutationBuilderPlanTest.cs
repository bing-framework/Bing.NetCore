using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// 默认实体 Mutation Builder 的映射计划缓存测试。
/// </summary>
public sealed class DefaultSqlMutationBuilderPlanTest
{
    /// <summary>
    /// 测试目的：相同 Insert 映射应命中同一个计划与属性 Getter 缓存。
    /// </summary>
    [Fact]
    public void Insert_WhenEntityMappingIsRepeated_ShouldReusePlanAndGetters()
    {
        // Arrange
        var builder = CreateBuilder();
        var first = new MutationSample { Id = 1, Name = "first", Amount = 1m, Version = "v1" };
        var second = new MutationSample { Id = 2, Name = "second", Amount = 2m, Version = "v2" };

        // Act
        builder.Insert(first);
        var planCount = builder.PlanCacheCount;
        var getterCount = builder.GetterCacheCount;
        builder.Insert(second);

        // Assert
        Assert.Equal(1, planCount);
        Assert.True(getterCount > 0);
        Assert.Equal(planCount, builder.PlanCacheCount);
        Assert.Equal(getterCount, builder.GetterCacheCount);
    }

    /// <summary>
    /// 测试目的：组合 Insert 应使用同一映射计划生成多行 Values，并保留每个实体独立的参数值。
    /// </summary>
    [Fact]
    public void InsertCombined_WhenEntitiesAreProvided_ShouldRenderSingleMultiRowValuesCommand()
    {
        // Arrange
        var builder = CreateBuilder();
        var entities = new[]
        {
            new MutationSample { Id = 1, Name = "first", Amount = 1m, Version = "v1" },
            new MutationSample { Id = 2, Name = "second", Amount = 2m, Version = "v2" }
        };

        // Act
        var command = builder.InsertCombined(entities);

        // Assert
        Assert.Equal("Insert Into [mutation_samples] ([Id], [Name], [Amount], [Version]) Values " +
                     "(@_p_0, @_p_1, @_p_2, @_p_3), (@_p_4, @_p_5, @_p_6, @_p_7)", command.Sql);
        Assert.Equal(new object[] { 1, "first", 1m, "v1", 2, "second", 2m, "v2" },
            command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(1, builder.PlanCacheCount);
    }

    /// <summary>
    /// 测试目的：无并发列的单主键实体应生成单条参数化 IN 批量删除命令。
    /// </summary>
    [Fact]
    public void DeleteCombined_WhenSingleKeyHasNoConcurrencyColumn_ShouldRenderInPredicate()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var command = builder.DeleteCombined(new[]
        {
            new SimpleDeleteSample { Id = 1 },
            new SimpleDeleteSample { Id = 2 }
        });

        // Assert
        Assert.Equal("Delete From [simple_delete_samples] Where [Id] In (@_p_0,@_p_1)", command.Sql);
        Assert.Equal(new object[] { 1, 2 }, command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 测试目的：存在并发列时，主键与版本必须按实体配对，不能拆分为会交叉匹配的 IN 条件。
    /// </summary>
    [Fact]
    public void DeleteCombined_WhenConcurrencyColumnsExist_ShouldKeepConditionsPairedPerEntity()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var command = builder.DeleteCombined(new[]
        {
            new MutationSample { Id = 1, Version = "v1" },
            new MutationSample { Id = 2, Version = "v2" }
        });

        // Assert
        Assert.Equal("Delete From [mutation_samples] Where (([Id] = @_p_0 And [Version] = @_p_1) Or ([Id] = @_p_2 And [Version] = @_p_3))",
            command.Sql);
        Assert.Equal(new object[] { 1, "v1", 2, "v2" }, command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 测试目的：CompositePredicate 应强制单主键实体使用按实体配对条件，供调用方在需要统一形状时避免 IN 语义。
    /// </summary>
    [Fact]
    public void DeleteCombined_WhenCompositePredicateIsRequested_ShouldRenderPairedConditions()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var command = builder.DeleteCombined(new[]
        {
            new SimpleDeleteSample { Id = 1 },
            new SimpleDeleteSample { Id = 2 }
        }, strategy: SqlBatchDeleteStrategy.CompositePredicate);

        // Assert
        Assert.Equal("Delete From [simple_delete_samples] Where (([Id] = @_p_0) Or ([Id] = @_p_1))", command.Sql);
        Assert.Equal(new object[] { 1, 2 }, command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 测试目的：InPredicate 不得用于带并发令牌的实体，防止主键和值版本被拆分成不安全的独立条件。
    /// </summary>
    [Fact]
    public void DeleteCombined_WhenInPredicateIsRequestedForConcurrencyEntity_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.DeleteCombined(new[]
        {
            new MutationSample { Id = 1, Version = "v1" }
        }, strategy: SqlBatchDeleteStrategy.InPredicate));

        // Assert
        Assert.Equal("InPredicate 策略仅支持不带并发令牌的单主键实体。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Include 集合的大小写与顺序不应创建重复 Update 计划，不同筛选应保持隔离。
    /// </summary>
    [Fact]
    public void Update_WhenColumnFiltersDiffer_ShouldReuseEquivalentPlanAndIsolateDifferentPlan()
    {
        // Arrange
        var builder = CreateBuilder();
        var entity = new MutationSample { Id = 1, Name = "updated", Amount = 2m, Version = "v1" };

        // Act
        builder.Update(entity, new SqlUpdateOptions { IncludeProperties = new[] { "Name", "Amount" } });
        var equivalentPlanCount = builder.PlanCacheCount;
        builder.Update(entity, new SqlUpdateOptions { IncludeProperties = new[] { "amount", "name" } });
        var differentPlanCountBefore = builder.PlanCacheCount;
        builder.Update(entity, new SqlUpdateOptions { IncludeProperties = new[] { "Name" } });

        // Assert
        Assert.Equal(1, equivalentPlanCount);
        Assert.Equal(equivalentPlanCount, differentPlanCountBefore);
        Assert.Equal(2, builder.PlanCacheCount);
    }

    /// <summary>
    /// 测试目的：强类型并发原始值应按指定属性写入条件，且重复执行保持稳定。
    /// </summary>
    [Fact]
    public void Update_WhenTypedOriginalValueIsRepeated_ShouldRenderConfiguredConditionValue()
    {
        // Arrange
        var builder = CreateBuilder();
        var first = new MutationSample { Id = 1, Name = "first", Amount = 1m, Version = "v1" };
        var second = new MutationSample { Id = 2, Name = "second", Amount = 2m, Version = "v2" };
        var options = new SqlUpdateOptions<MutationSample>
        {
            IncludeProperties = new[] { nameof(MutationSample.Name) }
        }.Original(item => item.Version, "v1");

        // Act
        var firstCommand = builder.Update(first, options);
        var secondCommand = builder.Update(second, options);

        // Assert
        Assert.Equal("v1", firstCommand.Parameters.Last().Value);
        Assert.Equal("v1", secondCommand.Parameters.Last().Value);
    }

    /// <summary>
    /// 测试目的：不同实体映射解析器必须使用独立的 Mutation Plan 分区，避免跨服务复用元数据。
    /// </summary>
    [Fact]
    public void Insert_WhenMappingResolversDiffer_ShouldKeepPlanCachesIsolated()
    {
        // Arrange
        var firstBuilder = CreateBuilder();
        var secondBuilder = CreateBuilder();
        var entity = new MutationSample { Id = 1, Name = "sample", Amount = 1m, Version = "v1" };

        // Act
        firstBuilder.Insert(entity);
        secondBuilder.Insert(entity);

        // Assert
        Assert.Equal(1, firstBuilder.PlanCacheCount);
        Assert.Equal(1, secondBuilder.PlanCacheCount);
    }

    /// <summary>
    /// 测试目的：并发缓存未命中时计划工厂只能执行一次，所有调用方必须获得同一个已发布计划。
    /// </summary>
    [Fact]
    public void PlanCache_WhenConcurrentMissesOccur_ShouldCreatePlanOnlyOnce()
    {
        // Arrange
        var cache = new SqlMutationPlanCache();
        var mapping = CreatePlanCacheMapping();
        var key = SqlMutationPlanCacheKey.Create(mapping, "test.mutation", SqlMutationOperation.Insert, null, null);
        var factoryCallCount = 0;
        var results = new SqlMutationPlan[32];

        // Act
        Parallel.For(0, results.Length, index =>
        {
            results[index] = cache.GetOrAdd(key, () =>
            {
                Interlocked.Increment(ref factoryCallCount);
                return SqlMutationPlan.Create(mapping, SqlMutationOperation.Insert, null, null);
            });
        });

        // Assert
        Assert.Equal(1, factoryCallCount);
        Assert.All(results, plan => Assert.Same(results[0], plan));
        Assert.Equal(1, cache.PlanCount);
    }

    /// <summary>
    /// 测试目的：计划工厂失败时缓存不得保留失败 Lazy 实例，后续调用必须能够重新创建有效计划。
    /// </summary>
    [Fact]
    public void PlanCache_WhenFactoryThrows_ShouldRemoveFailureAndAllowRetry()
    {
        // Arrange
        var cache = new SqlMutationPlanCache();
        var mapping = CreatePlanCacheMapping();
        var key = SqlMutationPlanCacheKey.Create(mapping, "test.mutation", SqlMutationOperation.Insert, null, null);
        var factoryCallCount = 0;

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd(key, () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            throw new InvalidOperationException("plan failed");
        }));
        var plan = cache.GetOrAdd(key, () =>
        {
            Interlocked.Increment(ref factoryCallCount);
            return SqlMutationPlan.Create(mapping, SqlMutationOperation.Insert, null, null);
        });

        // Assert
        Assert.Equal("plan failed", exception.Message);
        Assert.NotNull(plan);
        Assert.Equal(2, factoryCallCount);
        Assert.Equal(1, cache.PlanCount);
    }

    /// <summary>
    /// 测试目的：属性 Getter 编译失败时不得缓存异常 Lazy，重复调用仍应重新验证属性并保持缓存为空。
    /// </summary>
    [Fact]
    public void GetterCache_WhenPropertyIsMissing_ShouldRemoveFailedEntry()
    {
        // Arrange
        var cache = new SqlMutationPlanCache();
        var column = new ColumnMappingMetadata { PropertyName = "Missing" };
        var entity = new MutationSample();

        // Act
        var firstException = Assert.Throws<InvalidOperationException>(() => cache.GetValue(entity, column));
        var countAfterFirstFailure = cache.GetterCount;
        var secondException = Assert.Throws<InvalidOperationException>(() => cache.GetValue(entity, column));

        // Assert
        Assert.Equal("原始值对象未包含属性 Missing。", firstException.Message);
        Assert.Equal(firstException.Message, secondException.Message);
        Assert.Equal(0, countAfterFirstFailure);
        Assert.Equal(0, cache.GetterCount);
    }

    /// <summary>
    /// 创建使用独立映射解析器的实体 Mutation Builder。
    /// </summary>
    private static DefaultSqlEntityMutationCommandBuilder CreateBuilder() =>
        new(TestMutationSqlProvider.Instance, new SqlBuilderServices());

    /// <summary>
    /// 创建用于直接缓存测试的稳定映射。
    /// </summary>
    private static EntityMappingMetadata CreatePlanCacheMapping() => new()
    {
        EntityType = typeof(MutationSample),
        Table = new SqlTableReference { TableName = "mutation_samples" },
        Columns = new Dictionary<string, ColumnMappingMetadata>(StringComparer.Ordinal)
        {
            [nameof(MutationSample.Name)] = new ColumnMappingMetadata
            {
                PropertyName = nameof(MutationSample.Name),
                ColumnName = "Name",
                CanInsert = true,
                CanUpdate = true
            }
        }
    };

    /// <summary>
    /// 映射到测试表的实体。
    /// </summary>
    [Table("mutation_samples")]
    private sealed class MutationSample
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

        /// <summary>
        /// 金额。
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// 乐观并发令牌。
        /// </summary>
        [ConcurrencyCheck]
        public string Version { get; set; }
    }

    /// <summary>
    /// 映射到仅含单主键测试表的实体。
    /// </summary>
    [Table("simple_delete_samples")]
    private sealed class SimpleDeleteSample
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [Key]
        public int Id { get; set; }
    }

}
