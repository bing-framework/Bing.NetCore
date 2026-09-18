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

    private static EntityMappingOptions CreateMapping(string tableName) => new()
    {
        EntityType = typeof(QueryEntity),
        TableName = tableName
    };

    private sealed class SnapshotQuery : SqlQueryBase
    {
        public SnapshotQuery(IServiceProvider serviceProvider, SqlOptions<SnapshotQuery> options)
            : base(serviceProvider, options)
        {
        }

        public string ResolveTable() => EntityMappingResolver.Resolve(typeof(QueryEntity), null).Table.TableName;

        protected override ISqlBuilder CreateSqlBuilder() => null;
    }

    private sealed class QueryEntity
    {
        public int Id { get; set; }
    }
}

