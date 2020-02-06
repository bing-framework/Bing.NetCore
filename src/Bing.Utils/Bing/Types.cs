using System;
using System.Linq;

namespace Bing
{
    /// <summary>
    /// 类型 操作
    /// </summary>
    public static partial class Types
    {
        #region Of(获取类型)

        /// <summary>
        /// 获取类型
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        public static Type Of<T>() => Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        /// <summary>
        /// 获取类型数组
        /// </summary>
        /// <param name="objColl">对象数组</param>
        public static Type[] Of(object[] objColl)
        {
            if (objColl is null)
                return null;
            if (!objColl.Contains(null))
                return Type.GetTypeArray(objColl);
            var types = new Type[objColl.Length];
            for (var i = 0; i < objColl.Length; i++)
                types[i] = objColl[i].GetType();
            return types;
        }

        #endregion

        #region DefaultValue(获取默认值)

        /// <summary>
        /// 获取默认值
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        public static T DefaultValue<T>() => TypeDefault.Of<T>();

        #endregion
    }
}
