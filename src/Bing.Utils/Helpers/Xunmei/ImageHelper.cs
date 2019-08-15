using System;
using System.ComponentModel;
using System.Drawing;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// Class ImageHelper.
    /// </summary>
    public class ImageHelper
    {
        #region 构造函数 (1)

        /// <summary>
        /// Prevents a default instance of the <see cref="ImageHelper"/> class from being created.
        /// </summary>
        private ImageHelper()
        {
        }

        #endregion 构造函数

        #region 公共方法 (1)

        /// <summary>
        /// 生成缩略图
        /// </summary>
        /// <param name="originalImagePath">源图路径（物理路径）</param>
        /// <param name="thumbnailPath">缩略图路径（物理路径）</param>
        /// <param name="width">缩略图宽度</param>
        /// <param name="height">缩略图高度</param>
        /// <param name="mode">生成缩略图的方式</param>
        /// <param name="allowBigger">允许生成缩略图大于原始尺寸</param>
        /// <param name="fillWithEmpty">自动以空白填充（只在allowBigger为 true时启用）</param>
        /// <exception cref="Exception">指定缩略图的尺寸不能大于原始图片!</exception>
        public static void MakeThumbnail(string originalImagePath, string thumbnailPath, int width, int height, string mode, bool allowBigger, bool fillWithEmpty)
        {
            var originalImage = Image.FromFile(originalImagePath);

            int towidth = width;//目标图片宽
            int toheight = height;//目标图片高
            int canvasHeight = height;//目标画布高
            int canvasWidth = width;//目标画面宽
            int canvasX = 0;//画布X偏移坐标
            int canvasY = 0;//画布Y偏移坐标
            int x = 0;//图片X偏移坐标
            int y = 0;//图片Y偏移坐标
            int ow = originalImage.Width;//图片原始宽
            int oh = originalImage.Height;//图片原始高

            //指定的长和宽不能同时大于原始图片
            if (towidth > ow && toheight > oh)
            {
                if (allowBigger)
                {
                    mode = fillWithEmpty ? ThumbImgMode.FillHW.ToString() : mode;
                }
                else
                {
                    originalImage.Dispose();
                    throw new Exception("指定缩略图的尺寸不能大于原始图片!");
                }
            }

            bool scale = (ow * 1.0 / oh) > (towidth * 1.0 / toheight);//原始的长宽比是否大于目标图片长宽比

            switch (mode)
            {
                case "HW"://指定高宽缩放（可能变形）
                    break;

                case "FillHW"://画布固定大小，图片居中，根据长宽比例，自动以宽或高按比例缩放
                    if (scale)
                        toheight = originalImage.Height * width / originalImage.Width;//高按比例
                    else
                        towidth = originalImage.Width * height / originalImage.Height;//宽按比例

                    canvasX = (canvasWidth - towidth) / 2;
                    canvasY = (canvasHeight - toheight) / 2;

                    break;

                case "AutoHW"://根据长宽比例，自动以宽或高按比例缩放
                    if (scale)
                        toheight = originalImage.Height * width / originalImage.Width;//高按比例
                    else
                        towidth = originalImage.Width * height / originalImage.Height;//宽按比例
                    canvasWidth = towidth;
                    canvasHeight = toheight;
                    break;

                case "W"://指定宽，高按比例
                    toheight = originalImage.Height * width / originalImage.Width;
                    canvasHeight = toheight;
                    break;

                case "H"://指定高，宽按比例
                    towidth = originalImage.Width * height / originalImage.Height;
                    canvasWidth = towidth;
                    break;

                case "Cut"://指定高宽裁减（不变形）
                    if ((double)originalImage.Width / (double)originalImage.Height > (double)towidth / (double)toheight)
                    {
                        oh = originalImage.Height;
                        ow = originalImage.Height * towidth / toheight;
                        y = 0;
                        x = (originalImage.Width - ow) / 2;
                    }
                    else
                    {
                        ow = originalImage.Width;
                        oh = originalImage.Width * height / towidth;
                        x = 0;
                        y = (originalImage.Height - oh) / 2;
                    }
                    canvasWidth = towidth;
                    canvasHeight = toheight;
                    break;

                default:
                    break;
            }

            //新建一个bmp图片
            Image bitmap = new System.Drawing.Bitmap(canvasWidth, canvasHeight);
            //新建一个画板
            Graphics g = System.Drawing.Graphics.FromImage(bitmap);
            //设置高质量插值法
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
            //设置高质量,低速度呈现平滑程度
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            //清空画布并以透明背景色填充
            g.Clear(Color.Transparent);
            //在指定位置并且按指定大小绘制原图片的指定部分
            g.DrawImage(originalImage, new Rectangle(canvasX, canvasY, towidth, toheight),
                new Rectangle(x, y, ow, oh),
                GraphicsUnit.Pixel);
            try
            {
                //以jpg格式保存缩略图
                bitmap.Save(thumbnailPath, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch (System.Exception e)
            {
                throw e;
            }
            finally
            {
                originalImage.Dispose();
                bitmap.Dispose();
                g.Dispose();
            }
        }

        /// <summary>
        /// 生成缩略图
        /// </summary>
        /// <param name="originalImagePath">源图路径（物理路径）</param>
        /// <param name="thumbnailPath">缩略图路径（物理路径）</param>
        /// <param name="width">缩略图宽度</param>
        /// <param name="height">缩略图高度</param>
        /// <param name="mode">生成缩略图的方式</param>
        public static void MakeThumbnail(string originalImagePath, string thumbnailPath, int width, int height, string mode)
        {
            MakeThumbnail(originalImagePath, thumbnailPath, width, height, mode, true, true);
        }

        /// <summary>
        /// 生成缩略图
        /// </summary>
        /// <param name="originalImagePath">源图路径（物理路径）</param>
        /// <param name="thumbnailPath">缩略图路径（物理路径）</param>
        /// <param name="width">缩略图宽度</param>
        /// <param name="height">缩略图高度</param>
        /// <param name="mode">生成缩略图的方式</param>
        public static void MakeThumbnail(string originalImagePath, string thumbnailPath, int width, int height, ThumbImgMode mode)
        {
            MakeThumbnail(originalImagePath, thumbnailPath, width, height, mode.ToString());
        }

        #endregion 公共方法
    }

    /// <summary>
    /// 缩略图模式
    /// </summary>
    public enum ThumbImgMode
    {
        /// <summary>
        /// 默认
        /// </summary>
        [Description("默认")]
        Default = 0,

        /// <summary>
        /// 指定高宽缩放（可能变形）
        /// </summary>
        [Description("指定高宽缩放（可能变形）")]
        HW,

        /// <summary>
        /// 画布固定大小，图片居中，根据长宽比例，自动以宽或高按比例缩放
        /// </summary>
        [Description("画布固定大小，图片居中，根据长宽比例，自动以宽或高按比例缩放")]
        FillHW,

        /// <summary>
        /// 根据长宽比例，自动以宽或高按比例缩放
        /// </summary>
        [Description("根据长宽比例，自动以宽或高按比例缩放")]
        AutoHW,

        /// <summary>
        /// 指定宽，高按比例
        /// </summary>
        [Description("指定宽，高按比例")]
        W,

        /// <summary>
        /// 指定高，宽按比例
        /// </summary>
        [Description("指定高，宽按比例")]
        H,

        /// <summary>
        /// 指定高宽裁减（不变形）
        /// </summary>
        [Description("指定高宽裁减（不变形）")]
        Cut
    }
}
