using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Bing.Utils.Medias.Images
{
    /// <summary>
    /// 图片操作辅助类 - 信息
    /// </summary>
    public static partial class ImageUtil
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

        #region GetBase64String(获取真正的图片Base64数据)

        /// <summary>
        /// 获取真正的图片Base64数据。
        /// 即去掉data:image/jpg;base64,这样的格式
        /// </summary>
        /// <param name="base64Url">带前缀的Base64图片字符串</param>
        /// <returns></returns>
        public static string GetBase64String(string base64Url)
        {
            string parttern = "^(data:image/.*?;base64,).*?$";
            var match = Regex.Match(base64Url, parttern);
            return base64Url.Replace(match.Groups[1].ToString(), "");
        }

        #endregion
    }
}
