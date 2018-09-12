using System;
using System.Drawing;
using System.IO;

namespace Bing.Utils.Medias.Images
{
    /// <summary>
    /// 图片操作辅助类 - 转换
    /// </summary>
    public static partial class ImageUtil
    {
        #region ByteToImage(将字节数组转换成图片)

        /// <summary>
        /// 将字节数组转换成图片
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <returns></returns>
        public static Image ByteToImage(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                Image image = Image.FromStream(ms);
                return image;
            }
        }

        #endregion

        #region ImageToByte(将图片转换成字节数组)

        /// <summary>
        /// 将图片转换成字节数组
        /// </summary>
        /// <param name="image">图片</param>
        /// <returns></returns>
        public static byte[] ImageToByte(Image image)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, image.RawFormat);
                byte[] bytes = new byte[ms.Length];
                ms.Seek(0, SeekOrigin.Begin);
                ms.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }

        #endregion

        #region ImageToStream(将图片转换成字节流)

        /// <summary>
        /// 将图片转换成字节流
        /// </summary>
        /// <param name="image">图片</param>
        /// <returns></returns>
        public static Stream ImageToStream(Image image)
        {
            MemoryStream ms=new MemoryStream();
            image.Save(ms,image.RawFormat);
            return ms;
        }

        #endregion

        #region ImageToBase64(将图片转换成Base64字符串)

        /// <summary>
        /// 将图片转换成Base64字符串
        /// </summary>
        /// <param name="image">图片</param>
        /// <returns></returns>
        public static string ImageToBase64(Image image)
        {
            using (MemoryStream ms=new MemoryStream())
            {
                image.Save(ms, image.RawFormat);
                byte[] bytes = new byte[ms.Length];
                ms.Seek(0, SeekOrigin.Begin);
                ms.Read(bytes, 0, bytes.Length);
                return Convert.ToBase64String(bytes);
            }
        }

        /// <summary>
        /// 将图片转换成Base64字符串，带有头部"data:image/png;base64,xxxxx"
        /// </summary>
        /// <param name="image">图片</param>
        /// <returns></returns>
        public static string ImageToBase64WithHeader(Image image)
        {
            return $"data:image/{GetImageExtension(image)};base64,{ImageToBase64(image)}";
        }

        #endregion

        #region Base64ToImage(将Base64字符串转换成图片)

        /// <summary>
        /// 将Base64字符串转换成图片
        /// </summary>
        /// <param name="base64">Base64字符串</param>
        /// <returns></returns>
        public static Image Base64ToImage(string base64)
        {
            if (base64.IndexOf(',') > -1)
            {
                base64 = base64.Substring(base64.IndexOf(',') + 1);
            }
            byte[] bytes = Convert.FromBase64String(base64);
            using (MemoryStream ms=new MemoryStream(bytes))
            {
                Image image = Image.FromStream(ms);
                return image;
            }
        }

        #endregion
    }
}
