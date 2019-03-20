using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Bing.Utils.Drawing
{
    /// <summary>
    /// 图片操作辅助类 - 转换
    /// </summary>
    public static partial class ImageUtil
    {
        #region ToBytes(转换为字节数组)

        /// <summary>
        /// 将图像转换为字节数组
        /// </summary>
        /// <param name="bitmap">图像</param>
        /// <returns></returns>
        public static byte[] ToBytes(Bitmap bitmap)
        {
            using (var newBitmap = new Bitmap(bitmap))
            {
                using (var ms = new MemoryStream())
                {
                    ImageFormat format = newBitmap.RawFormat;
                    if (ImageFormat.MemoryBmp.Equals(format))
                    {
                        format = ImageFormat.Bmp;
                    }

                    newBitmap.Save(ms, format);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// 将图片转换成字节数组
        /// </summary>
        /// <param name="image">图片</param>
        /// <returns></returns>
        public static byte[] ToBytes(Image image)
        {
            using (var ms = new MemoryStream())
            {
                ImageFormat format = image.RawFormat;
                if (ImageFormat.MemoryBmp.Equals(format))
                {
                    format = ImageFormat.Bmp;
                }

                image.Save(ms, format);
                return ms.ToArray();
            }
        }

        #endregion

        #region ToStream(转换为内存流)

        /// <summary>
        /// 将图片转换为内存流，需要释放资源
        /// </summary>
        /// <param name="image">图片</param>
        /// <returns></returns>
        public static Stream ToStream(Image image)
        {
            MemoryStream ms = new MemoryStream();
            image.Save(ms, image.RawFormat);
            return ms;
        }

        /// <summary>
        /// 将图像转换为内存流，需要释放资源
        /// </summary>
        /// <param name="bitmap">图像</param>
        /// <returns></returns>
        public static Stream ToStream(Bitmap bitmap)
        {
            MemoryStream ms = new MemoryStream();
            bitmap.Save(ms, bitmap.RawFormat);
            return ms;
        }

        #endregion

        #region ToBase64String(转换为Base64字符串)

        /// <summary>
        /// 将图片转换为Base64字符串
        /// </summary>
        /// <param name="image">图片</param>
        /// <param name="appendPrefix">是否追加前缀</param>
        /// <returns></returns>
        public static string ToBase64String(Image image, bool appendPrefix = false)
        {
            return ToBase64String(image, image.RawFormat, appendPrefix);
        }

        /// <summary>
        /// 将图片转换为Base64字符串
        /// </summary>
        /// <param name="image">图片</param>
        /// <param name="imageFormat">图片格式</param>
        /// <param name="appendPrefix">是否追加前缀</param>
        /// <returns></returns>
        public static string ToBase64String(Image image, ImageFormat imageFormat, bool appendPrefix = false)
        {
            using (var ms = new MemoryStream())
            {
                image.Save(ms, imageFormat);
                var result = Convert.ToBase64String(ms.ToArray());
                if (appendPrefix)
                {
                    result = $"data:image/{imageFormat.ToString().ToLower()};base64,{result}";
                }

                return result;
            }
        }

        /// <summary>
        /// 将图像转换为Base64字符串
        /// </summary>
        /// <param name="bitmap">图像</param>
        /// <param name="appendPrefix">是否追加前缀</param>
        /// <returns></returns>
        public static string ToBase64String(Bitmap bitmap, bool appendPrefix = false)
        {
            return ToBase64String(bitmap, bitmap.RawFormat, appendPrefix);
        }

        /// <summary>
        /// 将图像转换为Base64字符串
        /// </summary>
        /// <param name="bitmap">图像</param>
        /// <param name="imageFormat">图片格式</param>
        /// <param name="appendPrefix">是否追加前缀</param>
        /// <returns></returns>
        public static string ToBase64String(Bitmap bitmap, ImageFormat imageFormat, bool appendPrefix = false)
        {
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, imageFormat);
                var result = Convert.ToBase64String(ms.ToArray());
                if (appendPrefix)
                {
                    result = $"data:image/{imageFormat.ToString().ToLower()};base64,{result}";
                }

                return result;
            }
        }

        #endregion
    }
}
