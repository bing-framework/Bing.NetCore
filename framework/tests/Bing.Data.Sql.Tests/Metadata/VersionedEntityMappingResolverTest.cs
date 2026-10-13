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
    /// <summary>
    /// 验证发布新的表和列映射后，旧快照仍保留原配置。
    /// </summary>
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

    /// <summary>
    /// 验证发布映射时会复制输入配置，后续修改不会影响已发布快照。
    /// </summary>
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

    /// <summary>
    /// 验证枚举映射失败时当前版本和映射保持不变。
    /// </summary>
    [Fact]
    public void PublishMappings_WhenEnumerationFails_ShouldKeepCurrentVersionAndMapping()
    {
        var resolver = new VersionedEntityMappingResolver(options: CreateOptions("OldTable", "old_name"));

        Assert.Throws<InvalidOperationException>(() => resolver.PublishMappings(new ThrowingMappings()));

        Assert.Equal(1, resolver.CurrentVersion);
        Assert.Equal("OldTable", resolver.Resolve(typeof(Sample), Context).Table.TableName);
    }

    /// <summary>
    /// 验证候选映射新增、匹配和移除后选择索引保持正确。
    /// </summary>
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
    /// <summary>
    /// 验证并发发布产生完整且单调递增的版本。
    /// </summary>
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

    /// <summary>
    /// 验证已有 Builder 在发布新版本后继续使用原映射快照。
    /// </summary>
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

    /// <summary>
    /// 验证逻辑标识中的空白规范化后仍能选择相同列映射。
    /// </summary>
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

    /// <summary>
    /// 解析测试映射时使用的数据库上下文。
    /// </summary>
    private static readonly DatabaseContext Context = new()
    {
        DbKey = "reporting", MappingProfile = "read", TenantId = "route-a",
        DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer }
    };

    /// <summary>
    /// 创建仅包含一个样例实体映射的元数据配置。
    /// </summary>
    /// <param name="table">映射表名。</param>
    /// <param name="column">名称属性对应的列名。</param>
    /// <returns>包含样例实体映射的元数据配置。</returns>
    private static SqlMetadataOptions CreateOptions(string table, string column)
    {
        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(CreateMapping(table, column));
        return options;
    }

    /// <summary>
    /// 创建包含数据库、配置和路由范围的样例实体映射。
    /// </summary>
    /// <param name="table">映射表名。</param>
    /// <param name="column">名称属性对应的列名。</param>
    /// <returns>样例实体映射配置。</returns>
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

    /// <summary>
    /// 枚举时抛出异常的映射集合测试替身。
    /// </summary>
    private sealed class ThrowingMappings : IEnumerable<EntityMappingOptions>
    {
        /// <inheritdoc />
        public IEnumerator<EntityMappingOptions> GetEnumerator() => throw new InvalidOperationException("Failed to enumerate mappings.");

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// 映射解析测试使用的样例实体。
    /// </summary>
    private sealed class Sample
    {
        /// <summary>
        /// 获取或设置实体名称。
        /// </summary>
        public string Name { get; set; }
    }
}
