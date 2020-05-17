using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;

namespace Bing.Drawing
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
        public static string GetImageExtension(Image image)
        {
            var type = typeof(ImageFormat);
            var imageFormatList = type.GetProperties(BindingFlags.Static | BindingFlags.Public);
            for (var i = 0; i != imageFormatList.Length; i++)
            {
                var formatClass = (ImageFormat)imageFormatList[i].GetValue(null, null);
                if (formatClass.Guid.Equals(image.RawFormat.Guid))
                    return imageFormatList[i].Name;
            }
            return string.Empty;
        }

        #endregion

        #region GetCodecInfo(获取特定图像编解码信息)

        /// <summary>
        /// 获取特定图像编解码信息
        /// </summary>
        /// <param name="image">图片</param>
        public static ImageCodecInfo GetCodecInfo(Image image) => GetCodecInfo(image.RawFormat);

        /// <summary>
        /// 获取特定图像编解码信息
        /// </summary>
        /// <param name="imageFormat">图片格式</param>
        public static ImageCodecInfo GetCodecInfo(ImageFormat imageFormat)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();
            return codecs.FirstOrDefault(x => x.FormatID == imageFormat.Guid);
        }

        #endregion
    }
}
