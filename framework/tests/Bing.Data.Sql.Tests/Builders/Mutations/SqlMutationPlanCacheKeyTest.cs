using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Builders.Mutations;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Mutation Plan 缓存键的直接职责测试。
/// </summary>
public sealed class SqlMutationPlanCacheKeyTest
{
    /// <summary>
    /// 测试目的：字符串维度和属性集合的等价表达应生成同一个规范化缓存键。
    /// </summary>
    [Fact]
    public void Create_WhenEquivalentValuesDifferInCaseWhitespaceOrOrder_ShouldReturnEqualKey()
    {
        var mapping = CreateMapping();

        var first = SqlMutationPlanCacheKey.Create(mapping, " mysql ", SqlMutationOperation.Update,
            new[] { " Name ", "AMOUNT", "Name" }, new[] { " Id " });
        var second = SqlMutationPlanCacheKey.Create(mapping, "MYSQL", SqlMutationOperation.Update,
            new[] { "amount", "name" }, new[] { "id" });

        Assert.Equal(first, second);
    }

    /// <summary>
    /// 测试目的：实体、Provider、对象名、路由、操作和列筛选任一维度变化都必须隔离 Plan。
    /// </summary>
    [Fact]
    public void Create_WhenAnyPlanDimensionChanges_ShouldReturnDifferentKey()
    {
        var mapping = CreateMapping();
        var key = SqlMutationPlanCacheKey.Create(mapping, "mysql", SqlMutationOperation.Insert,
            new[] { "Name" }, new[] { "Id" });

        Assert.NotEqual(key, SqlMutationPlanCacheKey.Create(CreateMapping(typeof(OtherMutationEntity)),
            "mysql", SqlMutationOperation.Insert, new[] { "Name" }, new[] { "Id" }));
        Assert.NotEqual(key, SqlMutationPlanCacheKey.Create(mapping, "sqlserver", SqlMutationOperation.Insert,
            new[] { "Name" }, new[] { "Id" }));
        Assert.NotEqual(key, SqlMutationPlanCacheKey.Create(CreateMapping(database: "other_db"),
            "mysql", SqlMutationOperation.Insert, new[] { "Name" }, new[] { "Id" }));
        Assert.NotEqual(key, SqlMutationPlanCacheKey.Create(CreateMapping(schema: "other_schema"),
            "mysql", SqlMutationOperation.Insert, new[] { "Name" }, new[] { "Id" }));
        Assert.NotEqual(key, SqlMutationPlanCacheKey.Create(CreateMapping(table: "other_table"),
            "mysql", SqlMutationOperation.Insert, new[] { "Name" }, new[] { "Id" }));
        Assert.NotEqual(key, SqlMutationPlanCacheKey.Create(CreateMapping(profile: "write"),
            "mysql", SqlMutationOperation.Insert, new[] { "Name" }, new[] { "Id" }));
        Assert.NotEqual(key, SqlMutationPlanCacheKey.Create(CreateMapping(route: "tenant-b"),
            "mysql", SqlMutationOperation.Insert, new[] { "Name" }, new[] { "Id" }));
        Assert.NotEqual(key, SqlMutationPlanCacheKey.Create(mapping, "mysql", SqlMutationOperation.Update,
            new[] { "Name" }, new[] { "Id" }));
        Assert.NotEqual(key, SqlMutationPlanCacheKey.Create(mapping, "mysql", SqlMutationOperation.Insert,
            new[] { "Amount" }, new[] { "Id" }));
        Assert.NotEqual(key, SqlMutationPlanCacheKey.Create(mapping, "mysql", SqlMutationOperation.Insert,
            new[] { "Name" }, new[] { "Version" }));
    }

    /// <summary>
    /// 创建稳定的测试映射。
    /// </summary>
    private static EntityMappingMetadata CreateMapping(Type entityType = null, string database = "database",
        string schema = "schema", string table = "table", string profile = "read", string route = "tenant-a") =>
        new()
        {
            EntityType = entityType ?? typeof(MutationEntity),
            MappingProfile = profile,
            TableRouteKey = route,
            Table = new SqlTableReference
            {
                Database = database,
                Schema = schema,
                TableName = table
            }
        };

    /// <summary>
    /// 主测试实体。
    /// </summary>
    private sealed class MutationEntity
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 第二个测试实体。
    /// </summary>
    private sealed class OtherMutationEntity
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }
    }
}
