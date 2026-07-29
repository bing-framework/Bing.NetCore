using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
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
    /// 测试目的：并发原始值对象应按其运行时类型缓存 Getter，并在相同类型重复执行时复用。
    /// </summary>
    [Fact]
    public void Update_WhenOriginalValuesTypeIsRepeated_ShouldReuseOriginalValueGetter()
    {
        // Arrange
        var builder = CreateBuilder();
        var first = new MutationSample { Id = 1, Name = "first", Amount = 1m, Version = "v1" };
        var second = new MutationSample { Id = 2, Name = "second", Amount = 2m, Version = "v2" };
        var options = new SqlUpdateOptions
        {
            IncludeProperties = new[] { nameof(MutationSample.Name) },
            OriginalValues = new MutationOriginalValues { Version = "v1" }
        };

        // Act
        builder.Update(first, options);
        var getterCount = builder.GetterCacheCount;
        builder.Update(second, options);

        // Assert
        Assert.True(getterCount >= 3);
        Assert.Equal(getterCount, builder.GetterCacheCount);
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
    /// 创建使用独立映射解析器的实体 Mutation Builder。
    /// </summary>
    private static DefaultSqlMutationBuilder CreateBuilder() =>
        new(TestMutationSqlProvider.Instance, new SqlBuilderServices());

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
    /// 仅保存并发原始值的对象。
    /// </summary>
    private sealed class MutationOriginalValues
    {
        /// <summary>
        /// 原始并发令牌。
        /// </summary>
        public string Version { get; set; }
    }
}
