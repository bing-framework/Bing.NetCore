using Bing.DependencyInjection;

namespace Bing.Reflection;

/// <summary>
/// 定义方法信息查找器
/// </summary>
[IgnoreDependency]
public interface IMethodInfoFinder
{
    /// <summary>
    /// 查找指定条件的项
    /// </summary>
    /// <param name="type">要查找的类型</param>
    /// <param name="predicate">筛选条件</param>
    /// <returns>符合筛选条件的方法信息数组。</returns>
    MethodInfo[] Find(Type type, Func<MethodInfo, bool> predicate);

    /// <summary>
    /// 查找所有项
    /// </summary>
    /// <param name="type">要查找的类型</param>
    /// <returns>指定类型声明的方法信息数组。</returns>
    MethodInfo[] FindAll(Type type);
}
