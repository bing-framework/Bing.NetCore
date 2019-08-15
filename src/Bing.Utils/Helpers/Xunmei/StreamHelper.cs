using System.IO;

namespace Bing.Utils.Helpers
{
    /// <summary>
    ///
    /// </summary>
    public static class StreamHelper
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="context"></param>
        public static void Write(this Stream stream, string context)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(context);
            stream.Write(buffer, 0, buffer.Length);
        }
    }
}
