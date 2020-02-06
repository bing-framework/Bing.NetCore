using System;

namespace Bing.Dependency
{
    /// <summary>
    /// 作用域
    /// </summary>
    public interface IScope : IDisposable
    {
        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="T">实例类型</typeparam>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        T Create<T>(string name = null);

        /// <summary>
        /// 创建对象
        /// </summary>
        /// <param name="type">对象类型</param>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        object Create(Type type, string name = null);
    }
}
