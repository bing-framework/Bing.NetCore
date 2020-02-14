using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 字符串拼接器(<see cref="StringBuilder" />) 扩展
    /// </summary>
    public static partial class StringBuilderExtensions
    {
        /// <summary>
        /// 反转字符串
        /// </summary>
        /// <param name="builder">StringBuilder</param>
        public static void Reverse(this StringBuilder builder)
        {
            if (builder == null || builder.Length == 0)
                return;
            var destination = new char[builder.Length];
            builder.CopyTo(0, destination, 0, builder.Length);
            //destination.Reverse();

            builder.Clear();
            builder.Append(destination);
        }

    }
}
