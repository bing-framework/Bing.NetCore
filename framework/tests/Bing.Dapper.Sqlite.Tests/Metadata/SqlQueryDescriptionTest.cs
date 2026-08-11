using System.Data;
using Bing.Data.Sql;
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
        var first = rootQuery.Query<int>().Select("Id").From("users").Where("Id", 1);
        var second = rootQuery.Query<string>().Select("Name").From("users").Where("Name", "Bing");

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
        var query = rootQuery.Text<int>("Select * From users Where Id = @Id", parameters);
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
        var text = rootQuery.Text<int>("Select 1", parameters);
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
        var description = rootQuery.TextInterpolated<string>(
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
        var exception = Assert.Throws<NotSupportedException>(() => rootQuery.TextInterpolated<int>(
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
        var query = rootQuery.Query<QueryParameterSample>().AddParam("status", (QueryParameterSample item) => item.Status, "active");
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
        var query = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Select((user, review) => new object[] { user.Id, review.UserId })
            .Where((user, review) => user.Id == review.UserId);

        // Assert
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId` \r\nFrom `users`, `reviews` \r\nWhere `users`.`Id`=`reviews`.`UserId`", query.ToSql());
    }

    /// <summary>
    /// 测试目的：多表投影应在保持参数位置列绑定的同时切换到指定的结果映射类型，As 不应改变既有 SQL。
    /// </summary>
    [Fact]
    public void From_WhenProjectionResultTypeSelected_ShouldKeepBoundSqlAndTransitionResultType()
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
        var projection = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Select<MultiSourceProjection>((user, review) => new object[] { user.Id, review.UserId });
        var transition = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Select((user, review) => new object[] { user.Id, review.UserId })
            .As<MultiSourceProjection>();

        // Assert
        Assert.IsType<SqlQuery<MultiSourceProjection>>(projection);
        Assert.IsType<SqlQuery<MultiSourceProjection>>(transition);
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId` \r\nFrom `users`, `reviews`", projection.ToSql());
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId` \r\nFrom `users`, `reviews`", transition.ToSql());
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
        var query = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .SelectDto((user, review) => new MultiSourceProjection
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
        var query = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Select((user, review) => new object[] { user.Id, review.UserId });

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => query.SelectDto((user, review) =>
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
            .Select(user => new object[] { user.Id })
            .Where(user => user.Id > 7);
        var multi = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Select((user, review) => new object[] { user.Id, review.UserId })
            .Where((user, review) => review.UserId > 9);
        var singleSql = single.ToSql();
        var multiSql = multi.ToSql();

        // Act
        var singleException = Assert.Throws<NotSupportedException>(() => single.SelectSubquery(
            user => new EmptyProjection { }, "empty"));
        var multiException = Assert.Throws<NotSupportedException>(() => multi.SelectDto(
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
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        var derivedRoot = rootQuery.From(subquery);
        var originalSql = derivedRoot.ToSql();
        var joined = rootQuery.From<MultiSourceReview>().Join(subquery);
        var joinedSql = joined.ToSql();

        // Act
        var whereException = Assert.Throws<NotSupportedException>(() => derivedRoot.Where(
            summary => summary.Profile.OwnerId > 0));
        var selectException = Assert.Throws<NotSupportedException>(() => derivedRoot.Select(
            summary => new object[] { summary.Profile.OwnerId }));
        var orderException = Assert.Throws<NotSupportedException>(() => derivedRoot.OrderBy(
            summary => new object[] { summary.Profile.OwnerId }));
        var onException = Assert.Throws<NotSupportedException>(() => joined.On(
            (review, summary) => review.UserId == summary.Profile.OwnerId));

        // Assert
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", whereException.Message);
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", selectException.Message);
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", orderException.Message);
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", onException.Message);
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
        var subquery = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Where((user, review) => user.Id > 7)
            .SelectSubquery((user, review) => new MultiSourceProjection
            {
                OwnerId = user.Id,
                ReviewUserId = review.UserId
            }, "summary");

        // Act
        var query = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Join(subquery)
            .On((user, review, summary) => user.Id == summary.OwnerId && review.UserId == summary.ReviewUserId)
            .Select((user, review, summary) => new object[] { user.Id, review.UserId, summary.OwnerId });

        // Assert
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`summary`.`OwnerId` \r\nFrom `users`, `reviews` \r\nJoin (Select `users`.`Id` As `OwnerId`,`reviews`.`UserId` As `ReviewUserId` \r\nFrom `users`, `reviews` \r\nWhere `users`.`Id`>@_p_0) As `summary` On `users`.`Id`=`summary`.`OwnerId` And `reviews`.`UserId`=`summary`.`ReviewUserId`", query.ToSql());
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
            .Where(user => user.Id > 7)
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var query = rootQuery.From<MultiSourceReview>()
            .Join(subquery)
            .On((review, summary) => review.UserId == summary.OwnerId)
            .Select((review, summary) => new object[] { review.UserId, summary.OwnerId });

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
            .Where(user => user.Id > 7)
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var query = rootQuery.From<MultiSourceReview>()
            .CrossJoin(subquery)
            .Where((review, summary) => review.UserId == summary.OwnerId)
            .Select((review, summary) => new object[] { review.UserId, summary.OwnerId });
        var crossJoinWithOn = rootQuery.From<MultiSourceReview>().CrossJoin(subquery);

        // Assert
        Assert.Equal("Select `reviews`.`UserId`,`summary`.`OwnerId` \r\nFrom `reviews` \r\nCross Join (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `summary` \r\nWhere `reviews`.`UserId`=`summary`.`OwnerId`", query.ToSql());
        Assert.Equal(7, query.GetParams().Values.Single());
        var exception = Assert.Throws<InvalidOperationException>(() => crossJoinWithOn.On(
            (review, summary) => review.UserId == summary.OwnerId));
        Assert.Equal("Cross Join 不支持 On 条件。", exception.Message);
    }

    /// <summary>
    /// 测试目的：单表实体查询应能以 Cross Join 进入双表 Lambda 链；SQLite 对 Right Join 和 Full Join 仍须在渲染前拒绝。
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
            .Where((user, review) => user.Id == review.UserId)
            .Select((user, review) => new object[] { user.Id, review.UserId });
        var crossJoinWithOn = rootQuery.From<MultiSourceUser>().CrossJoin<MultiSourceReview>("review");
        var rightJoin = rootQuery.From<MultiSourceUser>()
            .RightJoin<MultiSourceReview>("review")
            .On((user, review) => user.Id == review.UserId);
        var fullJoin = rootQuery.From<MultiSourceUser>()
            .FullJoin<MultiSourceReview>("review")
            .On((user, review) => user.Id == review.UserId);

        // Assert
        Assert.Equal("Select `users`.`Id`,`review`.`UserId` \r\nFrom `users` \r\nCross Join `reviews` As `review` \r\nWhere `users`.`Id`=`review`.`UserId`", crossJoin.ToSql());
        var onException = Assert.Throws<InvalidOperationException>(() => crossJoinWithOn.On(
            (user, review) => user.Id == review.UserId));
        var rightJoinException = Assert.Throws<NotSupportedException>(() => rightJoin.ToSql());
        var fullJoinException = Assert.Throws<NotSupportedException>(() => fullJoin.ToSql());
        Assert.Equal("Cross Join 不支持 On 条件。", onException.Message);
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Right Join。", rightJoinException.Message);
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Full Join。", fullJoinException.Message);
    }

    /// <summary>
    /// 测试目的：SQLite 不支持的单表类型化派生表 Right Join 和 Full Join 应在渲染前被能力门禁拒绝。
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
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var rightJoin = rootQuery.From<MultiSourceReview>()
            .RightJoin(subquery)
            .On((review, summary) => review.UserId == summary.OwnerId);
        var fullJoin = rootQuery.From<MultiSourceReview>()
            .FullJoin(subquery)
            .On((review, summary) => review.UserId == summary.OwnerId);

        // Assert
        var rightJoinException = Assert.Throws<NotSupportedException>(() => rightJoin.ToSql());
        var fullJoinException = Assert.Throws<NotSupportedException>(() => fullJoin.ToSql());
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
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        var query = rootQuery.From<MultiSourceReview>().LeftJoin(subquery);
        var expectedSql = query.ToSql();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => query.Where(
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
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        var outer = secondQuery.From<MultiSourceReview>();
        var expectedSql = outer.ToSql();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => outer.Join(subquery));

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
                .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
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
            var exception = Assert.Throws<NotSupportedException>(() => outer.Join(subquery));

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
                    .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
            ISqlQuery outerQuery;
            using (databaseScopeManager.Use(new DatabaseScopeOptions { DbKey = "tenant", TenantId = "tenant-b" }))
                outerQuery = queryFactory.Create("tenant");
            using (outerQuery)
            {
                var outer = outerQuery.From<MultiSourceReview>();
                var expectedSql = outer.ToSql();

                // Act
                var joinException = Assert.Throws<NotSupportedException>(() => outer.Join(subquery));
                var rootException = Assert.Throws<NotSupportedException>(() => outerQuery.From(subquery));

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
                .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
            var outer = outerQuery.From<MultiSourceReview>();
            var expectedSql = outer.ToSql();

            // Act
            var exception = Assert.Throws<NotSupportedException>(() => outer.Join(subquery));

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
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var query = secondQuery.From<MultiSourceReview>()
            .Join(subquery)
            .On((review, summary) => review.UserId == summary.OwnerId)
            .Select((review, summary) => new object[] { review.UserId, summary.OwnerId });

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
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        var outer = outerQuery.From<MultiSourceReview>();
        var expectedSql = outer.ToSql();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => outer.Join(subquery));

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
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var query = rootQuery.From(subquery)
            .Select(summary => new object[] { summary.OwnerId });

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
            .Where(user => user.Id > 7)
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var query = rootQuery.From(subquery)
            .Where(summary => summary.OwnerId > 9)
            .Select(summary => new object[] { summary.OwnerId });

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
            .Where(user => user.Id > 7)
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        var query = rootQuery.From(subquery)
            .Where(summary => summary.OwnerId > 9);
        var expectedSql = query.ToSql();
        var expectedParameters = query.GetParams();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.Join(subquery));

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
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "summary");

        // Act
        var dataSourceException = Assert.Throws<NotSupportedException>(() => secondQuery.From(subquery));
        var query = firstQuery.From(subquery);
        var expectedSql = query.ToSql();
        var memberException = Assert.Throws<NotSupportedException>(() => query.Where(summary => summary.ReviewUserId > 0));

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
            .Where(user => user.Id > 7)
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "owner");
        var review = rootQuery.From(owner)
            .Where(summary => summary.OwnerId > 9)
            .SelectSubquery(summary => new MultiSourceProjection { ReviewUserId = summary.OwnerId }, "review");

        // Act
        var query = rootQuery.From(review)
            .Where(summary => summary.ReviewUserId > 11)
            .Select(summary => new object[] { summary.ReviewUserId });
        var expectedSql = query.ToSql();
        var exception = Assert.Throws<NotSupportedException>(() => query.Where(summary => summary.OwnerId > 13));

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
            .Where(user => user.Id > 7)
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "owner");
        var joined = rootQuery.From(owner)
            .LeftJoin<MultiSourceReview>("review")
            .On((summary, review) => summary.OwnerId == review.UserId)
            .Where((summary, review) => summary.OwnerId > 9);
        var refined = joined.SelectSubquery((summary, review) => new MultiSourceProjection
        {
            OwnerId = summary.OwnerId,
            ReviewUserId = review.UserId
        }, "refined");
        joined.Where((summary, review) => review.UserId > 11);

        // Act
        var query = rootQuery.From(refined)
            .Where(summary => summary.OwnerId > 13)
            .Select(summary => new object[] { summary.OwnerId, summary.ReviewUserId });

        // Assert
        Assert.Equal("Select `refined`.`OwnerId`,`refined`.`ReviewUserId` \r\nFrom (Select `owner`.`OwnerId` As `OwnerId`,`review`.`UserId` As `ReviewUserId` \r\nFrom (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `owner` \r\nLeft Join `reviews` As `review` On `owner`.`OwnerId`=`review`.`UserId` \r\nWhere `owner`.`OwnerId`>@_p_1) As `refined` \r\nWhere `refined`.`OwnerId`>@_p_2",
            query.ToSql());
        Assert.Equal(new object[] { 7, 9, 13 }, query.GetParams().Values.ToArray());
    }

    /// <summary>
    /// 测试目的：严格 DTO 根经混合 Join 链扩展到七来源后再次冻结时，应按参数位置保留来源顺序、别名和投影白名单。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryRootBuildsSevenSourceMixedJoinChainAndReprojected_ShouldKeepSourceOrderAliasesAndWhitelist()
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
            .Where(user => user.Id > 7)
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "owner");
        var summary = rootQuery.From(owner)
            .Join<MultiSourceReview>("review")
            .On((item, review) => item.OwnerId == review.UserId)
            .CrossJoin<MultiSourcePermission>("permission")
            .LeftJoin<MultiSourceUser>("reviewer")
            .On((item, review, permission, reviewer) => reviewer.Id == permission.UserId)
            .Join<MultiSourceReview>("review2")
            .On((item, review, permission, reviewer, review2) => reviewer.Id == review2.UserId)
            .CrossJoin<MultiSourcePermission>("permission2")
            .Join<MultiSourceUser>("lastUser")
            .On((item, review, permission, reviewer, review2, permission2, lastUser) =>
                lastUser.Id == permission2.UserId)
            .Where((item, review, permission, reviewer, review2, permission2, lastUser) =>
                item.OwnerId > 9 && review.UserId == lastUser.Id)
            .SelectSubquery((item, review, permission, reviewer, review2, permission2, lastUser) =>
                new HighArityProjection
                {
                    FirstId = item.OwnerId,
                    FourthId = reviewer.Id,
                    SeventhId = lastUser.Id
                }, "summary");

        // Act
        var query = rootQuery.From(summary)
            .Where(item => item.SeventhId > 13)
            .Select(item => new object[] { item.FirstId, item.FourthId, item.SeventhId });
        var expectedSql = query.ToSql();
        var expectedParameters = query.GetParams();
        var exception = Assert.Throws<NotSupportedException>(() => query.Where(item => item.UnprojectedId > 0));

        // Assert
        Assert.Equal("Select `summary`.`FirstId`,`summary`.`FourthId`,`summary`.`SeventhId` \r\nFrom (Select `owner`.`OwnerId` As `FirstId`,`reviewer`.`Id` As `FourthId`,`lastUser`.`Id` As `SeventhId` \r\nFrom (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `owner` \r\nJoin `reviews` As `review` On `owner`.`OwnerId`=`review`.`UserId` \r\nCross Join `permissions` As `permission` \r\nLeft Join `users` As `reviewer` On `reviewer`.`Id`=`permission`.`UserId` \r\nJoin `reviews` As `review2` On `reviewer`.`Id`=`review2`.`UserId` \r\nCross Join `permissions` As `permission2` \r\nJoin `users` As `lastUser` On `lastUser`.`Id`=`permission2`.`UserId` \r\nWhere `owner`.`OwnerId`>@_p_1 And `review`.`UserId`=`lastUser`.`Id`) As `summary` \r\nWhere `summary`.`SeventhId`>@_p_2",
            expectedSql);
        Assert.Equal(new object[] { 7, 9, 13 }, expectedParameters.Values.ToArray());
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", exception.Message);
        Assert.Equal(expectedSql, query.ToSql());
        Assert.Equal(expectedParameters, query.GetParams());
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
            .Where(user => user.Id > 7)
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "owner");
        var review = rootQuery.From<MultiSourceReview>()
            .Where(item => item.UserId > 11)
            .SelectSubquery(item => new MultiSourceProjection { ReviewUserId = item.UserId }, "review");

        // Act
        var entityCrossJoin = rootQuery.From(owner)
            .CrossJoin<MultiSourceReview>("entityReview")
            .Where((summary, item) => summary.OwnerId == item.UserId)
            .Select((summary, item) => new object[] { summary.OwnerId, item.UserId });
        var derivedCrossJoin = rootQuery.From(owner)
            .CrossJoin(review)
            .Where((summary, item) => summary.OwnerId == item.ReviewUserId)
            .Select((summary, item) => new object[] { summary.OwnerId, item.ReviewUserId });
        var crossJoinWithOn = rootQuery.From(owner).CrossJoin<MultiSourceReview>("entityReview");

        // Assert
        Assert.Equal("Select `owner`.`OwnerId`,`entityReview`.`UserId` \r\nFrom (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `owner` \r\nCross Join `reviews` As `entityReview` \r\nWhere `owner`.`OwnerId`=`entityReview`.`UserId`", entityCrossJoin.ToSql());
        Assert.Equal(new object[] { 7 }, entityCrossJoin.GetParams().Values.ToArray());
        Assert.Equal("Select `owner`.`OwnerId`,`review`.`ReviewUserId` \r\nFrom (Select `users`.`Id` As `OwnerId` \r\nFrom `users` \r\nWhere `users`.`Id`>@_p_0) As `owner` \r\nCross Join (Select `reviews`.`UserId` As `ReviewUserId` \r\nFrom `reviews` \r\nWhere `reviews`.`UserId`>@_p_1) As `review` \r\nWhere `owner`.`OwnerId`=`review`.`ReviewUserId`", derivedCrossJoin.ToSql());
        Assert.Equal(new object[] { 7, 11 }, derivedCrossJoin.GetParams().Values.ToArray());
        var exception = Assert.Throws<InvalidOperationException>(() => crossJoinWithOn.On(
            (summary, item) => summary.OwnerId == item.UserId));
        Assert.Equal("Cross Join 不支持 On 条件。", exception.Message);
    }

    /// <summary>
    /// 测试目的：SQLite 不支持的根派生表 Right Join 和 Full Join 应在渲染前拒绝，且实体和派生表连接入口遵循同一能力门禁。
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
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "owner");
        var review = rootQuery.From<MultiSourceReview>()
            .SelectSubquery(item => new MultiSourceProjection { ReviewUserId = item.UserId }, "review");

        // Act
        var entityRightJoin = rootQuery.From(owner)
            .RightJoin<MultiSourceReview>("entityReview")
            .On((summary, item) => summary.OwnerId == item.UserId);
        var derivedFullJoin = rootQuery.From(owner)
            .FullJoin(review)
            .On((summary, item) => summary.OwnerId == item.ReviewUserId);

        // Assert
        var rightJoinException = Assert.Throws<NotSupportedException>(() => entityRightJoin.ToSql());
        var fullJoinException = Assert.Throws<NotSupportedException>(() => derivedFullJoin.ToSql());
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Right Join。", rightJoinException.Message);
        Assert.Equal("Provider bing.sqlite 的当前查询能力配置不支持 Full Join。", fullJoinException.Message);
    }

    /// <summary>
    /// 测试目的：外层多表 Lambda 只能引用派生 DTO 显式投影的成员，且 Cross Join 继续拒绝 On 条件。
    /// </summary>
    [Fact]
    public void From_WhenDtoSubqueryMemberIsNotProjectedOrCrossJoined_ShouldRejectBeforeChangingOuterQuery()
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
        var subquery = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .SelectSubquery((user, review) => new MultiSourceProjection { ReviewUserId = review.UserId }, "summary");
        var query = rootQuery.From<MultiSourceUser, MultiSourceReview>().Join(subquery);
        var crossJoin = rootQuery.From<MultiSourceUser, MultiSourceReview>().CrossJoin(subquery);

        // Act
        var memberException = Assert.Throws<NotSupportedException>(() => query.Where(
            (user, review, summary) => summary.OwnerId == user.Id));
        var onException = Assert.Throws<InvalidOperationException>(() => crossJoin.On(
            (user, review, summary) => review.UserId == summary.ReviewUserId));

        // Assert
        Assert.Equal("多表派生表只能引用已投影的 DTO 成员。", memberException.Message);
        Assert.Equal("Cross Join 不支持 On 条件。", onException.Message);
        Assert.Equal("Select * \r\nFrom `users`, `reviews` \r\nJoin (Select `reviews`.`UserId` As `ReviewUserId` \r\nFrom `users`, `reviews`) As `summary`", query.ToSql());
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
        var subquery = firstQuery.From<MultiSourceUser, MultiSourceReview>()
            .SelectSubquery((user, review) => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        var outer = secondQuery.From<MultiSourceUser, MultiSourceReview>();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => outer.Join(subquery));

        // Assert
        Assert.Equal("类型化派生表数据源 first 与当前数据源 second 不兼容。", exception.Message);
        Assert.Equal("Select * \r\nFrom `users`, `reviews`", outer.ToSql());
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
        var source = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Where((user, review) => user.Id > 7);
        var subquery = source.SelectSubquery((user, review) => new MultiSourceProjection { OwnerId = user.Id }, "summary");
        source.Where((user, review) => review.UserId > 11);

        // Act
        var query = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Where((user, review) => user.Id > 3)
            .Join(subquery)
            .On((user, review, summary) => user.Id == summary.OwnerId);

        // Assert
        Assert.Equal("Select * \r\nFrom `users`, `reviews` \r\nJoin (Select `users`.`Id` As `OwnerId` \r\nFrom `users`, `reviews` \r\nWhere `users`.`Id`>@_p_1) As `summary` On `users`.`Id`=`summary`.`OwnerId` \r\nWhere `users`.`Id`>@_p_0", query.ToSql());
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
            .Where(review => review.UserId > 7)
            .SelectSubquery(review => new MultiSourceProjection { ReviewUserId = review.UserId }, "audit");
        var owner = rootQuery.From<MultiSourceUser>()
            .Where(user => user.Id > 11)
            .SelectSubquery(user => new MultiSourceProjection { OwnerId = user.Id }, "owner");

        // Act
        var query = rootQuery.From<MultiSourceUser>()
            .Where(user => user.Id > 3)
            .Join(audit)
            .On((user, review) => user.Id == review.ReviewUserId)
            .Join(owner)
            .On((user, review, summary) => user.Id == summary.OwnerId)
            .Select((user, review, summary) => new object[] { user.Id, review.ReviewUserId, summary.OwnerId });
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
        var summary = rootQuery.From<MultiSourceUser, MultiSourceReview, MultiSourcePermission, MultiSourceUser,
                MultiSourceReview, MultiSourcePermission, MultiSourceUser>()
            .Where((first, second, third, fourth, fifth, sixth, seventh) =>
                first.Id == seventh.Id && fifth.UserId == sixth.UserId)
            .SelectSubquery((first, second, third, fourth, fifth, sixth, seventh) => new HighArityProjection
            {
                FirstId = first.Id,
                FourthId = fourth.Id,
                SeventhId = seventh.Id
            }, "summary");
        var query = rootQuery.From(summary)
            .Where(item => item.SeventhId > 13)
            .Select(item => new object[] { item.FirstId, item.FourthId, item.SeventhId });

        // Assert
        Assert.Equal("Select `summary`.`FirstId`,`summary`.`FourthId`,`summary`.`SeventhId` \r\nFrom (Select `users`.`Id` As `FirstId`,`users_2`.`Id` As `FourthId`,`users_3`.`Id` As `SeventhId` \r\nFrom `users`, `reviews`, `permissions`, `users` As `users_2`, `reviews` As `reviews_2`, `permissions` As `permissions_2`, `users` As `users_3` \r\nWhere `users`.`Id`=`users_3`.`Id` And `reviews_2`.`UserId`=`permissions_2`.`UserId`) As `summary` \r\nWhere `summary`.`SeventhId`>@_p_0",
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
        var query = rootQuery.From<MultiSourceUser, MultiSourceUser>()
            .Select((owner, reviewer) => new object[] { owner.Id, reviewer.Id })
            .Where((owner, reviewer) => owner.Id == reviewer.Id);

        // Assert
        Assert.Equal("Select `users`.`Id`,`users_2`.`Id` \r\nFrom `users`, `users` As `users_2` \r\nWhere `users`.`Id`=`users_2`.`Id`", query.ToSql());
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
        var query = rootQuery.From<MultiSourceUser, MultiSourceUser>()
            .Select((owner, reviewer) => new object[] { owner.Id, reviewer.Id })
            .GroupBy((owner, reviewer) => new object[] { owner.Id, reviewer.Id })
            .OrderBy((owner, reviewer) => new object[] { reviewer.Id, owner.Id }, true);

        // Assert
        Assert.Equal("Select `users`.`Id`,`users_2`.`Id` \r\nFrom `users`, `users` As `users_2` \r\nGroup By `users`.`Id`,`users_2`.`Id` \r\nOrder By `users_2`.`Id` Desc,`users`.`Id` Desc", query.ToSql());
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
        var query = rootQuery.From<MultiSourceUser, MultiSourceUser>()
            .Select((owner, reviewer) => new object[] { owner.Id, reviewer.Id })
            .GroupBy((owner, reviewer) => new object[] { owner.Id, reviewer.Id })
            .Having((owner, reviewer) => reviewer.Id > 10 && owner.Id > 0);

        // Assert
        Assert.Equal("Select `users`.`Id`,`users_2`.`Id` \r\nFrom `users`, `users` As `users_2` \r\nGroup By `users`.`Id`,`users_2`.`Id` Having `users_2`.`Id`>@_p_0 And `users`.`Id`>@_p_1", query.ToSql());
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
        var query = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Join<MultiSourceUser>("reviewer")
            .On((owner, review, reviewer) => owner.Id == review.UserId && reviewer.Id == review.UserId && reviewer.Id > 7)
            .Join<MultiSourcePermission>("permission")
            .On((owner, review, reviewer, permission) => reviewer.Id == permission.UserId)
            .Select((owner, review, reviewer, permission) => new object[]
            {
                owner.Id, review.UserId, reviewer.Id, permission.UserId
            });

        // Assert
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`reviewer`.`Id`,`permission`.`UserId` \r\nFrom `users`, `reviews` \r\nJoin `users` As `reviewer` On `users`.`Id`=`reviews`.`UserId` And `reviewer`.`Id`=`reviews`.`UserId` And `reviewer`.`Id`>@_p_0 \r\nJoin `permissions` As `permission` On `reviewer`.`Id`=`permission`.`UserId`", query.ToSql());
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
        var query = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .LeftJoin<MultiSourceUser>("reviewer")
            .On((owner, review, reviewer) => owner.Id == reviewer.Id && reviewer.Id > 3)
            .Select((owner, review, reviewer) => new object[] { owner.Id, review.UserId, reviewer.Id });

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
        var query = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .CrossJoin<MultiSourcePermission>("permission")
            .Select((user, review, permission) => new object[] { user.Id, review.UserId, permission.UserId })
            .Where((user, review, permission) => review.UserId == permission.UserId);
        var crossJoinWithOn = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .CrossJoin<MultiSourcePermission>("permission");

        // Assert
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId`,`permission`.`UserId` \r\nFrom `users`, `reviews` \r\nCross Join `permissions` As `permission` \r\nWhere `reviews`.`UserId`=`permission`.`UserId`", query.ToSql());
        var exception = Assert.Throws<InvalidOperationException>(() => crossJoinWithOn.On(
            (user, review, permission) => review.UserId == permission.UserId));
        Assert.Equal("Cross Join 不支持 On 条件。", exception.Message);
    }

    /// <summary>
    /// 测试目的：不支持 Right Join 和 Full Join 的 SQLite 应在多表类型化连接渲染前拒绝，避免访问数据库。
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
        var rightJoin = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .RightJoin<MultiSourcePermission>("permission")
            .On((user, review, permission) => review.UserId == permission.UserId);
        var fullJoin = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .FullJoin<MultiSourcePermission>("permission")
            .On((user, review, permission) => review.UserId == permission.UserId);

        // Assert
        var rightJoinException = Assert.Throws<NotSupportedException>(() => rightJoin.ToSql());
        var fullJoinException = Assert.Throws<NotSupportedException>(() => fullJoin.ToSql());
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
        var query = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Join<MultiSourceUser>("reviewer")
            .On((owner, review, reviewer) => owner.Id == reviewer.Id)
            .Select((owner, review, reviewer) => new object[] { owner.Id, review.UserId, reviewer.Id })
            .OrderBy((owner, review, reviewer) => new object[] { reviewer.Id })
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
        var first = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Select((user, review) => new object[] { user.Id, review.UserId })
            .OrderBy((user, review) => new object[] { user.Id })
            .Skip(2)
            .Take(3);
        var second = rootQuery.From<MultiSourceUser, MultiSourceReview>()
            .Select((user, review) => new object[] { user.Id, review.UserId })
            .OrderBy((user, review) => new object[] { review.UserId })
            .Skip(7)
            .Take(11);

        // Assert
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId` \r\nFrom `users`, `reviews` \r\nOrder By `users`.`Id` \r\nLimit @_p_1 OFFSET @_p_0", first.ToSql());
        Assert.Equal("Select `users`.`Id`,`reviews`.`UserId` \r\nFrom `users`, `reviews` \r\nOrder By `reviews`.`UserId` \r\nLimit @_p_1 OFFSET @_p_0", second.ToSql());
        Assert.Equal(new object[] { 2, 3 }, first.GetParams().Values.ToArray());
        Assert.Equal(new object[] { 7, 11 }, second.GetParams().Values.ToArray());
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