using Bing.Data.Enums;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>
/// 实体映射缓存键值隔离测试。
/// </summary>
public sealed class EntityMappingCacheKeyTest
{
    /// <summary>
    /// 测试目的：每个参与最终映射的键维度变化都必须产生不同缓存键。
    /// </summary>
    [Fact]
    public void Key_WhenAnyMappingDimensionChanges_ShouldNotRemainEqual()
    {
        var key = new EntityMappingCacheKey(
            typeof(KeyEntity).TypeHandle,
            "reporting",
            DatabaseType.SqlServer,
            "read",
            "tenant-a",
            "database",
            "schema",
            "table");

        Assert.NotEqual(key, key with { EntityTypeHandle = typeof(OtherKeyEntity).TypeHandle });
        Assert.NotEqual(key, key with { DbKey = "archive" });
        Assert.NotEqual(key, key with { DatabaseType = DatabaseType.MySql });
        Assert.NotEqual(key, key with { MappingProfile = "write" });
        Assert.NotEqual(key, key with { TableRouteKey = "tenant-b" });
        Assert.NotEqual(key, key with { Database = "other_database" });
        Assert.NotEqual(key, key with { Schema = "other_schema" });
        Assert.NotEqual(key, key with { TableName = "other_table" });
    }

    /// <summary>
    /// 测试实体。
    /// </summary>
    private sealed class KeyEntity
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 第二个测试实体。
    /// </summary>
    private sealed class OtherKeyEntity
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }
    }
}
