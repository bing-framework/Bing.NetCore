using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.FreeSQL;
using Bing.Uow;
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
        using var unitOfWork = new MetadataUnitOfWork(new FreeSqlWrapper { Orm = orm });

        // Act and Assert
        Assert.Null(unitOfWork.GetTable(null));
        Assert.Null(unitOfWork.GetSchema(null));
        Assert.Null(unitOfWork.GetColumn(null, nameof(DottedCompany.CompanyName)));
        Assert.Null(unitOfWork.GetColumn(typeof(DottedCompany), null));
    }

    /// <summary>
    /// 测试目的：未配置映射的实体和属性应回退到 CLR 类型与属性名称。
    /// </summary>
    [Fact]
    public void MetadataProvider_WhenEntityOrPropertyIsUnmapped_ShouldFallbackToClrNames()
    {
        // Arrange
        using var orm = CreateOrm();
        using var unitOfWork = new MetadataUnitOfWork(new FreeSqlWrapper { Orm = orm });

        // Act
        var table = unitOfWork.GetTable(typeof(UnmappedCompany));
        var column = unitOfWork.GetColumn(typeof(UnmappedCompany), nameof(UnmappedCompany.Name));

        // Assert
        Assert.Equal(nameof(UnmappedCompany), table);
        Assert.Equal(nameof(UnmappedCompany.Name), column);
    }

    /// <summary>
    /// 测试 - FreeSQL 元数据和 MySQL Builder 应保留带点物理表名。
    /// </summary>
    [Fact]
    public void MetadataProvider_WhenTableNameContainsDot_ShouldKeepAtomicNameForMySqlBuilder()
    {
        // Arrange
        using var orm = CreateOrm();
        using var unitOfWork = new MetadataUnitOfWork(new FreeSqlWrapper { Orm = orm });
        var builder = new MySqlBuilder(new SqlBuilderServices(entityModelMetadataProvider: unitOfWork));

        // Act
        var tableName = unitOfWork.GetTable(typeof(DottedCompany));
        var schema = unitOfWork.GetSchema(typeof(DottedCompany));
        var columnName = unitOfWork.GetColumn(typeof(DottedCompany), nameof(DottedCompany.CompanyName));
        var sql = builder.Select("*").From<DottedCompany>("c").ToSql();

        // Assert
        Assert.Equal("Merchants.Company", tableName);
        Assert.Equal(tableName, unitOfWork.GetTableName(typeof(DottedCompany)));
        Assert.Null(schema);
        Assert.Equal("company_name", columnName);
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
    /// 仅用于测试 FreeSQL 元数据的工作单元。
    /// </summary>
    private sealed class MetadataUnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化仅提供元数据的工作单元。
        /// </summary>
        /// <param name="wrapper">FreeSQL 包装器。</param>
        public MetadataUnitOfWork(FreeSqlWrapper wrapper) : base(wrapper, EmptyServiceProvider.Instance)
        {
        }
    }

    /// <summary>
    /// 不提供依赖服务的测试服务提供程序。
    /// </summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        /// <summary>
        /// 空服务提供程序单例。
        /// </summary>
        public static readonly EmptyServiceProvider Instance = new();

        /// <summary>
        /// 不解析任何服务。
        /// </summary>
        /// <param name="serviceType">请求的服务类型。</param>
        /// <returns>始终返回 <see langword="null"/>。</returns>
        public object GetService(Type serviceType) => null;
    }

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