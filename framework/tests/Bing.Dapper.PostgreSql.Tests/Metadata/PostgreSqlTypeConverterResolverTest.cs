using System.Data;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// PostgreSql 类型转换器解析器测试
/// </summary>
public class PostgreSqlTypeConverterResolverTest
{
    /// <summary>
    /// 测试 - 类型转换器解析器应解析 PostgreSql 对应的 Provider 转换器。
    /// </summary>
    [Fact]
    public void TypeConverterResolver_ShouldResolveProviderConverter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddPostgreSqlQuery("Host=localhost;Database=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        var resolver = provider.GetRequiredService<ITypeConverterResolver>();
        var converter = resolver.Resolve(DatabaseType.PgSql);

        // Assert
        Assert.IsType<PostgreSqlTypeConverter>(converter);
    }

    /// <summary>
    /// 测试数据库
    /// </summary>
    private sealed class TestDatabase : IDatabase
    {
        public IDbConnection GetConnection() => null;
    }
}