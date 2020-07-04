using System.IO;

// ReSharper disable once CheckNamespace
namespace Bing.IO
{
    /// <summary>
    /// 流(<see cref="Stream"/>) 扩展
    /// </summary>
    public static partial class StreamExtensions
    {
        /// <summary>
        /// 尝试重新设定流位置
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="offset">偏移量</param>
        /// <param name="origin">流定位</param>
        public static long TrySeek(this Stream stream, long offset, SeekOrigin origin) =>
            stream.CanSeek ? stream.Seek(offset, origin) : default;
    }
}
