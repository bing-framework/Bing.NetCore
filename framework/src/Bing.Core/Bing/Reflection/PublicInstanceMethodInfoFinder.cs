namespace Bing.Reflection;

/// <summary>
/// 公共实例方法查找器
/// </summary>
public class PublicInstanceMethodInfoFinder : IMethodInfoFinder
{
    /// <inheritdoc />
    public MethodInfo[] Find(Type type, Func<MethodInfo, bool> predicate) => FindAll(type).Where(predicate).ToArray();

    /// <inheritdoc />
    public MethodInfo[] FindAll(Type type) => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
}
