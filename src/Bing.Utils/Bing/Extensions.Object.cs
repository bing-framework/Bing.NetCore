using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Text;

namespace Bing
{
    /// <summary>
    /// 对象(<see cref="object"/>) 扩展
    /// </summary>
    public static class ObjectExtensions
    {
        #region As(强制转换)

        /// <summary>
        /// 强制转换
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="this">对象</param>
        public static T As<T>(this object @this) => (T)@this;

        /// <summary>
        /// 强制转换。如果转换失败，则返回默认值
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="this">对象</param>
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
        /// 强制转换。如果转换失败，则返回默认值
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="this">对象</param>
        /// <param name="defaultVal">默认值</param>
        public static T AsOrDefault<T>(this object @this,T defaultVal)
        {
            try
            {
                return (T)@this;
            }
            catch (Exception)
            {
                return defaultVal;
            }
        }

        /// <summary>
        /// 强制转换。如果转换失败，则返回默认值
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="this">对象</param>
        /// <param name="defaultValueFactory">默认值</param>
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
        /// 强制转换。如果转换失败，则返回默认值
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="this">对象</param>
        /// <param name="defaultValueFactory">默认值</param>
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

        #endregion

        #region TryAs(尝试强制转换)

        /// <summary>
        /// 尝试强制转换
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="this">对象</param>
        /// <param name="value">值</param>
        public static bool TryAs<T>(this object @this, out T value)
        {
            try
            {
                value = @this.As<T>();
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// 尝试强制转换。如果转换失败，则返回默认值
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="this">对象</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="value">值</param>
        public static bool TryAsOrDefault<T>(this object @this, T defaultValue, out T value)
        {
            try
            {
                value = @this.As<T>();
                return true;
            }
            catch
            {
                value = defaultValue;
                return false;
            }
        }

        /// <summary>
        /// 尝试强制转换。如果转换失败，则返回默认值
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="this">对象</param>
        /// <param name="defaultValueFactory">默认值</param>
        /// <param name="value">值</param>
        public static bool TryAsOrDefault<T>(this object @this, Func<T> defaultValueFactory, out T value)
        {
            try
            {
                value = @this.As<T>();
                return true;
            }
            catch
            {
                value = defaultValueFactory();
                return false;
            }
        }

        /// <summary>
        /// 尝试强制转换。如果转换失败，则返回默认值
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="this">对象</param>
        /// <param name="defaultValueFactory">默认值</param>
        /// <param name="value">值</param>
        public static bool TryAsOrDefault<T>(this object @this, Func<object, T> defaultValueFactory, out T value)
        {
            try
            {
                value = @this.As<T>();
                return true;
            }
            catch
            {
                value = defaultValueFactory(@this);
                return false;
            }
        }

        #endregion

        #region IsOn(是否在指定列表内)

        /// <summary>
        /// 是否在指定列表内
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="list">列表</param>
        public static bool IsOn(this byte source, params byte[] list) => IsOn<byte>(source, list);

        /// <summary>
        /// 是否在指定列表内
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="list">列表</param>
        public static bool IsOn(this short source, params short[] list) => IsOn<short>(source, list);

        /// <summary>
        /// 是否在指定列表内
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="source">数据源</param>
        /// <param name="list">列表</param>
        public static bool IsOn<T>(this T source, params T[] list) where T : IComparable => list.Any(t => t.CompareTo(source) == 0);

        /// <summary>
        /// 是否在指定列表内
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="source">数据源</param>
        /// <param name="list">列表</param>
        public static bool IsOn<T>(this T source, IEnumerable<T> list) where T : IComparable => list.Any(item => item.CompareTo(source) == 0);

        /// <summary>
        /// 是否在指定列表内
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="source">数据源</param>
        /// <param name="list">列表</param>
        public static bool IsOn<T>(this T source, HashSet<T> list) where T : IComparable => list.Contains(source);

        /// <summary>
        /// 是否在指定列表内
        /// </summary>
        /// <param name="source">数据源</param>
        /// <param name="list">列表</param>
        public static bool IsOnIgnoreCase(this string source, params string[] list) => list.Any(source.EqualsIgnoreCase);

        #endregion
    }
}
