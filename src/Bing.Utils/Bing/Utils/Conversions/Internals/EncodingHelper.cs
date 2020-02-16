using System.Text;

namespace Bing.Utils.Conversions.Internals
{
    /// <summary>
    /// 编码操作辅助类
    /// </summary>
    internal static class EncodingHelper
    {
        /// <summary>
        /// 补全。如果编码为空，则默认返回<see cref="Encoding.UTF8"/>
        /// </summary>
        /// <param name="encoding">编码</param>
        public static Encoding Fixed(this Encoding encoding) => encoding ?? Encoding.UTF8;
    }
}
