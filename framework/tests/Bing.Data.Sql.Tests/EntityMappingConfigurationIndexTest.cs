using Bing.Data.Enums;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 实体映射配置索引测试。
/// </summary>
public class EntityMappingConfigurationIndexTest
{
    /// <summary>
    /// 测试 - Provider 专用映射应优先于同一实体的通用映射。
    /// </summary>
    [Fact]
    public void Resolve_WhenProviderSpecificMappingExists_ShouldPreferSpecificMapping()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(IndexedSample),
            DbKey = "reporting",
            TableName = "reporting_samples"
        });
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(IndexedSample),
            DbKey = "reporting",
            DatabaseType = DatabaseType.PgSql,
            TableName = "reporting_pg_samples"
        });
        var resolver = new DefaultEntityMappingResolver(options: options);
        var context = new DatabaseContext
        {
            DbKey = "reporting",
            DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.PgSql }
        };

        // Act
        var mapping = resolver.Resolve(typeof(IndexedSample), context);

        // Assert
        Assert.Equal("reporting_pg_samples", mapping.Table.TableName);
    }

    /// <summary>
    /// 配置索引测试实体。
    /// </summary>
    private sealed class IndexedSample
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }
    }
}