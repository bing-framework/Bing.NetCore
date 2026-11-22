using System.Data;
using Bing.Data.Enums;
using Bing.Data.Metadata;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;

// 消费工程中不直接创建转换器，以验证裁剪后依赖注入仍能发现构造函数。
internal static class GenericConverterProbe
{
    internal static object Register(IServiceCollection services)
    {
        var dependency = new ConverterDependency();
        services.AddSingleton(dependency);
        services.AddDatabaseTypeConverter<InjectedConverter>(DatabaseType.SqlServer);
        return dependency;
    }

    internal static void Verify(IServiceProvider provider, object dependency)
    {
        var converter = provider.GetRequiredService<ITypeConverterResolver>().Resolve(DatabaseType.SqlServer);
        if (converter is not InjectedConverter injected || !ReferenceEquals(injected.Dependency, dependency) ||
            converter.ToDbType("probe") != DbType.Guid)
            throw new InvalidOperationException("generic converter constructor injection");
        Console.WriteLine("GENERIC_CONVERTER_NATIVE_PASS");
    }

    internal sealed class ConverterDependency { }

    internal sealed class InjectedConverter : ITypeConverter
    {
        public InjectedConverter(ConverterDependency dependency) => Dependency = dependency;
        internal ConverterDependency Dependency { get; }
        public DbType? ToDbType(string dataType, int? length = null) => dataType == "probe" ? DbType.Guid : null;
    }
}
