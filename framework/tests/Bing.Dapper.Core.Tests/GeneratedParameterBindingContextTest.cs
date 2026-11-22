using System.Collections.Generic;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>验证生成对象与严格字典绑定使用单次解析的当前数据库上下文。</summary>
public sealed class GeneratedParameterBindingContextTest
{
    /// <summary>生成对象映射和参数工厂应收到同一个上下文，且绑定只解析一次。</summary>
    [Fact]
    public void GeneratedModel_ShouldResolveContextOnceAndShareItWithMappingAndFactory()
    {
        var contextA = new DatabaseContext { DbKey = "first" };
        var contextB = new DatabaseContext { DbKey = "second" };
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Value), typeof(int), true, false,
                generatedGetter: source => ((Row)source).Value)
        });
        var mapping = CreateMapping(model);
        var models = new RegisteredEntityModelMetadataProvider(model);
        var mappingResolver = new Mock<IEntityMappingResolver>(MockBehavior.Strict);
        mappingResolver.Setup(resolver => resolver.Resolve(typeof(Row), contextA)).Returns(mapping);
        var contextResolver = new Mock<ISqlDatabaseContextResolver>(MockBehavior.Strict);
        contextResolver.SetupSequence(resolver => resolver.Resolve(It.IsAny<SqlOptions>()))
            .Returns(contextA)
            .Returns(contextB);
        var factory = new Mock<ISqlParameterFactory>(MockBehavior.Strict);
        factory.Setup(item => item.Create(nameof(Row.Value), 7, mapping.Columns[nameof(Row.Value)], contextA,
                typeof(Row), SqlParameterSource.RawSql))
            .Returns(new SqlParam(nameof(Row.Value), 7));
        var binder = new DefaultSqlParameterBinder(mappingResolver.Object,
            sqlParameterFactory: factory.Object,
            options: StrictOptions(),
            databaseContextResolver: contextResolver.Object,
            models: models);

        var bound = Assert.IsAssignableFrom<IDapperParameterSet>(binder.Bind(new Row { Value = 7 }));
        var parameter = Assert.Single(bound.Parameters);

        Assert.Equal(nameof(Row.Value), parameter.Name);
        Assert.Equal(7, parameter.Value);
        mappingResolver.Verify(resolver => resolver.Resolve(typeof(Row), contextA), Times.Once);
        contextResolver.Verify(resolver => resolver.Resolve(It.IsAny<SqlOptions>()), Times.Once);
        mappingResolver.VerifyNoOtherCalls();
        contextResolver.VerifyNoOtherCalls();
        factory.VerifyAll();
        factory.VerifyNoOtherCalls();
    }

    /// <summary>严格字典的全部参数应共用单次解析得到的上下文。</summary>
    [Fact]
    public void StrictDictionary_ShouldResolveContextOnceForAllValues()
    {
        var contextA = new DatabaseContext { DbKey = "first" };
        var contextB = new DatabaseContext { DbKey = "second" };
        var contextResolver = new Mock<ISqlDatabaseContextResolver>(MockBehavior.Strict);
        contextResolver.SetupSequence(resolver => resolver.Resolve(It.IsAny<SqlOptions>()))
            .Returns(contextA)
            .Returns(contextB);
        var factory = new Mock<ISqlParameterFactory>(MockBehavior.Strict);
        factory.Setup(item => item.Create("first", (object)3, null, contextA, null, SqlParameterSource.RawSql))
            .Returns(new SqlParam("first", 3));
        factory.Setup(item => item.Create("second", null, null, contextA, null, SqlParameterSource.RawSql))
            .Returns(new SqlParam("second", null));
        var binder = new DefaultSqlParameterBinder(sqlParameterFactory: factory.Object,
            options: StrictOptions(), databaseContextResolver: contextResolver.Object);
        var source = new Dictionary<string, object>
        {
            ["first"] = 3,
            ["second"] = null
        };

        var bound = Assert.IsAssignableFrom<IDapperParameterSet>(binder.Bind((object)source));
        var first = Assert.Single(bound.Parameters, parameter => parameter.Name == "first");
        var second = Assert.Single(bound.Parameters, parameter => parameter.Name == "second");

        Assert.Equal(3, first.Value);
        Assert.Null(second.Value);
        contextResolver.Verify(resolver => resolver.Resolve(It.IsAny<SqlOptions>()), Times.Once);
        contextResolver.VerifyNoOtherCalls();
        factory.VerifyAll();
        factory.VerifyNoOtherCalls();
    }

    /// <summary>连续绑定应读取各自的新上下文和值，不缓存前一次绑定结果。</summary>
    [Fact]
    public void GeneratedModel_ShouldUseFreshContextAndValueForEachBind()
    {
        var contextA = new DatabaseContext { DbKey = "first" };
        var contextB = new DatabaseContext { DbKey = "second" };
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Value), typeof(int), true, false,
                generatedGetter: source => ((Row)source).Value)
        });
        var mapping = CreateMapping(model);
        var mappingResolver = new Mock<IEntityMappingResolver>(MockBehavior.Strict);
        mappingResolver.Setup(resolver => resolver.Resolve(typeof(Row), contextA)).Returns(mapping);
        mappingResolver.Setup(resolver => resolver.Resolve(typeof(Row), contextB)).Returns(mapping);
        var contextResolver = new Mock<ISqlDatabaseContextResolver>(MockBehavior.Strict);
        contextResolver.SetupSequence(resolver => resolver.Resolve(It.IsAny<SqlOptions>()))
            .Returns(contextA)
            .Returns(contextB);
        var factory = new Mock<ISqlParameterFactory>(MockBehavior.Strict);
        factory.Setup(item => item.Create(nameof(Row.Value), 7, mapping.Columns[nameof(Row.Value)], contextA,
                typeof(Row), SqlParameterSource.RawSql))
            .Returns(new SqlParam(nameof(Row.Value), 7));
        factory.Setup(item => item.Create(nameof(Row.Value), 11, mapping.Columns[nameof(Row.Value)], contextB,
                typeof(Row), SqlParameterSource.RawSql))
            .Returns(new SqlParam(nameof(Row.Value), 11));
        var binder = new DefaultSqlParameterBinder(mappingResolver.Object,
            sqlParameterFactory: factory.Object,
            options: StrictOptions(),
            databaseContextResolver: contextResolver.Object,
            models: new RegisteredEntityModelMetadataProvider(model));

        var first = Assert.Single(Assert.IsAssignableFrom<IDapperParameterSet>(
            binder.Bind(new Row { Value = 7 })).Parameters);
        var second = Assert.Single(Assert.IsAssignableFrom<IDapperParameterSet>(
            binder.Bind(new Row { Value = 11 })).Parameters);

        Assert.Equal(nameof(Row.Value), first.Name);
        Assert.Equal(7, first.Value);
        Assert.Equal(nameof(Row.Value), second.Name);
        Assert.Equal(11, second.Value);
        mappingResolver.Verify(resolver => resolver.Resolve(typeof(Row), contextA), Times.Once);
        mappingResolver.Verify(resolver => resolver.Resolve(typeof(Row), contextB), Times.Once);
        contextResolver.Verify(resolver => resolver.Resolve(It.IsAny<SqlOptions>()), Times.Exactly(2));
        mappingResolver.VerifyNoOtherCalls();
        contextResolver.VerifyNoOtherCalls();
        factory.VerifyAll();
        factory.VerifyNoOtherCalls();
    }

    /// <summary>忽略或不可读属性不应调用 getter，最终映射缺 getter 时应在读取任何属性前拒绝。</summary>
    [Fact]
    public void GeneratedModel_ShouldSkipIgnoredAndUnreadableGettersAndRejectIncompleteFinalMapping()
    {
        var context = new DatabaseContext { DbKey = "main" };
        var ignoredCalls = 0;
        var unreadableCalls = 0;
        var readableCalls = 0;
        var filteredModel = new EntityModelMetadata(typeof(MemberRow), "members", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(MemberRow.Value), typeof(int), true, false,
                generatedGetter: source =>
                {
                    readableCalls++;
                    return ((MemberRow)source).Value;
                }),
            EntityPropertyMetadata.CreateGenerated("Ignored", typeof(int), true, false,
                isIgnored: true, generatedGetter: _ =>
                {
                    ignoredCalls++;
                    throw new Xunit.Sdk.XunitException("Ignored getter was invoked.");
                }),
            EntityPropertyMetadata.CreateGenerated("Unreadable", typeof(int), false, false,
                generatedGetter: _ =>
                {
                    unreadableCalls++;
                    throw new Xunit.Sdk.XunitException("Unreadable getter was invoked.");
                })
        });
        var filteredMapping = CreateMapping(filteredModel);
        var filteredResolver = new Mock<IEntityMappingResolver>(MockBehavior.Strict);
        filteredResolver.Setup(resolver => resolver.Resolve(typeof(MemberRow), context)).Returns(filteredMapping);
        var filteredContextResolver = new Mock<ISqlDatabaseContextResolver>(MockBehavior.Strict);
        filteredContextResolver.Setup(resolver => resolver.Resolve(It.IsAny<SqlOptions>())).Returns(context);
        var filteredFactory = new Mock<ISqlParameterFactory>(MockBehavior.Strict);
        filteredFactory.Setup(item => item.Create(nameof(MemberRow.Value), 13,
                filteredMapping.Columns[nameof(MemberRow.Value)], context, typeof(MemberRow), SqlParameterSource.RawSql))
            .Returns(new SqlParam(nameof(MemberRow.Value), 13));
        var filteredBinder = new DefaultSqlParameterBinder(filteredResolver.Object,
            sqlParameterFactory: filteredFactory.Object,
            options: StrictOptions(),
            databaseContextResolver: filteredContextResolver.Object,
            models: new RegisteredEntityModelMetadataProvider(filteredModel));

        var filteredParameters = Assert.IsAssignableFrom<IDapperParameterSet>(
            filteredBinder.Bind(new MemberRow { Value = 13 }));
        var filteredParameter = Assert.Single(filteredParameters.Parameters);

        Assert.Equal(nameof(MemberRow.Value), filteredParameter.Name);
        Assert.Equal(13, filteredParameter.Value);
        Assert.Equal(1, readableCalls);
        Assert.Equal(0, ignoredCalls);
        Assert.Equal(0, unreadableCalls);
        filteredResolver.Verify(resolver => resolver.Resolve(typeof(MemberRow), context), Times.Once);
        filteredContextResolver.Verify(resolver => resolver.Resolve(It.IsAny<SqlOptions>()), Times.Once);
        filteredResolver.VerifyNoOtherCalls();
        filteredContextResolver.VerifyNoOtherCalls();
        filteredFactory.VerifyAll();
        filteredFactory.VerifyNoOtherCalls();

        var earlyGetterCalls = 0;
        var registeredModel = new EntityModelMetadata(typeof(IncompleteRow), "incomplete", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated("First", typeof(int), true, false,
                generatedGetter: _ => 1),
            EntityPropertyMetadata.CreateGenerated("Second", typeof(int), true, false,
                generatedGetter: _ => 2)
        });
        var finalMappingModel = new EntityModelMetadata(typeof(IncompleteRow), "incomplete", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated("First", typeof(int), true, false,
                generatedGetter: _ =>
                {
                    earlyGetterCalls++;
                    return 1;
                }),
            EntityPropertyMetadata.CreateGenerated("Second", typeof(int), true, false)
        });
        var incompleteMapping = CreateMapping(finalMappingModel);
        var incompleteResolver = new Mock<IEntityMappingResolver>(MockBehavior.Strict);
        incompleteResolver.Setup(resolver => resolver.Resolve(typeof(IncompleteRow), context)).Returns(incompleteMapping);
        var incompleteContextResolver = new Mock<ISqlDatabaseContextResolver>(MockBehavior.Strict);
        incompleteContextResolver.Setup(resolver => resolver.Resolve(It.IsAny<SqlOptions>())).Returns(context);
        var incompleteFactory = new Mock<ISqlParameterFactory>(MockBehavior.Strict);
        var incompleteBinder = new DefaultSqlParameterBinder(incompleteResolver.Object,
            sqlParameterFactory: incompleteFactory.Object,
            options: StrictOptions(),
            databaseContextResolver: incompleteContextResolver.Object,
            models: new RegisteredEntityModelMetadataProvider(registeredModel));

        Assert.Throws<System.NotSupportedException>(() => incompleteBinder.Bind(new IncompleteRow()));

        Assert.Equal(0, earlyGetterCalls);
        incompleteResolver.Verify(resolver => resolver.Resolve(typeof(IncompleteRow), context), Times.Once);
        incompleteContextResolver.Verify(resolver => resolver.Resolve(It.IsAny<SqlOptions>()), Times.Once);
        incompleteResolver.VerifyNoOtherCalls();
        incompleteContextResolver.VerifyNoOtherCalls();
        incompleteFactory.VerifyNoOtherCalls();
    }

    /// <summary>NULL 和空字典应保持原有行为，空字典不解析上下文。</summary>
    [Fact]
    public void Bind_NullAndEmptyDictionary_ShouldKeepExistingBehaviorWithoutContextResolution()
    {
        var contextResolver = new Mock<ISqlDatabaseContextResolver>(MockBehavior.Strict);
        var factory = new Mock<ISqlParameterFactory>(MockBehavior.Strict);
        var binder = new DefaultSqlParameterBinder(sqlParameterFactory: factory.Object,
            options: StrictOptions(), databaseContextResolver: contextResolver.Object);

        Assert.Null(binder.Bind((object)null));
        var bound = Assert.IsAssignableFrom<IDapperParameterSet>(
            binder.Bind((object)new Dictionary<string, object>()));

        Assert.IsAssignableFrom<ISqlStaticParameterSource>(bound);
        Assert.Empty(bound.Parameters);
        contextResolver.VerifyNoOtherCalls();
        factory.VerifyNoOtherCalls();
    }

    private static EntityMappingMetadata CreateMapping(EntityModelMetadata model) => new()
    {
        EntityType = model.EntityType,
        Model = model,
        Columns = model.Properties.Values.ToDictionary(property => property.PropertyName,
            property => new ColumnMappingMetadata
            {
                PropertyName = property.PropertyName,
                ColumnName = property.ColumnName,
                ClrType = property.ClrType
            })
    };

    private static SqlMetadataOptions StrictOptions() =>
        new() { ExecutionMode = SqlExecutionMode.GeneratedOnly };

    public sealed class Row
    {
        public int Value { get; set; }
    }

    public sealed class MemberRow
    {
        public int Value { get; set; }
    }

    public sealed class IncompleteRow { }
}
