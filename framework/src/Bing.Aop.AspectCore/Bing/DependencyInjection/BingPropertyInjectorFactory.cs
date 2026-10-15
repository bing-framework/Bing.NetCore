using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using AspectCore.DependencyInjection;
using Bing.Aspects;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 为 AspectCore 创建 Bing 属性注入器。
/// </summary>
internal sealed class BingPropertyInjectorFactory : IPropertyInjectorFactory
{
    /// <summary>
    /// 当前作用域的服务提供程序。
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 当前作用域内按类型缓存的属性描述。
    /// </summary>
    private readonly ConcurrentDictionary<Type, PropertyInjectionDescriptor[]> _descriptors = new();

    /// <summary>
    /// 当前作用域内仅按 Bing 标记缓存的属性描述。
    /// </summary>
    private readonly ConcurrentDictionary<Type, PropertyInjectionDescriptor[]> _autowiredDescriptors = new();

    /// <summary>
    /// 当前作用域内仅按继承的 AspectCore 标记缓存的属性描述。
    /// </summary>
    private readonly ConcurrentDictionary<Type, PropertyInjectionDescriptor[]> _inheritedNativeDescriptors = new();

    /// <summary>
    /// 初始化一个 <see cref="BingPropertyInjectorFactory" /> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">当前服务作用域。</param>
    public BingPropertyInjectorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public IPropertyInjector Create(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        var descriptors = _descriptors.GetOrAdd(type, currentType =>
            CreateDescriptors(currentType));
        return new BingPropertyInjector(_serviceProvider, descriptors);
    }

    /// <summary>
    /// 创建仅处理 Bing 标记的属性注入器。
    /// </summary>
    /// <param name="type">待处理的实例类型。</param>
    /// <returns>属性注入器。</returns>
    internal IPropertyInjector CreateAutowiredOnly(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        var descriptors = _autowiredDescriptors.GetOrAdd(type, CreateAutowiredDescriptors);
        return new BingPropertyInjector(_serviceProvider, descriptors);
    }

    /// <summary>
    /// 创建仅处理继承原生标记的属性注入器。
    /// </summary>
    /// <param name="type">待处理的实例类型。</param>
    /// <returns>属性注入器。</returns>
    internal IPropertyInjector CreateInheritedNativeOnly(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        var descriptors = _inheritedNativeDescriptors.GetOrAdd(type, CreateInheritedNativeDescriptors);
        return new BingPropertyInjector(_serviceProvider, descriptors);
    }

    /// <summary>
    /// 判断类型是否包含直接声明的原生注入标记。
    /// </summary>
    /// <param name="type">待检查的实例类型。</param>
    /// <returns>包含直接标记时返回 <see langword="true" />。</returns>
    internal bool HasDirectNativeMarker(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Any(property => property.IsDefined(typeof(FromServiceContextAttribute), inherit: false));
    }

    /// <summary>
    /// 创建指定类型的属性注入描述。
    /// </summary>
    /// <param name="type">待分析的实例类型。</param>
    /// <returns>符合注入条件的属性描述。</returns>
    private static PropertyInjectionDescriptor[] CreateDescriptors(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Select(property => CreateDescriptor(property, HasAnyInjectionAttribute))
            .Where(descriptor => descriptor != null)
            .ToArray();
    }

    /// <summary>
    /// 创建仅处理 Bing 标记的属性描述。
    /// </summary>
    /// <param name="type">待分析的实例类型。</param>
    /// <returns>符合注入条件的属性描述。</returns>
    private static PropertyInjectionDescriptor[] CreateAutowiredDescriptors(Type type)
    {
        return CreateDescriptors(type, property =>
            HasAutowiredAttribute(property) && HasDirectFromServiceContextAttribute(property) == false);
    }

    /// <summary>
    /// 创建仅处理继承原生标记的属性描述。
    /// </summary>
    /// <param name="type">待分析的实例类型。</param>
    /// <returns>符合注入条件的属性描述。</returns>
    private static PropertyInjectionDescriptor[] CreateInheritedNativeDescriptors(Type type)
    {
        return CreateDescriptors(type, property =>
            HasAutowiredAttribute(property) == false &&
            HasFromServiceContextAttribute(property) &&
            HasDirectFromServiceContextAttribute(property) == false);
    }

    /// <summary>
    /// 创建符合指定标记条件的属性描述。
    /// </summary>
    /// <param name="type">待分析的实例类型。</param>
    /// <param name="predicate">属性标记筛选条件。</param>
    /// <returns>符合注入条件的属性描述。</returns>
    private static PropertyInjectionDescriptor[] CreateDescriptors(
        Type type,
        Func<PropertyInfo, bool> predicate)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Select(property => CreateDescriptor(property, predicate))
            .Where(descriptor => descriptor != null)
            .ToArray();
    }

    /// <summary>
    /// 创建单个属性的注入描述。
    /// </summary>
    /// <param name="property">待分析的属性。</param>
    /// <param name="predicate">属性标记筛选条件。</param>
    /// <returns>符合注入条件时返回描述，否则返回 <see langword="null" />。</returns>
    private static PropertyInjectionDescriptor CreateDescriptor(
        PropertyInfo property,
        Func<PropertyInfo, bool> predicate)
    {
        if (property.GetSetMethod(nonPublic: false) == null)
            return null;

        if (predicate(property) == false)
            return null;

        return new PropertyInjectionDescriptor(property);
    }

    /// <summary>
    /// 判断属性是否带有任一支持的注入标记。
    /// </summary>
    /// <param name="property">待检查的属性。</param>
    /// <returns>带有任一标记时返回 <see langword="true" />。</returns>
    private static bool HasAnyInjectionAttribute(PropertyInfo property)
    {
        return HasAutowiredAttribute(property) || HasFromServiceContextAttribute(property);
    }

    /// <summary>
    /// 判断属性或其基类定义是否带有 Bing 注入标记。
    /// </summary>
    /// <param name="property">待检查的属性。</param>
    /// <returns>带有标记时返回 <see langword="true" />。</returns>
    private static bool HasAutowiredAttribute(PropertyInfo property)
    {
        return HasAttribute(property, typeof(AutowiredAttribute), includeBaseDefinitions: true);
    }

    /// <summary>
    /// 判断属性是否带有 AspectCore 原生注入标记。
    /// </summary>
    /// <param name="property">待检查的属性。</param>
    /// <returns>带有标记时返回 <see langword="true" />。</returns>
    private static bool HasFromServiceContextAttribute(PropertyInfo property)
    {
        return HasAttribute(property, typeof(FromServiceContextAttribute), includeBaseDefinitions: true);
    }

    /// <summary>
    /// 判断属性是否直接带有 AspectCore 原生注入标记。
    /// </summary>
    /// <param name="property">待检查的属性。</param>
    /// <returns>直接带有标记时返回 <see langword="true" />。</returns>
    private static bool HasDirectFromServiceContextAttribute(PropertyInfo property)
    {
        return property.IsDefined(typeof(FromServiceContextAttribute), inherit: false);
    }

    /// <summary>
    /// 按属性访问器定义查找指定标记。
    /// </summary>
    /// <param name="property">待检查的属性。</param>
    /// <param name="attributeType">待查找的特性类型。</param>
    /// <param name="includeBaseDefinitions">是否检查基类访问器定义。</param>
    /// <returns>找到标记时返回 <see langword="true" />。</returns>
    private static bool HasAttribute(PropertyInfo property, Type attributeType, bool includeBaseDefinitions)
    {
        if (property.IsDefined(attributeType, inherit: false))
            return true;

        if (includeBaseDefinitions == false)
            return false;

        var accessor = property.GetGetMethod(nonPublic: true) ?? property.GetSetMethod(nonPublic: true);
        var baseAccessor = accessor?.GetBaseDefinition();
        if (baseAccessor == null)
            return false;

        for (var declaringType = property.DeclaringType;
             declaringType != null;
             declaringType = declaringType.BaseType)
        {
            var baseProperty = declaringType.GetProperty(
                property.Name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var basePropertyAccessor = baseProperty?.GetGetMethod(nonPublic: true) ??
                baseProperty?.GetSetMethod(nonPublic: true);
            if (basePropertyAccessor?.GetBaseDefinition() != baseAccessor)
                continue;
            if (baseProperty.IsDefined(attributeType, inherit: false))
                return true;
        }

        return false;
    }
}

/// <summary>
/// 在 AspectCore 服务解析完成后执行 Bing 属性注入。
/// </summary>
internal sealed class BingPropertyInjectorCallback : IServiceResolveCallback
{
    /// <inheritdoc />
    public object Invoke(IServiceResolver resolver, object instance, ServiceDefinition definition)
    {
        if (instance == null ||
            instance is IPropertyInjectorFactory ||
            instance is IServiceResolveCallback ||
            instance is IServiceResolver)
            return instance;

        var factory = resolver.Resolve(typeof(IPropertyInjectorFactory)) as BingPropertyInjectorFactory;
        if (factory == null)
            return instance;

        if (factory.HasDirectNativeMarker(instance.GetType()))
            return instance;

        factory.CreateAutowiredOnly(instance.GetType()).Invoke(instance);
        factory.CreateInheritedNativeOnly(instance.GetType()).Invoke(instance);
        return instance;
    }
}

/// <summary>
/// 属性注入描述。
/// </summary>
internal sealed class PropertyInjectionDescriptor
{
    /// <summary>
    /// 初始化一个 <see cref="PropertyInjectionDescriptor" /> 类型的实例。
    /// </summary>
    /// <param name="property">属性信息。</param>
    public PropertyInjectionDescriptor(PropertyInfo property)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
    }

    /// <summary>
    /// 获取属性信息。
    /// </summary>
    public PropertyInfo Property { get; }
}

/// <summary>
/// 执行 Bing 属性注入。
/// </summary>
internal sealed class BingPropertyInjector : IPropertyInjector
{
    /// <summary>
    /// 当前作用域的服务提供程序。
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 待执行的属性注入描述。
    /// </summary>
    private readonly PropertyInjectionDescriptor[] _descriptors;

    /// <summary>
    /// 初始化一个 <see cref="BingPropertyInjector" /> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">当前服务作用域。</param>
    /// <param name="descriptors">属性描述集合。</param>
    public BingPropertyInjector(
        IServiceProvider serviceProvider,
        PropertyInjectionDescriptor[] descriptors)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _descriptors = descriptors ?? throw new ArgumentNullException(nameof(descriptors));
    }

    /// <inheritdoc />
    public void Invoke(object instance)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        foreach (var descriptor in _descriptors)
        {
            var service = _serviceProvider.GetService(descriptor.Property.PropertyType);
            if (service == null)
                continue;

            try
            {
                descriptor.Property.SetValue(instance, service, index: null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }
    }
}
