using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Enums;
using Bing.Data.Sql.CustomProvider.Tests.Samples;
using Xunit;

namespace Bing.Data.Sql.CustomProvider.Tests;

/// <summary>
/// 外部 SQL Provider Builder 验收测试。
/// </summary>
public class CustomProviderBuilderTest
{
    /// <summary>
    /// 测试目的：外部 Provider 应可仅通过公开 SPI 创建全部 SQL 子句。
    /// </summary>
    [Fact]
    public void Constructor_WhenUsingPublicProviderSpi_ShouldCreateAllClauses()
    {
        // Arrange
        var builder = new CustomSqlBuilder();
        var accessor = (ISqlPartAccessor)builder;

        // Act
        var provider = builder.Provider;

        // Assert
        Assert.Same(CustomSqlProvider.Instance, provider);
        Assert.IsType<CustomClauseFactory>(provider.ClauseFactory);
        Assert.IsType<SelectClause>(accessor.SelectClause);
        Assert.IsType<FromClause>(accessor.FromClause);
        Assert.IsType<JoinClause>(accessor.JoinClause);
        Assert.IsType<WhereClause>(accessor.WhereClause);
        Assert.IsType<GroupByClause>(accessor.GroupByClause);
        Assert.IsType<OrderByClause>(accessor.OrderByClause);
    }

    /// <summary>
    /// 测试目的：外部 Provider 应能生成包含 From、Join 和 Where 的完整 SQL 与参数。
    /// </summary>
    [Fact]
    public void Build_WhenFromJoinWhereConfigured_ShouldRenderSqlAndParameters()
    {
        // Arrange
        var builder = new CustomSqlBuilder();

        // Act
        var sql = builder.Select("u.Id")
            .From("Users", "u")
            .LeftJoin("Orders", "o")
            .Where("u.Enabled", true)
            .ToSql();

        // Assert
        Assert.Equal("Select [u].[Id] \r\nFrom [Users] As [u] \r\nLeft Join [Orders] As [o] \r\nWhere [u].[Enabled]=@_p_0", sql);
        Assert.Equal(true, builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试目的：外部 Provider 的表引用解析器应参与 From、Join 及 Clone 后新增 Join 的字符串表名解析。
    /// </summary>
    [Fact]
    public void TableReferenceParser_WhenCustomNamesConfigured_ShouldRenderParserResultsAcrossClone()
    {
        // Arrange
        var source = new CustomSqlBuilder();
        source.Select("u.Id").From("custom:users", "u");

        // Act
        var clone = Assert.IsType<CustomSqlBuilder>(source.Clone());
        clone.Join("custom:orders", "o");

        // Assert
        Assert.Equal("Select [u].[Id] \r\nFrom [ParsedUsers] As [u]", source.ToSql());
        Assert.Equal("Select [u].[Id] \r\nFrom [ParsedUsers] As [u] \r\nJoin [ParsedOrders] As [o]",
            clone.ToSql());
    }

    /// <summary>
    /// 测试目的：公开 Builder 工厂应按 Provider 实例和数据库类型创建对应外部 Builder。
    /// </summary>
    [Fact]
    public void Factory_WhenProviderAndDatabaseTypeRegistered_ShouldCreateExpectedBuilder()
    {
        // Arrange
        var factory = new SqlBuilderFactory(new[]
        {
            new SqlBuilderFactoryRegistration(CustomSqlProvider.Instance, () => new CustomSqlBuilder())
        });

        // Act
        var byProvider = factory.Create(CustomSqlProvider.Instance);
        var byDatabaseType = factory.Create(CustomSqlProvider.Instance.DatabaseType);

        // Assert
        Assert.IsType<CustomSqlBuilder>(byProvider);
        Assert.IsType<CustomSqlBuilder>(byDatabaseType);
    }

    /// <summary>
    /// 测试目的：Provider Key 应为大小写不敏感的正式创建入口，并接受首尾空白。
    /// </summary>
    [Fact]
    public void Factory_WhenProviderKeyUsesDifferentCaseAndWhitespace_ShouldCreateExpectedBuilder()
    {
        // Arrange
        var factory = new SqlBuilderFactory(new[]
        {
            new SqlBuilderFactoryRegistration(CustomSqlProvider.Instance, () => new CustomSqlBuilder())
        });

        // Act
        var builder = factory.Create("  CUSTOM.TEST  ");

        // Assert
        Assert.IsType<CustomSqlBuilder>(builder);
    }

    /// <summary>
    /// 测试目的：Factory 应将调用方提供的查询级共享服务原样传递给外部 Builder。
    /// </summary>
    [Fact]
    public void Factory_WhenQueryServicesAreProvided_ShouldPassSameInstanceToBuilder()
    {
        // Arrange
        var services = new SqlBuilderServices();
        var factory = new SqlBuilderFactory(new[]
        {
            new SqlBuilderFactoryRegistration(CustomSqlProvider.Instance, builderServices => new CustomSqlBuilder(builderServices))
        });

        // Act
        var builder = Assert.IsType<CustomSqlBuilder>(factory.Create(CustomSqlProvider.Instance, services));

        // Assert
        Assert.Same(services, builder.SharedServices);
    }

    /// <summary>
    /// 测试目的：不同 Key 的外部 Provider 应可复用同一个 DatabaseType，兼容入口保留首个官方映射。
    /// </summary>
    [Fact]
    public void Factory_WhenDifferentProviderKeysShareDatabaseType_ShouldAllowRegistration()
    {
        // Arrange
        var factory = new SqlBuilderFactory(new[]
        {
            new SqlBuilderFactoryRegistration(CustomSqlProvider.Instance, () => new CustomSqlBuilder()),
            new SqlBuilderFactoryRegistration(CustomSqliteAliasProvider.Instance, () => new CustomSqlBuilder())
        });

        // Act
        var first = factory.Create(CustomSqlProvider.Instance.Key);
        var alias = factory.Create(CustomSqliteAliasProvider.Instance.Key);
        var compatibility = factory.Create(DatabaseType.Sqlite);

        // Assert
        Assert.IsType<CustomSqlBuilder>(first);
        Assert.IsType<CustomSqlBuilder>(alias);
        Assert.IsType<CustomSqlBuilder>(compatibility);
    }

    /// <summary>
    /// 测试目的：未知和重复 Provider Key 应返回包含 Key 的明确异常。
    /// </summary>
    [Fact]
    public void Factory_WhenProviderKeyIsUnknownOrDuplicated_ShouldThrowWithKey()
    {
        // Arrange
        var registration = new SqlBuilderFactoryRegistration(CustomSqlProvider.Instance, () => new CustomSqlBuilder());
        var factory = new SqlBuilderFactory(new[] { registration });

        // Act
        var unknown = Assert.Throws<NotSupportedException>(() => factory.Create("custom.missing"));
        var duplicated = Assert.Throws<ArgumentException>(() => new SqlBuilderFactory(new[] { registration, registration }));

        // Assert
        Assert.Contains("custom.missing", unknown.Message);
        Assert.Contains("custom.test", duplicated.Message);
    }

    /// <summary>
    /// 测试目的：Provider 声明参数上限时，新增参数应在达到上限后被拒绝，已有参数仍可替换。
    /// </summary>
    [Fact]
    public void ParameterLimit_WhenLimitReached_ShouldRejectNewParameterAndAllowReplacement()
    {
        // Arrange
        var builder = new LimitedCustomSqlBuilder();
        var parameterManager = ((ISqlPartAccessor)builder).ParameterManager;

        // Act
        builder.Where("u.Id", 1);
        parameterManager.Add("@_p_0", 2);
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Where("u.Name", "blocked"));

        // Assert
        Assert.Equal("SQL 参数数量不能超过 1。", exception.Message);
        Assert.Single(builder.GetParams());
        Assert.Equal(2, builder.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试目的：参数上限配置应在 New 与 Clone 后保留，且 New 不继承来源参数。
    /// </summary>
    [Fact]
    public void ParameterLimit_WhenBuilderIsNewOrCloned_ShouldPreserveLimitAndIsolateParameters()
    {
        // Arrange
        var source = new LimitedCustomSqlBuilder();
        source.Where("u.Id", 1);

        // Act
        var clone = Assert.IsType<LimitedCustomSqlBuilder>(source.Clone());
        var fresh = Assert.IsType<LimitedCustomSqlBuilder>(source.New());
        var cloneException = Assert.Throws<InvalidOperationException>(() => clone.Where("u.Name", "blocked"));
        fresh.Where("u.Name", "fresh");
        var freshException = Assert.Throws<InvalidOperationException>(() => fresh.Where("u.Enabled", true));

        // Assert
        Assert.Equal("SQL 参数数量不能超过 1。", cloneException.Message);
        Assert.Equal("SQL 参数数量不能超过 1。", freshException.Message);
        Assert.Single(source.GetParams());
        Assert.Single(clone.GetParams());
        Assert.Single(fresh.GetParams());
        Assert.Equal(1, source.GetParam("@_p_0"));
        Assert.Equal("fresh", fresh.GetParam("@_p_0"));
    }

    /// <summary>
    /// 测试目的：外部 Provider 的分页渲染器应参与完整 SQL 输出。
    /// </summary>
    [Fact]
    public void PaginationRenderer_WhenSkipAndTakeConfigured_ShouldRenderProviderSql()
    {
        // Arrange
        var builder = new CustomSqlBuilder();

        // Act
        var sql = builder.Select("*").From("Users").OrderBy("Id").Skip(3).Take(5).ToSql();

        // Assert
        Assert.Equal("Select * \r\nFrom [Users] \r\nOrder By [Id] \r\nLimit @_p_1 Offset @_p_0", sql);
        Assert.Equal(3, builder.GetParam("@_p_0"));
        Assert.Equal(5, builder.GetParam("@_p_1"));
    }

    /// <summary>
    /// 测试目的：New 应复用共享服务，同时提供独立且为空的参数管理器。
    /// </summary>
    [Fact]
    public void New_WhenSourceContainsParameters_ShouldShareServicesAndIsolateParameters()
    {
        // Arrange
        var source = new CustomSqlBuilder();
        source.Select("u.Id").From("Users", "u").Where("u.Id", 7);

        // Act
        var fresh = Assert.IsType<CustomSqlBuilder>(source.New());
        fresh.Select("o.Id").From("Orders", "o").Where("o.Id", 9);

        // Assert
        Assert.Same(source.SharedServices, fresh.SharedServices);
        Assert.Equal(7, source.GetParam("@_p_0"));
        Assert.Equal(9, fresh.GetParam("@_p_0"));
        Assert.Single(source.GetParams());
        Assert.Single(fresh.GetParams());
    }

    /// <summary>
    /// 测试目的：Clone 修改 Join 与参数后不得影响来源 Builder 的 SQL 或参数。
    /// </summary>
    [Fact]
    public void Clone_WhenJoinAndParametersChange_ShouldPreserveSourceAndIsolateState()
    {
        // Arrange
        var source = new CustomSqlBuilder();
        source.Select("u.Id").From("Users", "u").Where("u.Enabled", true);
        var sourceSql = source.ToSql();

        // Act
        var clone = Assert.IsType<CustomSqlBuilder>(source.Clone());
        clone.LeftJoin("Orders", "o").Where("o.Paid", false);

        // Assert
        Assert.Equal("Select [u].[Id] \r\nFrom [Users] As [u] \r\nWhere [u].[Enabled]=@_p_0", sourceSql);
        Assert.Equal(sourceSql, source.ToSql());
        Assert.Equal("Select [u].[Id] \r\nFrom [Users] As [u] \r\nLeft Join [Orders] As [o] \r\nWhere [u].[Enabled]=@_p_0 And [o].[Paid]=@_p_1", clone.ToSql());
        Assert.Equal(true, source.GetParam("@_p_0"));
        Assert.Equal(false, clone.GetParam("@_p_1"));
        Assert.Single(source.GetParams());
        Assert.Equal(2, clone.GetParams().Count);
    }
}