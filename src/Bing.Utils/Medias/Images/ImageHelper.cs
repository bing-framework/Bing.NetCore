using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Bing.Utils.Medias.Images
{
    /// <summary>
    /// 图片操作辅助类
    /// </summary>
    public static partial class ImageHelper
    {
        #region BrightnessHandle(亮度处理)

        /// <summary>
        /// 亮度处理
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="val">增加或减少的光暗值</param>
        /// <returns></returns>
        public static Bitmap BrightnessHandle(Bitmap bitmap, int width, int height, int val)
        {
            Bitmap bm = new Bitmap(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    // 红绿蓝三值
                    var resultR = pixel.R + val;
                    var resultG = pixel.G + val;
                    var resultB = pixel.B + val;
                    bm.SetPixel(x, y, Color.FromArgb(resultR, resultG, resultB));
                }
            }
            return bm;
        }

        #endregion

        #region FilterColor(滤色处理)

        /// <summary>
        /// 滤色处理
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <returns></returns>
        public static Bitmap FilterColor(Bitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    bitmap.SetPixel(x, y, Color.FromArgb(0, pixel.G, pixel.B));
                }
            }
            return bitmap;
        }

        #endregion

        #region LeftRightTurn(左右翻转)

        /// <summary>
        /// 左右翻转
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <returns></returns>
        public static Bitmap LeftRightTurn(Bitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            for (var y = height - 1; y >= 0; y--)
            {
                for (int x = width - 1, z = 0; x >= 0; x--)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    bitmap.SetPixel(z++, y, Color.FromArgb(pixel.R, pixel.G, pixel.B));
                }
            }
            return bitmap;
        }

        #endregion

        #region TopBottomTurn(上下翻转)

        /// <summary>
        /// 上下翻转
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <returns></returns>
        public static Bitmap TopBottomTurn(Bitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            for (var x = 0; x < width; x++)
            {
                for (int y = height - 1, z = 0; y >= 0; y--)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    bitmap.SetPixel(x, z++, Color.FromArgb(pixel.R, pixel.G, pixel.B));
                }
            }
            return bitmap;
        }

        #endregion

        #region ToBlackWhiteImage(转换为黑白图片)

        /// <summary>
        /// 转换为黑白图片
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <returns></returns>
        public static Bitmap ToBlackWhiteImage(Bitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    int result = (pixel.R + pixel.G + pixel.B) / 3;
                    bitmap.SetPixel(x, y, Color.FromArgb(result, result, result));
                }
            }
            return bitmap;
        }

        #endregion

        #region TwistImage(扭曲图片，滤镜效果)

        /// <summary>
        /// 正弦曲线Wave扭曲图片
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <param name="isTwist">是否扭曲，true:扭曲,false:不扭曲</param>
        /// <param name="shapeMultValue">波形的幅度倍数，越大扭曲的程度越高，默认为3</param>
        /// <param name="shapePhase">波形的起始相位，取值区间[0-2*PI]</param>
        /// <returns></returns>
        public static Bitmap TwistImage(Bitmap bitmap, bool isTwist, double shapeMultValue, double shapePhase)
        {
            Bitmap destBitmap = new Bitmap(bitmap.Width, bitmap.Height);
            // 将位图背景填充为白色
            Graphics g = Graphics.FromImage(destBitmap);
            g.FillRectangle(new SolidBrush(Color.White), 0, 0, destBitmap.Width, destBitmap.Height);
            g.Dispose();
            double dBaseAxisLen = isTwist ? (double)destBitmap.Height : (double)destBitmap.Width;
            for (var i = 0; i < destBitmap.Width; i++)
            {
                for (var j = 0; j < destBitmap.Height; j++)
                {
                    double dx = 0;
                    dx = isTwist
                        ? (2 * Math.PI * (double)j) / dBaseAxisLen
                        : (2 * Math.PI * (double)i) / dBaseAxisLen;
                    dx += shapePhase;
                    double dy = Math.Sin(dx);
                    // 取当前点的颜色
                    int nOldX = 0, nOldY = 0;
                    nOldX = isTwist ? i + (int)(dy * shapeMultValue) : i;
                    nOldY = isTwist ? j : j + (int)(dy * shapeMultValue);
                    Color color = bitmap.GetPixel(i, j);
                    if (nOldX >= 0 && nOldX <= destBitmap.Width && nOldY >= 0 && nOldY <= destBitmap.Height)
                    {
                        destBitmap.SetPixel(nOldX, nOldY, color);
                    }
                }
            }
            return destBitmap;
        }

        #endregion

        #region Rotate(图片旋转)

        /// <summary>
        /// 图片旋转，使图像绕中心点旋转一定角度
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <param name="angle">旋转的角度，正值为逆时针方向</param>
        /// <returns></returns>
        public static Bitmap Rotate(Bitmap bitmap, int angle)
        {
            angle = angle % 360;
            // 弧度转换
            double radian = angle * Math.PI / 180.0;
            double cos = Math.Cos(radian);
            double sin = Math.Sin(radian);
            // 原图的宽和高
            int w1 = bitmap.Width;
            int h1 = bitmap.Height;
            // 旋转后的宽和高
            int w2 = (int)(Math.Max(Math.Abs(w1 * cos - h1 * sin), Math.Abs(w1 * cos + h1 * sin)));
            int h2 = (int)(Math.Max(Math.Abs(w1 * sin - h1 * cos), Math.Abs(w1 * sin + h1 * cos)));
            // 目标位图
            Bitmap newBmp = new Bitmap(w2, h2);
            Graphics graphics = Graphics.FromImage(newBmp);
            graphics.InterpolationMode = InterpolationMode.Bilinear;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            // 计算偏移量
            Point offset = new Point((w2 - w1) / 2, (h2 - h1) / 2);
            // 构造图像显示区域：使原始图像与目标图像中心点一致
            Rectangle rect = new Rectangle(offset.X, offset.Y, w1, h1);
            Point center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            graphics.TranslateTransform(center.X, center.Y);
            graphics.RotateTransform(360 - angle);
            // 恢复图像在水平和垂直方向的平移
            graphics.TranslateTransform(-center.X, -center.Y);
            graphics.DrawImage(bitmap, rect);
            // 重置绘图的所有变换
            graphics.ResetTransform();
            graphics.Save();
            graphics.Dispose();
            return newBmp;
        }

        #endregion

        #region Gray(图片灰度化)

        /// <summary>
        /// 图片灰度化
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <returns></returns>
        public static Bitmap Gray(Bitmap bitmap)
        {
            for (int i = 0; i < bitmap.Width; i++)
            {
                for (int j = 0; j < bitmap.Height; j++)
                {
                    Color pixel = bitmap.GetPixel(i, j);
                    byte r = pixel.R;
                    byte g = pixel.G;
                    byte b = pixel.B;

                    // Gray = 0.299*R + 0.587*G + 0.114*B 灰度计算公式
                    if (r + b + g != 0)
                    {
                        byte gray = (byte)((r * 19595 + g * 38469 + b * 7472) >> 16);
                        bitmap.SetPixel(i, j, Color.FromArgb(gray, gray, gray));
                    }
                    else
                    {
                        bitmap.SetPixel(i, j, Color.White);
                    }
                }
            }
            return bitmap;
        }

        #endregion

        #region Plate(底片效果)

        /// <summary>
        /// 底片效果
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <returns></returns>
        public static Bitmap Plate(Bitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    Color pixel = bitmap.GetPixel(i, j);
                    int r = 255 - pixel.R;
                    int g = 255 - pixel.G;
                    int b = 255 - pixel.B;
                    bitmap.SetPixel(i, j, Color.FromArgb(r, g, b));
                }
            }
            return bitmap;
        }

        #endregion

        #region Emboss(浮雕效果)

        /// <summary>
        /// 浮雕效果
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <returns></returns>
        public static Bitmap Emboss(Bitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    Color pixel1 = bitmap.GetPixel(i, j);
                    Color pixel2 = bitmap.GetPixel(i + 1, j + 1);
                    int r = Math.Abs(pixel1.R - pixel2.R + 128);
                    int g = Math.Abs(pixel1.G - pixel2.G + 128);
                    int b = Math.Abs(pixel1.B - pixel2.B + 128);
                    r = r > 255 ? 255 : r;
                    r = r < 0 ? 0 : r;
                    g = g > 255 ? 255 : g;
                    g = g < 0 ? 0 : g;
                    b = b > 255 ? 255 : b;
                    b = b < 0 ? 0 : b;
                    bitmap.SetPixel(i, j, Color.FromArgb(r, g, b));
                }
            }
            return bitmap;
        }

        #endregion

        #region Soften(柔化效果)

        /// <summary>
        /// 柔化效果
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <returns></returns>
        public static Bitmap Soften(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            //高斯模板
            int[] gauss = { 1, 2, 1, 2, 4, 2, 1, 2, 1 };
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    int index = 0;
                    int r = 0, g = 0, b = 0;
                    for (int col = -1; col <= 1; col++)
                    {
                        for (int row = -1; row <= 1; row++)
                        {
                            Color pixel = bitmap.GetPixel(i + row, j + col);
                            r += pixel.R * gauss[index];
                            g += pixel.G * gauss[index];
                            b += pixel.B * gauss[index];
                            index++;
                        }
                    }
                    r /= 16;
                    g /= 16;
                    b /= 16;
                    // 处理颜色值溢出
                    r = r > 255 ? 255 : r;
                    r = r < 0 ? 0 : r;
                    g = g > 255 ? 255 : g;
                    g = g < 0 ? 0 : g;
                    b = b > 255 ? 255 : b;
                    b = b < 0 ? 0 : b;
                    bitmap.SetPixel(i - 1, j - 1, Color.FromArgb(r, g, b));
                }
            }
            return bitmap;
        }

        #endregion

        #region Sharpen(锐化效果)

        /// <summary>
        /// 锐化效果
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <returns></returns>
        public static Bitmap Sharpen(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            // 拉普拉斯模板
            int[] laplacian = { -1, -1, -1, -1, 9, -1, -1, -1, -1 };
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    int index = 0;
                    int r = 0, g = 0, b = 0;
                    for (int col = -1; col <= 1; col++)
                    {
                        for (int row = -1; row <= 1; row++)
                        {
                            Color pixel = bitmap.GetPixel(i + row, j + col);
                            r += pixel.R * laplacian[index];
                            g += pixel.G * laplacian[index];
                            b += pixel.B * laplacian[index];
                            index++;
                        }
                    }
                    r /= 16;
                    g /= 16;
                    b /= 16;
                    // 处理颜色值溢出
                    r = r > 255 ? 255 : r;
                    r = r < 0 ? 0 : r;
                    g = g > 255 ? 255 : g;
                    g = g < 0 ? 0 : g;
                    b = b > 255 ? 255 : b;
                    b = b < 0 ? 0 : b;
                    bitmap.SetPixel(i - 1, j - 1, Color.FromArgb(r, g, b));
                }
            }
            return bitmap;
        }

        #endregion

        #region Atomizing(雾化效果)

        /// <summary>
        /// 雾化效果
        /// </summary>
        /// <param name="bitmap">图片</param>
        /// <returns></returns>
        public static Bitmap Atomizing(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    Random rnd = new Random();
                    int k = rnd.Next(123456);
                    // 像素块大小
                    int dx = i + k % 19;
                    int dy = j + k % 19;
                    if (dx >= width)
                    {
                        dx = width - 1;
                    }
                    if (dy >= height)
                    {
                        dy = height - 1;
                    }
                    Color pixel = bitmap.GetPixel(dx, dy);
                    bitmap.SetPixel(i, j, pixel);
                }
            }
            return bitmap;
        }

        #endregion
    }
}
