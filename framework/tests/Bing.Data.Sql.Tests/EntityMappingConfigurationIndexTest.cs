using System.ComponentModel.DataAnnotations.Schema;
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
    /// 测试目的：未配置显式映射时，应使用实体 TableAttribute 指定的表名和架构。
    /// </summary>
    [Fact]
    public void Resolve_WhenEntityUsesTableAttribute_ShouldUseAttributeTableAndSchema()
    {
        // Arrange
        var resolver = new DefaultEntityMappingResolver();

        // Act
        var mapping = resolver.Resolve(typeof(AttributedSample), null);

        // Assert
        Assert.Equal("sales", mapping.Table.Schema);
        Assert.Equal("sales_orders", mapping.Table.TableName);
    }

    /// <summary>
    /// 测试目的：TableAttribute 未指定架构时，默认映射应保留空架构。
    /// </summary>
    [Fact]
    public void Resolve_WhenTableAttributeDoesNotSpecifySchema_ShouldKeepEmptySchema()
    {
        // Arrange
        var resolver = new DefaultEntityMappingResolver();

        // Act
        var mapping = resolver.Resolve(typeof(TableOnlySample), null);

        // Assert
        Assert.Equal(string.Empty, mapping.Table.Schema);
        Assert.Equal("table_only_samples", mapping.Table.TableName);
    }

    /// <summary>
    /// 测试目的：显式实体映射应覆盖 TableAttribute 提供的默认表路由。
    /// </summary>
    [Fact]
    public void Resolve_WhenExplicitMappingAndTableAttributeExist_ShouldPreferExplicitMapping()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(AttributedSample),
            Schema = "reporting",
            TableName = "reporting_orders"
        });
        var resolver = new DefaultEntityMappingResolver(options: options);

        // Act
        var mapping = resolver.Resolve(typeof(AttributedSample), null);

        // Assert
        Assert.Equal("reporting", mapping.Table.Schema);
        Assert.Equal("reporting_orders", mapping.Table.TableName);
    }

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

    /// <summary>
    /// 带数据表映射特性的测试实体。
    /// </summary>
    [Table("sales_orders", Schema = "sales")]
    private sealed class AttributedSample
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 只指定表名的测试实体。
    /// </summary>
    [Table("table_only_samples")]
    private sealed class TableOnlySample
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }
    }
}