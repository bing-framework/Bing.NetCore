using System;
using System.Drawing;
using System.Drawing.Imaging;
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

        #region ToBase64String(将图片转换为Base64字符串)

        /// <summary>
        /// 将图片转换为Base64字符串，默认使用jpg格式
        /// </summary>
        /// <param name="image">图片</param>
        /// <returns></returns>
        public static string ToBase64String(Image image)
        {
            return ToBase64String(image, ImageFormat.Jpeg);
        }

        /// <summary>
        /// 将图片转换为Base64字符串，使用指定格式
        /// </summary>
        /// <param name="image">图片</param>
        /// <param name="imageFormat">图片格式</param>
        /// <returns></returns>
        public static string ToBase64String(Image image, ImageFormat imageFormat)
        {
            using (MemoryStream ms=new MemoryStream())
            {
                image.Save(ms, imageFormat);
                byte[] bytes = ms.ToArray();
                return Convert.ToBase64String(bytes);
            }
        }

        #endregion

        #region ToBase64StringUrl(将图片转换为Base64字符串URL格式)

        /// <summary>
        /// 将图片转换为Base64字符串URL格式
        /// 默认使用jpg格式，并添加data:image/jpg;base64,前缀
        /// </summary>
        /// <param name="image">图片</param>
        /// <returns></returns>
        public static string ToBase64StringUrl(Image image)
        {
            return $"data:image/jpg;base64,{ToBase64String(image, ImageFormat.Jpeg)}";
        }

        /// <summary>
        /// 将图片转换为Base64字符串URL格式。
        /// 使用指定格式，并添加data:image/jpg;base64,前缀
        /// </summary>
        /// <param name="image">图片</param>
        /// <param name="imageFormat">图片格式</param>
        /// <returns></returns>
        public static string ToBase64StringUrl(Image image, ImageFormat imageFormat)
        {
            return $"data:image/{imageFormat.ToString().ToLower()};base64,{ToBase64String(image, imageFormat)}";
        }

        #endregion
    }
}
