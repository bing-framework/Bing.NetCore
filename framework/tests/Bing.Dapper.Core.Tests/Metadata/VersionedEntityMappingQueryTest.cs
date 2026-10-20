using Xunit;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>
/// Dapper Query 映射版本快照契约测试。
/// </summary>
public sealed class VersionedEntityMappingQueryTest
{
    /// <summary>
    /// 验证查询实例在创建时捕获当前的实体映射快照。
    /// </summary>
    [Fact]
    public void Query_WhenMappingsArePublished_ShouldKeepCreationTimeSnapshot()
    {
        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(CreateMapping("OldTable"));
        var resolver = new VersionedEntityMappingResolver(options: options);
        var services = new ServiceCollection();
        services.AddSingleton<IEntityMappingResolver>(resolver);
        using var provider = services.BuildServiceProvider();
        var oldQuery = new SnapshotQuery(provider, new SqlOptions<SnapshotQuery>());

        resolver.PublishMappings(new[] { CreateMapping("NewTable") });
        var newQuery = new SnapshotQuery(provider, new SqlOptions<SnapshotQuery>());

        Assert.Equal("OldTable", oldQuery.ResolveTable());
        Assert.Equal("NewTable", newQuery.ResolveTable());
    }

    /// <summary>
    /// 创建指向指定表名的实体映射配置。
    /// </summary>
    /// <param name="tableName">映射使用的表名。</param>
    /// <returns>对应查询实体的映射配置。</returns>
    private static EntityMappingOptions CreateMapping(string tableName) => new()
    {
        EntityType = typeof(QueryEntity),
        TableName = tableName
    };

    /// <summary>
    /// 使用基础 SQL 查询类型捕获实体映射快照的测试查询。
    /// </summary>
    private sealed class SnapshotQuery : SqlQueryBase
    {
        /// <summary>
        /// 初始化一个 <see cref="SnapshotQuery"/> 类型的实例。
        /// </summary>
        /// <param name="serviceProvider">提供查询依赖项的服务提供程序。</param>
        /// <param name="options">查询配置。</param>
        public SnapshotQuery(IServiceProvider serviceProvider, SqlOptions<SnapshotQuery> options)
            : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// 获取当前查询捕获的实体映射表名。
        /// </summary>
        /// <returns>当前快照中的查询表名。</returns>
        public string ResolveTable() => EntityMappingResolver.Resolve(typeof(QueryEntity), null).Table.TableName;

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    /// <summary>
    /// 用于映射快照测试的实体类型。
    /// </summary>
    private sealed class QueryEntity
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }
    }
}

