using System.Data;
using System.Reflection;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var first = rootQuery.Query().Select("Id").From("users").Where("Id", 1);
        var second = rootQuery.Query().Select("Name").From("users").Where("Name", "Bing");

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
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var parameters = new Dictionary<string, object> { ["Id"] = 1 };

        // Act
        var query = rootQuery.Sql("Select * From users Where Id = @Id", parameters);
        parameters["Id"] = 2;

        // Assert
        Assert.Equal("Select * From users Where Id = @Id", query.CommandText);
        Assert.NotSame(parameters, query.Parameters);
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(query.Parameters)["Id"]);
    }

    /// <summary>
    /// 测试目的：文本描述必须冻结嵌套字典、集合和数组；调用方或公开参数副本的后续修改不得污染执行输入。
    /// </summary>
    [Fact]
    public void Text_WhenNestedParameterContainersChange_ShouldKeepIndependentSnapshots()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var payload = new byte[] { 1, 2 };
        var identifiers = new List<int> { 3, 4 };
        var parameters = new Dictionary<string, object>
        {
            ["filter"] = new Dictionary<string, object>
            {
                ["Payload"] = payload,
                ["Identifiers"] = identifiers
            }
        };

        // Act
        var text = rootQuery.Sql("Select 1", parameters);
        payload[0] = 9;
        identifiers[0] = 8;
        ((IDictionary<string, object>)parameters["filter"])["Payload"] = new byte[] { 7 };
        var exposed = Assert.IsAssignableFrom<IDictionary<string, object>>(text.Parameters);
        exposed["filter"] = "changed";

        // Assert
        var textFilter = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(text.Parameters)["filter"]);
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(textFilter["Payload"]));
        Assert.Equal(new object[] { 3, 4 }, Assert.IsType<object[]>(textFilter["Identifiers"]));
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
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var description = rootQuery.SqlInterpolated(
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
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => rootQuery.SqlInterpolated(
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
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var query = rootQuery.Query().AddParam("status", (QueryParameterSample item) => item.Status, "active");
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
    /// 测试目的：双表根查询应按参数位置绑定相同或不同实体来源，并生成完整的 SQLite 投影与比较 SQL。
    /// </summary>
    [Fact]
    public void From_WhenTwoMappedEntitiesProvided_ShouldRenderBoundSourcesProjectionAndPredicate()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var query = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Select<MultiSourceUser, MultiSourceReview>((user, review) => new object[] { user.Id, review.UserId })
            .Where<MultiSourceUser, MultiSourceReview>((user, review) => user.Id == review.UserId);

        // Assert
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId` \r\nFrom `users`, `reviews` \r\nWhere `users`.`Id`=`reviews`.`UserId`", query.ToSql());
    }

    /// <summary>
    /// 测试目的：条件组应保持嵌套 And/Or 优先级，并在成功提交后按稳定顺序绑定参数。
    /// </summary>
    [Fact]
    public void WhereGroup_WhenNestedConditionsProvided_ShouldRenderGroupedSqlAndParameters()
    {
        // Arrange
        var rootQuery = CreateMultiSourceQuery();

        // Act
        var query = rootQuery.From<MultiSourceUser>()
            .Select<MultiSourceUser>(user => new object[] { user.Id })
            .WhereGroup(group =>
            {
                group.And<MultiSourceUser>(user => user.Id > 1);
                group.Or<MultiSourceUser>(user => user.Id == 2);
                group.AndGroup(nested => nested.And<MultiSourceUser>(user => user.Id < 10));
            });

        // Assert
        Assert.Equal("Select `users`.`Id` \r\nFrom `users` \r\nWhere (`users`.`Id`>@_p_0 Or `users`.`Id`=@_p_1) And `users`.`Id`<@_p_2", query.ToSql());
        Assert.Equal(new[] { "@_p_0", "@_p_1", "@_p_2" }, query.GetParams().Keys.ToArray());
        Assert.Equal(new object[] { 1, 2, 10 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：重复渲染应复用同一查询形状，条件为 false 时不得使 SQL 形状发生变化。
    /// </summary>
    [Fact]
    public void ToSql_WhenRenderedRepeatedly_ShouldKeepStableShapeAfterFalseCondition()
    {
        // Arrange
        var query = CreateMultiSourceQuery().From<MultiSourceUser>()
            .Select<MultiSourceUser>(user => new object[] { user.Id });

        // Act
        var first = query.ToSql();
        var second = query.ToSql();
        query.WhereIf<MultiSourceUser>(false, user => user.Id > 1);

        // Assert
        Assert.Equal(first, second);
        Assert.Equal("Select `users`.`Id` \r\nFrom `users`", query.ToSql());
    }

    /// <summary>
    /// 测试目的：多表投影应保持来源元数和参数位置列绑定，结果映射类型由终结操作决定。
    /// </summary>
    [Fact]
    public void From_WhenProjectionSelected_ShouldKeepSourceArityAndBoundSql()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var projection = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Select<MultiSourceUser, MultiSourceReview, MultiSourceProjection>((user, review) => new MultiSourceProjection
            {
                OwnerId = user.Id,
                ReviewUserId = review.UserId
            });
        var transition = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Select<MultiSourceUser, MultiSourceReview, MultiSourceProjection>((user, review) => new MultiSourceProjection
            {
                OwnerId = user.Id,
                ReviewUserId = review.UserId
            });

        // Assert
        Assert.IsType<SqlLambdaQuery>(projection);
        Assert.IsType<SqlLambdaQuery>(transition);
        Assert.Equal("Select `users`.`Id` As `OwnerId`,`reviews`.`UserId` As `ReviewUserId` \r\nFrom `users`, `reviews`", projection.ToSql());
        Assert.Equal("Select `users`.`Id` As `OwnerId`,`reviews`.`UserId` As `ReviewUserId` \r\nFrom `users`, `reviews`", transition.ToSql());
    }

    /// <summary>
    /// 测试目的：多表 DTO 投影应使用 DTO 成员名生成结果列别名，并按 Lambda 参数位置解析来源列。
    /// </summary>
    [Fact]
    public void From_WhenDtoProjectionSelected_ShouldRenderBoundColumnsWithTargetAliases()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var query = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Select<MultiSourceUser, MultiSourceReview, MultiSourceProjection>((user, review) => new MultiSourceProjection
            {
                OwnerId = user.Id,
                ReviewUserId = review.UserId
            });

        // Assert
        Assert.Equal("Select `users`.`Id` As `OwnerId`,`reviews`.`UserId` As `ReviewUserId` \r\nFrom `users`, `reviews`", query.ToSql());
    }

    /// <summary>
    /// 测试目的：多表 DTO 投影包含计算成员时应在替换既有投影前拒绝，避免产生部分 SQL 状态。
    /// </summary>
    [Fact]
    public void From_WhenDtoProjectionContainsComputedMember_ShouldRejectWithoutChangingExistingProjection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var query = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Select<MultiSourceUser, MultiSourceReview>((user, review) => new object[] { user.Id, review.UserId });

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => query.Select<MultiSourceUser, MultiSourceReview, MultiSourceProjection>((user, review) =>
            new MultiSourceProjection { OwnerId = user.Id + 1 }));

        // Assert
        Assert.Equal("多表 DTO 投影成员必须引用当前查询的 Lambda 参数。", exception.Message);
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId` \r\nFrom `users`, `reviews`", query.ToSql());
    }

    /// <summary>
    /// 测试目的：空 DTO 成员初始化不能退化为 Select *，单源派生和多源 DTO 投影均应在写入状态前拒绝。
    /// </summary>
    [Fact]
    public void From_WhenDtoProjectionHasNoMemberBindings_ShouldRejectWithoutChangingQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var single = rootQuery.From<MultiSourceUser>()
            .Select<MultiSourceUser>(user => new object[] { user.Id })
            .Where<MultiSourceUser>(user => user.Id > 7);
        var multi = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Select<MultiSourceUser, MultiSourceReview>((user, review) => new object[] { user.Id, review.UserId })
            .Where<MultiSourceUser, MultiSourceReview>((user, review) => review.UserId > 9);
        var singleSql = single.ToSql();
        var multiSql = multi.ToSql();

        // Act
        var singleException = Assert.Throws<NotSupportedException>(() => single.SelectSubquery<MultiSourceUser, EmptyProjection>(
            user => new EmptyProjection { }, "empty"));
        var multiException = Assert.Throws<NotSupportedException>(() => multi.Select<MultiSourceUser, MultiSourceReview, EmptyProjection>(
            (user, review) => new EmptyProjection { }));

        // Assert
        Assert.Equal("多表 DTO 投影至少需要一个成员初始化绑定。", singleException.Message);
        Assert.Equal("多表 DTO 投影至少需要一个成员初始化绑定。", multiException.Message);
        Assert.Equal("Select `users`.`Id` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0", singleSql);
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId` \r\nFrom `users`, `reviews` \r\nWhere `reviews`.`UserId`>@_p_0", multiSql);
        Assert.Equal(singleSql, single.ToSql());
        Assert.Equal(multiSql, multi.ToSql());
        Assert.Equal(new object[] { 7 }, single.GetParams().Values.ToArray());
        Assert.Equal(new object[] { 9 }, multi.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：单表 Lambda 查询替换 DTO 投影失败时，必须保留调用前已配置的投影。
    /// </summary>
    [Fact]
    public void From_WhenReplacementDtoProjectionFails_ShouldKeepExistingProjection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MultiSourceUser),
            DbKey = "sqlite",
            TableName = "users"
        }));
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var query = rootQuery.From<MultiSourceUser>().Select<MultiSourceUser>(user => new object[] { user.Id });
        var expectedSql = query.ToSql();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => query.Select<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection
        {
            OwnerId = user.Id + 1
        }));

        // Assert
        Assert.Equal("多表 DTO 投影成员必须引用当前查询的 Lambda 参数。", exception.Message);
        Assert.Equal(expectedSql, query.ToSql());
    }

    /// <summary>
    /// 测试目的：单表 Lambda 查询替换为非法聚合失败时，必须保留调用前已配置的投影。
    /// </summary>
    [Fact]
    public void From_WhenReplacementAggregateValidationFails_ShouldKeepExistingProjection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MultiSourceUser),
            DbKey = "sqlite",
            TableName = "users"
        }));
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var query = rootQuery.From<MultiSourceUser>().Select<MultiSourceUser>(user => new object[] { user.Id });
        var expectedSql = query.ToSql();

        // Act
        Assert.Throws<ArgumentOutOfRangeException>(() => query.Aggregate<MultiSourceUser>(
            (SqlAggregateFunction)999, user => user.Id));

        // Assert
        Assert.Equal(expectedSql, query.ToSql());
    }

    /// <summary>
    /// 测试目的：派生 DTO 只能访问直接投影成员，嵌套成员不能按末级同名属性绕过白名单。
    /// </summary>
    [Fact]
    public void From_WhenDerivedDtoNestedMemberMatchesProjectedName_ShouldRejectAcrossLambdaClauses()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var subquery = rootQuery.From<MultiSourceUser>()
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        var derivedRoot = rootQuery.FromSubquery(subquery);
        var originalSql = derivedRoot.ToSql();
        var joined = rootQuery.From<MultiSourceReview>();
        var joinedSql = joined.ToSql();

        // Act
        var whereException = Assert.Throws<NotSupportedException>(() => derivedRoot.Where<MultiSourceProjection>(
            summary => summary.Profile.OwnerId > 0));
        var selectException = Assert.Throws<NotSupportedException>(() => derivedRoot.Select<MultiSourceProjection>(
            summary => new object[] { summary.Profile.OwnerId }));
        var orderException = Assert.Throws<NotSupportedException>(() => derivedRoot.OrderBy<MultiSourceProjection>(
            summary => new object[] { summary.Profile.OwnerId }));
        var joinException = Assert.Throws<NotSupportedException>(() => joined.Join<MultiSourceReview, MultiSourceProjection>(subquery,
            (review, summary) => review.UserId == summary.Profile.OwnerId));

        // Assert
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", whereException.Message);
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", selectException.Message);
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", orderException.Message);
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", joinException.Message);
        Assert.Equal(originalSql, derivedRoot.ToSql());
        Assert.Equal(joinedSql, joined.ToSql());
        Assert.Empty(derivedRoot.GetParams());
        Assert.Empty(joined.GetParams());
    }

    /// <summary>
    /// 测试目的：严格 DTO 投影创建的派生表应保留内部参数和结果别名，并可作为后续多表 Join 的类型化来源。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryJoined_ShouldBindProjectedMembersAndParameters()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var subquery = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Where<MultiSourceUser, MultiSourceReview>((user, review) => user.Id > 7)
            .SelectSubquery<MultiSourceUser, MultiSourceReview, MultiSourceProjection>((user, review) => new MultiSourceProjection
            {
                OwnerId = user.Id,
                ReviewUserId = review.UserId
            }, "summary");

        // Act
        var query = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Join<MultiSourceUser, MultiSourceProjection>(subquery, (user, summary) =>
                user.Id == summary.OwnerId)
            .Where<MultiSourceReview, MultiSourceProjection>((review, summary) =>
                review.UserId == summary.ReviewUserId)
            .Select<MultiSourceUser, MultiSourceReview>((user, review) => new object[] { user.Id, review.UserId })
            .AppendSelect<MultiSourceProjection>(summary => new object[] { summary.OwnerId }, "summary");

        // Assert
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`summary`.`OwnerId` \r\nFrom `users`, `reviews` \r\nJoin (Select `users`.`Id` As `OwnerId`,`reviews`.`UserId` As `ReviewUserId` \r\nFrom `users`, `reviews` \r\nWhere `users`.`Id`>@_p_0) As `summary` On `users`.`Id`=`summary`.`OwnerId` \r\nWhere `reviews`.`UserId`=`summary`.`ReviewUserId`", query.ToSql());
        Assert.Equal(7, query.GetParams().Values.Single());
    }

    /// <summary>
    /// 测试目的：单表严格 DTO 投影应冻结为派生表，并在内连接后按双表 Lambda 参数位置绑定列与条件。
    /// </summary>
    [Fact]
    public void From_WhenSingleSourceDtoSubqueryJoined_ShouldBindProjectionAndParameters()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var subquery = rootQuery.From<MultiSourceUser>()
            .Where<MultiSourceUser>(user => user.Id > 7)
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var query = rootQuery.From<MultiSourceReview>()
            .Join<MultiSourceReview, MultiSourceProjection>(subquery, (review, summary) => review.UserId == summary.OwnerId)
            .Select<MultiSourceReview, MultiSourceProjection>((review, summary) => new object[] { review.UserId, summary.OwnerId });

        // Assert
        Assert.Equal("Select `reviews`.`UserId`,`summary`.`OwnerId` \r\nFrom `reviews` \r\nJoin (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `summary` On `reviews`.`UserId`=`summary`.`OwnerId`", query.ToSql());
        Assert.Equal(7, query.GetParams().Values.Single());
    }

    /// <summary>
    /// 测试目的：单表实体查询应能以 Cross Join 组合类型化派生表，并按双表 Lambda 参数位置绑定投影和筛选条件。
    /// </summary>
    [Fact]
    public void From_WhenSingleSourceDtoSubqueryCrossJoined_ShouldBindProjectionAndRejectOn()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var subquery = rootQuery.From<MultiSourceUser>()
            .Where<MultiSourceUser>(user => user.Id > 7)
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var query = rootQuery.From<MultiSourceReview>()
            .CrossJoin<MultiSourceProjection>(subquery)
            .Where<MultiSourceReview, MultiSourceProjection>((review, summary) => review.UserId == summary.OwnerId)
            .Select<MultiSourceReview, MultiSourceProjection>((review, summary) => new object[] { review.UserId, summary.OwnerId });

        // Assert
        Assert.Equal("Select `reviews`.`UserId`,`summary`.`OwnerId` \r\nFrom `reviews` \r\nCross Join (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `summary` \r\nWhere `reviews`.`UserId`=`summary`.`OwnerId`", query.ToSql());
        Assert.Equal(7, query.GetParams().Values.Single());
    }

    /// <summary>
    /// 测试目的：单表实体查询应能以 Cross Join 进入双表 Lambda 链；SQLite 对 Right Join 和 Full Join 须在调用阶段拒绝。
    /// </summary>
    [Fact]
    public void From_WhenSingleSourceTypedEntityOuterJoinsConfigured_ShouldBindSourcesAndEnforceCapabilities()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var crossJoin = rootQuery.From<MultiSourceUser>()
            .CrossJoin<MultiSourceReview>("review")
            .Where<MultiSourceUser, MultiSourceReview>((user, review) => user.Id == review.UserId)
            .Select<MultiSourceUser, MultiSourceReview>((user, review) => new object[] { user.Id, review.UserId });
        // Assert
        Assert.Equal("Select `users`.`Id`,`review`.`UserId` \r\nFrom `users` \r\nCross Join `reviews` As `review` \r\nWhere `users`.`Id`=`review`.`UserId`", crossJoin.ToSql());
        var rightJoinException = Assert.Throws<NotSupportedException>(() => rootQuery.From<MultiSourceUser>()
            .RightJoin<MultiSourceUser, MultiSourceReview>((user, review) => user.Id == review.UserId, "review"));
        var fullJoinException = Assert.Throws<NotSupportedException>(() => rootQuery.From<MultiSourceUser>()
            .FullJoin<MultiSourceUser, MultiSourceReview>((user, review) => user.Id == review.UserId, "review"));
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Right Join。", rightJoinException.Message);
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Full Join。", fullJoinException.Message);
    }

    /// <summary>
    /// 测试目的：SQLite 不支持的单表类型化派生表 Right Join 和 Full Join 应在调用阶段被能力门禁拒绝。
    /// </summary>
    [Fact]
    public void From_WhenSingleSourceDtoSubqueryUsesUnsupportedOuterJoin_ShouldRejectBeforeDatabaseAccess()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var subquery = rootQuery.From<MultiSourceUser>()
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        // Assert
        var rightJoinException = Assert.Throws<NotSupportedException>(() => rootQuery.From<MultiSourceReview>()
            .RightJoin<MultiSourceReview, MultiSourceProjection>(subquery, (review, summary) => review.UserId == summary.OwnerId));
        var fullJoinException = Assert.Throws<NotSupportedException>(() => rootQuery.From<MultiSourceReview>()
            .FullJoin<MultiSourceReview, MultiSourceProjection>(subquery, (review, summary) => review.UserId == summary.OwnerId));
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Right Join。", rightJoinException.Message);
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Full Join。", fullJoinException.Message);
    }

    /// <summary>
    /// 测试目的：单表派生表在 Join 后只能引用显式投影成员，非法成员筛选不得改变外层 SQL。
    /// </summary>
    [Fact]
    public void From_WhenSingleSourceDtoSubqueryMemberIsNotProjected_ShouldRejectWithoutChangingOuterQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var subquery = rootQuery.From<MultiSourceUser>()
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        var query = rootQuery.From<MultiSourceReview>().LeftJoin<MultiSourceReview, MultiSourceProjection>(subquery,
            (review, summary) => review.UserId == summary.OwnerId);
        var expectedSql = query.ToSql();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => query.Where<MultiSourceReview, MultiSourceProjection>(
            (review, summary) => summary.ReviewUserId == review.UserId));

        // Assert
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", exception.Message);
        Assert.Equal(expectedSql, query.ToSql());
        Assert.Empty(query.GetParams());
    }

    /// <summary>
    /// 测试目的：单表 DTO 派生表跨具名数据源 Join 时，应在别名和参数状态写入前拒绝。
    /// </summary>
    [Fact]
    public void From_WhenSingleSourceDtoSubqueryUsesDifferentDataSource_ShouldRejectWithoutChangingOuterQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            foreach (var dbKey in new[] { "first", "second" })
            {
                options.EntityMappings.Add(new EntityMappingOptions
                {
                    EntityType = typeof(MultiSourceUser),
                    DbKey = dbKey,
                    TableName = "users"
                });
                options.EntityMappings.Add(new EntityMappingOptions
                {
                    EntityType = typeof(MultiSourceReview),
                    DbKey = dbKey,
                    TableName = "reviews"
                });
            }
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("first", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=first.db");
        services.AddSqlDataSource("second", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=second.db");
        using var provider = services.BuildServiceProvider();
        using var firstQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("first");
        using var secondQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("second");
        var subquery = firstQuery.From<MultiSourceUser>()
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        var outer = secondQuery.From<MultiSourceReview>();
        var expectedSql = outer.ToSql();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => outer.Join<MultiSourceReview, MultiSourceProjection>(subquery,
            (review, summary) => review.UserId == summary.OwnerId));

        // Assert
        Assert.Equal("类型化派生表数据源 first 与当前数据源 second 不兼容。", exception.Message);
        Assert.Equal(expectedSql, outer.ToSql());
        Assert.Empty(outer.GetParams());
    }

    /// <summary>
    /// 测试目的：相同逻辑数据源键按读取偏好路由到不同 SQLite 文件时，类型化派生表必须在合并前拒绝。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryUsesSameLogicalKeyWithDifferentPhysicalDatabase_ShouldRejectWithoutChangingOuterQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "tenant",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "tenant",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.Replace(ServiceDescriptor.Singleton<ISqlDataSourceResolver>(
            new TenantSqliteDataSourceResolver("Data Source=tenant-a.db", "Data Source=tenant-b.db")));
        using var provider = services.BuildServiceProvider();
        var databaseScopeManager = provider.GetRequiredService<IDatabaseScopeManager>();
        var queryFactory = provider.GetRequiredService<ISqlQueryFactory>();
        SqlSubquery<MultiSourceProjection> subquery;
        using (databaseScopeManager.Use(new DatabaseScopeOptions { DbKey = "tenant", TenantId = "tenant" }))
        using (var sourceQuery = queryFactory.Create("tenant"))
            subquery = sourceQuery.From<MultiSourceUser>()
                .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        ISqlQuery outerQuery;
        using (databaseScopeManager.Use(new DatabaseScopeOptions
        {
            DbKey = "tenant",
            TenantId = "tenant",
            ReadPreference = SqlReadPreference.Primary
        }))
            outerQuery = queryFactory.Create("tenant");
        using (outerQuery)
        {
            var outer = outerQuery.From<MultiSourceReview>();
            var expectedSql = outer.ToSql();

            // Act
            var exception = Assert.Throws<NotSupportedException>(() => outer.Join<MultiSourceReview, MultiSourceProjection>(subquery,
                (review, summary) => review.UserId == summary.OwnerId));

            // Assert
            Assert.Equal("类型化派生表物理数据库身份与当前查询不兼容。", exception.Message);
            Assert.Equal(expectedSql, outer.ToSql());
            Assert.Empty(outer.GetParams());
        }
    }

        /// <summary>
        /// 测试目的：同一物理 SQLite 数据库中的不同租户不能组合类型化派生表，也不能作为派生根来源。
        /// </summary>
        [Fact]
        public void From_WhenDtoSubqueryUsesDifferentTenantWithSamePhysicalDatabase_ShouldRejectAcrossCompositionEntrypoints()
        {
            // Arrange
            var services = new ServiceCollection();
            services.ConfigureSqlMetadata(options =>
            {
                options.EntityMappings.Add(new EntityMappingOptions
                {
                    EntityType = typeof(MultiSourceUser),
                    DbKey = "tenant",
                    TableName = "users"
                });
                options.EntityMappings.Add(new EntityMappingOptions
                {
                    EntityType = typeof(MultiSourceReview),
                    DbKey = "tenant",
                    TableName = "reviews"
                });
            });
            services.AddSqliteProvider();
            services.AddSqlDataSource("tenant", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=shared-tenant.db");
            using var provider = services.BuildServiceProvider();
            var databaseScopeManager = provider.GetRequiredService<IDatabaseScopeManager>();
            var queryFactory = provider.GetRequiredService<ISqlQueryFactory>();
            SqlSubquery<MultiSourceProjection> subquery;
            using (databaseScopeManager.Use(new DatabaseScopeOptions { DbKey = "tenant", TenantId = "tenant-a" }))
            using (var sourceQuery = queryFactory.Create("tenant"))
                subquery = sourceQuery.From<MultiSourceUser>()
                    .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
            ISqlQuery outerQuery;
            using (databaseScopeManager.Use(new DatabaseScopeOptions { DbKey = "tenant", TenantId = "tenant-b" }))
                outerQuery = queryFactory.Create("tenant");
            using (outerQuery)
            {
                var outer = outerQuery.From<MultiSourceReview>();
                var expectedSql = outer.ToSql();

                // Act
                var joinException = Assert.Throws<NotSupportedException>(() => outer.Join<MultiSourceReview, MultiSourceProjection>(subquery,
                    (review, summary) => review.UserId == summary.OwnerId));
                var rootException = Assert.Throws<NotSupportedException>(() => outerQuery.FromSubquery(subquery));

                // Assert
                Assert.Equal("类型化派生表租户上下文与当前查询不兼容。", joinException.Message);
                Assert.Equal("类型化派生表租户上下文与当前查询不兼容。", rootException.Message);
                Assert.Equal(expectedSql, outer.ToSql());
                Assert.Empty(outer.GetParams());
            }
        }

        /// <summary>
        /// 测试目的：同一物理 SQLite 数据库的派生表与外层查询映射配置不一致时必须拒绝。
        /// </summary>
        [Fact]
        public void From_WhenDtoSubqueryUsesDifferentMappingProfileWithSamePhysicalDatabase_ShouldReject()
        {
            // Arrange
            var services = new ServiceCollection();
            services.ConfigureSqlMetadata(options =>
            {
                foreach (var dbKey in new[] { "reader", "default" })
                {
                    options.EntityMappings.Add(new EntityMappingOptions
                    {
                        EntityType = typeof(MultiSourceUser),
                        DbKey = dbKey,
                        TableName = "users"
                    });
                    options.EntityMappings.Add(new EntityMappingOptions
                    {
                        EntityType = typeof(MultiSourceReview),
                        DbKey = dbKey,
                        TableName = "reviews"
                    });
                }
            });
            services.AddSqliteProvider();
            services.AddSqlDataSource("reader", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=shared-profile.db",
                setupAction: source => source.MappingProfile = "reader");
            services.AddSqlDataSource("default", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=shared-profile.db");
            using var provider = services.BuildServiceProvider();
            using var sourceQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("reader");
            using var outerQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("default");
            var subquery = sourceQuery.From<MultiSourceUser>()
                .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
            var outer = outerQuery.From<MultiSourceReview>();
            var expectedSql = outer.ToSql();

            // Act
            var exception = Assert.Throws<NotSupportedException>(() => outer.Join<MultiSourceReview, MultiSourceProjection>(subquery,
                (review, summary) => review.UserId == summary.OwnerId));

            // Assert
            Assert.Equal("类型化派生表映射配置 reader 与当前映射配置 <默认> 不兼容。", exception.Message);
            Assert.Equal(expectedSql, outer.ToSql());
            Assert.Empty(outer.GetParams());
        }

    /// <summary>
    /// 测试目的：不同逻辑数据源键指向相同 SQLite 文件时，类型化派生表可安全组合并按各自映射生成完整 SQL。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryUsesDifferentLogicalKeyWithSamePhysicalDatabase_ShouldAllowComposition()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            foreach (var dbKey in new[] { "first", "second" })
            {
                options.EntityMappings.Add(new EntityMappingOptions
                {
                    EntityType = typeof(MultiSourceUser),
                    DbKey = dbKey,
                    TableName = "users"
                });
                options.EntityMappings.Add(new EntityMappingOptions
                {
                    EntityType = typeof(MultiSourceReview),
                    DbKey = dbKey,
                    TableName = "reviews"
                });
            }
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("first", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=shared.db");
        services.AddSqlDataSource("second", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=shared.db");
        using var provider = services.BuildServiceProvider();
        using var firstQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("first");
        using var secondQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("second");
        var subquery = firstQuery.From<MultiSourceUser>()
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var query = secondQuery.From<MultiSourceReview>()
            .Join<MultiSourceReview, MultiSourceProjection>(subquery, (review, summary) => review.UserId == summary.OwnerId)
            .Select<MultiSourceReview, MultiSourceProjection>((review, summary) => new object[] { review.UserId, summary.OwnerId });

        // Assert
        Assert.Equal("Select `reviews`.`UserId`,`summary`.`OwnerId` \r\nFrom `reviews` \r\nJoin (Select `users`.`Id` As `OwnerId` \r\nFrom `users`) As `summary` On `reviews`.`UserId`=`summary`.`OwnerId`", query.ToSql());
        Assert.Empty(query.GetParams());
    }

    /// <summary>
    /// 测试目的：独占 SQLite 内存数据库不能跨根查询组合类型化派生表，即使逻辑数据源键相同。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryUsesExclusiveMemoryDatabaseFromDifferentRoot_ShouldRejectWithoutChangingOuterQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        var queryFactory = provider.GetRequiredService<ISqlQueryFactory>();
        using var sourceQuery = queryFactory.Create("sqlite");
        using var outerQuery = queryFactory.Create("sqlite");
        var subquery = sourceQuery.From<MultiSourceUser>()
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        var outer = outerQuery.From<MultiSourceReview>();
        var expectedSql = outer.ToSql();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => outer.Join<MultiSourceReview, MultiSourceProjection>(subquery,
            (review, summary) => review.UserId == summary.OwnerId));

        // Assert
        Assert.Equal("类型化派生表物理数据库身份与当前查询不兼容。", exception.Message);
        Assert.Equal(expectedSql, outer.ToSql());
        Assert.Empty(outer.GetParams());
    }

    /// <summary>
    /// 测试目的：类型化派生表冻结与根来源组合应使用容器注册的物理数据库身份解析器。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryUsesCustomDatabaseIdentityResolver_ShouldInvokeRegisteredResolver()
    {
        // Arrange
        var resolver = new TrackingDatabaseIdentityResolver();
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MultiSourceUser),
            DbKey = "sqlite",
            TableName = "users"
        }));
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=identity-resolver.db");
        services.Replace(ServiceDescriptor.Singleton<ISqlDatabaseIdentityResolver>(resolver));
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var subquery = rootQuery.From<MultiSourceUser>()
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var query = rootQuery.FromSubquery(subquery)
            .Select<MultiSourceProjection>(summary => new object[] { summary.OwnerId });

        // Assert
        Assert.Equal("Select `summary`.`OwnerId` \r\nFrom (Select `users`.`Id` As `OwnerId` \r\nFrom `users`) As `summary`", query.ToSql());
        Assert.True(resolver.CallCount >= 2);
    }

    /// <summary>
    /// 测试目的：类型化派生根只能访问显式投影成员，并应合并内外层参数且保留来源别名。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryUsedAsRoot_ShouldBindProjectedMembersAndParameters()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MultiSourceUser),
            DbKey = "sqlite",
            TableName = "users"
        }));
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var subquery = rootQuery.From<MultiSourceUser>()
            .Where<MultiSourceUser>(user => user.Id > 7)
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var query = rootQuery.FromSubquery(subquery)
            .Where<MultiSourceProjection>(summary => summary.OwnerId > 9)
            .Select<MultiSourceProjection>(summary => new object[] { summary.OwnerId });

        // Assert
        Assert.Equal("Select `summary`.`OwnerId` \r\nFrom (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `summary` \r\nWhere `summary`.`OwnerId`>@_p_1", query.ToSql());
        Assert.Equal(new object[] { 7, 9 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：严格 DTO 派生表已作为根来源时，同别名派生 Join 应在渲染和参数合并前拒绝，保留原查询状态。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryJoinDuplicatesRootAlias_ShouldRejectWithoutChangingQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MultiSourceUser),
            DbKey = "sqlite",
            TableName = "users"
        }));
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var subquery = rootQuery.From<MultiSourceUser>()
            .Where<MultiSourceUser>(user => user.Id > 7)
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        var query = rootQuery.FromSubquery(subquery)
            .Where<MultiSourceProjection>(summary => summary.OwnerId > 9);
        var expectedSql = query.ToSql();
        var expectedParameters = query.GetParams();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.Join<MultiSourceProjection, MultiSourceProjection>(subquery,
            (left, right) => left.OwnerId == right.OwnerId));

        // Assert
        Assert.Equal("查询中已存在表别名 \"summary\"。", exception.Message);
        Assert.Equal(expectedSql, query.ToSql());
        Assert.Equal(expectedParameters, query.GetParams());
    }

    /// <summary>
    /// 测试目的：类型化派生根引用未投影成员或跨数据源来源时，应在写入外层状态前拒绝。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryRootIsInvalid_ShouldRejectWithoutChangingQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            foreach (var dbKey in new[] { "first", "second" })
                options.EntityMappings.Add(new EntityMappingOptions
                {
                    EntityType = typeof(MultiSourceUser),
                    DbKey = dbKey,
                    TableName = "users"
                });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("first", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=first.db");
        services.AddSqlDataSource("second", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=second.db");
        using var provider = services.BuildServiceProvider();
        using var firstQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("first");
        using var secondQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("second");
        var subquery = firstQuery.From<MultiSourceUser>()
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var dataSourceException = Assert.Throws<NotSupportedException>(() => secondQuery.FromSubquery(subquery));
        var query = firstQuery.FromSubquery(subquery);
        var expectedSql = query.ToSql();
        var memberException = Assert.Throws<NotSupportedException>(() => query.Where<MultiSourceProjection>(summary => summary.ReviewUserId > 0));

        // Assert
        Assert.Equal("类型化派生表数据源 first 与当前数据源 second 不兼容。", dataSourceException.Message);
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", memberException.Message);
        Assert.Equal(expectedSql, query.ToSql());
        Assert.Empty(query.GetParams());
    }

    /// <summary>
    /// 测试目的：类型化派生根再次冻结并作为根来源时，应逐层保留成员白名单、别名及参数绑定。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryRootIsReprojected_ShouldKeepNestedProjectionBindings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MultiSourceUser),
            DbKey = "sqlite",
            TableName = "users"
        }));
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var owner = rootQuery.From<MultiSourceUser>()
            .Where<MultiSourceUser>(user => user.Id > 7)
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "owner");
        var review = rootQuery.FromSubquery(owner)
            .Where<MultiSourceProjection>(summary => summary.OwnerId > 9)
            .SelectSubquery<MultiSourceProjection, MultiSourceProjection>(summary => new MultiSourceProjection { ReviewUserId = summary.OwnerId }, "review");

        // Act
        var query = rootQuery.FromSubquery(review)
            .Where<MultiSourceProjection>(summary => summary.ReviewUserId > 11)
            .Select<MultiSourceProjection>(summary => new object[] { summary.ReviewUserId });
        var expectedSql = query.ToSql();
        var exception = Assert.Throws<NotSupportedException>(() => query.Where<MultiSourceProjection>(summary => summary.OwnerId > 13));

        // Assert
        Assert.Equal("Select `review`.`ReviewUserId` \r\nFrom (Select `owner`.`OwnerId` As `ReviewUserId` \r\nFrom (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `owner` \r\nWhere `owner`.`OwnerId`>@_p_1) As `review` \r\nWhere `review`.`ReviewUserId`>@_p_2", expectedSql);
        Assert.Equal(new object[] { 7, 9, 11 }, query.GetParams().Values.ToArray());
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", exception.Message);
        Assert.Equal(expectedSql, query.ToSql());
        Assert.Equal(new object[] { 7, 9, 11 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：派生 DTO 根经 Left Join 后再次冻结时，应保持根与 Join 的参数位置、成员白名单和冻结 SQL 隔离。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryRootIsLeftJoinedAndReprojected_ShouldKeepBoundSourcesAndFrozenState()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var owner = rootQuery.From<MultiSourceUser>()
            .Where<MultiSourceUser>(user => user.Id > 7)
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "owner");
        var joined = rootQuery.FromSubquery(owner)
            .LeftJoin<MultiSourceProjection, MultiSourceReview>((summary, review) => summary.OwnerId == review.UserId, "review")
            .Where<MultiSourceProjection, MultiSourceReview>((summary, review) => summary.OwnerId > 9);
        var refined = joined.SelectSubquery<MultiSourceProjection, MultiSourceReview, MultiSourceProjection>((summary, review) => new MultiSourceProjection
        {
            OwnerId = summary.OwnerId,
            ReviewUserId = review.UserId
        }, "refined");
        joined.Where<MultiSourceProjection, MultiSourceReview>((summary, review) => review.UserId > 11);

        // Act
        var query = rootQuery.FromSubquery(refined)
            .Where<MultiSourceProjection>(summary => summary.OwnerId > 13)
            .Select<MultiSourceProjection>(summary => new object[] { summary.OwnerId, summary.ReviewUserId });

        // Assert
        Assert.Equal("Select `refined`.`OwnerId`,`refined`.`ReviewUserId` \r\nFrom (Select `owner`.`OwnerId` As `OwnerId`,`review`.`UserId` As `ReviewUserId` \r\nFrom (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `owner` \r\nLeft Join `reviews` As `review` On `owner`.`OwnerId`=`review`.`UserId` \r\nWhere `owner`.`OwnerId`>@_p_1) As `refined` \r\nWhere `refined`.`OwnerId`>@_p_2",
            query.ToSql());
        Assert.Equal(new object[] { 7, 9, 13 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：严格 DTO 根经混合 Join 链扩展到七来源后再次冻结时，应按参数位置保留来源顺序、别名和投影白名单。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryRootBuildsSevenSourceMixedJoinChain_ShouldKeepSourceOrderAliasesAndParameters()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourcePermission),
                DbKey = "sqlite",
                TableName = "permissions"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var owner = rootQuery.From<MultiSourceUser>()
            .Where<MultiSourceUser>(user => user.Id > 7)
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "owner");
        var query = rootQuery.FromSubquery(owner)
            .Join<MultiSourceProjection, MultiSourceReview>((item, review) => item.OwnerId == review.UserId, "review")
            .CrossJoin<MultiSourcePermission>("permission")
            .LeftJoin<MultiSourcePermission, MultiSourceUser>((permission, reviewer) => reviewer.Id == permission.UserId,
                new SqlJoinOptions { RightAlias = "reviewer", LeftAlias = "permission" })
            .Join<MultiSourceUser, MultiSourceReview>((reviewer, review2) => reviewer.Id == review2.UserId,
                new SqlJoinOptions { RightAlias = "review2", LeftAlias = "reviewer" })
            .CrossJoin<MultiSourcePermission>("permission2")
            .Join<MultiSourcePermission, MultiSourceUser>((permission2, lastUser) =>
                lastUser.Id == permission2.UserId,
                new SqlJoinOptions { RightAlias = "lastUser", LeftAlias = "permission2" })
            .Where<MultiSourceProjection, MultiSourceReview>((owner, review) => owner.OwnerId > 9, "owner", "review")
            .Select<MultiSourceProjection, MultiSourceReview>((owner, review) => new object[] { owner.OwnerId, review.UserId }, "owner", "review")
            .AppendSelect<MultiSourcePermission>(permission => new object[] { permission.UserId }, "permission")
            .AppendSelect<MultiSourceUser>(reviewer => new object[] { reviewer.Id }, "reviewer")
            .AppendSelect<MultiSourceReview>(review2 => new object[] { review2.UserId }, "review2")
            .AppendSelect<MultiSourcePermission>(permission2 => new object[] { permission2.UserId }, "permission2")
            .AppendSelect<MultiSourceUser>(lastUser => new object[] { lastUser.Id }, "lastUser");

        // Assert
        Assert.Equal("Select `owner`.`OwnerId`,`review`.`UserId`,`permission`.`UserId`,`reviewer`.`Id`,`review2`.`UserId`,`permission2`.`UserId`,`lastUser`.`Id` \r\nFrom (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `owner` \r\nJoin `reviews` As `review` On `owner`.`OwnerId`=`review`.`UserId` \r\nCross Join `permissions` As `permission` \r\nLeft Join `users` As `reviewer` On `reviewer`.`Id`=`permission`.`UserId` \r\nJoin `reviews` As `review2` On `reviewer`.`Id`=`review2`.`UserId` \r\nCross Join `permissions` As `permission2` \r\nJoin `users` As `lastUser` On `lastUser`.`Id`=`permission2`.`UserId` \r\nWhere `owner`.`OwnerId`>@_p_1", query.ToSql());
        Assert.Equal(new object[] { 7, 9 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：类型化派生根应支持实体和派生表 Cross Join，并保留投影成员绑定、参数隔离及 Cross Join 的 On 拒绝语义。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryRootCrossJoined_ShouldRenderCompleteSqlAndRejectOn()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var owner = rootQuery.From<MultiSourceUser>()
            .Where<MultiSourceUser>(user => user.Id > 7)
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "owner");
        var review = rootQuery.From<MultiSourceReview>()
            .Where<MultiSourceReview>(item => item.UserId > 11)
            .SelectSubquery<MultiSourceReview, MultiSourceProjection>(item => new MultiSourceProjection { ReviewUserId = item.UserId }, "review");

        // Act
        var entityCrossJoin = rootQuery.FromSubquery(owner)
            .CrossJoin<MultiSourceReview>("entityReview")
            .Where<MultiSourceProjection, MultiSourceReview>((owner, item) => owner.OwnerId == item.UserId, "owner", "entityReview")
            .Select<MultiSourceProjection, MultiSourceReview>((owner, item) => new object[] { owner.OwnerId, item.UserId }, "owner", "entityReview");
        var derivedCrossJoin = rootQuery.FromSubquery(owner)
            .CrossJoin<MultiSourceProjection>(review)
            .Where<MultiSourceProjection, MultiSourceProjection>((owner, review) => owner.OwnerId == review.ReviewUserId, "owner", "review")
            .Select<MultiSourceProjection, MultiSourceProjection>((owner, review) => new object[] { owner.OwnerId, review.ReviewUserId }, "owner", "review");

        // Assert
        Assert.Equal("Select `owner`.`OwnerId`,`entityReview`.`UserId` \r\nFrom (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `owner` \r\nCross Join `reviews` As `entityReview` \r\nWhere `owner`.`OwnerId`=`entityReview`.`UserId`", entityCrossJoin.ToSql());
        Assert.Equal(new object[] { 7 }, entityCrossJoin.GetParams().Values.ToArray());
        Assert.Equal("Select `owner`.`OwnerId`,`review`.`ReviewUserId` \r\nFrom (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `owner` \r\nCross Join (Select `reviews`.`UserId` As `ReviewUserId` \r\nFrom `reviews` \r\nWhere `reviews`.`UserId`>@_p_1) As `review` \r\nWhere `owner`.`OwnerId`=`review`.`ReviewUserId`", derivedCrossJoin.ToSql());
        Assert.Equal(new object[] { 7, 11 }, derivedCrossJoin.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：SQLite 不支持的根派生表 Right Join 和 Full Join 应在调用阶段拒绝，且实体和派生表连接入口遵循同一能力门禁。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryRootUsesUnsupportedOuterJoin_ShouldRejectBeforeDatabaseAccess()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var owner = rootQuery.From<MultiSourceUser>()
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "owner");
        var review = rootQuery.From<MultiSourceReview>()
            .SelectSubquery<MultiSourceReview, MultiSourceProjection>(item => new MultiSourceProjection { ReviewUserId = item.UserId }, "review");

        // Act
        // Assert
        var rightJoinException = Assert.Throws<NotSupportedException>(() => rootQuery.FromSubquery(owner)
            .RightJoin<MultiSourceProjection, MultiSourceReview>((summary, item) => summary.OwnerId == item.UserId, "entityReview"));
        var fullJoinException = Assert.Throws<NotSupportedException>(() => rootQuery.FromSubquery(owner)
            .FullJoin<MultiSourceProjection, MultiSourceProjection>(review, (summary, item) => summary.OwnerId == item.ReviewUserId));
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Right Join。", rightJoinException.Message);
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Full Join。", fullJoinException.Message);
    }

    /// <summary>
    /// 测试目的：外层多表 Lambda 只能引用派生 DTO 显式投影成员，失败筛选不得改变已完成的原子 Join。
    /// </summary>
    [Fact]
    public void From_WhenJoinedDtoSubqueryMemberIsNotProjected_ShouldRejectWithoutChangingOuterQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var subquery = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .SelectSubquery<MultiSourceUser, MultiSourceReview, MultiSourceProjection>((user, review) => new MultiSourceProjection { ReviewUserId = review.UserId }, "summary");
        var query = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Join<MultiSourceReview, MultiSourceProjection>(subquery,
                (review, summary) => review.UserId == summary.ReviewUserId);

        // Act
        var memberException = Assert.Throws<NotSupportedException>(() => query.Where<MultiSourceProjection>(
            summary => summary.OwnerId == 1));

        // Assert
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", memberException.Message);
        Assert.Equal("Select `users`.`Id` \r\nFrom `users`, `reviews` \r\nJoin (Select `reviews`.`UserId` As `ReviewUserId` \r\nFrom `users`, `reviews`) As `summary` On `reviews`.`UserId`=`summary`.`ReviewUserId`", query.ToSql());
    }

    /// <summary>
    /// 测试目的：派生 DTO 查询必须绑定创建时的数据源，跨具名 SQLite 数据源 Join 应在别名和参数写入前拒绝。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryUsesDifferentDataSource_ShouldRejectWithoutChangingOuterQuery()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            foreach (var dbKey in new[] { "first", "second" })
            {
                options.EntityMappings.Add(new EntityMappingOptions
                {
                    EntityType = typeof(MultiSourceUser),
                    DbKey = dbKey,
                    TableName = "users"
                });
                options.EntityMappings.Add(new EntityMappingOptions
                {
                    EntityType = typeof(MultiSourceReview),
                    DbKey = dbKey,
                    TableName = "reviews"
                });
            }
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("first", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=first.db");
        services.AddSqlDataSource("second", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=second.db");
        using var provider = services.BuildServiceProvider();
        using var firstQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("first");
        using var secondQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("second");
        var subquery = firstQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .SelectSubquery<MultiSourceUser, MultiSourceReview, MultiSourceProjection>((user, review) => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        var outer = secondQuery.From<MultiSourceUser>().From<MultiSourceReview>();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => outer.Join<MultiSourceUser, MultiSourceProjection>(subquery,
            (user, summary) => user.Id == summary.OwnerId));

        // Assert
        Assert.Equal("类型化派生表数据源 first 与当前数据源 second 不兼容。", exception.Message);
        Assert.Equal("Select `users`.`Id` \r\nFrom `users`, `reviews`", outer.ToSql());
        Assert.Empty(outer.GetParams());
    }

    /// <summary>
    /// 测试目的：DTO 派生表应冻结创建时的 SQL 状态，并在与外层同名参数合并时稳定重命名。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryParametersConflict_ShouldKeepFrozenProjectionAndBindings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var source = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Where<MultiSourceUser, MultiSourceReview>((user, review) => user.Id > 7);
        var subquery = source.SelectSubquery<MultiSourceUser, MultiSourceReview, MultiSourceProjection>((user, review) => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        source.Where<MultiSourceUser, MultiSourceReview>((user, review) => review.UserId > 11);

        // Act
        var query = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Where<MultiSourceUser, MultiSourceReview>((user, review) => user.Id > 3)
            .Join<MultiSourceUser, MultiSourceProjection>(subquery, (user, summary) => user.Id == summary.OwnerId);

        // Assert
        Assert.Equal("Select `users`.`Id` \r\nFrom `users`, `reviews` \r\nJoin (Select `users`.`Id` As `OwnerId` \r\nFrom `users`, `reviews` \r\nWhere `users`.`Id`>@_p_1) As `summary` On `users`.`Id`=`summary`.`OwnerId` \r\nWhere `users`.`Id`>@_p_0", query.ToSql());
        Assert.Equal(new object[] { 3, 7 }, query.GetParams().Values.ToArray());
        Assert.Equal(query.ToSql(), query.ToSql());
    }

    /// <summary>
    /// 测试目的：多个严格 DTO 派生表与外层同名参数合并时，应按 Join 顺序分配稳定名称，重复渲染不得污染参数状态。
    /// </summary>
    [Fact]
    public void From_WhenMultipleDtoSubqueryParametersConflict_ShouldKeepStableBindingsAcrossRenderings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var audit = rootQuery.From<MultiSourceReview>()
            .Where<MultiSourceReview>(review => review.UserId > 7)
            .SelectSubquery<MultiSourceReview, MultiSourceProjection>(review => new MultiSourceProjection { ReviewUserId = review.UserId }, "audit");
        var owner = rootQuery.From<MultiSourceUser>()
            .Where<MultiSourceUser>(user => user.Id > 11)
            .SelectSubquery<MultiSourceUser, MultiSourceProjection>(user => new MultiSourceProjection { OwnerId = user.Id }, "owner");

        // Act
        var query = rootQuery.From<MultiSourceUser>()
            .Where<MultiSourceUser>(user => user.Id > 3)
            .Join<MultiSourceUser, MultiSourceProjection>(audit, (user, review) => user.Id == review.ReviewUserId)
            .Join<MultiSourceUser, MultiSourceProjection>(owner, (user, summary) => user.Id == summary.OwnerId)
            .Select<MultiSourceUser>(user => new object[] { user.Id })
            .AppendSelect<MultiSourceProjection>(review => new object[] { review.ReviewUserId }, "audit")
            .AppendSelect<MultiSourceProjection>(summary => new object[] { summary.OwnerId }, "owner");
        var firstSql = query.ToSql();
        var secondSql = query.ToSql();

        // Assert
        Assert.Equal("Select `users`.`Id`,`audit`.`ReviewUserId`,`owner`.`OwnerId` \r\nFrom `users` \r\nJoin (Select `reviews`.`UserId` As `ReviewUserId` \r\nFrom `reviews` \r\nWhere `reviews`.`UserId`>@_p_1) As `audit` On `users`.`Id`=`audit`.`ReviewUserId` \r\nJoin (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_2) As `owner` On `users`.`Id`=`owner`.`OwnerId` \r\nWhere `users`.`Id`>@_p_0",
            firstSql);
        Assert.Equal(firstSql, secondSql);
        Assert.Equal(new object[] { 3, 7, 11 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：七表根来源应能按 Lambda 参数位置冻结首、中、尾来源为严格 DTO 派生表，并保留重复实体的自动别名。
    /// </summary>
    [Fact]
    public void From_WhenSevenSourcesAreFrozenAsDtoSubquery_ShouldKeepBoundSourceOrderAndAliases()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourcePermission),
                DbKey = "sqlite",
                TableName = "permissions"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var summary = rootQuery.From<MultiSourceUser>("first").From<MultiSourceUser>("seventh")
            .Where<MultiSourceUser, MultiSourceUser>((first, seventh) => first.Id == seventh.Id, "first", "seventh")
            .SelectSubquery<MultiSourceUser, MultiSourceUser, HighArityProjection>((first, seventh) => new HighArityProjection
            {
                FirstId = first.Id,
                FourthId = seventh.Id,
                SeventhId = seventh.Id
            }, "summary", "first", "seventh");
        var query = rootQuery.FromSubquery(summary)
            .Where<HighArityProjection>(item => item.SeventhId > 13)
            .Select<HighArityProjection>(item => new object[] { item.FirstId, item.FourthId, item.SeventhId });

        // Assert
        Assert.Equal("Select `summary`.`FirstId`,`summary`.`FourthId`,`summary`.`SeventhId` \r\nFrom (Select `first`.`Id` As `FirstId`,`seventh`.`Id` As `FourthId`,`seventh`.`Id` As `SeventhId` \r\nFrom `users` As `first`, `users` As `seventh` \r\nWhere `first`.`Id`=`seventh`.`Id`) As `summary` \r\nWhere `summary`.`SeventhId`>@_p_0",
            query.ToSql());
        Assert.Equal(13, query.GetParams().Values.Single());
    }

    /// <summary>
    /// 测试目的：同一实体的多根来源应自动分配稳定别名，并按 Lambda 参数位置引用正确列。
    /// </summary>
    [Fact]
    public void From_WhenSameMappedEntityProvidedTwice_ShouldUseDistinctAutomaticSourceAliases()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MultiSourceUser),
            DbKey = "sqlite",
            TableName = "users"
        }));
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var query = rootQuery.From<MultiSourceUser>("owner").From<MultiSourceUser>("reviewer")
            .Select<MultiSourceUser, MultiSourceUser>((owner, reviewer) => new object[] { owner.Id, reviewer.Id }, "owner", "reviewer")
            .Where<MultiSourceUser, MultiSourceUser>((owner, reviewer) => owner.Id == reviewer.Id, "owner", "reviewer");

        // Assert
        Assert.Equal("Select `owner`.`Id`,`reviewer`.`Id` \r\nFrom `users` As `owner`, `users` As `reviewer` \r\nWhere `owner`.`Id`=`reviewer`.`Id`", query.ToSql());
    }

    /// <summary>
    /// 测试目的：多表分组和排序应按 Lambda 参数位置选择同类型根来源的正确别名。
    /// </summary>
    [Fact]
    public void From_WhenSameMappedEntityGroupedAndOrdered_ShouldUseBoundSourceAliases()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MultiSourceUser),
            DbKey = "sqlite",
            TableName = "users"
        }));
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var query = rootQuery.From<MultiSourceUser>("owner").From<MultiSourceUser>("reviewer")
            .Select<MultiSourceUser, MultiSourceUser>((owner, reviewer) => new object[] { owner.Id, reviewer.Id }, "owner", "reviewer")
            .GroupBy<MultiSourceUser, MultiSourceUser>((owner, reviewer) => new object[] { owner.Id, reviewer.Id }, "owner", "reviewer")
            .OrderBy<MultiSourceUser, MultiSourceUser>((owner, reviewer) => new object[] { reviewer.Id, owner.Id }, "owner", "reviewer", true);

        // Assert
        Assert.Equal("Select `owner`.`Id`,`reviewer`.`Id` \r\nFrom `users` As `owner`, `users` As `reviewer` \r\nGroup By `owner`.`Id`,`reviewer`.`Id` \r\nOrder By `reviewer`.`Id` Desc,`owner`.`Id` Desc", query.ToSql());
    }

    /// <summary>
    /// 测试目的：多表 Having 应按 Lambda 参数位置绑定同类型根来源，并将比较值参数化。
    /// </summary>
    [Fact]
    public void From_WhenSameMappedEntityHavingConfigured_ShouldUseBoundSourceAliasesAndParameter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MultiSourceUser),
            DbKey = "sqlite",
            TableName = "users"
        }));
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var query = rootQuery.From<MultiSourceUser>("owner").From<MultiSourceUser>("reviewer")
            .Select<MultiSourceUser, MultiSourceUser>((owner, reviewer) => new object[] { owner.Id, reviewer.Id }, "owner", "reviewer")
            .GroupBy<MultiSourceUser, MultiSourceUser>((owner, reviewer) => new object[] { owner.Id, reviewer.Id }, "owner", "reviewer")
            .Having<MultiSourceUser, MultiSourceUser>((owner, reviewer) => reviewer.Id > 10 && owner.Id > 0, "owner", "reviewer");

        // Assert
        Assert.Equal("Select `owner`.`Id`,`reviewer`.`Id` \r\nFrom `users` As `owner`, `users` As `reviewer` \r\nGroup By `owner`.`Id`,`reviewer`.`Id` Having `reviewer`.`Id`>@_p_0 And `owner`.`Id`>@_p_1", query.ToSql());
        Assert.Equal(new object[] { 10, 0 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：多表类型化 Join 链的 On 条件应按 Lambda 参数位置区分根来源、同类型连接来源和参数值。
    /// </summary>
    [Fact]
    public void From_WhenTypedJoinChainConfigured_ShouldBindAllSourcesByParameterPosition()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourcePermission),
                DbKey = "sqlite",
                TableName = "permissions"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var query = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Join<MultiSourceUser, MultiSourceUser>((user, reviewer) => user.Id == reviewer.Id && reviewer.Id > 7,
                "reviewer")
            .Join<MultiSourceUser, MultiSourcePermission>((reviewer, permission) => reviewer.Id == permission.UserId,
                new SqlJoinOptions { RightAlias = "permission", LeftAlias = "reviewer" })
            .Where<MultiSourceUser, MultiSourceReview>((users, reviews) => users.Id == reviews.UserId, "users", "reviews")
            .Select<MultiSourceUser, MultiSourceReview>((users, reviews) => new object[] { users.Id, reviews.UserId }, "users", "reviews")
            .AppendSelect<MultiSourceUser>(reviewer => new object[] { reviewer.Id }, "reviewer")
            .AppendSelect<MultiSourcePermission>(permission => new object[] { permission.UserId }, "permission");

        // Assert
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`reviewer`.`Id`,`permission`.`UserId` \r\nFrom `users`, `reviews` \r\nJoin `users` As `reviewer` On `users`.`Id`=`reviewer`.`Id` And `reviewer`.`Id`>@_p_0 \r\nJoin `permissions` As `permission` On `reviewer`.`Id`=`permission`.`UserId` \r\nWhere `users`.`Id`=`reviews`.`UserId`", query.ToSql());
        Assert.Equal(7, query.GetParams().Values.Single());
    }

    /// <summary>
    /// 测试目的：多表类型化左外连接应按 Lambda 参数位置绑定同类型连接表，并保留参数化 On 条件。
    /// </summary>
    [Fact]
    public void From_WhenTypedLeftJoinConfigured_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var query = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .LeftJoin<MultiSourceUser, MultiSourceUser>((owner, reviewer) => owner.Id == reviewer.Id && reviewer.Id > 3,
                "reviewer")
            .Select<MultiSourceUser, MultiSourceReview>((users, reviews) => new object[] { users.Id, reviews.UserId }, "users", "reviews")
            .AppendSelect<MultiSourceUser>(reviewer => new object[] { reviewer.Id }, "reviewer");

        // Assert
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`reviewer`.`Id` \r\nFrom `users`, `reviews` \r\nLeft Join `users` As `reviewer` On `users`.`Id`=`reviewer`.`Id` And `reviewer`.`Id`>@_p_0", query.ToSql());
        Assert.Equal(3, query.GetParams().Values.Single());
    }

    /// <summary>
    /// 测试目的：多表类型化交叉连接不应生成 On 条件，后续筛选和投影仍应绑定新表源。
    /// </summary>
    [Fact]
    public void From_WhenTypedCrossJoinConfigured_ShouldRenderCompleteSqlWithoutOn()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourcePermission),
                DbKey = "sqlite",
                TableName = "permissions"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var query = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .CrossJoin<MultiSourcePermission>("permission")
            .Select<MultiSourceUser, MultiSourceReview>((user, review) => new object[] { user.Id, review.UserId })
            .AppendSelect<MultiSourcePermission>(permission => new object[] { permission.UserId }, "permission")
            .Where<MultiSourceReview, MultiSourcePermission>((review, permission) => review.UserId == permission.UserId);

        // Assert
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`permission`.`UserId` \r\nFrom `users`, `reviews` \r\nCross Join `permissions` As `permission` \r\nWhere `reviews`.`UserId`=`permission`.`UserId`", query.ToSql());
    }

    /// <summary>
    /// 测试目的：不支持 Right Join 和 Full Join 的 SQLite 应在多表类型化连接调用阶段拒绝，避免访问数据库。
    /// </summary>
    [Fact]
    public void From_WhenTypedUnsupportedOuterJoinConfigured_ShouldRejectBeforeDatabaseAccess()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourcePermission),
                DbKey = "sqlite",
                TableName = "permissions"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        // Assert
        var rightJoinException = Assert.Throws<NotSupportedException>(() => rootQuery.From<MultiSourceReview>()
            .RightJoin<MultiSourceReview, MultiSourcePermission>((review, permission) => review.UserId == permission.UserId,
                "permission"));
        var fullJoinException = Assert.Throws<NotSupportedException>(() => rootQuery.From<MultiSourceReview>()
            .FullJoin<MultiSourceReview, MultiSourcePermission>((review, permission) => review.UserId == permission.UserId,
                "permission"));
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Right Join。", rightJoinException.Message);
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Full Join。", fullJoinException.Message);
    }

    /// <summary>
    /// 测试目的：多表 Join 查询应保持参数位置投影和排序，并按 SQLite 方言生成分页 SQL。
    /// </summary>
    [Fact]
    public void From_WhenTypedJoinAndPaginationConfigured_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var query = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Join<MultiSourceUser, MultiSourceUser>((owner, reviewer) => owner.Id == reviewer.Id, "reviewer")
            .Select<MultiSourceUser, MultiSourceReview>((users, reviews) => new object[] { users.Id, reviews.UserId }, "users", "reviews")
            .AppendSelect<MultiSourceUser>(reviewer => new object[] { reviewer.Id }, "reviewer")
            .OrderBy<MultiSourceUser>(reviewer => new object[] { reviewer.Id }, "reviewer")
            .Skip(5)
            .Take(10);

        // Assert
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`reviewer`.`Id` \r\nFrom `users`, `reviews` \r\nJoin `users` As `reviewer` On `users`.`Id`=`reviewer`.`Id` \r\nOrder By `reviewer`.`Id` \r\nLimit @_p_1 OFFSET @_p_0", query.ToSql());
        Assert.Equal(new object[] { 5, 10 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：同一根查询创建的多表分页描述应分别持有 Builder 和分页参数，避免后续配置污染既有描述。
    /// </summary>
    [Fact]
    public void From_WhenMultiplePagedDescriptionsCreated_ShouldKeepBuilderAndPaginationStateIsolated()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");

        // Act
        var first = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Select<MultiSourceUser, MultiSourceReview>((users, reviews) => new object[] { users.Id, reviews.UserId })
            .OrderBy<MultiSourceUser, MultiSourceReview>((users, reviews) => new object[] { users.Id })
            .Skip(2)
            .Take(3);
        var second = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>()
            .Select<MultiSourceUser, MultiSourceReview>((users, reviews) => new object[] { users.Id, reviews.UserId })
            .OrderBy<MultiSourceUser, MultiSourceReview>((users, reviews) => new object[] { reviews.UserId })
            .Skip(7)
            .Take(11);

        // Assert
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId` \r\nFrom `users`, `reviews` \r\nOrder By `users`.`Id` \r\nLimit @_p_1 OFFSET @_p_0", first.ToSql());
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId` \r\nFrom `users`, `reviews` \r\nOrder By `reviews`.`UserId` \r\nLimit @_p_1 OFFSET @_p_0", second.ToSql());
    }

    /// <summary>
    /// 测试目的：单来源 Lambda 应按参数位置绑定首来源，并输出完整参数名和值。
    /// </summary>
    [Fact]
    public void From_WhenOneLambdaSourceConfigured_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var rootQuery = CreateMultiSourceQuery();

        // Act
        var query = rootQuery.From<MultiSourceUser>()
            .Where<MultiSourceUser>(user => user.Id > 1)
            .Select<MultiSourceUser>(user => new object[] { user.Id });

        // Assert
        Assert.IsType<SqlLambdaQuery>(query);
        Assert.Equal("Select `users`.`Id` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0", query.ToSql());
        Assert.Equal(new[] { "@_p_0" }, query.GetParams().Keys);
        Assert.Equal(new object[] { 1 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：双来源连续 Join 应绑定首尾来源，并输出完整 Join SQL、参数名和值。
    /// </summary>
    [Fact]
    public void From_WhenTwoSourcesAreJoinedInUnit_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var rootQuery = CreateMultiSourceQuery();

        // Act
        var query = rootQuery.From<MultiSourceUser>()
            .Join<MultiSourceUser, MultiSourceReview>((user, review) => user.Id == review.UserId && review.UserId > 2, "review")
            .Select<MultiSourceUser, MultiSourceReview>((user, review) => new object[] { user.Id, review.UserId });

        // Assert
        Assert.IsType<SqlLambdaQuery>(query);
        Assert.Equal("Select `users`.`Id`,`review`.`UserId` \r\nFrom `users` \r\nJoin `reviews` As `review` On `users`.`Id`=`review`.`UserId` And `review`.`UserId`>@_p_0", query.ToSql());
        Assert.Equal(new[] { "@_p_0" }, query.GetParams().Keys);
        Assert.Equal(new object[] { 2 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：三来源连续 Join 应绑定首、中、尾来源，并输出完整 Join SQL、参数名和值。
    /// </summary>
    [Fact]
    public void From_WhenThreeSourcesAreJoinedInUnit_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var rootQuery = CreateMultiSourceQuery();

        // Act
        var query = BuildJoinMatrixQuery(rootQuery, 3);

        // Assert
        Assert.IsType<SqlLambdaQuery>(query);
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`permission`.`UserId` \r\nFrom `users`, `reviews` \r\nJoin `permissions` As `permission` On `reviews`.`UserId`=`permission`.`UserId` And `permission`.`UserId`>@_p_0 \r\nWhere `users`.`Id`=`reviews`.`UserId`", query.ToSql());
        Assert.Equal(new[] { "@_p_0" }, query.GetParams().Keys);
        Assert.Equal(new object[] { 3 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：四来源连续 Join 应绑定中间来源和最后一个重复实体来源，并输出完整别名 SQL。
    /// </summary>
    [Fact]
    public void From_WhenFourSourcesAreJoinedInUnit_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var rootQuery = CreateMultiSourceQuery();

        // Act
        var query = BuildJoinMatrixQuery(rootQuery, 4);

        // Assert
        Assert.IsType<SqlLambdaQuery>(query);
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`permission`.`UserId`,`reviewer`.`Id` \r\nFrom `users`, `reviews` \r\nJoin `permissions` As `permission` On `reviews`.`UserId`=`permission`.`UserId` \r\nJoin `users` As `reviewer` On `permission`.`UserId`=`reviewer`.`Id` And `reviewer`.`Id`>@_p_0 \r\nWhere `users`.`Id`=`reviews`.`UserId`", query.ToSql());
        Assert.Equal(new[] { "@_p_0" }, query.GetParams().Keys);
        Assert.Equal(new object[] { 4 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：五来源连续 Join 应保持来源顺序、重复实体别名和尾来源参数绑定。
    /// </summary>
    [Fact]
    public void From_WhenFiveSourcesAreJoinedInUnit_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var rootQuery = CreateMultiSourceQuery();

        // Act
        var query = BuildJoinMatrixQuery(rootQuery, 5);

        // Assert
        Assert.IsType<SqlLambdaQuery>(query);
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`permission`.`UserId`,`reviewer`.`Id`,`review2`.`UserId` \r\nFrom `users`, `reviews` \r\nJoin `permissions` As `permission` On `reviews`.`UserId`=`permission`.`UserId` \r\nJoin `users` As `reviewer` On `permission`.`UserId`=`reviewer`.`Id` \r\nJoin `reviews` As `review2` On `reviewer`.`Id`=`review2`.`UserId` And `review2`.`UserId`>@_p_0 \r\nWhere `users`.`Id`=`reviews`.`UserId`", query.ToSql());
        Assert.Equal(new[] { "@_p_0" }, query.GetParams().Keys);
        Assert.Equal(new object[] { 5 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：六来源连续 Join 应绑定第六个来源，并保持前序参数和完整 SQL 不变。
    /// </summary>
    [Fact]
    public void From_WhenSixSourcesAreJoinedInUnit_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var rootQuery = CreateMultiSourceQuery();

        // Act
        var query = BuildJoinMatrixQuery(rootQuery, 6);

        // Assert
        Assert.IsType<SqlLambdaQuery>(query);
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`permission`.`UserId`,`reviewer`.`Id`,`review2`.`UserId`,`permission2`.`UserId` \r\nFrom `users`, `reviews` \r\nJoin `permissions` As `permission` On `reviews`.`UserId`=`permission`.`UserId` \r\nJoin `users` As `reviewer` On `permission`.`UserId`=`reviewer`.`Id` \r\nJoin `reviews` As `review2` On `reviewer`.`Id`=`review2`.`UserId` \r\nJoin `permissions` As `permission2` On `review2`.`UserId`=`permission2`.`UserId` And `permission2`.`UserId`>@_p_0 \r\nWhere `users`.`Id`=`reviews`.`UserId`", query.ToSql());
        Assert.Equal(new[] { "@_p_0" }, query.GetParams().Keys);
        Assert.Equal(new object[] { 6 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：七来源连续 Join 应绑定首、中、尾来源，覆盖重复实体和连续参数位置。
    /// </summary>
    [Fact]
    public void From_WhenSevenSourcesAreJoinedInUnit_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var rootQuery = CreateMultiSourceQuery();

        // Act
        var query = BuildJoinMatrixQuery(rootQuery, 7);

        // Assert
        Assert.IsType<SqlLambdaQuery>(query);
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`permission`.`UserId`,`reviewer`.`Id`,`review2`.`UserId`,`permission2`.`UserId`,`reviewer2`.`Id` \r\nFrom `users`, `reviews` \r\nJoin `permissions` As `permission` On `reviews`.`UserId`=`permission`.`UserId` \r\nJoin `users` As `reviewer` On `permission`.`UserId`=`reviewer`.`Id` \r\nJoin `reviews` As `review2` On `reviewer`.`Id`=`review2`.`UserId` \r\nJoin `permissions` As `permission2` On `review2`.`UserId`=`permission2`.`UserId` \r\nJoin `users` As `reviewer2` On `permission2`.`UserId`=`reviewer2`.`Id` And `reviewer2`.`Id`>@_p_0 \r\nWhere `users`.`Id`=`reviews`.`UserId`", query.ToSql());
        Assert.Equal(new[] { "@_p_0" }, query.GetParams().Keys);
        Assert.Equal(new object[] { 7 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：八来源连续 Join 应保持中间来源参数位置和最后重复实体别名。
    /// </summary>
    [Fact]
    public void From_WhenEightSourcesAreJoinedInUnit_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var rootQuery = CreateMultiSourceQuery();

        // Act
        var query = BuildJoinMatrixQuery(rootQuery, 8);

        // Assert
        Assert.IsType<SqlLambdaQuery>(query);
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`permission`.`UserId`,`reviewer`.`Id`,`review2`.`UserId`,`permission2`.`UserId`,`reviewer2`.`Id`,`review3`.`UserId` \r\nFrom `users`, `reviews` \r\nJoin `permissions` As `permission` On `reviews`.`UserId`=`permission`.`UserId` \r\nJoin `users` As `reviewer` On `permission`.`UserId`=`reviewer`.`Id` \r\nJoin `reviews` As `review2` On `reviewer`.`Id`=`review2`.`UserId` \r\nJoin `permissions` As `permission2` On `review2`.`UserId`=`permission2`.`UserId` \r\nJoin `users` As `reviewer2` On `permission2`.`UserId`=`reviewer2`.`Id` \r\nJoin `reviews` As `review3` On `reviewer2`.`Id`=`review3`.`UserId` And `review3`.`UserId`>@_p_0 \r\nWhere `users`.`Id`=`reviews`.`UserId`", query.ToSql());
        Assert.Equal(new[] { "@_p_0" }, query.GetParams().Keys);
        Assert.Equal(new object[] { 8 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：九来源连续 Join 应绑定最后一个权限来源，并保持完整参数序列。
    /// </summary>
    [Fact]
    public void From_WhenNineSourcesAreJoinedInUnit_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var rootQuery = CreateMultiSourceQuery();

        // Act
        var query = BuildJoinMatrixQuery(rootQuery, 9);

        // Assert
        Assert.IsType<SqlLambdaQuery>(query);
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`permission`.`UserId`,`reviewer`.`Id`,`review2`.`UserId`,`permission2`.`UserId`,`reviewer2`.`Id`,`review3`.`UserId`,`permission3`.`UserId` \r\nFrom `users`, `reviews` \r\nJoin `permissions` As `permission` On `reviews`.`UserId`=`permission`.`UserId` \r\nJoin `users` As `reviewer` On `permission`.`UserId`=`reviewer`.`Id` \r\nJoin `reviews` As `review2` On `reviewer`.`Id`=`review2`.`UserId` \r\nJoin `permissions` As `permission2` On `review2`.`UserId`=`permission2`.`UserId` \r\nJoin `users` As `reviewer2` On `permission2`.`UserId`=`reviewer2`.`Id` \r\nJoin `reviews` As `review3` On `reviewer2`.`Id`=`review3`.`UserId` \r\nJoin `permissions` As `permission3` On `review3`.`UserId`=`permission3`.`UserId` And `permission3`.`UserId`>@_p_0 \r\nWhere `users`.`Id`=`reviews`.`UserId`", query.ToSql());
        Assert.Equal(new[] { "@_p_0" }, query.GetParams().Keys);
        Assert.Equal(new object[] { 9 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：十来源连续 Join 应到达公开上限，绑定首、中、尾来源并保持不存在第十一来源 Join。
    /// </summary>
    [Fact]
    public void From_WhenTenSourcesAreJoinedInUnit_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var rootQuery = CreateMultiSourceQuery();

        // Act
        var query = BuildJoinMatrixQuery(rootQuery, 10);

        // Assert
        Assert.IsType<SqlLambdaQuery>(query);
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`permission`.`UserId`,`reviewer`.`Id`,`review2`.`UserId`,`permission2`.`UserId`,`reviewer2`.`Id`,`review3`.`UserId`,`permission3`.`UserId`,`reviewer3`.`Id` \r\nFrom `users`, `reviews` \r\nJoin `permissions` As `permission` On `reviews`.`UserId`=`permission`.`UserId` \r\nJoin `users` As `reviewer` On `permission`.`UserId`=`reviewer`.`Id` \r\nJoin `reviews` As `review2` On `reviewer`.`Id`=`review2`.`UserId` \r\nJoin `permissions` As `permission2` On `review2`.`UserId`=`permission2`.`UserId` \r\nJoin `users` As `reviewer2` On `permission2`.`UserId`=`reviewer2`.`Id` \r\nJoin `reviews` As `review3` On `reviewer2`.`Id`=`review3`.`UserId` \r\nJoin `permissions` As `permission3` On `review3`.`UserId`=`permission3`.`UserId` \r\nJoin `users` As `reviewer3` On `permission3`.`UserId`=`reviewer3`.`Id` And `reviewer3`.`Id`>@_p_0 \r\nWhere `users`.`Id`=`reviews`.`UserId`", query.ToSql());
        Assert.Equal(new[] { "@_p_0" }, query.GetParams().Keys);
        Assert.Equal(new object[] { 10 }, query.GetParams().Values.ToArray());
    }

    private static SqlLambdaQuery BuildJoinMatrixQuery(ISqlQuery rootQuery, int sourceCount)
    {
        if (sourceCount < 3 || sourceCount > 10)
            throw new ArgumentOutOfRangeException(nameof(sourceCount));

        var query = rootQuery.From<MultiSourceUser>().From<MultiSourceReview>();
        if (sourceCount == 3)
            query.Join<MultiSourceReview, MultiSourcePermission>((review, permission) =>
                review.UserId == permission.UserId && permission.UserId > 3,
                new SqlJoinOptions { RightAlias = "permission", LeftAlias = "reviews" });
        else
            query.Join<MultiSourceReview, MultiSourcePermission>((review, permission) =>
                review.UserId == permission.UserId,
                new SqlJoinOptions { RightAlias = "permission", LeftAlias = "reviews" });

        if (sourceCount == 4)
            query.Join<MultiSourcePermission, MultiSourceUser>((permission, reviewer) =>
                permission.UserId == reviewer.Id && reviewer.Id > 4,
                new SqlJoinOptions { RightAlias = "reviewer", LeftAlias = "permission" });
        else if (sourceCount >= 4)
            query.Join<MultiSourcePermission, MultiSourceUser>((permission, reviewer) =>
                permission.UserId == reviewer.Id,
                new SqlJoinOptions { RightAlias = "reviewer", LeftAlias = "permission" });
        if (sourceCount == 5)
            query.Join<MultiSourceUser, MultiSourceReview>((reviewer, review2) =>
                reviewer.Id == review2.UserId && review2.UserId > 5,
                new SqlJoinOptions { RightAlias = "review2", LeftAlias = "reviewer" });
        else if (sourceCount >= 5)
            query.Join<MultiSourceUser, MultiSourceReview>((reviewer, review2) =>
                reviewer.Id == review2.UserId,
                new SqlJoinOptions { RightAlias = "review2", LeftAlias = "reviewer" });
        if (sourceCount == 6)
            query.Join<MultiSourceReview, MultiSourcePermission>((review2, permission2) =>
                review2.UserId == permission2.UserId && permission2.UserId > 6,
                new SqlJoinOptions { RightAlias = "permission2", LeftAlias = "review2" });
        else if (sourceCount >= 6)
            query.Join<MultiSourceReview, MultiSourcePermission>((review2, permission2) =>
                review2.UserId == permission2.UserId,
                new SqlJoinOptions { RightAlias = "permission2", LeftAlias = "review2" });
        if (sourceCount == 7)
            query.Join<MultiSourcePermission, MultiSourceUser>((permission2, reviewer2) =>
                permission2.UserId == reviewer2.Id && reviewer2.Id > 7,
                new SqlJoinOptions { RightAlias = "reviewer2", LeftAlias = "permission2" });
        else if (sourceCount >= 7)
            query.Join<MultiSourcePermission, MultiSourceUser>((permission2, reviewer2) =>
                permission2.UserId == reviewer2.Id,
                new SqlJoinOptions { RightAlias = "reviewer2", LeftAlias = "permission2" });
        if (sourceCount == 8)
            query.Join<MultiSourceUser, MultiSourceReview>((reviewer2, review3) =>
                reviewer2.Id == review3.UserId && review3.UserId > 8,
                new SqlJoinOptions { RightAlias = "review3", LeftAlias = "reviewer2" });
        else if (sourceCount >= 8)
            query.Join<MultiSourceUser, MultiSourceReview>((reviewer2, review3) =>
                reviewer2.Id == review3.UserId,
                new SqlJoinOptions { RightAlias = "review3", LeftAlias = "reviewer2" });
        if (sourceCount == 9)
            query.Join<MultiSourceReview, MultiSourcePermission>((review3, permission3) =>
                review3.UserId == permission3.UserId && permission3.UserId > 9,
                new SqlJoinOptions { RightAlias = "permission3", LeftAlias = "review3" });
        else if (sourceCount >= 9)
            query.Join<MultiSourceReview, MultiSourcePermission>((review3, permission3) =>
                review3.UserId == permission3.UserId,
                new SqlJoinOptions { RightAlias = "permission3", LeftAlias = "review3" });
        if (sourceCount >= 10)
            query.Join<MultiSourcePermission, MultiSourceUser>((permission3, reviewer3) =>
                permission3.UserId == reviewer3.Id && reviewer3.Id > 10,
                new SqlJoinOptions { RightAlias = "reviewer3", LeftAlias = "permission3" });

        query.Where<MultiSourceUser, MultiSourceReview>((users, reviews) => users.Id == reviews.UserId, "users", "reviews")
            .Select<MultiSourceUser, MultiSourceReview>((users, reviews) => new object[] { users.Id, reviews.UserId }, "users", "reviews");
        if (sourceCount >= 3)
            query.AppendSelect<MultiSourcePermission>(permission => new object[] { permission.UserId }, "permission");
        if (sourceCount >= 4)
            query.AppendSelect<MultiSourceUser>(reviewer => new object[] { reviewer.Id }, "reviewer");
        if (sourceCount >= 5)
            query.AppendSelect<MultiSourceReview>(review2 => new object[] { review2.UserId }, "review2");
        if (sourceCount >= 6)
            query.AppendSelect<MultiSourcePermission>(permission2 => new object[] { permission2.UserId }, "permission2");
        if (sourceCount >= 7)
            query.AppendSelect<MultiSourceUser>(reviewer2 => new object[] { reviewer2.Id }, "reviewer2");
        if (sourceCount >= 8)
            query.AppendSelect<MultiSourceReview>(review3 => new object[] { review3.UserId }, "review3");
        if (sourceCount >= 9)
            query.AppendSelect<MultiSourcePermission>(permission3 => new object[] { permission3.UserId }, "permission3");
        if (sourceCount >= 10)
            query.AppendSelect<MultiSourceUser>(reviewer3 => new object[] { reviewer3.Id }, "reviewer3");
        return query;
    }

    private static ISqlQuery CreateMultiSourceQuery()
    {
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceUser),
                DbKey = "sqlite",
                TableName = "users"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourceReview),
                DbKey = "sqlite",
                TableName = "reviews"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MultiSourcePermission),
                DbKey = "sqlite",
                TableName = "permissions"
            });
        });
        services.AddSqliteProvider();
        services.AddSqlDataSource("sqlite", Bing.Data.Enums.DatabaseType.Sqlite, "Data Source=:memory:");
        return services.BuildServiceProvider().GetRequiredService<ISqlQueryFactory>().Create("sqlite");
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

    /// <summary>
    /// 双表查询的用户来源样例。
    /// </summary>
    private sealed class MultiSourceUser
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 双表查询的审核来源样例。
    /// </summary>
    private sealed class MultiSourceReview
    {
        /// <summary>
        /// 用户标识。
        /// </summary>
        public int UserId { get; set; }
    }

    /// <summary>
    /// 多表查询的权限来源样例。
    /// </summary>
    private sealed class MultiSourcePermission
    {
        /// <summary>
        /// 用户标识。
        /// </summary>
        public int UserId { get; set; }
    }

    /// <summary>
    /// 多表投影结果样例。
    /// </summary>
    private sealed class MultiSourceProjection
    {
        /// <summary>
        /// 所有者标识。
        /// </summary>
        public int OwnerId { get; set; }

        /// <summary>
        /// 审核用户标识。
        /// </summary>
        public int ReviewUserId { get; set; }

        /// <summary>
        /// 未投影的嵌套 DTO。
        /// </summary>
        public NestedProjection Profile { get; set; }
    }

    /// <summary>
    /// 七表派生查询的投影模型。
    /// </summary>
    private sealed class HighArityProjection
    {
        /// <summary>
        /// 首个来源标识。
        /// </summary>
        public int FirstId { get; set; }

        /// <summary>
        /// 第四个来源标识。
        /// </summary>
        public int FourthId { get; set; }

        /// <summary>
        /// 第七个来源标识。
        /// </summary>
        public int SeventhId { get; set; }

        /// <summary>
        /// 未投影成员。
        /// </summary>
        public int UnprojectedId { get; set; }
    }

    /// <summary>
    /// 嵌套成员白名单绕过测试样例。
    /// </summary>
    private sealed class NestedProjection
    {
        /// <summary>
        /// 与外层已投影成员同名的属性。
        /// </summary>
        public int OwnerId { get; set; }
    }

    /// <summary>
    /// 按租户解析同一逻辑数据源键的 SQLite 数据源。
    /// </summary>
    private sealed class TenantSqliteDataSourceResolver : ISqlDataSourceResolver
    {
        private readonly string _firstConnectionString;
        private readonly string _secondConnectionString;

        /// <summary>
        /// 初始化按租户选择物理 SQLite 数据库的解析器。
        /// </summary>
        public TenantSqliteDataSourceResolver(string firstConnectionString, string secondConnectionString)
        {
            _firstConnectionString = firstConnectionString;
            _secondConnectionString = secondConnectionString;
        }

        /// <inheritdoc />
        public SqlDataSourceDescriptor Resolve(string dbKey = null, DatabaseScopeOptions options = null)
        {
            if (string.Equals(dbKey ?? options?.DbKey, "tenant", StringComparison.OrdinalIgnoreCase) == false)
                throw new InvalidOperationException("仅支持 tenant 数据源。");
            var connectionString = options?.ReadPreference == SqlReadPreference.Primary
                ? _secondConnectionString
                : _firstConnectionString;
            return new SqlDataSourceDescriptor
            {
                Key = "tenant",
                DatabaseType = Bing.Data.Enums.DatabaseType.Sqlite,
                ConnectionString = connectionString
            };
        }
    }

    /// <summary>
    /// 记录物理数据库身份解析调用的测试解析器。
    /// </summary>
    private sealed class TrackingDatabaseIdentityResolver : ISqlDatabaseIdentityResolver
    {
        private readonly DefaultSqlDatabaseIdentityResolver _inner = new();

        /// <summary>
        /// 解析调用次数。
        /// </summary>
        public int CallCount { get; private set; }

        /// <inheritdoc />
        public SqlDatabaseIdentity Resolve(Bing.Data.Enums.DatabaseType databaseType, string connectionString)
        {
            CallCount++;
            return _inner.Resolve(databaseType, connectionString);
        }
    }

    /// <summary>
    /// 空成员初始化投影样例。
    /// </summary>
    private sealed class EmptyProjection
    {
    }
}

internal static class QueryDescriptionTestExtensions
{
    public static IReadOnlyDictionary<string, object> GetParams(this object source)
    {
        var accessor = source.GetType().GetInterface("Bing.Data.Sql.ISqlQueryBuilderAccessor");
        if (accessor == null)
            throw new ArgumentException("查询描述未提供测试所需的 Builder 访问契约。", nameof(source));
        var builder = accessor.GetMethod("GetSqlBuilder", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.Invoke(source, null);
        var commonPart = builder?.GetType().GetInterface("Bing.Data.Sql.Builders.ISqlCommonPartAccessor");
        var parameterManager = commonPart?.GetProperty("ParameterManager")?.GetValue(builder);
        var parameters = parameterManager?.GetType().GetMethod("GetParams", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.Invoke(parameterManager, null);
        return parameters as IReadOnlyDictionary<string, object> ??
            throw new InvalidOperationException("查询描述未提供参数读取能力。");
    }
}