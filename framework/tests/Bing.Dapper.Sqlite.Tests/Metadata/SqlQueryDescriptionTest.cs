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
    }
}