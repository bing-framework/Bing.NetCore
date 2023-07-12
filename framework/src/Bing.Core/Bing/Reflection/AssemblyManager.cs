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
    private static readonly string[] _filters = { "dotnet-", "Microsoft.", "mscorlib", "netstandard", "System", "Windows" };

    /// <summary>
    /// 全部程序集
    /// </summary>
    private static Assembly[] _allAssemblies;

    /// <summary>
    /// 全部类型
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
    /// 全部程序集
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
    /// 全部类型
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
    /// 初始化
    /// </summary>
    /// <exception cref="BingFrameworkException"></exception>
    public static void Init()
    {
        if (AssemblyFilterFunc == null)
            throw new BingFrameworkException("AssemblyManager.AssemblyFilterFunc 不能为空");
        Debug.WriteLine("AssemblyManager: 初始化程序集");
        _allAssemblies = DependencyContext.Default.GetDefaultAssemblyNames()
            .Where(AssemblyFilterFunc)
            .Select(Assembly.Load)
            .ToArray();
        _allTypes = _allAssemblies.SelectMany(m => m.GetTypes()).ToArray();
        foreach (var assembly in _allAssemblies)
        {
            Debug.WriteLine($"【AssemblyManager】程序集: {assembly.FullName}");
        }
    }

    /// <summary>
    /// 查找指定条件的类型
    /// </summary>
    /// <param name="predicate">条件</param>
    public static Type[] FindTypes(Func<Type, bool> predicate) => AllTypes.Where(predicate).ToArray();

    /// <summary>
    /// 查找指定基类的实现类型
    /// </summary>
    /// <typeparam name="TBaseType">基类类型</typeparam>
    /// <returns></returns>
    public static Type[] FindTypesByBase<TBaseType>() => FindTypesByBase(typeof(TBaseType));

    /// <summary>
    /// 查找指定基类的实现类型
    /// </summary>
    /// <param name="baseType">基类类型</param>
    public static Type[] FindTypesByBase(Type baseType)
    {
        return AllTypes.Where(type => type.IsDeriveClassFrom(baseType)).Distinct().ToArray();
    }

    /// <summary>
    /// 查找指定 <see cref="Attribute"/> 特性的实现类型
    /// </summary>
    /// <typeparam name="TAttribute">特性类型</typeparam>
    /// <param name="inherit">是否搜索此成员的继承链来查找特性</param>
    public static Type[] FindTypesByAttribute<TAttribute>(bool inherit = true) => FindTypesByAttribute(typeof(TAttribute), inherit);

    /// <summary>
    /// 查找指定 <see cref="Attribute"/> 特性的实现类型
    /// </summary>
    /// <param name="attributeType">特性类型</param>
    /// <param name="inherit">是否搜索此成员的继承链来查找特性</param>
    public static Type[] FindTypesByAttribute(Type attributeType, bool inherit = true)
    {
        return AllTypes.Where(type => type.IsDefined(attributeType, inherit)).Distinct().ToArray();
    }
}
