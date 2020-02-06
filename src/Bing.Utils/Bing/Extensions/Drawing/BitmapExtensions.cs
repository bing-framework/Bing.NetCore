using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 图像(<see cref="Bitmap"/>) 扩展
    /// </summary>
    public static class BitmapExtensions
    {
        #region ToPixelArray2D(转换为 Color[,]颜色值二维数组)

        /// <summary>
        /// 将图像转换为 Color[,]颜色值二维数组
        /// </summary>
        /// <param name="bitmap">图像</param>
        /// <returns></returns>
        public static Color[,] ToPixelArray2D(this Bitmap bitmap)
        {
            int width = bitmap.Width, height = bitmap.Height;
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                Color[,] pixels = new Color[width, height];
                int offset = data.Stride - width * 3;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        pixels[x, y] = Color.FromArgb(ptr[2], ptr[1], ptr[0]);
                    }

                    ptr += offset;
                }

                return pixels;
            }
        }

        #endregion

        #region ToGrayArray2D(转换为 byte[,]灰度值二维数组)

        /// <summary>
        /// 将图像转换为 byte[,]灰度值二维数组，后续所有操作都以二维数组作为中间变量
        /// </summary>
        /// <param name="bitmap">图像</param>
        /// <returns></returns>
        public static byte[,] ToGrayArray2D(this Bitmap bitmap)
        {
            int width = bitmap.Width, height = bitmap.Height;
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                byte[,] grayBytes = new byte[width, height];
                int offset = data.Stride - width * 3;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        grayBytes[x, y] = GetGrayValue(ptr[2], ptr[1], ptr[0]);
                        ptr += 3;
                    }

                    ptr += offset;
                }

                return grayBytes;
            }
        }

        /// <summary>
        /// 获取灰度值
        /// </summary>
        /// <param name="red">红</param>
        /// <param name="green">绿</param>
        /// <param name="blue">蓝</param>
        /// <returns></returns>
        private static byte GetGrayValue(byte red, byte green, byte blue)
        {
            return (byte)((red * 19595 + green * 38469 + blue * 7472) >> 16);
        }

        /// <summary>
        /// 将颜色二维数组转换为 byte[,]灰度值二维数组
        /// </summary>
        /// <param name="pixels">颜色二维数组</param>
        /// <returns></returns>
        public static byte[,] ToGrayArray2D(this Color[,] pixels)
        {
            int width = pixels.GetLength(0), height = pixels.GetLength(1);
            byte[,] grayBytes = new byte[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    grayBytes[x, y] = GetGrayValue(pixels[x, y]);
                }
            }

            return grayBytes;
        }

        /// <summary>
        /// 获取灰度值
        /// </summary>
        /// <param name="pixel">颜色</param>
        /// <returns></returns>
        private static byte GetGrayValue(Color pixel)
        {
            return GetGrayValue(pixel.R, pixel.G, pixel.B);
        }

        #endregion

        #region ToBitmap(转换为图像)

        /// <summary>
        /// 将颜色二维数组转换为图像
        /// </summary>
        /// <param name="pixels">颜色二维数组</param>
        /// <returns></returns>
        public static Bitmap ToBitmap(this Color[,] pixels)
        {
            int width = pixels.GetLength(0), height = pixels.GetLength(1);
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                int offset = data.Stride - width * 3;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color pixel = pixels[x, y];
                        ptr[2] = pixel.R;
                        ptr[1] = pixel.G;
                        ptr[0] = pixel.B;
                        ptr += 3;
                    }

                    ptr += offset;
                }

                bitmap.UnlockBits(data);
                return bitmap;
            }
        }

        /// <summary>
        /// 将灰度值二维数组转换为图像
        /// </summary>
        /// <param name="grayBytes">灰度值二维数组</param>
        /// <returns></returns>
        public static Bitmap ToBitmap(this byte[,] grayBytes)
        {
            int width = grayBytes.GetLength(0), height = grayBytes.GetLength(1);
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* ptr = (byte*)data.Scan0;
                int offset = data.Stride - width * 3;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        ptr[2] = ptr[1] = ptr[0] = grayBytes[x, y];
                        ptr += 3;
                    }

                    ptr += offset;
                }

                bitmap.UnlockBits(data);
                return bitmap;
            }
        }

        #endregion

        #region Binaryzation(将灰度值二维数组二值化)

        /// <summary>
        /// 将灰度值二维数组二值化
        /// </summary>
        /// <param name="grayBytes">灰度值二维数组</param>
        /// <param name="gray">灰度值</param>
        /// <returns></returns>
        public static byte[,] Binaryzation(this byte[,] grayBytes, byte gray)
        {
            int width = grayBytes.GetLength(0), height = grayBytes.GetLength(1);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    grayBytes[x, y] = (byte)(grayBytes[x, y] > gray ? 255 : 0);
                }
            }

            return grayBytes;
        }

        #endregion

        #region DeepFore(将灰度值二维数组前景色加黑)

        /// <summary>
        /// 将灰度值二维数组前景色加黑
        /// </summary>
        /// <param name="grayBytes">灰度值二维数组</param>
        /// <param name="gray">灰度值</param>
        /// <returns></returns>
        public static byte[,] DeepFore(this byte[,] grayBytes, byte gray = 200)
        {
            int width = grayBytes.GetLength(0), height = grayBytes.GetLength(1);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (grayBytes[x, y] < gray)
                    {
                        grayBytes[x, y] = 0;
                    }
                }
            }

            return grayBytes;
        }

        #endregion

        #region ClearNoiseRound(去除附近噪音)

        /// <summary>
        /// 去除附近噪音，周边有效点数的方式（适合杂点/细线）
        /// </summary>
        /// <param name="binBytes">二进制数组</param>
        /// <param name="gray">灰度值</param>
        /// <param name="maxNearPoints">噪点阀值</param>
        /// <returns></returns>
        public static byte[,] ClearNoiseRound(this byte[,] binBytes, byte gray, int maxNearPoints)
        {
            int width = binBytes.GetLength(0), height = binBytes.GetLength(1);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte value = binBytes[x, y];
                    // 背景，边框
                    if (value > gray || (x == 0 || y == 0 || x == width - 1 || y == height - 1))
                    {
                        binBytes[x, y] = 255;
                        continue;
                    }

                    int count = 0;
                    if (binBytes[x - 1, y - 1] < gray) count++;
                    if (binBytes[x, y - 1] < gray) count++;
                    if (binBytes[x + 1, y - 1] < gray) count++;
                    if (binBytes[x, y - 1] < gray) count++;
                    if (binBytes[x, y + 1] < gray) count++;
                    if (binBytes[x - 1, y + 1] < gray) count++;
                    if (binBytes[x, y + 1] < gray) count++;
                    if (binBytes[x + 1, y + 1] < gray) count++;
                    // 如果周边有效点数小于指定阀值，则清除该噪点
                    if (count < maxNearPoints)
                    {
                        binBytes[x, y] = 255;
                    }
                }
            }

            return binBytes;
        }

        #endregion

        #region ClearNoiseArea(去除区域噪音)

        /// <summary>
        /// 去除区域噪音，联通域降噪方式，去除连通点数小于阀值的连通区域
        /// </summary>
        /// <param name="binBytes">二进制数组</param>
        /// <param name="gray">灰度值</param>
        /// <param name="minAreaPoints">噪点阀值</param>
        /// <returns></returns>
        public static byte[,] ClearNoiseArea(this byte[,] binBytes, byte gray, int minAreaPoints)
        {
            int width = binBytes.GetLength(0), height = binBytes.GetLength(1);
            byte[,] newBinBytes = binBytes.Copy();
            // 遍历所有点，是黑点0，把与黑点连通的所有点灰度都改为1，下一个连通区域改为2，直到所有连通区域都标记完毕
            Dictionary<byte, Point[]> areaPointDict = new Dictionary<byte, Point[]>();
            byte setGray = 1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (IsBlack(newBinBytes[x, y]))
                    {
                        newBinBytes.FloodFill(new Point(x, y), setGray, out Point[] setPoints);
                        areaPointDict.Add(setGray, setPoints);

                        setGray++;
                        if (setGray >= 255)
                        {
                            setGray = 254;
                        }
                    }
                }
            }
            // 筛选出区域点数小于阈值的区域，将原图相应点设置为白色
            List<Point[]> pointsList =
                areaPointDict.Where(m => m.Value.Length < minAreaPoints).Select(m => m.Value).ToList();
            foreach (var points in pointsList)
            {
                foreach (var point in points)
                {
                    binBytes[point.X, point.Y] = 255;
                }
            }

            return binBytes;
        }

        /// <summary>
        /// 是否黑色
        /// </summary>
        /// <param name="value">颜色值</param>
        /// <returns></returns>
        private static bool IsBlack(byte value)
        {
            return value == 0;
        }

        #endregion

        #region FloodFill(泛水填充算法)

        /// <summary>
        /// 泛水填充算法，将相连通的区域使用指定灰度值填充
        /// </summary>
        /// <param name="binBytes">二进制数组</param>
        /// <param name="point">点坐标</param>
        /// <param name="replacementGray">填充灰度值</param>
        /// <returns></returns>
        public static byte[,] FloodFill(this byte[,] binBytes, Point point, byte replacementGray)
        {
            int width = binBytes.GetLength(0), height = binBytes.GetLength(1);
            Stack<Point> stack = new Stack<Point>();
            byte gray = binBytes[point.X, point.Y];
            stack.Push(point);

            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (p.X <= 0 || p.X >= width || p.Y <= 0 || p.Y >= height)
                {
                    continue;
                }

                if (binBytes[p.X, p.Y] == gray)
                {
                    binBytes[p.X, p.Y] = replacementGray;

                    stack.Push(new Point(p.X - 1, p.Y));
                    stack.Push(new Point(p.X + 1, p.Y));
                    stack.Push(new Point(p.X, p.Y - 1));
                    stack.Push(new Point(p.X, p.Y + 1));
                }
            }

            return binBytes;
        }

        /// <summary>
        /// 泛水填充算法，将相连通的区域使用指定灰度值填充
        /// </summary>
        /// <param name="binBytes">二进制数组</param>
        /// <param name="point">点坐标</param>
        /// <param name="replacementGray">填充灰度值</param>
        /// <param name="points">已填充灰度值的点坐标数组</param>
        /// <returns></returns>
        public static byte[,] FloodFill(this byte[,] binBytes, Point point, byte replacementGray, out Point[] points)
        {
            int width = binBytes.GetLength(0), height = binBytes.GetLength(1);
            List<Point> pointList = new List<Point>();
            Stack<Point> stack = new Stack<Point>();
            byte gray = binBytes[point.X, point.Y];
            stack.Push(point);

            while (stack.Count > 0)
            {
                var p = stack.Pop();
                if (p.X <= 0 || p.X >= width || p.Y <= 0 || p.Y >= height)
                {
                    continue;
                }

                if (binBytes[p.X, p.Y] == gray)
                {
                    binBytes[p.X, p.Y] = replacementGray;
                    pointList.Add(p);

                    stack.Push(new Point(p.X - 1, p.Y));
                    stack.Push(new Point(p.X + 1, p.Y));
                    stack.Push(new Point(p.X, p.Y - 1));
                    stack.Push(new Point(p.X, p.Y + 1));
                }
            }

            points = pointList.ToArray();
            return binBytes;
        }

        #endregion

        #region ClearBorder(去除图片边框)

        /// <summary>
        /// 去除图片边框
        /// </summary>
        /// <param name="grayBytes">灰度值二维数组</param>
        /// <param name="border">边框宽度</param>
        /// <returns></returns>
        public static byte[,] ClearBorder(this byte[,] grayBytes, int border)
        {
            int width = grayBytes.GetLength(0), height = grayBytes.GetLength(1);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < border || y < border || x > width - 1 - border || y > height - 1 - border)
                    {
                        grayBytes[x, y] = 255;
                    }
                }
            }

            return grayBytes;
        }

        #endregion

        #region AddBorder(添加图片边框)

        /// <summary>
        /// 添加图片边框，默认白色
        /// </summary>
        /// <param name="grayBytes">灰度值二维数组</param>
        /// <param name="border">边框宽度</param>
        /// <param name="gray">灰度值</param>
        /// <returns></returns>
        public static byte[,] AddBorder(this byte[,] grayBytes, int border, byte gray = 255)
        {
            int width = grayBytes.GetLength(0) + border * 2, height = grayBytes.GetLength(1) + border * 2;
            byte[,] newBytes = new byte[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < border || y < border || x > width - 1 - border || y > height - 1 - border)
                    {
                        newBytes[x, y] = gray;
                    }
                }
            }

            newBytes = grayBytes.DrawTo(newBytes, border, border);
            return newBytes;
        }

        #endregion

        #region DrawTo(将小图画到大图中)

        /// <summary>
        /// 将小图画到大图中
        /// </summary>
        /// <param name="smallBytes">小图二维数组</param>
        /// <param name="bigBytes">大图二维数组</param>
        /// <param name="x1">边框横坐标</param>
        /// <param name="y1">边框纵坐标</param>
        /// <returns></returns>
        public static byte[,] DrawTo(this byte[,] smallBytes, byte[,] bigBytes, int x1, int y1)
        {
            int smallWidth = smallBytes.GetLength(0),
                smallHeight = smallBytes.GetLength(1),
                bigWidth = bigBytes.GetLength(0),
                bigHeight = bigBytes.GetLength(1);

            if (x1 + smallWidth > bigWidth)
            {
                throw new ArgumentException("大图矩阵宽度无法装下小矩阵宽度");
            }

            if (y1 + smallHeight > bigHeight)
            {
                throw new ArgumentException("大图矩阵高度无法装下小矩阵高度");
            }

            for (int y = 0; y < smallHeight; y++)
            {
                for (int x = 0; x < smallWidth; x++)
                {
                    bigBytes[x1 + x, y1 + y] = smallBytes[x, y];
                }
            }

            return bigBytes;
        }

        #endregion

        #region ClearGray(去除指定范围的灰度)

        /// <summary>
        /// 去除指定范围的灰度
        /// </summary>
        public static byte[,] ClearGray(this byte[,] grayBytes, byte minGray, byte maxGray)
        {
            int width = grayBytes.GetLength(0), height = grayBytes.GetLength(1);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte value = grayBytes[x, y];
                    if (minGray <= value && value <= maxGray)
                    {
                        grayBytes[x, y] = 255;
                    }
                }
            }
            return grayBytes;
        }

        #endregion

        #region ToValid(去除空白边界获取有效的图形)

        /// <summary>
        /// 去除空白边界获取有效的图形
        /// </summary>
        public static byte[,] ToValid(this byte[,] binBytes, byte gray = 200)
        {
            int width = binBytes.GetLength(0), height = binBytes.GetLength(1);
            // 有效矩形的左上/右下角坐标，左上坐标从右下开始拉，右下坐标从左上开始拉，所以初始值为
            int x1 = width, y1 = height, x2 = 0, y2 = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte value = binBytes[x, y];
                    if (value >= gray)
                    {
                        continue;
                    }
                    if (x1 > x) x1 = x;
                    if (y1 > y) y1 = y;
                    if (x2 < x) x2 = x;
                    if (y2 < y) y2 = y;
                }
            }
            // 创建新矩阵，复制原数据到新矩阵
            int newWidth = x2 - x1 + 1, newHeight = y2 - y1 + 1;
            byte[,] newBytes = binBytes.Clone(x1, y1, newWidth, newHeight);
            return newBytes;
        }

        #endregion

        #region Clone(从原矩阵中复制指定矩阵)

        /// <summary>
        /// 从原矩阵中复制指定矩阵
        /// </summary>
        public static byte[,] Clone(this byte[,] sourceBytes, int x1, int y1, int width, int height)
        {
            int swidth = sourceBytes.GetLength(0), sheight = sourceBytes.GetLength(1);
            if (swidth - x1 < width)
            {
                throw new ArgumentException("要截取的宽度超出界限");
            }
            if (sheight - y1 < height)
            {
                throw new ArgumentException("要截取的高度超出界限");
            }
            byte[,] newBytes = new byte[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    newBytes[x, y] = sourceBytes[x1 + x, y1 + y];
                }
            }
            return newBytes;
        }

        #endregion

        #region ShadowY(统计二维二值化数组的的竖直投影)

        /// <summary>
        /// 统计二维二值化数组的的竖直投影
        /// </summary>
        public static int[] ShadowY(this byte[,] binBytes)
        {
            int width = binBytes.GetLength(0), height = binBytes.GetLength(1);
            int[] nums = new int[width];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (IsBlack(binBytes[x, y]))
                    {
                        nums[x]++;
                    }
                }
            }
            return nums;
        }

        #endregion

        #region ShadowX(统计二维二值化数组的横向投影)

        /// <summary>
        /// 统计二维二值化数组的横向投影
        /// </summary>
        public static int[] ShadowX(this byte[,] binBytes)
        {
            int width = binBytes.GetLength(0), height = binBytes.GetLength(1);
            int[] nums = new int[height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (IsBlack(binBytes[x, y]))
                    {
                        nums[y]++;
                    }
                }
            }
            return nums;
        }

        #endregion

        #region SplitShadowY(根据二值化数组的竖直投影数据分割图片)

        /// <summary>
        /// 根据二值化数组的竖直投影数据分割图片
        /// </summary>
        /// <param name="binBytes">二维二值化数组</param>
        /// <param name="minFontWidth">最小字符宽度，0则自动</param>
        /// <param name="minLines">最小有效投影行数</param>
        /// <returns></returns>
        public static List<byte[,]> SplitShadowY(this byte[,] binBytes, byte minFontWidth = 0, byte minLines = 0)
        {
            int height = binBytes.GetLength(1);
            int[] shadow = binBytes.ShadowY();
            List<Tuple<int, int>> validXs = new List<Tuple<int, int>>();
            int x1 = 0;
            bool inFont = false;
            for (int x = 0; x < shadow.Length; x++)
            {
                int value = shadow[x];
                if (!inFont)
                {
                    if (value > minLines)
                    {
                        inFont = true;
                        x1 = x;
                    }
                }
                else
                {
                    if (value <= minLines)
                    {
                        inFont = false;
                        if (minFontWidth == 0 || x - x1 > minFontWidth)
                        {
                            validXs.Add(new Tuple<int, int>(x1, x));
                        }
                    }
                }
            }

            List<byte[,]> splits = validXs.Select(valid => binBytes.Clone(valid.Item1, 0, valid.Item2 - valid.Item1 + 1, height).ToValid()).ToList();
            return splits;
        }

        #endregion

        #region ToCodeString(将二维二值化数组转换为特征码字符串)

        /// <summary>
        /// 将二维二值化数组转换为特征码字符串
        /// </summary>
        public static string ToCodeString(this byte[,] binBytes, byte gray, bool breakLine = false)
        {
            int width = binBytes.GetLength(0), height = binBytes.GetLength(1);
            string code = string.Empty;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    code += binBytes[x, y] < gray ? 1 : 0;
                }
                if (breakLine)
                {
                    code += "\r\n";
                }
            }
            return code;
        }

        #endregion

        private static byte GetAverageColor(byte[,] source, int x, int y, int w, int h)
        {
            int result = source[x, y]
                         + (x == 0 ? 255 : source[x - 1, y])
                         + (x == 0 || y == 0 ? 255 : source[x - 1, y - 1])
                         + (x == 0 || y == h - 1 ? 255 : source[x - 1, y + 1])
                         + (y == 0 ? 255 : source[x, y - 1])
                         + (y == h - 1 ? 255 : source[x, y + 1])
                         + (x == w - 1 ? 255 : source[x + 1, y])
                         + (x == w - 1 || y == 0 ? 255 : source[x + 1, y - 1])
                         + (x == w - 1 || y == h - 1 ? 255 : source[x + 1, y + 1]);
            return (byte)(result / 9);
        }

        private static bool IsWhite(byte value)
        {
            return value == 255;
        }
    }
}
