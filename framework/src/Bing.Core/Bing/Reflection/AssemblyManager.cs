using System.Diagnostics;
using Bing.Extensions;
using Microsoft.Extensions.DependencyModel;

namespace Bing.Reflection;

/// <summary>
/// 程序集管理器
/// </summary>
public static class AssemblyManager
{
    /// <summary>
    /// 过滤程序集数组
    /// </summary>
    private static readonly string[] _filters =
    {
        "dotnet-", "Microsoft.", "mscorlib", "netstandard", "System", "Windows", "AutoMapper", "AspectCore",
        "Dapper", "Pomelo", "MySqlConnector", "MongoDB", "DotNetCore.CAP", "RabbitMQ", "Exceptionless", "Serilog",
        "NLog", "Google.Protobuf", "Grpc", "EasyCaching", "CSRedisCore", "Consul", "SkyAPM", "SkyApm",
        "Swashbuckle", "Newtonsoft", "IdentityModel", "ReSharper", "JetBrains", "NuGet"
    };

    /// <summary>
    /// 获取按程序集筛选规则收集的全部程序集。
    /// </summary>
    private static Assembly[] _allAssemblies;

    /// <summary>
    /// 获取已收集程序集中的全部类型。
    /// </summary>
    private static Type[] _allTypes;

    /// <summary>
    /// 静态构造函数
    /// </summary>
    static AssemblyManager()
    {
        AssemblyFilterFunc = name =>
        {
            return name.Name != null && !_filters.Any(m => name.Name.StartsWith(m));
        };
    }

    /// <summary>
    /// 程序集过滤函数
    /// </summary>
    public static Func<AssemblyName, bool> AssemblyFilterFunc { private get; set; }

    /// <summary>
    /// 获取按程序集筛选规则收集的全部程序集。
    /// </summary>
    public static Assembly[] AllAssemblies
    {
        get
        {
            if (_allAssemblies == null)
                Init();
            return _allAssemblies;
        }
    }

    /// <summary>
    /// 获取已收集程序集中的全部类型。
    /// </summary>
    public static Type[] AllTypes
    {
        get
        {
            if (_allTypes == null)
                Init();
            return _allTypes;
        }
    }

    /// <summary>
    /// 加载依赖上下文中的程序集并重新构建程序集和类型缓存。
    /// </summary>
    /// <exception cref="BingFrameworkException">程序集筛选委托未配置时抛出。</exception>
    public static void Init()
    {
        if (AssemblyFilterFunc == null)
            throw new BingFrameworkException("AssemblyManager.AssemblyFilterFunc 不能为空");
        Debug.WriteLine("AssemblyManager: 初始化程序集");

        var defaultAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assemblyName in DependencyContext.Default.GetDefaultAssemblyNames())
            LoadAssembly(assemblyName, defaultAssemblies);
        var allAssemblies = new List<Assembly>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if(IsSkip(assembly))
                continue;
            allAssemblies.Add(assembly);
        }

        _allAssemblies = allAssemblies.ToArray();
        _allTypes = _allAssemblies.SelectMany(AssemblyHelper.GetAllTypes).OfType<Type>().ToArray();
        foreach (var assembly in _allAssemblies)
        {
            Debug.WriteLine($"【AssemblyManager】程序集: {assembly.FullName}");
        }
    }

    /// <summary>
    /// 尝试将指定程序集加载到当前应用程序域。
    /// </summary>
    /// <param name="assemblyName">要加载的程序集名称。</param>
    /// <param name="currentDomainAssemblies">当前已经加载的程序集快照。</param>
    private static void LoadAssembly(AssemblyName assemblyName, Assembly[] currentDomainAssemblies)
    {
        try
        {
            if(IsSkip(assemblyName))
                return;
            if (currentDomainAssemblies.Any(t => t.FullName == assemblyName.FullName))
                return;
            Debug.WriteLine($"加载程序集：{assemblyName}");
            AppDomain.CurrentDomain.Load(assemblyName);
        }
        catch (BadImageFormatException e)
        {
            Debug.WriteLine($"[{nameof(AssemblyManager)}-{nameof(LoadAssembly)}]: {e.Message}");
        }
    }

    /// <summary>
    /// 是否过滤程序集
    /// </summary>
    /// <param name="assemblyName">程序集名称</param>
    /// <returns>程序集被过滤时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private static bool IsSkip(AssemblyName assemblyName)
    {
        var applicationName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (assemblyName.Name.StartsWith($"{applicationName}.Views"))
            return false;
        if (assemblyName.Name.StartsWith($"{applicationName}.PrecompiledViews"))
            return false;
        return !AssemblyFilterFunc(assemblyName);
    }

    /// <summary>
    /// 是否过滤程序集
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>程序集被过滤时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private static bool IsSkip(Assembly assembly) => IsSkip(assembly.GetName());

    /// <summary>
    /// 查找指定条件的类型
    /// </summary>
    /// <param name="predicate">条件</param>
    /// <returns>符合条件的类型数组。</returns>
    public static Type[] FindTypes(Func<Type, bool> predicate) => AllTypes.Where(predicate).ToArray();

    /// <summary>
    /// 查找指定基类的实现类型
    /// </summary>
    /// <typeparam name="TBaseType">基类类型</typeparam>
    /// <returns>继承指定基类的非重复类型集合。</returns>
    public static Type[] FindTypesByBase<TBaseType>() => FindTypesByBase(typeof(TBaseType));

    /// <summary>
    /// 查找指定基类的实现类型
    /// </summary>
    /// <param name="baseType">基类类型</param>
    /// <returns>继承指定基类的非重复类型集合。</returns>
    public static Type[] FindTypesByBase(Type baseType)
    {
        return AllTypes.Where(type => type.IsDeriveClassFrom(baseType)).Distinct().ToArray();
    }

    /// <summary>
    /// 查找指定 <see cref="Attribute"/> 特性的实现类型
    /// </summary>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    /// <param name="inherit">是否搜索此成员的继承链来查找特性</param>
    /// <returns>标记指定特性的非重复类型集合。</returns>
    public static Type[] FindTypesByAttribute<TAttribute>(bool inherit = true) => FindTypesByAttribute(typeof(TAttribute), inherit);

    /// <summary>
    /// 查找指定 <see cref="Attribute"/> 特性的实现类型
    /// </summary>
    /// <param name="attributeType">特性类型</param>
    /// <param name="inherit">是否搜索此成员的继承链来查找特性</param>
    /// <returns>标记指定特性的非重复类型集合。</returns>
    public static Type[] FindTypesByAttribute(Type attributeType, bool inherit = true)
    {
        return AllTypes.Where(type => type.IsDefined(attributeType, inherit)).Distinct().ToArray();
    }
}
