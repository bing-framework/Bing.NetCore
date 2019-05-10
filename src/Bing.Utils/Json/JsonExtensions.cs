using Newtonsoft.Json.Linq;

namespace Bing.Utils.Json
{
    /// <summary>
    /// Json辅助扩展操作
    /// </summary>
    public static class JsonExtensions
    {
        #region ToObject(将Json字符串转换为对象)

        /// <summary>
        /// 将Json字符串转换为对象
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="json">Json字符串</param>
        /// <returns></returns>
        public static T ToObject<T>(this string json)
        {
            return JsonHelper.ToObject<T>(json);
        }

        /// <summary>
        /// 将Json字符串转换为独享
        /// </summary>
        /// <param name="json">Json字符串</param>
        /// <returns></returns>
        public static object ToObject(this string json)
        {
            return JsonHelper.ToObject(json);
        }

        #endregion

        #region ToJson(将对象转换为Json字符串)

        /// <summary>
        /// 将对象转换为Json字符串
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="isConvertToSingleQuotes">是否将双引号转换成单引号</param>
        /// <param name="camelCase">是否驼峰式命名</param>
        /// <param name="indented">是否缩进</param>
        /// <returns></returns>
        public static string ToJson(this object target, bool isConvertToSingleQuotes = false, bool camelCase = false,
            bool indented = false)
        {
            return JsonHelper.ToJson(target, isConvertToSingleQuotes, camelCase, indented);
        }

        #endregion

        #region ToJObject(将Json字符串转换为Linq对象)

        /// <summary>
        /// 将Json字符串转换为Linq对象
        /// </summary>
        /// <param name="json">Json字符串</param>
        /// <returns></returns>
        public static JObject ToJObject(this string json)
        {
            return JsonHelper.ToJObject(json);
        }

        #endregion
    }
}
