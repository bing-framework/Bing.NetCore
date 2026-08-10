using System.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// 独立 SQL 查询描述单元测试。
/// </summary>
public class SqlQueryDescriptionTest
{
    /// <summary>
    /// 测试目的：同一根查询创建的 Fluent 描述应分别持有 Builder、参数和 SQL 状态。
    /// </summary>
    [Fact]
    public void Sql_WhenMultipleDescriptionsCreated_ShouldKeepBuilderStateIsolated()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create<ISqlQuery>("sqlite");

        // Act
        var first = rootQuery.Sql<int>().Select("Id").From("users").Where("Id", 1);
        var second = rootQuery.Sql<string>().Select("Name").From("users").Where("Name", "Bing");

        // Assert
        Assert.NotEqual(first.ToSql(), second.ToSql());
        Assert.Single(first.GetParams());
        Assert.Single(second.GetParams());
        Assert.Equal(1, first.GetParams().Values.Single());
        Assert.Equal("Bing", second.GetParams().Values.Single());
        Assert.Contains("Id", first.ToSql(), StringComparison.Ordinal);
        Assert.Contains("Name", second.ToSql(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试目的：原生文本查询应保留 SQL 文本，并在创建描述时隔离可变字典参数。
    /// </summary>
    [Fact]
    public void Sql_WhenRawTextDictionaryProvided_ShouldSnapshotParameters()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create<ISqlQuery>("sqlite");
        var parameters = new Dictionary<string, object> { ["Id"] = 1 };

        // Act
        var query = rootQuery.Sql<int>("Select * From users Where Id = @Id", parameters);
        parameters["Id"] = 2;

        // Assert
        Assert.Equal("Select * From users Where Id = @Id", query.CommandText);
        Assert.NotSame(parameters, query.Parameters);
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(query.Parameters)["Id"]);
    }

    /// <summary>
    /// 测试目的：字符串、标识符和注释中的参数样式不应导致插值参数错误改名。
    /// </summary>
    [Fact]
    public void SqlInterpolated_WhenTokenAppearsOnlyInProtectedSqlContexts_ShouldKeepDefaultParameterName()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create<ISqlQuery>("sqlite");

        // Act
        var description = rootQuery.SqlInterpolated<string>(
            $"Select '@p0', \"@p0\", `@p0`, [@p0] Where Name = {"Bing"} -- @p0\n/* @p0 */");
        var parameters = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(description.Parameters);

        // Assert
        Assert.Contains("Where Name = @p0", description.CommandText, StringComparison.Ordinal);
        Assert.True(parameters.ContainsKey("p0"));
        Assert.Equal("Bing", parameters["p0"]);
    }

    /// <summary>
    /// 测试目的：插值 SQL 尚未定义集合展开语义时，应在创建描述前明确拒绝集合参数。
    /// </summary>
    [Fact]
    public void SqlInterpolated_WhenArgumentIsCollection_ShouldRejectBeforeDescriptionCreation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create<ISqlQuery>("sqlite");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => rootQuery.SqlInterpolated<int>(
            $"Select {new[] { 1, 2, 3 }}"));

        // Assert
        Assert.Equal("插值 SQL 暂不支持集合参数，请使用显式参数化查询。", exception.Message);
    }

    /// <summary>
    /// 测试目的：独立 Fluent 查询的类型化参数应解析自身 Builder 的实体列映射，而不是退化为弱元数据。
    /// </summary>
    [Fact]
    public void SqlQuery_AddParamWithEntityProperty_ShouldCreateFullMetadataParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(QueryParameterSample),
            DbKey = "sqlite",
            TableName = "users",
            Columns =
            {
                [nameof(QueryParameterSample.Status)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(QueryParameterSample.Status),
                    ColumnName = "status_code",
                    DbType = DbType.String,
                    Size = 32
                }
            }
        }));
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create<ISqlQuery>("sqlite");

        // Act
        var query = rootQuery.Sql<QueryParameterSample>().AddParam("status", (QueryParameterSample item) => item.Status, "active");
        var parameter = query.GetSqlParams().Single().Value;

        // Assert
        Assert.Equal("@status", parameter.Name);
        Assert.Equal(typeof(QueryParameterSample), parameter.EntityType);
        Assert.Equal(nameof(QueryParameterSample.Status), parameter.PropertyName);
        Assert.Equal("status_code", parameter.ColumnName);
        Assert.Equal(DbType.String, parameter.DbType);
        Assert.Equal(32, parameter.Size);
        Assert.Equal(SqlParameterMetadataLevel.Full, parameter.MetadataLevel);
        Assert.Equal(SqlParameterSource.Manual, parameter.Source);
        Assert.Equal("active", parameter.Value);
    }

    /// <summary>
    /// 查询描述参数映射样例。
    /// </summary>
    private sealed class QueryParameterSample
    {
        /// <summary>
        /// 状态。
        /// </summary>
        public string Status { get; set; }
    }
}