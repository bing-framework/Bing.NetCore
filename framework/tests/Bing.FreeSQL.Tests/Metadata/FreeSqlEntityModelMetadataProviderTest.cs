using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.FreeSQL;
using FreeSql;
using FreeSql.DataAnnotations;
using MySqlConnector;
using Xunit;

namespace Bing.FreeSQL.Tests.Metadata;

/// <summary>
/// FreeSQL 实体模型元数据提供器测试。
/// </summary>
public class FreeSqlEntityModelMetadataProviderTest
{
    /// <summary>
    /// 测试目的：实体类型或属性名缺失时元数据提供器应返回 null。
    /// </summary>
    [Fact]
    public void MetadataProvider_WhenEntityOrPropertyMissing_ShouldReturnNull()
    {
        // Arrange
        using var orm = CreateOrm();
        var provider = new FreeSqlEntityModelMetadataProvider(orm);

        // Act and Assert
        Assert.Null(provider.GetMetadata(null));
    }

    /// <summary>
    /// 测试目的：未配置映射的实体和属性应回退到 CLR 类型与属性名称。
    /// </summary>
    [Fact]
    public void MetadataProvider_WhenEntityOrPropertyIsUnmapped_ShouldFallbackToClrNames()
    {
        // Arrange
        using var orm = CreateOrm();
        var provider = new FreeSqlEntityModelMetadataProvider(orm);

        // Act
        var metadata = provider.GetMetadata(typeof(UnmappedCompany));

        // Assert
        Assert.Equal(nameof(UnmappedCompany), metadata.TableName);
        Assert.Equal(nameof(UnmappedCompany.Name), metadata.Properties[nameof(UnmappedCompany.Name)].ColumnName);
    }

    /// <summary>
    /// 测试 - FreeSQL 元数据和 MySQL Builder 应保留带点物理表名。
    /// </summary>
    [Fact]
    public void MetadataProvider_WhenTableNameContainsDot_ShouldKeepAtomicNameForMySqlBuilder()
    {
        // Arrange
        using var orm = CreateOrm();
        var metadataProvider = new FreeSqlEntityModelMetadataProvider(orm);
        var builder = new MySqlBuilder(new SqlBuilderServices(entityModelMetadataProvider: metadataProvider));

        // Act
        var metadata = metadataProvider.GetMetadata(typeof(DottedCompany));
        var sql = builder.Select("*").From<DottedCompany>("c").ToSql();

        // Assert
        Assert.Equal("Merchants.Company", metadata.TableName);
        Assert.Equal("company_name", metadata.Properties[nameof(DottedCompany.CompanyName)].ColumnName);
        Assert.Equal("Select * \r\nFrom `Merchants.Company` As `c`", sql);
    }

    /// <summary>
    /// 创建不打开外部连接的 MySQL FreeSQL 实例。
    /// </summary>
    /// <returns>仅用于元数据解析的 FreeSQL 实例。</returns>
    private static IFreeSql CreateOrm() => new FreeSqlBuilder()
        .UseConnectionFactory(DataType.MySql, () => new MySqlConnection())
        .Build();

    /// <summary>
    /// 带点物理表名实体。
    /// </summary>
    [Table(Name = "Merchants.Company")]
    private sealed class DottedCompany
    {
        /// <summary>
        /// 公司名称。
        /// </summary>
        [Column(Name = "company_name")]
        public string CompanyName { get; set; }
    }

    /// <summary>
    /// 未配置元数据映射的测试实体。
    /// </summary>
    private sealed class UnmappedCompany
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }
}