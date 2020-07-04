using Bing.Collections;

namespace System.Data
{
    /// <summary>
    /// 连接状态(<see cref="ConnectionState"/>) 扩展
    /// </summary>
    public static class ConnectionStateExtensions
    {
        /// <summary>
        /// 当前连接状态是否在指定连接状态数组内
        /// </summary>
        /// <param name="this">ConnectionState</param>
        /// <param name="values">连接状态数组</param>
        public static bool In(this ConnectionState @this, params ConnectionState[] values) =>
            values.IndexOf(@this) != -1;

        /// <summary>
        /// 当前连接状态是否不在指定连接状态数组内
        /// </summary>
        /// <param name="this">ConnectionState</param>
        /// <param name="values">连接状态数组</param>
        public static bool NotIn(this ConnectionState @this, params ConnectionState[] values) =>
            values.IndexOf(@this) == -1;
    }
}
