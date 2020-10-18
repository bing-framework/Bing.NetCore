using System;
using System.Collections.Generic;
using Bing.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection
{
    /// <summary>
    /// 应用程序服务定位器。可随时正常解析<see cref="ServiceLifetime.Singleton"/>与<see cref="ServiceLifetime.Transient"/>生命周期类型的服务
    /// 如果当前处于HttpContext有效的范围内，可正常解析<see cref="ServiceLifetime.Scoped"/>的服务
    /// 注：服务定位器尚不能正常解析 RootServiceProvider.CreateScope() 生命周期内的 Scoped 的服务
    /// </summary>
    public sealed class ServiceLocator : Disposable
    {
        #region 字段

        /// <summary>
        /// 懒加载实例
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static readonly Lazy<ServiceLocator> InstanceLazy = new Lazy<ServiceLocator>(() => new ServiceLocator());

        /// <summary>
        /// 服务提供程序
        /// </summary>
        private IServiceProvider _provider;

        /// <summary>
        /// 服务集合
        /// </summary>
        private IServiceCollection _services;

        #endregion

        #region 属性

        /// <summary>
        /// 服务定位器实例
        /// </summary>
        public static ServiceLocator Instance => InstanceLazy.Value;

        /// <summary>
        /// ServiceProvider是否可用
        /// </summary>
        public bool IsProviderEnabled => _provider != null;

        /// <summary>
        /// <see cref="ServiceLifetime.Scoped"/>生命周期的服务提供者
        /// </summary>
        public IServiceProvider ScopedProvider
        {
            get
            {
                var scopeResolver = _provider.GetService<IScopedServiceResolver>();
                return scopeResolver != null && scopeResolver.ResolveEnabled ? scopeResolver.ScopedProvider : null;
            }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="ServiceLocator"/>类型的实例
        /// </summary>
        private ServiceLocator() { }

        #endregion

        #region InScoped(是否在作用域生命周期中)

        /// <summary>
        /// 当前是否处于<see cref="ServiceLifetime.Scoped"/>生命周期中
        /// </summary>
        public static bool InScoped() => Instance.ScopedProvider != null;

        #endregion

        #region SetServiceCollection(设置应用程序服务集合)

        /// <summary>
        /// 设置应用程序服务集合
        /// </summary>
        /// <param name="services">服务集合</param>
        internal void SetServiceCollection(IServiceCollection services)
        {
            Check.NotNull(services, nameof(services));
            _services = services;
        }

        #endregion

        #region SetApplicationServiceProvider(设置应用程序服务提供程序)

        /// <summary>
        /// 设置应用程序服务提供程序
        /// </summary>
        /// <param name="provider">服务提供程序</param>
        internal void SetApplicationServiceProvider(IServiceProvider provider)
        {
            Check.NotNull(provider, nameof(provider));
            _provider = provider;
        }

        #endregion

        #region GetServiceDescriptors(获取所有已注册的 ServiceDescriptor 对象)

        /// <summary>
        /// 获取所有已注册的<see cref="ServiceDescriptor"/>对象
        /// </summary>
        public IEnumerable<ServiceDescriptor> GetServiceDescriptors()
        {
            Check.NotNull(_services, nameof(_services));
            return _services;
        }

        #endregion

        #region GetService(解析指定类型的服务实例)

        /// <summary>
        /// 解析指定类型的服务实例
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        public T GetService<T>()
        {
            Check.NotNull(_services, nameof(_services));
            Check.NotNull(_provider, nameof(_provider));

            var scopedResolver = _provider.GetService<IScopedServiceResolver>();
            if (scopedResolver != null && scopedResolver.ResolveEnabled)
                return scopedResolver.GetService<T>();
            return _provider.GetService<T>();
        }

        /// <summary>
        /// 解析指定类型的服务实例
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        public object GetService(Type serviceType)
        {
            Check.NotNull(_services, nameof(_services));
            Check.NotNull(_provider, nameof(_provider));

            var scopedResolver = _provider.GetService<IScopedServiceResolver>();
            if (scopedResolver != null && scopedResolver.ResolveEnabled)
                return scopedResolver.GetService(serviceType);
            return _provider.GetService(serviceType);
        }

        #endregion

        #region GetServices(解析指定类型的所有服务实例)

        /// <summary>
        /// 解析指定类型的所有服务实例
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        public IEnumerable<T> GetServices<T>()
        {
            Check.NotNull(_services, nameof(_services));
            Check.NotNull(_provider, nameof(_provider));

            var scopedResolver = _provider.GetService<IScopedServiceResolver>();
            if (scopedResolver != null && scopedResolver.ResolveEnabled)
                return scopedResolver.GetServices<T>();
            return _provider.GetServices<T>();
        }

        /// <summary>
        /// 解析指定类型的所有服务实例
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        public IEnumerable<object> GetServices(Type serviceType)
        {
            Check.NotNull(_services, nameof(_services));
            Check.NotNull(_provider, nameof(_provider));

            var scopedResolver = _provider.GetService<IScopedServiceResolver>();
            if (scopedResolver != null && scopedResolver.ResolveEnabled)
                return scopedResolver.GetServices(serviceType);
            return _provider.GetServices(serviceType);
        }

        #endregion

        #region Dispose(释放资源)

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放中</param>
        protected override void Dispose(bool disposing)
        {
            _services = null;
            _provider = null;
            base.Dispose(disposing);
        }

        #endregion
    }
}
