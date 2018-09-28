using NPOI.SS.UserModel;

namespace Bing.Offices.Excels.Npoi.Extensions
{
    /// <summary>
    /// 字体(<see cref="IFont"/>) 扩展
    /// </summary>
    public static class FontExtensions
    {
        /// <summary>
        /// 设置字体大小
        /// </summary>
        /// <param name="font">字体</param>
        /// <param name="fontSize">字体大小</param>
        /// <returns></returns>
        public static IFont SetFontHeightInPoints(this IFont font, short fontSize)
        {
            font.FontHeightInPoints = fontSize;
            return font;
        }

        /// <summary>
        /// 设置字体颜色
        /// </summary>
        /// <param name="font">字体</param>
        /// <param name="color">颜色</param>
        /// <returns></returns>
        public static IFont SetColor(this IFont font, short color)
        {
            font.Color = color;
            return font;
        }

        /// <summary>
        /// 设置粗体
        /// </summary>
        /// <param name="font">字体</param>
        /// <param name="boldweight">粗体大小</param>
        /// <returns></returns>
        public static IFont SetBoldweight(this IFont font, short boldweight)
        {
            font.Boldweight = boldweight;
            return font;
        }
    }
}
