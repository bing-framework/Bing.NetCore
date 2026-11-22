using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>
/// 验证默认映射解析器保留生成主键的具体类型和缓存隔离。
/// </summary>
public sealed class GeneratedKeyMappingTest
{
    /// <summary>
    /// 验证生成键元数据保留不同键类别并隔离缓存项。
    /// </summary>
    [Fact]
    public void Resolve_WhenGeneratedKeyKindsDiffer_ShouldPreserveKindsAndIsolateCacheEntries()
    {
        var resolver = new DefaultEntityMappingResolver();
        var context = new DatabaseContext();

        var identity = resolver.Resolve(typeof(IdentityKeyEntity), context);
        var computed = resolver.Resolve(typeof(ComputedKeyEntity), context);
        var assigned = resolver.Resolve(typeof(AssignedKeyEntity), context);
        var identityAgain = resolver.Resolve(typeof(IdentityKeyEntity), context);
        var computedAgain = resolver.Resolve(typeof(ComputedKeyEntity), context);
        var assignedAgain = resolver.Resolve(typeof(AssignedKeyEntity), context);

        Assert.Equal(DatabaseGeneratedOption.Identity, identity.Columns[nameof(IdentityKeyEntity.Id)].DatabaseGeneratedOption);
        Assert.Equal(DatabaseGeneratedOption.Computed, computed.Columns[nameof(ComputedKeyEntity.Id)].DatabaseGeneratedOption);
        Assert.Equal(DatabaseGeneratedOption.None, assigned.Columns[nameof(AssignedKeyEntity.Id)].DatabaseGeneratedOption);
        Assert.True(identity.Columns[nameof(IdentityKeyEntity.Id)].IsDatabaseGenerated);
        Assert.True(computed.Columns[nameof(ComputedKeyEntity.Id)].IsDatabaseGenerated);
        Assert.False(assigned.Columns[nameof(AssignedKeyEntity.Id)].IsDatabaseGenerated);

        Assert.Same(identity, identityAgain);
        Assert.Same(computed, computedAgain);
        Assert.Same(assigned, assignedAgain);
        Assert.NotSame(identity, computed);
        Assert.NotSame(identity, assigned);
        Assert.NotSame(computed, assigned);

        var statistics = resolver.MappingCacheStatistics;
        Assert.Equal(3, statistics.CacheMissCount);
        Assert.Equal(3, statistics.CacheHitCount);
        Assert.Equal(3, statistics.EntryCount);
    }

    /// <summary>
    /// 提供实体主键生成策略测试使用的查询数据模型。
    /// </summary>
    [Table("generated_identity")]
    private sealed class IdentityKeyEntity
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
    }

    /// <summary>
    /// 提供实体主键生成策略测试使用的查询数据模型。
    /// </summary>
    [Table("generated_computed")]
    private sealed class ComputedKeyEntity
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public long Id { get; set; }
    }

    /// <summary>
    /// 提供实体主键生成策略测试使用的查询数据模型。
    /// </summary>
    [Table("generated_assigned")]
    private sealed class AssignedKeyEntity
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { get; set; }
    }
}
