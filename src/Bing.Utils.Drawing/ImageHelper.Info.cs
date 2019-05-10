using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;

namespace Bing.Utils.Drawing
{
    /// <summary>
    /// 图片操作辅助类 - 信息
    /// </summary>
    public static partial class ImageHelper
    {
        #region GetImageExtensions(获取图片扩展名)

        /// <summary>
        /// 获取图片扩展名
        /// </summary>
        /// <param name="image">图片</param>
        /// <returns></returns>
        public static string GetImageExtension(Image image)
        {
            Type type = typeof(ImageFormat);
            PropertyInfo[] imageFormatList = type.GetProperties(BindingFlags.Static | BindingFlags.Public);
            for (int i = 0; i != imageFormatList.Length; i++)
            {
                ImageFormat formatClass = (ImageFormat)imageFormatList[i].GetValue(null, null);
                if (formatClass.Guid.Equals(image.RawFormat.Guid))
                {
                    return imageFormatList[i].Name;
                }
            }
            return string.Empty;
        }

        #endregion

        #region GetCodecInfo(获取特定图像编解码信息)

        /// <summary>
        /// 获取特定图像编解码信息
        /// </summary>
        /// <param name="image">图片</param>
        /// <returns></returns>
        public static ImageCodecInfo GetCodecInfo(Image image)
        {
            return GetCodecInfo(image.RawFormat);
        }

        /// <summary>
        /// 获取特定图像编解码信息
        /// </summary>
        /// <param name="imageFormat">图片格式</param>
        /// <returns></returns>
        public static ImageCodecInfo GetCodecInfo(ImageFormat imageFormat)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            return codecs.FirstOrDefault(x => x.FormatID == imageFormat.Guid);
        }

        #endregion
    }
}
