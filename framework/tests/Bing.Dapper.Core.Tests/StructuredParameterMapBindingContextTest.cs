using System.Collections.Generic;
using System.Data;
using System.Linq;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 验证结构化参数映射在一次绑定中复用同一个数据库上下文。
/// </summary>
public sealed class StructuredParameterMapBindingContextTest
{
    /// <summary>
    /// 两个映射参数应共享一次上下文解析，并保留完整的参数元数据和值。
    /// </summary>
    [Fact]
    public void Bind_ParameterMap_ShouldResolveContextOnceAndShareItWithMappingAndFactory()
    {
        var context = new DatabaseContext { DbKey = "main" };
        var laterContext = new DatabaseContext { DbKey = "later" };
        var contextCalls = 0;
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.First), typeof(int), true, true),
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Second), typeof(string), true, true)
        });
        var mapping = new EntityMappingMetadata
        {
            EntityType = typeof(Row),
            Model = model,
            Table = new SqlTableReference { TableName = "rows" },
            Columns = new Dictionary<string, ColumnMappingMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                [nameof(Row.First)] = new ColumnMappingMetadata
                {
                    PropertyName = nameof(Row.First),
                    ColumnName = "first_value",
                    ClrType = typeof(int),
                    DbType = DbType.Int32,
                    Size = 4
                },
                [nameof(Row.Second)] = new ColumnMappingMetadata
                {
                    PropertyName = nameof(Row.Second),
                    ColumnName = "second_value",
                    ClrType = typeof(string),
                    DbType = DbType.String,
                    Size = 32
                }
            }
        };
        var mappingContexts = new List<DatabaseContext>();
        var factoryContexts = new List<DatabaseContext>();
        var mappingResolver = new Mock<IEntityMappingResolver>(MockBehavior.Strict);
        mappingResolver
            .Setup(resolver => resolver.Resolve(typeof(Row), It.IsAny<DatabaseContext>()))
            .Callback<Type, DatabaseContext>((_, resolvedContext) => mappingContexts.Add(resolvedContext))
            .Returns(mapping);
        var contextResolver = new Mock<ISqlDatabaseContextResolver>(MockBehavior.Strict);
        contextResolver
            .Setup(resolver => resolver.Resolve(It.IsAny<SqlOptions>()))
            .Returns(() =>
            {
                contextCalls++;
                return contextCalls == 1 ? context : laterContext;
            });
        var factory = new Mock<ISqlParameterFactory>(MockBehavior.Strict);
        factory
            .Setup(item => item.Create(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<ColumnMappingMetadata>(),
                It.IsAny<DatabaseContext>(), typeof(Row), SqlParameterSource.RawSql))
            .Callback<string, object, ColumnMappingMetadata, DatabaseContext, System.Type, SqlParameterSource>(
                (_, _, _, resolvedContext, _, _) => factoryContexts.Add(resolvedContext))
            .Returns((string name, object value, ColumnMappingMetadata column, DatabaseContext _, System.Type entityType,
                SqlParameterSource source) => new SqlParam(name, value, column.DbType, ParameterDirection.Input,
                column.Size, column.Precision, column.Scale)
            {
                EntityType = entityType,
                PropertyName = column.PropertyName,
                ColumnName = column.ColumnName,
                Source = source,
                MetadataLevel = SqlParameterMetadataLevel.Full
            });
        var binder = new DefaultSqlParameterBinder(mappingResolver.Object,
            sqlParameterFactory: factory.Object,
            options: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
            databaseContextResolver: contextResolver.Object);
        var map = new SqlParameterMap<Row>()
            .Add("first", row => row.First)
            .Add("second", row => row.Second)
            .UseSource(new Row { First = 7, Second = "seven" });

        var bound = Assert.IsAssignableFrom<IDapperParameterSet>(binder.Bind(map));
        var parameters = bound.Parameters.ToArray();

        Assert.Equal(1, contextCalls);
        Assert.Equal(2, mappingContexts.Count);
        Assert.Equal(2, factoryContexts.Count);
        Assert.All(mappingContexts, resolvedContext => Assert.Same(context, resolvedContext));
        Assert.All(factoryContexts, resolvedContext => Assert.Same(context, resolvedContext));
        Assert.Collection(parameters,
            first =>
            {
                Assert.Equal("first", first.Name);
                Assert.Equal(7, first.Value);
                Assert.Equal(DbType.Int32, first.DbType);
                Assert.Equal(ParameterDirection.Input, first.Direction);
                Assert.Equal(4, first.Size);
                Assert.Null(first.Precision);
                Assert.Null(first.Scale);
                Assert.Equal(nameof(Row.First), first.PropertyName);
                Assert.Equal("first_value", first.ColumnName);
                Assert.Equal(typeof(Row), first.EntityType);
                Assert.Equal(SqlParameterSource.RawSql, first.Source);
                Assert.Equal(SqlParameterMetadataLevel.Full, first.MetadataLevel);
            },
            second =>
            {
                Assert.Equal("second", second.Name);
                Assert.Equal("seven", second.Value);
                Assert.Equal(DbType.String, second.DbType);
                Assert.Equal(ParameterDirection.Input, second.Direction);
                Assert.Equal(32, second.Size);
                Assert.Null(second.Precision);
                Assert.Null(second.Scale);
                Assert.Equal(nameof(Row.Second), second.PropertyName);
                Assert.Equal("second_value", second.ColumnName);
                Assert.Equal(typeof(Row), second.EntityType);
                Assert.Equal(SqlParameterSource.RawSql, second.Source);
                Assert.Equal(SqlParameterMetadataLevel.Full, second.MetadataLevel);
            });
        mappingResolver.Verify(resolver => resolver.Resolve(typeof(Row), It.IsAny<DatabaseContext>()), Times.Exactly(2));
        factory.Verify(item => item.Create(It.IsAny<string>(), It.IsAny<object>(),
            It.IsAny<ColumnMappingMetadata>(), It.IsAny<DatabaseContext>(), typeof(Row), SqlParameterSource.RawSql),
            Times.Exactly(2));
        contextResolver.Verify(resolver => resolver.Resolve(It.IsAny<SqlOptions>()), Times.Once);
    }

    private sealed class Row
    {
        public int First { get; set; }

        public string Second { get; set; }
    }
}
