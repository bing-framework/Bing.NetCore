using System.Reflection;

namespace Bing.Authorization.Functions;

/// <summary>
/// 发现、查询并缓存可授权功能元数据的处理器。
/// </summary>
public interface IFunctionHandler
{
    /// <summary>
    /// 初始化功能元数据发现结果。
    /// </summary>
    /// <remarks>实现通常从已加载程序集发现功能类型及其候选方法，例如 MVC 控制器和操作方法。</remarks>
    void Initialize();

    /// <summary>
    /// 获取已发现的所有功能类型。
    /// </summary>
    /// <returns>当前功能元数据中包含的功能类型数组。</returns>
    Type[] GetAllFunctionTypes();

    /// <summary>
    /// 获取指定功能类型的候选功能方法。
    /// </summary>
    /// <param name="functionType">要查询的功能类型。</param>
    /// <returns>指定功能类型包含的候选功能方法数组。</returns>
    MethodInfo[] GetMethodInfos(Type functionType);

    /// <summary>
    /// 按区域、控制器和操作名称查询功能信息。
    /// </summary>
    /// <param name="area">功能所属区域名称。</param>
    /// <param name="controller">功能所属控制器名称。</param>
    /// <param name="action">功能操作方法名称。</param>
    /// <returns>匹配的功能信息；未找到时返回 <c>null</c>。</returns>
    IFunction GetFunction(string area, string controller, string action);

    /// <summary>
    /// 刷新功能信息缓存。
    /// </summary>
    /// <remarks>刷新后后续查询使用重新发现或重新加载的功能元数据。</remarks>
    void RefreshCache();

    /// <summary>
    /// 清空当前功能信息缓存。
    /// </summary>
    /// <remarks>清空不负责立即重新发现功能；重新初始化或刷新由调用方按需触发。</remarks>
    void ClearCache();
}
