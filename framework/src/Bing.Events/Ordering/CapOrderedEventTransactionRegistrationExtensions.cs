using Bing.Data.Events.Ordering;
using DotNetCore.CAP;
using DotNetCore.CAP.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Events.Ordering;

/// <summary>
/// 注册 CAP 有序事务适配器。
/// </summary>
public static class CapOrderedEventTransactionRegistrationExtensions
{
    /// <summary>
    /// 使用指定的数据库事务工厂注册 CAP 有序事务。
    /// </summary>
    /// <typeparam name="TTransactionFactory">拥有数据库事务的底层工厂类型。</typeparam>
    /// <param name="services">当前宿主的服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>
    /// 调用方应在 CAP 完成注册后调用本方法；底层工厂继续拥有数据库事务，适配器只管理 CAP 缓冲。
    /// 同一底层工厂重复调用不会增加适配器描述。
    /// </remarks>
    public static IServiceCollection AddBingCapOrderedEventTransaction<TTransactionFactory>(
        this IServiceCollection services)
        where TTransactionFactory : class, IOrderedEventTransactionFactory
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        var marker = services.FirstOrDefault(item =>
            item.ServiceType == typeof(CapOrderedEventTransactionRegistrationMarker));
        if (marker?.ImplementationInstance is CapOrderedEventTransactionRegistrationMarker existing)
        {
            if (existing.FactoryType == typeof(TTransactionFactory))
                return services;
            throw new InvalidOperationException(
                $"当前服务集合已经注册 CAP 有序事务工厂 {existing.FactoryType.FullName}，不能再注册 {typeof(TTransactionFactory).FullName}。");
        }

        services.AddSingleton(new CapOrderedEventTransactionRegistrationMarker(typeof(TTransactionFactory)));
        services.TryAddScoped<TTransactionFactory>();
        services.AddScoped<IOrderedEventTransactionFactory>(provider =>
            new CapOrderedEventTransactionFactory(
                provider.GetRequiredService<TTransactionFactory>(),
                provider.GetRequiredService<ICapPublisher>(),
                provider.GetRequiredService<IDispatcher>()));
        return services;
    }

    /// <summary>
    /// 标识 CAP 有序事务适配器已经完成注册。
    /// </summary>
    private sealed class CapOrderedEventTransactionRegistrationMarker
    {
        /// <summary>
        /// 初始化一个 <see cref="CapOrderedEventTransactionRegistrationMarker" /> 类型的实例。
        /// </summary>
        /// <param name="factoryType">底层事务工厂类型。</param>
        internal CapOrderedEventTransactionRegistrationMarker(Type factoryType) =>
            FactoryType = factoryType ?? throw new ArgumentNullException(nameof(factoryType));

        /// <summary>
        /// 获取已注册的底层事务工厂类型。
        /// </summary>
        internal Type FactoryType { get; }
    }
}
