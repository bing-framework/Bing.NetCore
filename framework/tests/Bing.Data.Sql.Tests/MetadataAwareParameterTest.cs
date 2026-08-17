using System.Data;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Shouldly;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 元数据增强参数测试
/// </summary>
public class MetadataAwareParameterTest
{
    /// <summary>
    /// 测试目的：默认参数解析实现仅服务框架内部，公开程序集只能暴露对应的可替换接口。
    /// </summary>
    [Fact]
    public void DefaultParameterResolvers_ShouldNotBePublic()
    {
        // Arrange
        var exportedTypeNames = typeof(ISqlParameterResolver).Assembly.GetExportedTypes()
            .Select(type => type.Name);

        // Assert
        Assert.DoesNotContain("DefaultSqlParameterNameNormalizer", exportedTypeNames);
        Assert.DoesNotContain("DefaultSqlParameterSourceResolver", exportedTypeNames);
        Assert.DoesNotContain("DefaultSqlParameterResolver", exportedTypeNames);
        Assert.Contains("ISqlParameterNameNormalizer", exportedTypeNames);
        Assert.Contains("ISqlParameterSourceResolver", exportedTypeNames);
        Assert.Contains("ISqlParameterResolver", exportedTypeNames);
    }

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
    /// 测试目的：基础参数入口导出的增强参数应保持弱元数据等级。
    /// </summary>
    [Fact]
    public void GetSqlParams_WithBasicParameter_ShouldReturnWeakMetadata()
    {
        // Arrange
        var builder = new TestSqlBuilder(TestDialect.Instance);

        // Act
        builder.AddParam("name", "abc");
        var parameter = builder.GetSqlParams().Single().Value;

        // Assert
        parameter.MetadataLevel.ShouldBe(SqlParameterMetadataLevel.Weak);
        parameter.Source.ShouldBe(SqlParameterSource.Basic);
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
        var clause = new WhereClause(TestSqlBuilder.CreateTestClauseContext(
            entityResolver: new EntityResolver(metadata), aliasRegister: new EntityAliasRegister(),
            parameterManager: parameterManager, builder: new TestSqlBuilder(TestDialect.Instance, metadata),
            entityMappingResolver: new DefaultEntityMappingResolver(metadata),
            parameterFactory: new DefaultSqlParameterFactory(new DefaultFieldValueConverterSelector())));

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
    /// 测试目的：预取消的原生 SQL 映射调用不得创建参数映射、执行调用方回调或访问执行器。
    /// </summary>
    [Fact]
    public async Task ExecuteSqlAsync_WhenCancellationRequested_ShouldNotInvokeParameterMap()
    {
        // Arrange
        var executor = new Mock<ISqlExecutor>();
        var mapCalled = false;
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.Object.ExecuteSqlAsync<Sample>(
            "Update samples Set Name=@name", new { name = "Bing" }, _ => mapCalled = true,
            cancellationToken: cancellationTokenSource.Token));

        // Assert
        Assert.False(mapCalled);
        executor.Verify(item => item.ExecuteSqlAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
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

    /// <summary>
    /// 测试 - 框架参数集合应按标准名称替换参数并保留输出参数元数据。
    /// </summary>
    [Fact]
    public void SqlParameterCollection_ShouldNormalizeNamesAndPreserveOutputMetadata()
    {
        // Arrange
        var parameters = new SqlParameterCollection()
            .Add("@name", "first", DbType.String, 10)
            .Add(":name", "second", DbType.String, 20)
            .AddOutput("?result", DbType.Int32);

        // Act
        var result = new DefaultSqlParameterResolver().Resolve(new SqlParameterBindingContext
        {
            Source = parameters
        });

        // Assert
        parameters.Count.ShouldBe(2);
        result.Items.Count.ShouldBe(2);
        result.Items.Single(t => t.Name == "name").Value.ShouldBe("second");
        var output = result.Items.Single(t => t.Name == "result").Metadata;
        output.Direction.ShouldBe(ParameterDirection.Output);
        output.DbType.ShouldBe(DbType.Int32);
    }
}
