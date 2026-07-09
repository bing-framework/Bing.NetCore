using System.Data;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Shouldly;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 测试目的：验证元数据增强参数在 Builder 与 Where Lambda 链路中被正确生成。
/// </summary>
public class MetadataAwareParameterTest
{
    /// <summary>
    /// 测试目的：通过属性表达式手工添加参数时，应生成带完整列元数据的 SqlParam。
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
    /// 测试目的：旧参数入口仍应可导出增强参数，但元数据等级应保持弱类型，避免误判为完整映射。
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
    /// 测试目的：Where 的实体 Lambda 链路应保留列元数据，并继续保持原有 SQL 与参数值行为。
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
}
