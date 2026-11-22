using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql.Metadata;
using Bing.Dapper.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>验证 PostgreSQL 转换器在裁剪后的注册方式和自定义实例优先级。</summary>
public class PostgreSqlAotConverterRegistrationTest
{
    [Fact]
    public void AddPostgreSqlProvider_RegistersStaticallyCreatedConverter()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlProvider();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(PostgreSqlTypeConverter));
        var converter = Assert.IsType<PostgreSqlTypeConverter>(descriptor.ImplementationInstance);
        using var provider = services.BuildServiceProvider();
        Assert.Same(converter, provider.GetRequiredService<ITypeConverterResolver>().Resolve(DatabaseType.PgSql));
    }

    [Fact]
    public void AddPostgreSqlProvider_PreservesExistingConverterInstance()
    {
        var services = new ServiceCollection();
        var custom = new PostgreSqlTypeConverter();
        services.AddSingleton(custom);
        services.AddPostgreSqlProvider();

        using var provider = services.BuildServiceProvider();
        Assert.Same(custom, provider.GetRequiredService<ITypeConverterResolver>().Resolve(DatabaseType.PgSql));
    }
}
