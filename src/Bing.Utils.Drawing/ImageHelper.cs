using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Bing.Utils.Drawing
{
    /// <summary>
    /// 图片操作辅助类
    /// </summary>
    public static partial class ImageHelper
    {
        #region MakeThumbnail(生成缩略图)

        /// <summary>
        /// 生成缩略图
        /// </summary>
        /// <param name="sourceImage">源图</param>
        /// <param name="width">缩略图宽度</param>
        /// <param name="height">缩略图高度</param>
        /// <param name="mode">缩略图方式</param>
        public static Image MakeThumbnail(Image sourceImage, int width, int height, ThumbnailMode mode)
        {
            var towidth = width;
            var toheight = height;

            var x = 0;
            var y = 0;
            var ow = sourceImage.Width;
            var oh = sourceImage.Height;

            switch (mode)
            {
                case ThumbnailMode.FixedBoth:
                    break;

                case ThumbnailMode.FixedW:
                    toheight = oh * width / ow;
                    break;

                case ThumbnailMode.FixedH:
                    towidth = ow * height / oh;
                    break;

                case ThumbnailMode.Cut:
                    if (ow / (double)oh > towidth / (double)toheight)
                    {
                        oh = sourceImage.Height;
                        ow = sourceImage.Height * towidth / toheight;
                        y = 0;
                        x = (sourceImage.Width - ow) / 2;
                    }
                    else
                    {
                        ow = sourceImage.Width;
                        oh = sourceImage.Width * height / towidth;
                        x = 0;
                        y = (sourceImage.Height - oh) / 2;
                    }
                    break;
            }
            //1、新建一个BMP图片
            var bitmap = new Bitmap(towidth, toheight);
            //2、新建一个画板
            var g = Graphics.FromImage(bitmap);
            try
            {
                //3、设置高质量插值法
                g.InterpolationMode = InterpolationMode.High;
                //4、设置高质量，低速度呈现平滑程度
                g.SmoothingMode = SmoothingMode.HighQuality;
                //5、清空画布并以透明背景色填充
                g.Clear(Color.Transparent);
                //6、在指定位置并且按指定大小绘制原图片的指定部分
                g.DrawImage(sourceImage, new Rectangle(0, 0, towidth, toheight), new Rectangle(x, y, ow, oh),
                    GraphicsUnit.Pixel);
                return bitmap;
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                g.Dispose();
            }
        }

        /// <summary>
        /// 生成缩略图
        /// </summary>
        /// <param name="imgBytes">源文件字节数组</param>
        /// <param name="width">缩略图宽度</param>
        /// <param name="height">缩略图高度</param>
        /// <param name="mode">缩略图方式</param>
        public static Image MakeThumbnail(byte[] imgBytes, int width, int height, ThumbnailMode mode)
        {
            using (var sourceImage = FromBytes(imgBytes))
            {
                return MakeThumbnail(sourceImage, width, height, mode);
            }
        }

        /// <summary>
        /// 生成缩略图
        /// </summary>
        /// <param name="sourceImagePath">文件路径</param>
        /// <param name="thumbnailPath">缩略图文件生成路径</param>
        /// <param name="width">缩略图宽度</param>
        /// <param name="height">缩略图高度</param>
        /// <param name="mode">缩略图方式</param>
        public static void MakeThumbnail(string sourceImagePath, string thumbnailPath, int width, int height,
            ThumbnailMode mode)
        {
            using (var sourceImage = Image.FromFile(sourceImagePath))
            {
                using (var resultImage = MakeThumbnail(sourceImage, width, height, mode))
                {
                    resultImage.Save(thumbnailPath, ImageFormat.Jpeg);
                }
            }
        }

        /// <summary>
        /// 生成缩略图文件
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
                //以png格式保存缩略图
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
        /// 生成缩略图文件
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
        /// 生成缩略图文件
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

        #endregion

        #region TextWatermark(文字水印)

        //public static string TextWatermark(string path, string letter, int size, Color color, ImageLocationMode mode)
        //{
        //    if (string.IsNullOrWhiteSpace(path))
        //    {
        //        return string.Empty;
        //    }

        //    var extName = Path.GetExtension(path)?.ToLower();
        //    if (extName == ".jpg" || extName == ".bmp" || extName == ".jpeg")
        //    {
        //        var time = DateTime.Now;
        //        var fileName = time.ToString("yyyyMMddHHmmss.fff");
        //        var img = Image.FromFile(path);
        //        var g = Graphics.FromImage(img);
        //        var coors=GetLocation(mode,img,size)
        //    }
        //}

        ///// <summary>
        ///// 获取水印位置
        ///// </summary>
        ///// <param name="mode">水印位置</param>
        ///// <param name="img">图片</param>
        ///// <param name="width">宽度</param>
        ///// <param name="height">高度</param>
        ///// <returns></returns>
        //private static ArrayList GetLocation(ImageLocationMode mode, Image img, int width, int height)
        //{
        //    var coords = new ArrayList();
        //    var x = 0;
        //    var y = 0;

        //    switch (mode)
        //    {
        //        case ImageLocationMode.LeftTop:
        //            x = 10;
        //            y = 10;
        //            break;
        //        case ImageLocationMode.Top:
        //            x = img.Width / 2 - waterImg.Width / 2;
        //            y = img.Height - waterImg.Height;
        //            break;
        //        case ImageLocationMode.RightTop:
        //            x = img.Width - waterImg.Width;
        //            y = 10;
        //            break;
        //        case ImageLocationMode.LeftCenter:
        //            x = 10;
        //            y = img.Height / 2 - waterImg.Height / 2;
        //            break;
        //        case ImageLocationMode.Center:
        //            x = img.Width / 2 - waterImg.Width / 2;
        //            y = img.Height / 2 - waterImg.Height / 2;
        //            break;
        //        case ImageLocationMode.RightCenter:
        //            x = img.Width - waterImg.Width;
        //            y = img.Height / 2 - waterImg.Height / 2;
        //            break;
        //        case ImageLocationMode.LeftBottom:
        //            x = 10;
        //            y = img.Height - waterImg.Height;
        //            break;
        //        case ImageLocationMode.Bottom:
        //            x = img.Width / 2 - waterImg.Width / 2;
        //            y = img.Height - waterImg.Height;
        //            break;
        //        case ImageLocationMode.RightBottom:
        //            x = img.Width - waterImg.Width;
        //            y = img.Height - waterImg.Height;
        //            break;
        //    }
        //    coords.Add(x);
        //    coords.Add(y);
        //    return coords;
        //}

        #endregion

        #region DeleteCoordinate(删除图片中的经纬度信息)

        /// <summary>
        /// 删除图片中的经纬度信息，覆盖原图像
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static void DeleteCoordinate(string filePath)
        {
            using (var ms = new MemoryStream(File.ReadAllBytes(filePath)))
            {
                using (var image = Image.FromStream(ms))
                {
                    DeleteCoordinate(image);
                    image.Save(filePath);
                }
            }
        }

        /// <summary>
        /// 删除图片中的经纬度信息，并另存为
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="savePath">保存文件路径</param>
        public static void DeleteCoordinate(string filePath, string savePath)
        {
            using (var ms = new MemoryStream(File.ReadAllBytes(filePath)))
            {
                using (var image = Image.FromStream(ms))
                {
                    DeleteCoordinate(image);
                    image.Save(savePath);
                }
            }
        }

        /// <summary>
        /// 删除图片中的经纬度信息
        /// </summary>
        /// <param name="image">图片</param>
        public static void DeleteCoordinate(Image image)
        {
            /*PropertyItem 中对应属性
             * ID	Property tag
               0x0000	PropertyTagGpsVer
               0x0001	PropertyTagGpsLatitudeRef
               0x0002	PropertyTagGpsLatitude
               0x0003	PropertyTagGpsLongitudeRef
               0x0004	PropertyTagGpsLongitude
               0x0005	PropertyTagGpsAltitudeRef
               0x0006	PropertyTagGpsAltitude
             */
            var ids = new[] { 0x0000, 0x0001, 0x0002, 0x0003, 0x0004, 0x0005, 0x0006 };
            foreach (var id in ids)
            {
                if (image.PropertyIdList.Contains(id))
                {
                    image.RemovePropertyItem(id);
                }
            }
        }

        #endregion
    }
}
