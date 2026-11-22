using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql.Metadata;
using Bing.Dapper.MySql;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>验证 MySQL 与 Doris 在裁剪环境中的转换器注册及实例优先级。</summary>
public class MySqlAotConverterRegistrationTest
{
    [Fact]
    public void AddMySqlProvider_RegistersStaticallyCreatedConverterForBothProviders()
    {
        var services = new ServiceCollection();
        services.AddMySqlProvider();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(MySqlTypeConverter));
        var converter = Assert.IsType<MySqlTypeConverter>(descriptor.ImplementationInstance);
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<ITypeConverterResolver>();
        Assert.Same(converter, resolver.Resolve(DatabaseType.MySql));
        Assert.Same(converter, resolver.Resolve(DatabaseType.Doris));
    }

    [Fact]
    public void AddMySqlProvider_PreservesExistingConverterInstance()
    {
        var services = new ServiceCollection();
        var custom = new MySqlTypeConverter();
        services.AddSingleton(custom);
        services.AddMySqlProvider();

        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<ITypeConverterResolver>();
        Assert.Same(custom, resolver.Resolve(DatabaseType.MySql));
        Assert.Same(custom, resolver.Resolve(DatabaseType.Doris));
    }
}
