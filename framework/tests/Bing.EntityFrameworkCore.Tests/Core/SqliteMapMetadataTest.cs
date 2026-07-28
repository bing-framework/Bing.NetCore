using Bing.Datas.EntityFramework.Sqlite;
using Bing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xunit;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// SQLite 映射元数据单元测试。
/// </summary>
public class SqliteMapMetadataTest
{
    /// <summary>
    /// 测试目的：实现 IVersion 的 SQLite 聚合根应将 Version 映射为并发令牌和行版本生成列。
    /// </summary>
    [Fact]
    public void AggregateRootMap_WhenEntityImplementsIVersion_ShouldConfigureVersionMetadata()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();

        // Act
        new VersionedSampleMap().Map(modelBuilder);
        var property = modelBuilder.Model.FindEntityType(typeof(VersionedSample))
            .FindProperty(nameof(IVersion.Version));

        // Assert
        Assert.NotNull(property);
        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
        Assert.Equal(nameof(IVersion.Version), ((IProperty)property).GetColumnName(
            StoreObjectIdentifier.Table("versioned_samples")));
    }

    /// <summary>
    /// 测试目的：未实现 IVersion 的实体不应由基础映射附加 Version 属性。
    /// </summary>
    [Fact]
    public void EntityMap_WhenEntityDoesNotImplementIVersion_ShouldNotConfigureVersionProperty()
    {
        // Arrange
        var modelBuilder = new ModelBuilder();

        // Act
        new PlainSampleMap().Map(modelBuilder);
        var property = modelBuilder.Model.FindEntityType(typeof(PlainSample))
            .FindProperty(nameof(IVersion.Version));

        // Assert
        Assert.Null(property);
    }

    /// <summary>
    /// 版本实体 SQLite 映射。
    /// </summary>
    private sealed class VersionedSampleMap : AggregateRootMap<VersionedSample>
    {
        /// <inheritdoc />
        protected override void MapTable(EntityTypeBuilder<VersionedSample> builder)
        {
            builder.ToTable("versioned_samples");
            builder.HasKey(entity => entity.Id);
        }
    }

    /// <summary>
    /// 普通实体 SQLite 映射。
    /// </summary>
    private sealed class PlainSampleMap : EntityMap<PlainSample>
    {
        /// <inheritdoc />
        protected override void MapTable(EntityTypeBuilder<PlainSample> builder)
        {
            builder.ToTable("plain_samples");
            builder.HasKey(entity => entity.Id);
        }
    }

    /// <summary>
    /// 带版本的测试实体。
    /// </summary>
    private sealed class VersionedSample : IVersion
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 版本号。
        /// </summary>
        public byte[] Version { get; set; }
    }

    /// <summary>
    /// 不带版本的测试实体。
    /// </summary>
    private sealed class PlainSample
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }
    }
}