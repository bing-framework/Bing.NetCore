using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// <see cref="StringBuilder"/> 扩展
    /// </summary>
    public static partial class StringBuilderExtensions
    {
        /// <summary>
        /// 转换为字符数组
        /// </summary>
        /// <param name="builder">StringBuilder</param>
        public static char[] ToCharArray(this StringBuilder builder)
        {
            var res = new char[builder.Length];
            builder.CopyTo(0, res, 0, builder.Length);
            return res;
        }
    }
}
