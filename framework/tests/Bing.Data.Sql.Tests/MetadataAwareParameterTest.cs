using System.Data;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Shouldly;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 元数据增强参数测试
/// </summary>
public class MetadataAwareParameterTest
{
    /// <summary>
    /// 测试 - 通过实体属性表达式手工添加参数时应生成完整元数据参数。
    /// </summary>
    [Fact]
    public void AddParam_WithEntityProperty_ShouldCreateFullMetadataParameter()
    {
        // Arrange
        var metadata = new TestEntityMetadata();
        var builder = new TestSqlBuilder(TestDialect.Instance, metadata);

        // Act
        builder.AddParam("stringValue", (Sample t) => t.StringValue, "abc");
        var parameter = builder.GetSqlParams().Single().Value;

        // Assert
        parameter.EntityType.ShouldBe(typeof(Sample));
        parameter.PropertyName.ShouldBe(nameof(Sample.StringValue));
        parameter.ColumnName.ShouldBe("Sample_StringValue");
        parameter.DbType.ShouldBe(DbType.String);
        parameter.Size.ShouldBe(20);
        parameter.MetadataLevel.ShouldBe(SqlParameterMetadataLevel.Full);
        parameter.Source.ShouldBe(SqlParameterSource.Manual);
        parameter.Value.ShouldBe("abc");
    }

    /// <summary>
    /// 测试 - 旧参数入口导出的增强参数应保持弱元数据等级。
    /// </summary>
    [Fact]
    public void GetSqlParams_WithLegacyParameter_ShouldReturnWeakMetadata()
    {
        // Arrange
        var builder = new TestSqlBuilder(TestDialect.Instance);

        // Act
        builder.AddParam("name", "abc");
        var parameter = builder.GetSqlParams().Single().Value;

        // Assert
        parameter.MetadataLevel.ShouldBe(SqlParameterMetadataLevel.Weak);
        parameter.Source.ShouldBe(SqlParameterSource.Legacy);
        parameter.ColumnName.ShouldBeNull();
        parameter.PropertyName.ShouldBeNull();
        parameter.Value.ShouldBe("abc");
    }

    /// <summary>
    /// 测试 - Where 的实体 Lambda 链路应保留列元数据并保持原有 SQL 行为。
    /// </summary>
    [Fact]
    public void Where_WithEntityLambda_ShouldCreateFullMetadataParameter()
    {
        // Arrange
        var metadata = new TestEntityMetadata();
        var parameterManager = new ParameterManager(TestDialect.Instance);
        var clause = new WhereClause(
            new TestSqlBuilder(TestDialect.Instance, metadata),
            TestDialect.Instance,
            new EntityResolver(metadata),
            new EntityAliasRegister(),
            parameterManager,
            null,
            new DefaultEntityMappingResolver(metadata),
            null,
            new DefaultSqlParameterFactory(new DefaultFieldValueConverterSelector()));

        // Act
        clause.Where<Sample>(t => t.StringValue, "abc");
        var parameter = parameterManager.GetSqlParams().Single().Value;

        // Assert
        clause.ToSql().ShouldBe("Where [Sample_StringValue]=@_p_0");
        parameter.EntityType.ShouldBe(typeof(Sample));
        parameter.PropertyName.ShouldBe(nameof(Sample.StringValue));
        parameter.ColumnName.ShouldBe("Sample_StringValue");
        parameter.DbType.ShouldBe(DbType.String);
        parameter.Size.ShouldBe(20);
        parameter.MetadataLevel.ShouldBe(SqlParameterMetadataLevel.Full);
        parameter.Source.ShouldBe(SqlParameterSource.Lambda);
        parameter.Value.ShouldBe("abc");
    }

    /// <summary>
    /// 测试 - SQL 拼接增强参数应允许参数名与实体属性名不同。
    /// </summary>
    [Fact]
    public void SqlBuilder_AddParamWithEntityProperty_ShouldCreateFullMetadataParam()
    {
        // Arrange
        var metadata = new TestEntityMetadata();
        var builder = new TestSqlBuilder(TestDialect.Instance, metadata);

        // Act
        builder.AddParam("p_status", (Sample t) => t.StringValue, "abc");
        var parameter = builder.GetSqlParams().Single().Value;

        // Assert
        parameter.Name.ShouldBe("@p_status");
        parameter.PropertyName.ShouldBe(nameof(Sample.StringValue));
        parameter.ColumnName.ShouldBe("Sample_StringValue");
        parameter.MetadataLevel.ShouldBe(SqlParameterMetadataLevel.Full);
        parameter.Source.ShouldBe(SqlParameterSource.Manual);
    }

    /// <summary>
    /// 测试 - 原生 SQL 参数 Add 显式传入 null 时应保留显式空值语义。
    /// </summary>
    [Fact]
    public void SqlParameterMap_AddExplicitNull_ShouldMarkValueResolved()
    {
        // Arrange
        var map = new SqlParameterMap<Sample>()
            .UseSource(new { name = "source" });

        // Act
        map.Add("name", t => t.StringValue, null);
        var item = map.GetItems().Single();

        // Assert
        item.HasExplicitValue.ShouldBeTrue();
        item.ValueResolved.ShouldBeTrue();
        item.Value.ShouldBeNull();
    }

    /// <summary>
    /// 测试 - 原生 SQL 参数 Map 应继续从参数源对象解析值。
    /// </summary>
    [Fact]
    public void SqlParameterMap_Map_ShouldReadValueFromSource()
    {
        // Arrange
        var map = new SqlParameterMap<Sample>()
            .UseSource(new { name = "source" });

        // Act
        map.Map("name", t => t.StringValue);
        var item = map.GetItems().Single();

        // Assert
        item.HasExplicitValue.ShouldBeFalse();
        item.ValueResolved.ShouldBeTrue();
        item.Value.ShouldBe("source");
    }

    /// <summary>
    /// 测试 - 参数名称规范化器应移除各 Provider 的参数前缀。
    /// </summary>
    [Fact]
    public void SqlParameterNameNormalizer_WithProviderPrefixes_ShouldReturnCanonicalName()
    {
        // Arrange
        var normalizer = new DefaultSqlParameterNameNormalizer();

        // Act
        var names = new[] { normalizer.Normalize("@name"), normalizer.Normalize(":name"), normalizer.Normalize("?name") };

        // Assert
        names.ShouldAllBe(t => t == "name");
    }

    /// <summary>
    /// 测试 - 显式 null 应覆盖参数源中的同名值，且保留已提供参数值的语义。
    /// </summary>
    [Fact]
    public void SqlParameterResolver_WithExplicitNull_ShouldOverrideSourceValue()
    {
        // Arrange
        var map = new SqlParameterMap<Sample>()
            .UseSource(new { name = "source" })
            .Add("@name", t => t.StringValue, null);
        var resolver = new DefaultSqlParameterResolver();

        // Act
        var result = resolver.Resolve(new SqlParameterBindingContext { Sql = "select @name", Source = map });
        var item = result.Items.Single();

        // Assert
        item.Name.ShouldBe("name");
        item.HasValue.ShouldBeTrue();
        item.IsExplicitNull.ShouldBeTrue();
        item.Value.ShouldBeNull();
        item.OriginalValue.ShouldBe("source");
    }

    /// <summary>
    /// 测试 - 输入参数映射无法解析时应抛出包含 SQL、数据源和属性信息的异常。
    /// </summary>
    [Fact]
    public void SqlParameterResolver_WhenInputValueMissing_ShouldThrowBindingException()
    {
        // Arrange
        var map = new SqlParameterMap<Sample>().Map("name", t => t.StringValue);
        var resolver = new DefaultSqlParameterResolver();

        // Act
        var exception = Should.Throw<SqlParameterBindingException>(() => resolver.Resolve(new SqlParameterBindingContext
        {
            Sql = "select @name",
            DbKey = "master",
            Source = map,
            EntityType = typeof(Sample)
        }));

        // Assert
        exception.ParameterName.ShouldBe("name");
        exception.Sql.ShouldBe("select @name");
        exception.DbKey.ShouldBe("master");
        exception.PropertyName.ShouldBe(nameof(Sample.StringValue));
    }
}
