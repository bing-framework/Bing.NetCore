using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Dapper;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>验证派生参数的生成模型与兼容回退均能绑定完整参数。</summary>
public sealed class SqliteGeneratedInheritedParameterTest
{
    private const string Statement = "SELECT @Value + @Other";

    [Fact]
    public async Task ConcreteModelWithGeneratedGetters_ShouldBindInheritedAndOwnParametersInSqlite()
    {
        using var host = Host(SqlExecutionMode.GeneratedOnly, DerivedModels());
        var source = new InheritedSqliteFilter { Value = 9, Other = 12 };
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var command = connection.CreateCommand();
        command.CommandText = Statement;

        var bound = host.GetRequiredService<ISqlParameterBinder>().Bind(source);
        Assert.IsAssignableFrom<SqlMapper.IDynamicParameters>(bound).AddParameters(command, null);
        Assert.Equal(Statement, command.CommandText);
        Assert.Equal(9, command.Parameters["Value"].Value);
        Assert.Equal(12, command.Parameters["Other"].Value);
        await connection.OpenAsync();
        Assert.Equal(21L, await command.ExecuteScalarAsync());

        using var query = host.GetRequiredService<ISqlQueryFactory>().Create();
        Assert.Equal(21, await query.Sql(Statement, source).SingleAsync<int>());
    }

    [Theory]
    [InlineData(SqlExecutionMode.Compatible)]
    [InlineData(SqlExecutionMode.GeneratedPreferred)]
    public async Task BaseModelOnly_ShouldFallBackAndBindAllParametersInSqlite(SqlExecutionMode mode)
    {
        using var host = Host(mode, BaseModels());
        using var query = host.GetRequiredService<ISqlQueryFactory>().Create();
        var source = new InheritedSqliteFilter { Value = 9, Other = 12 };

        Assert.Same(source, host.GetRequiredService<ISqlParameterBinder>().Bind(source));
        Assert.Equal(21, await query.Sql(Statement, source).SingleAsync<int>());
    }

    [Fact]
    public void BaseModelOnly_ShouldRejectDerivedSourceInGeneratedOnlyMode()
    {
        using var host = Host(SqlExecutionMode.GeneratedOnly, BaseModels());

        Assert.Throws<NotSupportedException>(() => host.GetRequiredService<ISqlParameterBinder>()
            .Bind(new InheritedSqliteFilter { Value = 9, Other = 12 }));
    }

    private static ServiceProvider Host(SqlExecutionMode mode, IEnumerable<EntityModelMetadata> models)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureSqlMetadata(options => options.ExecutionMode = mode);
        services.AddSqlGeneratedModels(models);
        services.AddSqlDataSource("default", DatabaseType.Sqlite, "Data Source=:memory:");
        services.AddSqliteProvider();
        return services.BuildServiceProvider();
    }

    private static EntityModelMetadata[] BaseModels() => new[]
    {
        new EntityModelMetadata(typeof(BaseSqliteFilter), "Filter", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(BaseSqliteFilter.Value), typeof(int), true, false,
                generatedGetter: source => ((BaseSqliteFilter)source).Value)
        })
    };

    private static EntityModelMetadata[] DerivedModels() => new[]
    {
        new EntityModelMetadata(typeof(InheritedSqliteFilter), "Filter", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(InheritedSqliteFilter.Value), typeof(int), true, false,
                generatedGetter: source => ((InheritedSqliteFilter)source).Value),
            EntityPropertyMetadata.CreateGenerated(nameof(InheritedSqliteFilter.Other), typeof(int), true, false,
                generatedGetter: source => ((InheritedSqliteFilter)source).Other)
        })
    };
}

public class BaseSqliteFilter { public int Value { get; set; } }
public sealed class InheritedSqliteFilter : BaseSqliteFilter { public int Other { get; set; } }
