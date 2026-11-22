using System.Data;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>生成式参数通过最终映射读取，字典容器无需反射。</summary>
public sealed class GeneratedParameterBinderTest
{
    [Fact]
    public void Bind_ShouldReadGeneratedModelAndApplyConfiguredConversionOnce()
    {
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Value), typeof(int), true, false,
                generatedGetter: source => ((Row)source).Value)
        });
        var models = new RegisteredEntityModelMetadataProvider(model);
        var resolver = new DefaultEntityMappingResolver(models);
        var factory = new Mock<ISqlParameterFactory>();
        factory.Setup(item => item.Create("Value", 7, It.IsAny<ColumnMappingMetadata>(),
                It.IsAny<DatabaseContext>(), typeof(Row), SqlParameterSource.RawSql))
            .Returns(new SqlParam("Value", "converted", DbType.String));
        var binder = new DefaultSqlParameterBinder(resolver, sqlParameterFactory: factory.Object,
            options: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly }, models: models);

        var parameters = Assert.IsAssignableFrom<IDapperParameterSet>(binder.Bind(new Row { Value = 7 }));

        Assert.Equal("converted", Assert.Single(parameters.Parameters).Value);
        factory.VerifyAll();
        factory.Verify(item => item.Create(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<ColumnMappingMetadata>(),
            It.IsAny<DatabaseContext>(), It.IsAny<Type>(), It.IsAny<SqlParameterSource>()), Times.Once);
        Assert.Throws<NotSupportedException>(() => binder.Bind(new { Value = 7 }));
    }

    [Fact]
    public void Bind_WhenCompatibleSourceIsUnregistered_ShouldNotScanAnnotations()
    {
        CountingTableAttribute.Calls = 0;
        var binder = new DefaultSqlParameterBinder(models: new CompositeEntityModelMetadataProvider());
        var source = new UnregisteredFilter { Value = 9 };

        Assert.Same(source, binder.Bind(source));
        Assert.Equal(0, CountingTableAttribute.Calls);
    }

    [Fact]
    public void Bind_WhenOnlyBaseModelIsRegistered_ShouldRejectDerivedSourceInGeneratedOnlyMode()
    {
        var model = new EntityModelMetadata(typeof(BaseFilter), "filters", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(BaseFilter.Value), typeof(int), true, false,
                generatedGetter: source => ((BaseFilter)source).Value)
        });
        var binder = new DefaultSqlParameterBinder(models: new RegisteredEntityModelMetadataProvider(model),
            options: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });

        Assert.Throws<NotSupportedException>(() => binder.Bind(new MiddleFilter { Value = 9, Other = 12 }));
    }

    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedPreferred)]
    public void Bind_WhenOnlyBaseModelIsRegistered_ShouldPreserveDerivedSourceForFallback(SqlExecutionMode mode)
    {
        var model = new EntityModelMetadata(typeof(BaseFilter), "filters", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(BaseFilter.Value), typeof(int), true, false,
                generatedGetter: source => ((BaseFilter)source).Value)
        });
        var binder = new DefaultSqlParameterBinder(models: new RegisteredEntityModelMetadataProvider(model),
            options: new SqlMetadataOptions { ExecutionMode = mode });
        var source = new MiddleFilter { Value = 9, Other = 12 };

        Assert.Same(source, binder.Bind(source));
    }

    [Fact]
    public void Bind_WhenConcreteDerivedModelIsRegistered_ShouldReadInheritedAndOwnParameters()
    {
        var model = new EntityModelMetadata(typeof(MiddleFilter), "filters", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(MiddleFilter.Value), typeof(int), true, false,
                generatedGetter: source => ((MiddleFilter)source).Value),
            EntityPropertyMetadata.CreateGenerated(nameof(MiddleFilter.Other), typeof(int), true, false,
                generatedGetter: source => ((MiddleFilter)source).Other)
        });
        var binder = new DefaultSqlParameterBinder(models: new RegisteredEntityModelMetadataProvider(model),
            options: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });

        var bound = Assert.IsAssignableFrom<IDapperParameterSet>(binder.Bind(new MiddleFilter { Value = 9, Other = 12 }));

        Assert.Equal(new[] { nameof(MiddleFilter.Value), nameof(MiddleFilter.Other) },
            bound.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { 9, 12 }, bound.Parameters.Select(parameter => parameter.Value));
    }

    [Fact]
    public void Bind_WhenOnlyGeneratedModelsAreProvided_ShouldUseTheSameModelForMapping()
    {
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Value), typeof(int), true, false,
                generatedGetter: source => ((Row)source).Value)
        });
        var models = new RegisteredEntityModelMetadataProvider(model);
        var factory = new Mock<ISqlParameterFactory>(MockBehavior.Strict);
        factory.Setup(item => item.Create(nameof(Row.Value), 11, It.IsAny<ColumnMappingMetadata>(),
                It.IsAny<DatabaseContext>(), typeof(Row), SqlParameterSource.RawSql))
            .Returns(new SqlParam(nameof(Row.Value), 11));
        var binder = new DefaultSqlParameterBinder(sqlParameterFactory: factory.Object, models: models,
            options: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });

        var bound = Assert.IsAssignableFrom<IDapperParameterSet>(binder.Bind(new Row { Value = 11 }));

        Assert.Equal(11, Assert.Single(bound.Parameters).Value);
        factory.VerifyAll();
    }

    [Fact]
    public void StrictDictionary_ShouldBindNamedValuesWithoutModelRegistration()
    {
        var binder = new DefaultSqlParameterBinder(options: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });
        var bound = Assert.IsAssignableFrom<IDapperParameterSet>(binder.Bind(new Dictionary<string, object>
            { ["Id"] = 7, ["Name"] = null }));

        Assert.IsAssignableFrom<ISqlStaticParameterSource>(bound);
        Assert.Equal(new[] { "Id", "Name" }, bound.Parameters.Select(item => item.Name));
        Assert.Equal(new object[] { 7, null }, bound.Parameters.Select(item => item.Value));
    }

    [Fact]
    public void StrictDictionary_GetSqlParams_ShouldExposeNamedValuesForDiagnostics()
    {
        var binder = new DefaultSqlParameterBinder(options: new SqlMetadataOptions
        {
            ExecutionMode = SqlExecutionMode.GeneratedOnly
        });

        var parameters = binder.GetSqlParams((object)new Dictionary<string, object>
        {
            ["Id"] = 7,
            ["Name"] = null
        }, null);

        Assert.Equal(new[] { "Id", "Name" }, parameters.Select(item => item.Name));
        Assert.Equal(new object[] { 7, null }, parameters.Select(item => item.Value));
    }

    [Fact]
    public void PreparedProviderValue_ShouldKeepConversionAcrossSnapshotsWithoutConvertingAgain()
    {
        var factory = new Mock<ISqlParameterFactory>(MockBehavior.Strict);
        var parameter = new SqlParam("payload", "serialized")
        {
            OriginalValue = new Row { Value = 7 }, IsProviderValue = true,
            EntityType = typeof(Row), PropertyName = nameof(Row.Value), Source = SqlParameterSource.SqlBuilder
        };
        var binder = new DefaultSqlParameterBinder(sqlParameterFactory: factory.Object);
        var snapshot = SqlQueryPlan.Create("select @payload", new[] { parameter }).Parameters;
        var bound = Assert.IsAssignableFrom<IDapperParameterSet>(binder.Bind(snapshot));
        var result = Assert.Single(bound.Parameters);
        Assert.Equal("serialized", result.Value);
        Assert.Same(parameter.OriginalValue, result.OriginalValue);
        Assert.True(result.IsProviderValue);
        factory.VerifyNoOtherCalls();
        result.Value = "changed";
        Assert.False(result.IsProviderValue);
    }

    public sealed class Row { public int Value { get; set; } }

    public class BaseFilter { public int Value { get; set; } }

    public sealed class DerivedFilter : BaseFilter { }

    public class MiddleFilter : BaseFilter { public int Other { get; set; } }

    [CountingTable]
    public sealed class UnregisteredFilter { public int Value { get; set; } }

    public sealed class CountingTableAttribute : TableAttribute
    {
        public CountingTableAttribute() : base("filter") => Calls++;
        public static int Calls { get; set; }
    }

    public sealed class DeepFilter : MiddleFilter { }

    public interface IFilter { int Value { get; } }

    public sealed class InterfaceFilter : IFilter { public int Value { get; set; } }

    [Fact]
    public void NativePostgreSqlArray_ShouldRemainStaticSingleParameter()
    {
        var values = new[] { 1, 2 };
        var binder = new DefaultSqlParameterBinder(options: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });
        var bound = binder.Bind(new[] { new SqlParam("ids", values) { IsProviderValue = true, DatabaseType = DatabaseType.PgSql } });
        Assert.IsAssignableFrom<ISqlStaticParameterSource>(bound);
        Assert.Same(values, Assert.Single(Assert.IsAssignableFrom<IDapperParameterSet>(bound).Parameters).Value);
    }

    [Fact]
    public void ConvertedCollection_ShouldBeClassifiedUsingProviderValue()
    {
        var binder = new DefaultSqlParameterBinder(options: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });
        var bound = binder.Bind(new[] { new SqlParam("ids", "[1,2]", DbType.String)
            { OriginalValue = new[] { 1, 2 }, IsProviderValue = true, StorageKind = ColumnStorageKind.Json } });
        Assert.IsAssignableFrom<ISqlStaticParameterSource>(bound);
        Assert.Equal("[1,2]", Assert.Single(Assert.IsAssignableFrom<IDapperParameterSet>(bound).Parameters).Value);
    }

    [Fact]
    public void RegisteredCollection_WithCustomConversion_ShouldConvertOnceBeforeCapabilityCheck()
    {
        var values = new[] { 1, 2 };
        var models = new RegisteredEntityModelMetadataProvider(new EntityModelMetadata(typeof(CollectionRow), "rows", null,
            new[] { EntityPropertyMetadata.CreateGenerated("Ids", typeof(int[]), true, false,
                generatedGetter: source => ((CollectionRow)source).Ids) }));
        var factory = new Mock<ISqlParameterFactory>(MockBehavior.Strict);
        factory.Setup(item => item.Create("Ids", values, It.IsAny<ColumnMappingMetadata>(),
                It.IsAny<DatabaseContext>(), typeof(CollectionRow), SqlParameterSource.RawSql))
            .Returns(new SqlParam("Ids", "[1,2]", DbType.String) { OriginalValue = values, IsProviderValue = true });
        var binder = new DefaultSqlParameterBinder(new DefaultEntityMappingResolver(models),
            sqlParameterFactory: factory.Object, models: models,
            options: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });

        var result = binder.Bind(new CollectionRow { Ids = values });

        Assert.IsAssignableFrom<ISqlStaticParameterSource>(result);
        Assert.Equal("[1,2]", Assert.Single(Assert.IsAssignableFrom<IDapperParameterSet>(result).Parameters).Value);
        factory.Verify(item => item.Create("Ids", values, It.IsAny<ColumnMappingMetadata>(),
            It.IsAny<DatabaseContext>(), typeof(CollectionRow), SqlParameterSource.RawSql), Times.Once);
        factory.VerifyNoOtherCalls();
    }

    public sealed class CollectionRow { public int[] Ids { get; set; } }
}
