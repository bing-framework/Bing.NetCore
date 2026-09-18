using System.Collections;
using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>
/// 版本化实体映射解析器职责测试。
/// </summary>
public sealed class VersionedEntityMappingResolverTest
{
    [Fact]
    public void PublishMappings_WhenColumnAndTableChange_ShouldKeepOldSnapshotAndPublishNewSnapshot()
    {
        var resolver = new VersionedEntityMappingResolver(options: CreateOptions("OldTable", "old_name"));
        var oldSnapshot = resolver.CaptureSnapshot();

        var version = resolver.PublishMappings(CreateOptions("NewTable", "new_name").EntityMappings);
        var newSnapshot = resolver.CaptureSnapshot();

        Assert.Equal(2, version);
        Assert.Equal(2, resolver.CurrentVersion);
        Assert.Equal("OldTable", oldSnapshot.Resolve(typeof(Sample), Context).Table.TableName);
        Assert.Equal("old_name", oldSnapshot.Resolve(typeof(Sample), Context).Columns[nameof(Sample.Name)].ColumnName);
        Assert.Equal("NewTable", newSnapshot.Resolve(typeof(Sample), Context).Table.TableName);
        Assert.Equal("new_name", newSnapshot.Resolve(typeof(Sample), Context).Columns[nameof(Sample.Name)].ColumnName);
    }

    [Fact]
    public void PublishMappings_WhenInputChangesAfterPublication_ShouldUseDeepSnapshot()
    {
        var resolver = new VersionedEntityMappingResolver();
        var mapping = CreateMapping("PublishedTable", "published_name");

        resolver.PublishMappings(new[] { mapping });
        mapping.TableName = "ChangedTable";
        mapping.Columns[nameof(Sample.Name)].ColumnName = "changed_name";
        mapping.Columns[nameof(Sample.Name)].DbType = DbType.Int32;
        mapping.Columns[nameof(Sample.Name)].ConverterKind = FieldValueConverterKind.BoolToNumber;
        var result = resolver.Resolve(typeof(Sample), Context);

        Assert.Equal("PublishedTable", result.Table.TableName);
        Assert.Equal("published_name", result.Columns[nameof(Sample.Name)].ColumnName);
        Assert.Equal(DbType.String, result.Columns[nameof(Sample.Name)].DbType);
        Assert.Equal(FieldValueConverterKind.None, result.Columns[nameof(Sample.Name)].ConverterKind);
    }

    [Fact]
    public void PublishMappings_WhenEnumerationFails_ShouldKeepCurrentVersionAndMapping()
    {
        var resolver = new VersionedEntityMappingResolver(options: CreateOptions("OldTable", "old_name"));

        Assert.Throws<InvalidOperationException>(() => resolver.PublishMappings(new ThrowingMappings()));

        Assert.Equal(1, resolver.CurrentVersion);
        Assert.Equal("OldTable", resolver.Resolve(typeof(Sample), Context).Table.TableName);
    }

    [Fact]
    public void PublishMappings_WhenCandidateIsAddedAndRemoved_ShouldRebuildSelectionIndex()
    {
        var resolver = new VersionedEntityMappingResolver();
        var candidate = CreateMapping("ProfileTable", "profile_name");
        candidate.MappingProfile = "other";

        resolver.PublishMappings(new[] { candidate });
        var unmatched = resolver.Resolve(typeof(Sample), Context);
        candidate.MappingProfile = "read";
        resolver.PublishMappings(new[] { candidate });
        var matched = resolver.Resolve(typeof(Sample), Context);
        resolver.PublishMappings(Array.Empty<EntityMappingOptions>());
        var removed = resolver.Resolve(typeof(Sample), Context);

        Assert.Equal(nameof(Sample), unmatched.Table.TableName);
        Assert.Equal("ProfileTable", matched.Table.TableName);
        Assert.Equal("profile_name", matched.Columns[nameof(Sample.Name)].ColumnName);
        Assert.Equal(nameof(Sample), removed.Table.TableName);
    }
    [Fact]
    public async Task PublishMappings_WhenCalledConcurrently_ShouldPublishCompleteMonotonicVersions()
    {
        var resolver = new VersionedEntityMappingResolver();

        var versions = await Task.WhenAll(Enumerable.Range(1, 8).Select(index => Task.Run(() =>
            resolver.PublishMappings(new[] { CreateMapping($"Table{index}", $"name_{index}") }))));
        var result = resolver.Resolve(typeof(Sample), Context);

        Assert.Equal(Enumerable.Range(2, 8).Select(value => (long)value).ToArray(), versions.OrderBy(value => value).ToArray());
        Assert.Equal(9, resolver.CurrentVersion);
        Assert.StartsWith("Table", result.Table.TableName);
        Assert.StartsWith("name_", result.Columns[nameof(Sample.Name)].ColumnName);
    }

    [Fact]
    public void SqlBuilderServices_WhenVersionIsPublished_ShouldKeepExistingBuilderSnapshot()
    {
        var resolver = new VersionedEntityMappingResolver(options: CreateOptions("OldTable", "old_name"));
        var oldBuilder = new TestSqlBuilder(entityMappingResolver: resolver);

        resolver.PublishMappings(CreateOptions("NewTable", "new_name").EntityMappings);
        var newBuilder = new TestSqlBuilder(entityMappingResolver: resolver);
        var clone = (TestSqlBuilder)oldBuilder.Clone();

        Assert.Equal("OldTable", oldBuilder.SharedServices.EntityMappingResolver.Resolve(typeof(Sample), Context).Table.TableName);
        Assert.Equal("OldTable", clone.SharedServices.EntityMappingResolver.Resolve(typeof(Sample), Context).Table.TableName);
        Assert.Equal("NewTable", newBuilder.SharedServices.EntityMappingResolver.Resolve(typeof(Sample), Context).Table.TableName);
    }

    [Fact]
    public void Resolve_WhenLogicalIdentifiersContainWhitespace_ShouldSelectSameColumnMapping()
    {
        var resolver = new DefaultEntityMappingResolver(options: CreateOptions("MappedTable", "mapped_name"));
        var context = new DatabaseContext
        {
            DbKey = " REPORTING ", MappingProfile = " READ ", TenantId = " ROUTE-A ",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
        };

        var mapping = resolver.Resolve(typeof(Sample), context);

        Assert.Equal("mapped_name", mapping.Columns[nameof(Sample.Name)].ColumnName);
    }

    private static readonly DatabaseContext Context = new()
    {
        DbKey = "reporting", MappingProfile = "read", TenantId = "route-a",
        DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
    };

    private static SqlMetadataOptions CreateOptions(string table, string column)
    {
        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(CreateMapping(table, column));
        return options;
    }

    private static EntityMappingOptions CreateMapping(string table, string column)
    {
        var mapping = new EntityMappingOptions
        {
            EntityType = typeof(Sample), DbKey = "reporting", MappingProfile = "read",
            TableRouteKey = "route-a", DatabaseType = DatabaseType.SqlServer, TableName = table
        };
        mapping.Columns[nameof(Sample.Name)] = new ColumnMappingOptions
        {
            PropertyName = nameof(Sample.Name), ColumnName = column, DbType = DbType.String
        };
        return mapping;
    }

    private sealed class ThrowingMappings : IEnumerable<EntityMappingOptions>
    {
        public IEnumerator<EntityMappingOptions> GetEnumerator() => throw new InvalidOperationException("Failed to enumerate mappings.");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class Sample
    {
        public string Name { get; set; }
    }
}
