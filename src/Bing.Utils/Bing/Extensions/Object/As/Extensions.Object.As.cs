using System;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 对象(<see cref="object"/>) 扩展
    /// </summary>
    public static partial class ObjectExtensions
    {
        /// <summary>
        /// 转换为指定对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="this">object</param>
        public static T As<T>(this object @this) => (T)@this;

        /// <summary>
        /// 转换为指定对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="this">当前对象</param>
        public static T AsOrDefault<T>(this object @this)
        {
            try
            {
                return (T)@this;
            }
            catch (Exception)
            {
                return default;
            }
        }

        /// <summary>
        /// 转换为指定对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="this">object</param>
        /// <param name="defaultValue">默认值</param>
        public static T AsOrDefault<T>(this object @this, T defaultValue)
        {
            try
            {
                return (T)@this;
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 转换为指定对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="this">当前对象</param>
        /// <param name="defaultValueFactory">默认值工厂</param>
        public static T AsOrDefault<T>(this object @this, Func<T> defaultValueFactory)
        {
            try
            {
                return (T)@this;
            }
            catch (Exception)
            {
                return defaultValueFactory();
            }
        }

        /// <summary>
        /// 转换为指定对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="this">object</param>
        /// <param name="defaultValueFactory">默认值工厂</param>
        public static T AsOrDefault<T>(this object @this, Func<object, T> defaultValueFactory)
        {
            try
            {
                return (T)@this;
            }
            catch (Exception)
            {
                return defaultValueFactory(@this);
            }
        }
    }
}
