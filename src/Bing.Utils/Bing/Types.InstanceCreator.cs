using System;
using System.Linq;
using System.Reflection;
using AspectCore.Extensions.Reflection;

namespace Bing
{
    /// <summary>
    /// 类型 操作 - 实例创建者
    /// </summary>
    public static partial class Types
    {
        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="TInstance">实例类型</typeparam>
        /// <param name="args">参数</param>
        public static TInstance CreateInstance<TInstance>(params object[] args) =>
            args is null || args.Length == 0
                ? CreateInstanceCore<TInstance>()
                : CreateInstanceCore<TInstance>(args);

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="TInstance">实例类型</typeparam>
        /// <param name="className">实例名称</param>
        /// <param name="args">参数</param>
        public static TInstance CreateInstance<TInstance>(string className, params object[] args)
        {
            var type = Type.GetType(className) ?? Assembly.GetCallingAssembly().GetType(className);
            return CreateInstance<TInstance>(type, args);
        }

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="TInstance">实例类型</typeparam>
        /// <param name="type">实例类型</param>
        /// <param name="args">参数</param>
        public static TInstance CreateInstance<TInstance>(Type type, params object[] args) =>
            CreateInstance(type, args) is TInstance ret ? ret : default;

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <param name="type">实例类型</param>
        /// <param name="args">参数</param>
        public static object CreateInstance(Type type, params object[] args) => args is null || args.Length == 0
            ? CreateInstanceCore(type)
            : CreateInstanceCore(type, args);

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="TInstance">实例类型</typeparam>
        private static TInstance CreateInstanceCore<TInstance>() =>
            CreateInstanceCore(typeof(TInstance)) is TInstance ret ? ret : default;

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <typeparam name="TInstance">实例类型</typeparam>
        /// <param name="args">参数</param>
        private static TInstance CreateInstanceCore<TInstance>(object[] args) =>
            CreateInstanceCore(typeof(TInstance), args) is TInstance ret ? ret : default;

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <param name="type">实例类型</param>
        private static object CreateInstanceCore(Type type) => type.GetConstructors()
            .FirstOrDefault(x => !x.GetParameters().Any())?.GetReflector().Invoke();

        /// <summary>
        /// 创建实例
        /// </summary>
        /// <param name="type">实例类型</param>
        /// <param name="args">参数</param>
        private static object CreateInstanceCore(Type type, object[] args) => type.GetConstructor(Of(args))?.GetReflector().Invoke(args);
    }
}
