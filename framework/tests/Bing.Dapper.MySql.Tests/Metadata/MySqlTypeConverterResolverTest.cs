using System.Data;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql;
using Bing.Data.Sql.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// MySql 类型转换器解析器测试
/// </summary>
public class MySqlTypeConverterResolverTest
{
    /// <summary>
    /// 测试 - 类型转换器解析器应解析 MySql 对应的 Provider 转换器。
    /// </summary>
    [Fact]
    public void TypeConverterResolver_ShouldResolveProviderConverter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMySqlProvider();
        services.AddSqlDataSource("default", DatabaseType.MySql, "Server=default;Database=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        var resolver = provider.GetRequiredService<ITypeConverterResolver>();
        var converter = resolver.Resolve(DatabaseType.MySql);

        // Assert
        converter.ShouldBeOfType<MySqlTypeConverter>();
    }

    /// <summary>
    /// 测试数据库
    /// </summary>
    private sealed class TestDatabase : IDatabase
    {
        public IDbConnection GetConnection() => null;
    }
}