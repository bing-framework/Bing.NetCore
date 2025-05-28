using Bing.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 服务集合 - 对象访问器 扩展
/// </summary>
public static class ServiceCollectionObjectAccessorExtensions
{
    /// <summary>
    /// 尝试在服务集合中添加一个特定类型的对象访问器。如果已经存在，则返回现有的对象访问器。
    /// </summary>
    /// <typeparam name="T">对象访问器要管理的对象类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>返回现有的或新添加的对象访问器实例。</returns>
    public static ObjectAccessor<T> TryAddObjectAccessor<T>(this IServiceCollection services)
    {
        if (services.Any(s => s.ServiceType == typeof(ObjectAccessor<T>)))
            return services.GetSingletonInstance<ObjectAccessor<T>>();
        return services.AddObjectAccessor<T>();
    }

    /// <summary>
    /// 在服务集合中添加一个特定类型的对象访问器。
    /// </summary>
    /// <typeparam name="T">对象访问器要管理的对象类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>返回创建并添加到服务集合的对象访问器实例</returns>
    public static ObjectAccessor<T> AddObjectAccessor<T>(this IServiceCollection services) =>
        services.AddObjectAccessor(new ObjectAccessor<T>());

    /// <summary>
    /// 在服务集合中添加一个特定类型的对象访问器。
    /// </summary>
    /// <typeparam name="T">对象访问器要管理的对象类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="obj">对象</param>
    /// <returns>返回创建并添加到服务集合的对象访问器实例。</returns>
    public static ObjectAccessor<T> AddObjectAccessor<T>(this IServiceCollection services, T obj) =>
        services.AddObjectAccessor(new ObjectAccessor<T>(obj));

    /// <summary>
    /// 在服务集合中添加一个特定类型的对象访问器。
    /// </summary>
    /// <typeparam name="T">对象访问器要管理的对象类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="accessor">对象访问器实例</param>
    /// <returns>返回传入的对象访问器。</returns>
    /// <exception cref="Exception">如果同一类型的对象访问器已经注册，则抛出异常。</exception>
    public static ObjectAccessor<T> AddObjectAccessor<T>(this IServiceCollection services, ObjectAccessor<T> accessor)
    {
        // 检查服务集合中是否已经存在相同类型的对象访问器。
        if (services.Any(s => s.ServiceType == typeof(ObjectAccessor<T>)))
            throw new Exception($"An object accessor is registered before for type: {typeof(T).AssemblyQualifiedName}");

        // 在服务集合的最前面插入一个单例服务描述符，代表这个对象访问器。
        // 这样做确保了这个访问器可以被优先解析，减少查找时间。
        services.Insert(0, ServiceDescriptor.Singleton(typeof(ObjectAccessor<T>), accessor));

        // 除了添加 ObjectAccessor<T> 本身，还需要添加其接口 IObjectAccessor<T> 的映射。
        // 这允许通过接口或实际类型两种方式获取对象访问器。
        services.Insert(0, ServiceDescriptor.Singleton(typeof(IObjectAccessor<T>), accessor));
        return accessor;
    }

    /// <summary>
    /// 尝试从服务集合中获取一个指定类型的对象实例。
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>如果找到，则返回指定类型的对象实例；否则返回 null。</returns>
    public static T GetObjectOrNull<T>(this IServiceCollection services)
        where T : class
    {
        return services.GetSingletonInstanceOrNull<IObjectAccessor<T>>()?.Value;
    }

    /// <summary>
    /// 从服务集合中获取一个指定类型的对象实例。
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <returns>返回指定类型的对象实例。</returns>
    /// <exception cref="Exception">如果没有找到指定类型的对象，则抛出异常。</exception>
    public static T GetObject<T>(this IServiceCollection services)
        where T : class
    {
        return services.GetObjectOrNull<T>() ?? throw new Exception($"Could not find an object of {typeof(T).AssemblyQualifiedName} in service. Be sure that you have used AddObjectAccessor before!");
    }
}
