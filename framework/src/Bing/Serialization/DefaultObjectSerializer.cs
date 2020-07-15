using System;
using Bing.DependencyInjection;
using Bing.Serialization.Binary;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Serialization
{
    /// <summary>
    /// 默认对象序列化器
    /// </summary>
    public class DefaultObjectSerializer : IObjectSerializer, ITransientDependency
    {
        /// <summary>
        /// 服务提供程序
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 初始化一个<see cref="DefaultObjectSerializer"/>类型的实例
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        public DefaultObjectSerializer(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        /// <summary>
        /// 序列化
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">对象</param>
        public virtual byte[] Serialize<T>(T obj)
        {
            if (obj == null)
                return null;

            // 检查是否已注册特定的序列化程序
            using (var scope = _serviceProvider.CreateScope())
            {
                var specificSerializer = scope.ServiceProvider.GetService<IObjectSerializer<T>>();
                if (specificSerializer != null)
                    return specificSerializer.Serialize(obj);
            }

            return AutoSerialize(obj);
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="bytes">字节数组</param>
        public virtual T Deserialize<T>(byte[] bytes)
        {
            if (bytes == null)
                return default;

            // 检查是否已注册特定的序列化程序
            using (var scope = _serviceProvider.CreateScope())
            {
                var specificSerializer = scope.ServiceProvider.GetService<IObjectSerializer<T>>();
                if (specificSerializer != null)
                {
                    return specificSerializer.Deserialize(bytes);
                }
            }

            return AutoDeserialize<T>(bytes);
        }

        /// <summary>
        /// 自动序列化
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="obj">对象</param>
        protected virtual byte[] AutoSerialize<T>(T obj) => BinarySerializationUtil.Serialize(obj);

        /// <summary>
        /// 自动反序列化
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="bytes">字节数组</param>
        protected virtual T AutoDeserialize<T>(byte[] bytes) => (T)BinarySerializationUtil.DeserializeExtended(bytes);
    }
}
