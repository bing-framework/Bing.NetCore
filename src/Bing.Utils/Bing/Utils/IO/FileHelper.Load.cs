using System.IO;
using System.Threading.Tasks;
using Bing.Helpers;
using Bing.Utils.Helpers;

namespace Bing.Utils.IO
{
    /// <summary>
    /// 文件操作帮助类 - 加载
    /// </summary>
    public static partial class FileHelper
    {
        #region ReadAllText(读取文件所有文本)

        /// <summary>
        /// 读取文件所有文本
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static async Task<string> ReadAllTextAsync(string filePath)
        {
            Check.NotNull(filePath, nameof(filePath));
            using (var reader = File.OpenText(filePath))
            {
                return await reader.ReadToEndAsync();
            }
        }

        #endregion

        #region ReadAllBytes(读取文件所有字节)

        /// <summary>
        /// 读取文件所有字节
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static async Task<byte[]> ReadAllBytesAsync(string filePath)
        {
            Check.NotNull(filePath, nameof(filePath));
            using (var stream = File.Open(filePath, FileMode.Open))
            {
                var result = new byte[stream.Length];
                await stream.ReadAsync(result, 0, (int)stream.Length);
                return result;
            }
        }

        #endregion

    }
}
