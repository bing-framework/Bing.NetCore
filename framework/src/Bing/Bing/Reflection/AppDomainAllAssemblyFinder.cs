using System.Diagnostics;
using System.Text.RegularExpressions;
using Bing.Extensions;
using Bing.Finders;
using Microsoft.Extensions.DependencyModel;

namespace Bing.Reflection;

/// <summary>
/// 应用程序目录程序集查找器
/// </summary>
public class AppDomainAllAssemblyFinder : FinderBase<Assembly>, IAllAssemblyFinder
{
    /// <summary>
    /// 过滤Net程序集
    /// </summary>
    private readonly bool _filterNetAssembly;

    /// <summary>
    /// 跳过的程序集
    /// </summary>
    protected const string SkipAssemblies =
        "^System|^Mscorlib|^msvcr120|^Netstandard|^Microsoft|^Autofac|^AutoMapper|^EntityFramework|^Newtonsoft|^Castle|^NLog|^Pomelo|^AspectCore|^Xunit|^Nito|^Npgsql|^Exceptionless|^MySqlConnector|^Anonymously Hosted|^libuv|^api-ms|^clrcompression|^clretwrc|^clrjit|^coreclr|^dbgshim|^e_sqlite3|^hostfxr|^hostpolicy|^MessagePack|^mscordaccore|^mscordbi|^mscorrc|sni|sos|SOS.NETCore|^sos_amd64|^SQLitePCLRaw|^StackExchange|^Swashbuckle|WindowsBase|ucrtbase|^DotNetCore.CAP|^MongoDB|^Confluent.Kafka|^librdkafka|^EasyCaching|^RabbitMQ|^Consul|^Dapper|^EnyimMemcachedCore|^Pipelines|^DnsClient|^IdentityModel|^zlib|^testhost|^dotMemory|^NSubstitute|^Hangfire|^AspectCore|^CSRedisCore|^Remotion.Linq|^Window|^runtime.|^api-ms-win-core|^dotnet|^JetBrains";

    /// <summary>
    /// 初始化一个<see cref="AppDomainAllAssemblyFinder"/>类型的实例
    /// </summary>
    /// <param name="filterNetAssembly">过滤Net程序集</param>
    public AppDomainAllAssemblyFinder(bool filterNetAssembly = true) => _filterNetAssembly = filterNetAssembly;

    /// <summary>
    /// 重写已实现所有项的查找
    /// </summary>
    protected override Assembly[] FindAllItems()
    {
        var context = DependencyContext.Default;
        var names = new List<string>();
        if (context != null)
        {
                
            var dllNames = context.CompileLibraries.SelectMany(m => m.Assemblies)
                .Distinct()
                .Select(m => m.Replace(".dll", ""))
                .OrderBy(m => m).ToArray();
            if (dllNames.Length > 0)
            {
                names =
                    (from name in dllNames
                        let i = name.LastIndexOf('/') + 1
                        select name.Substring(i, name.Length - i))
                    .Distinct()
                    .WhereIf(Match, _filterNetAssembly)
                    .OrderBy(m => m)
                    .ToList();
            }
            else
            {
                foreach (var library in context.CompileLibraries)
                {
                    var name = library.Name;
                    if (_filterNetAssembly && !Match(name))
                        continue;
                    if (!names.Contains(name))
                        names.Add(name);
                }
            }
        }
        else
        {
            var path = AppContext.BaseDirectory;
            var dllNames = Directory.GetFiles(path, "*.dll", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(path, "*.exe", SearchOption.TopDirectoryOnly))
                .Select(m => m.Replace(".dll", "").Replace(".exe", ""))
                .Distinct()
                .OrderBy(m => m)
                .ToArray();
            if (dllNames.Length > 0)
                names = (from name in dllNames
                        let i = name.LastIndexOf('\\') + 1
                        select name.Substring(i, name.Length - i))
                    .Distinct()
                    .WhereIf(Match, _filterNetAssembly)
                    .OrderBy(m => m)
                    .ToList();
        }
        return LoadAssemblies(names);
    }

    /// <summary>
    /// 加载程序集
    /// </summary>
    /// <param name="files">文件集合</param>
    protected static Assembly[] LoadAssemblies(IEnumerable<string> files)
    {
        var assemblies = new List<Assembly>();
        foreach (var file in files)
        {
            var name = new AssemblyName(file);
            Debug.WriteLine($"加载程序集：{name}");
            try
            {
                assemblies.Add(Assembly.Load(name));
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine($"找不到程序集：{file}");
            }
        }
        return assemblies.ToArray();
    }

    /// <summary>
    /// 程序集是否匹配
    /// </summary>
    /// <param name="assemblyName">程序集名称</param>
    protected virtual bool Match(string assemblyName)
    {
        var applicationName = Assembly.GetEntryAssembly().GetName().Name;
        if (assemblyName.StartsWith($"{applicationName}.Views"))
            return false;
        if (assemblyName.StartsWith($"{applicationName}.PrecompiledViews"))
            return false;
        return Regex.IsMatch(assemblyName, SkipAssemblies, RegexOptions.IgnoreCase | RegexOptions.Compiled) == false;
    }
}
