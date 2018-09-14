using System;
using System.Drawing;
using System.IO;

namespace Bing.Utils.Medias.Images
{
    /// <summary>
    /// 图片操作辅助类 - 加载
    /// </summary>
    public static partial class ImageUtil
    {
        #region FromFile(从指定文件创建图片)

        /// <summary>
        /// 从指定文件创建图片
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        /// <returns></returns>
        public static Image FromFile(string filePath)
        {
            return Image.FromFile(filePath);
        }

        #endregion

        #region FromStream(从指定流创建图片)

        /// <summary>
        /// 从指定流创建图片
        /// </summary>
        /// <param name="stream">流</param>
        /// <returns></returns>
        public static Image FromStream(Stream stream)
        {
            return Image.FromStream(stream);
        }

        #endregion

        #region FromBytes(从指定字节数组创建图片)

        /// <summary>
        /// 从指定字节数组创建图片
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns></returns>
        public static Image FromBytes(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return FromStream(stream);
            }
        }

        #endregion

        #region FromBase64(从Base64字符串创建图片)

        /// <summary>
        /// 从Base64字符串创建图片
        /// </summary>
        /// <param name="base64">Base64字符串</param>
        /// <returns></returns>
        public static Image FromBase64(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);
            using (MemoryStream ms=new MemoryStream(bytes))
            {
                return Image.FromStream(ms);
            }
        }

        #endregion

        #region FromBase64Url(从URL格式的Base64字符串创建图片)

        /// <summary>
        /// 从URL格式的Base64字符串创建图片。
        /// 即去掉data:image/jpg;base64,这样的格式
        /// </summary>
        /// <param name="base64Url">带前缀的Base64图片字符串</param>
        /// <returns></returns>
        public static Image FromBase64Url(string base64Url)
        {
            string base64 = GetBase64String(base64Url);
            return FromBase64(base64);
        }

        #endregion
    }
}
