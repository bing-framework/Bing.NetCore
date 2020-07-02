using System;

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

        //public static bool TryAs<T>(this object @this, out T value)
        //{
        //    try
        //    {

        //    }
        //    catch
        //    {
        //        value = default;
        //        return false;
        //    }
        //}

        #endregion
    }
}
