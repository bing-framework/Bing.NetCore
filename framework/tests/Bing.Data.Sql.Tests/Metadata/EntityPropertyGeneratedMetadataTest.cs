using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Metadata;

/// <summary>
/// 生成式实体属性描述与映射解析器契约测试。
/// </summary>
public sealed class EntityPropertyGeneratedMetadataTest
{
    /// <summary>
    /// 测试目的：生成属性描述没有 PropertyInfo 时，GetDescriptor 仍应保留旧反射契约。
    /// </summary>
    [Fact]
    public void GetDescriptor_WhenGeneratedMetadataMatchesPublicMembers_ShouldPreserveReflectionContract()
    {
        var model = new EntityModelMetadata(
            typeof(DescriptorEntity),
            "generated_descriptor_table",
            "generated_descriptor_schema",
            new[]
            {
                EntityPropertyMetadata.CreateGenerated(nameof(DescriptorEntity.Id), typeof(long), true, true, isKey: true),
                EntityPropertyMetadata.CreateGenerated(nameof(DescriptorEntity.DisplayName), typeof(string), true, true)
            });
        GeneratedEntityModelMetadataProvider.Register(model);

        var descriptor = new DefaultEntityMappingResolver(new GeneratedEntityModelMetadataProvider())
            .GetDescriptor(typeof(DescriptorEntity));

        Assert.Equal(typeof(DescriptorEntity), descriptor.EntityType);
        Assert.Equal(2, descriptor.Properties.Count);
        Assert.Contains(descriptor.Properties, property => property.Name == nameof(DescriptorEntity.Id));
        Assert.Contains(descriptor.Properties, property => property.Name == nameof(DescriptorEntity.DisplayName));
        Assert.Single(descriptor.KeyProperties);
        Assert.Equal(nameof(DescriptorEntity.Id), descriptor.KeyProperties[0].Name);
    }

    /// <summary>
    /// 测试目的：没有对应 CLR 反射属性的生成描述不能被旧反射描述符静默接受。
    /// </summary>
    [Fact]
    public void GetDescriptor_WhenGeneratedMemberHasNoReflectionProperty_ShouldThrow()
    {
        var model = new EntityModelMetadata(
            typeof(MissingDescriptorEntity),
            "generated_missing_descriptor_table",
            string.Empty,
            new[] { EntityPropertyMetadata.CreateGenerated("GeneratedOnly", typeof(string), true, true) });
        GeneratedEntityModelMetadataProvider.Register(model);
        var resolver = new DefaultEntityMappingResolver(new GeneratedEntityModelMetadataProvider());

        var exception = Assert.Throws<NotSupportedException>(() => resolver.GetDescriptor(typeof(MissingDescriptorEntity)));

        Assert.Contains("生成式查询读取器", exception.Message);
    }

    /// <summary>
    /// 测试目的：生成描述应按属性名应用映射配置，并按配置 Profile、Provider 隔离缓存结果。
    /// </summary>
    [Fact]
    public void Resolve_WhenGeneratedMetadataUsesConfiguredProfilesAndProviders_ShouldMatchByPropertyNameAndIsolateCache()
    {
        var model = new EntityModelMetadata(
            typeof(MappedGeneratedEntity),
            "generated_mapping_table",
            "generated_mapping_schema",
            new[]
            {
                EntityPropertyMetadata.CreateGenerated(nameof(MappedGeneratedEntity.Id), typeof(long), true, true,
                    isKey: true, databaseGeneratedOption: System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity),
                EntityPropertyMetadata.CreateGenerated(nameof(MappedGeneratedEntity.DisplayName), typeof(string), true, true,
                    columnName: "generated_display_name", isRequired: true, maxLength: 80,
                    providerTypeName: "nvarchar(80)"),
                EntityPropertyMetadata.CreateGenerated(nameof(MappedGeneratedEntity.OptionalValue), typeof(int?), true, true),
                EntityPropertyMetadata.CreateGenerated(nameof(MappedGeneratedEntity.Ignored), typeof(string), true, true, isIgnored: true)
            });
        GeneratedEntityModelMetadataProvider.Register(model);

        var options = new SqlMetadataOptions();
        options.EntityMappings.Add(CreateMapping(DatabaseType.SqlServer, "read", "read_table", "read_name"));
        options.EntityMappings.Add(CreateMapping(DatabaseType.SqlServer, "write", "write_table", "write_name"));
        options.EntityMappings.Add(CreateMapping(DatabaseType.MySql, "read", "mysql_read_table", "mysql_read_name"));
        var resolver = new DefaultEntityMappingResolver(new GeneratedEntityModelMetadataProvider(), options: options);

        var sqlRead = resolver.Resolve(typeof(MappedGeneratedEntity), CreateContext(DatabaseType.SqlServer, "read"));
        var sqlReadAgain = resolver.Resolve(typeof(MappedGeneratedEntity), CreateContext(DatabaseType.SqlServer, "read"));
        var sqlWrite = resolver.Resolve(typeof(MappedGeneratedEntity), CreateContext(DatabaseType.SqlServer, "write"));
        var mySqlRead = resolver.Resolve(typeof(MappedGeneratedEntity), CreateContext(DatabaseType.MySql, "read"));

        Assert.Same(sqlRead, sqlReadAgain);
        Assert.NotSame(sqlRead, sqlWrite);
        Assert.NotSame(sqlRead, mySqlRead);
        Assert.NotSame(sqlWrite, mySqlRead);

        Assert.Same(model, sqlRead.Model);
        Assert.Equal("read", sqlRead.MappingProfile);
        Assert.Equal("analytics", sqlRead.Table.Database);
        Assert.Equal("reporting", sqlRead.Table.Schema);
        Assert.Equal("read_table", sqlRead.Table.TableName);
        Assert.Equal("read_name", sqlRead.Columns[nameof(MappedGeneratedEntity.DisplayName)].ColumnName);
        Assert.Equal(DbType.String, sqlRead.Columns[nameof(MappedGeneratedEntity.DisplayName)].DbType);
        Assert.Equal(32, sqlRead.Columns[nameof(MappedGeneratedEntity.DisplayName)].Size);
        Assert.Equal("varchar(32)", sqlRead.Columns[nameof(MappedGeneratedEntity.DisplayName)].ProviderTypeName);
        Assert.Equal(typeof(string), sqlRead.Columns[nameof(MappedGeneratedEntity.DisplayName)].ClrType);
        Assert.False(sqlRead.Columns[nameof(MappedGeneratedEntity.DisplayName)].IsNullable);
        Assert.True(sqlRead.Columns[nameof(MappedGeneratedEntity.Id)].IsKey);
        Assert.True(sqlRead.Columns[nameof(MappedGeneratedEntity.Id)].IsDatabaseGenerated);
        Assert.False(sqlRead.Columns[nameof(MappedGeneratedEntity.Id)].CanInsert);
        Assert.True(sqlRead.Columns[nameof(MappedGeneratedEntity.OptionalValue)].IsNullable);
        Assert.False(sqlRead.Columns.ContainsKey(nameof(MappedGeneratedEntity.Ignored)));

        Assert.Equal("write", sqlWrite.MappingProfile);
        Assert.Equal("write_table", sqlWrite.Table.TableName);
        Assert.Equal("write_name", sqlWrite.Columns[nameof(MappedGeneratedEntity.DisplayName)].ColumnName);
        Assert.Equal("mysql_read_table", mySqlRead.Table.TableName);
        Assert.Equal("mysql_read_name", mySqlRead.Columns[nameof(MappedGeneratedEntity.DisplayName)].ColumnName);

        var statistics = resolver.MappingCacheStatistics;
        Assert.Equal(1, statistics.CacheHitCount);
        Assert.Equal(3, statistics.CacheMissCount);
        Assert.Equal(3, statistics.EntryCount);
    }

    /// <summary>
    /// 创建指定数据库及配置档的实体映射。
    /// </summary>
    /// <param name="databaseType">目标数据库类型。</param>
    /// <param name="profile">实体映射配置档名称。</param>
    /// <param name="tableName">目标物理表名。</param>
    /// <param name="displayNameColumn">显示名称属性的映射列名。</param>
    /// <returns>包含测试表及属性列映射的实体配置。</returns>
    private static EntityMappingOptions CreateMapping(DatabaseType databaseType, string profile, string tableName,
        string displayNameColumn)
    {
        var mapping = new EntityMappingOptions
        {
            EntityType = typeof(MappedGeneratedEntity),
            DbKey = "reporting-db",
            DatabaseType = databaseType,
            MappingProfile = profile,
            TableRouteKey = "tenant-a",
            Database = "analytics",
            Schema = "reporting",
            TableName = tableName
        };
        mapping.Columns[nameof(MappedGeneratedEntity.DisplayName)] = new ColumnMappingOptions
        {
            PropertyName = nameof(MappedGeneratedEntity.DisplayName),
            ColumnName = displayNameColumn,
            DbType = DbType.String,
            Size = 32,
            ProviderTypeName = "varchar(32)"
        };
        return mapping;
    }

    /// <summary>
    /// 创建生成元数据测试的数据库上下文。
    /// </summary>
    /// <param name="databaseType">目标数据库类型。</param>
    /// <param name="profile">实体映射配置档名称。</param>
    /// <returns>包含指定数据源或映射信息的数据库上下文。</returns>
    private static DatabaseContext CreateContext(DatabaseType databaseType, string profile) => new()
    {
        DbKey = "reporting-db",
        TenantId = "tenant-a",
        MappingProfile = profile,
        DataSource = new SqlDataSourceDescriptor { DatabaseType = databaseType }
    };

    /// <summary>
    /// 提供实体属性元数据映射测试使用的查询数据模型。
    /// </summary>
    private sealed class DescriptorEntity
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 获取或设置结果展示名称。
        /// </summary>
        public string DisplayName { get; set; }
    }

    /// <summary>
    /// 提供实体属性元数据映射测试使用的查询数据模型。
    /// </summary>
    private sealed class MissingDescriptorEntity
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 提供实体属性元数据映射测试使用的查询数据模型。
    /// </summary>
    private sealed class MappedGeneratedEntity
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 获取或设置生成映射使用的显示名称。
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 获取或设置生成映射中可空的测试属性值。
        /// </summary>
        public int? OptionalValue { get; set; }

        /// <summary>
        /// 获取或设置配置为忽略生成映射的属性值。
        /// </summary>
        public string Ignored { get; set; }
    }
}
