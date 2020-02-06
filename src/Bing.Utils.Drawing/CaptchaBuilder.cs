using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Text.RegularExpressions;

namespace Bing.Utils.Drawing
{
    /// <summary>
    /// 验证码生成器
    /// </summary>
    public class CaptchaBuilder
    {
        #region 字段

        /// <summary>
        /// 随机数
        /// </summary>
        private static readonly Random Random = new Random((int)DateTime.Now.Ticks);

        /// <summary>
        /// 生成种子
        /// </summary>
        private const string Seed = "2,3,4,5,6,7,8,9," +
                                    "A,B,C,D,E,F,G,H,J,K,M,N,P,Q,R,S,T,U,V,W,X,Y,Z," +
                                    "a,b,c,d,e,f,g,h,k,m,n,p,q,r,s,t,u,v,w,x,y,z";

        #endregion

        #region 属性

        /// <summary>
        /// 字体名称集合
        /// </summary>
        public List<string> FontNames { get; set; }

        /// <summary>
        /// 汉字字体名称集合
        /// </summary>
        public List<string> FontNamesForChinese { get; set; }

        /// <summary>
        /// 字体大小
        /// </summary>
        public int FontSize { get; set; }

        /// <summary>
        /// 字体宽度
        /// </summary>
        public int FontWidth { get; set; }

        /// <summary>
        /// 图片高度
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 背景色
        /// </summary>
        public Color Background { get; set; }

        /// <summary>
        /// 是否有边框
        /// </summary>
        public bool HasBorder { get; set; }

        /// <summary>
        /// 是否随机位置
        /// </summary>
        public bool RandomPosition { get; set; }

        /// <summary>
        /// 是否随机颜色
        /// </summary>
        public bool RandomColor { get; set; }

        /// <summary>
        /// 是否随机倾斜字体
        /// </summary>
        public bool RandomItalic { get; set; }

        /// <summary>
        /// 随机干扰点百分比（百分数形式）
        /// </summary>
        public double RandomPointPercent { get; set; }

        /// <summary>
        /// 随机干扰线数量
        /// </summary>
        public int RandomLineCount { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="CaptchaBuilder"/>类型的实例
        /// </summary>
        public CaptchaBuilder()
        {
            FontNames = new List<string> { "Arial", "Batang", "Buxton Sketch", "David", "SketchFlow Print" };
            FontNamesForChinese = new List<string> { "宋体", "幼圆", "楷体", "仿宋", "隶书", "黑体" };
            FontSize = 20;
            FontWidth = FontSize;
            Background = Color.FromArgb(240, 240, 240);
            RandomPointPercent = 0;
        }

        #endregion

        #region GetCode(获取指定长度的验证码字符串)

        /// <summary>
        /// 获取指定长度的验证码字符串
        /// </summary>
        /// <param name="length">长度</param>
        /// <param name="captchaType">验证码类型</param>
        /// <returns></returns>
        public string GetCode(int length, CaptchaType captchaType = CaptchaType.NumberAndLetter)
        {
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            switch (captchaType)
            {
                case CaptchaType.Number:
                    return GetRandomNumbers(length);

                case CaptchaType.ChineseChar:
                    return GetRandomChinese(length);

                default:
                    return GetRandomNumbersAndLetters(length);
            }
        }

        /// <summary>
        /// 获取随机数字的字符串
        /// </summary>
        /// <param name="length">长度</param>
        /// <returns></returns>
        private static string GetRandomNumbers(int length)
        {
            StringBuilder result = new StringBuilder();
            for (var i = 0; i < length; i++)
            {
                result.Append(Random.Next(0, 9));
            }

            return result.ToString();
        }

        /// <summary>
        /// 获取随机数字与字母的字符串
        /// </summary>
        /// <param name="length">长度</param>
        /// <returns></returns>
        private static string GetRandomNumbersAndLetters(int length)
        {
            var allChars = Seed.Split(',');
            StringBuilder result = new StringBuilder();
            for (var i = 0; i < length; i++)
            {
                var index = Random.Next(allChars.Length);
                result.Append(allChars[index]);
            }

            return result.ToString();
        }

        /// <summary>
        /// 获取随机汉字的字符串
        /// </summary>
        /// <param name="length">长度</param>
        /// <returns></returns>
        private static string GetRandomChinese(int length)
        {
            StringBuilder result = new StringBuilder();
            // 每循环一次产生一个含两个元素的十六进制字节数组，并放入bytes数组中
            for (int i = 0; i < length; i++)
            {
                result.Append(GenerateChinese());
            }

            return result.ToString();
        }

        /// <summary>
        /// 生成汉字
        /// </summary>
        /// <returns></returns>
        private static string GenerateChinese()
        {
            // 汉字编码的组成元素，十六进制数
            string[] hexStrs = "0,1,2,3,4,5,6,7,8,9,a,b,c,d,e,f".Split(',');
            Encoding encoding = Encoding.GetEncoding("GB2312");

            // 汉字由四个区位码组成，1、2作为字节数组的第一元素，3、4作为第二元素
            Random rnd = Random;

            var index1 = rnd.Next(11, 14);
            var str1 = hexStrs[index1];

            var index2 = index1 == 13 ? rnd.Next(0, 7) : rnd.Next(0, 16);
            var str2 = hexStrs[index2];

            var index3 = rnd.Next(10, 16);
            var str3 = hexStrs[index3];

            var index4 = index3 == 10 ? rnd.Next(1, 16) : (index3 == 15 ? rnd.Next(0, 15) : rnd.Next(0, 16));
            var str4 = hexStrs[index4];

            // 定义两个字节变量存储生成的随机汉字区位码
            byte b1 = Convert.ToByte(str1 + str2, 16);
            byte b2 = Convert.ToByte(str3 + str4, 16);

            byte[] bs = { b1, b2 };
            return encoding.GetString(bs);
        }

        #endregion

        #region CreateImage(创建指定长度的验证码图片)

        /// <summary>
        /// 创建指定字符串的验证码图片
        /// </summary>
        /// <param name="code">验证码</param>
        /// <returns></returns>
        public Bitmap CreateImage(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentNullException(nameof(code));
            }

            var width = FontWidth * code.Length + FontWidth;
            var height = FontSize + FontSize / 2;

            Bitmap image = new Bitmap(width, height);
            Graphics graphics = Graphics.FromImage(image);
            graphics.Clear(Background);
            // 绘制边框
            DrawBorder(graphics, width, height);
            // 绘制干扰线
            DrawDisorderLine(graphics, width, height);
            // 绘制干扰点
            DrawDisorderPoint(graphics, width, height);
            // 绘制文字
            DrawText(graphics, code, height);
            graphics.Dispose();
            return image;
        }

        /// <summary>
        /// 绘制边框
        /// </summary>
        /// <param name="g">绘制器</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        private void DrawBorder(Graphics g, int width, int height)
        {
            if (!HasBorder)
            {
                return;
            }

            g.DrawRectangle(new Pen(Color.Silver), 0, 0, width - 1, height - 1);
        }

        /// <summary>
        /// 获取文字大小
        /// </summary>
        /// <param name="imageWidth">图片宽度</param>
        /// <param name="captchCodeCount">验证码长度</param>
        /// <returns></returns>
        private int GetFontSize(int imageWidth, int captchCodeCount)
        {
            var averageSize = imageWidth / captchCodeCount;
            return Convert.ToInt32(averageSize);
        }

        /// <summary>
        /// 获取随机深色
        /// </summary>
        /// <returns></returns>
        private Color GetRandomDeepColor()
        {
            int r = 160, g = 100, b = 160;
            return Color.FromArgb(Random.Next(r), Random.Next(g), Random.Next(b));
        }

        /// <summary>
        /// 获取随机浅色
        /// </summary>
        /// <returns></returns>
        private Color GetRandomLightColor()
        {
            int low = 180, high = 255;
            var r = Random.Next(high) % (high - low) + low;
            var g = Random.Next(high) % (high - low) + low;
            var b = Random.Next(high) % (high - low) + low;

            return Color.FromArgb(r, g, b);
        }

        /// <summary>
        /// 绘制干扰线
        /// </summary>
        /// <param name="g">绘制器</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        private void DrawDisorderLine(Graphics g, int width, int height)
        {
            var linePen = new Pen(new SolidBrush(Color.Black), 3);
            for (var i = 0; i < RandomLineCount; i++)
            {
                linePen.Color = GetDisorderColor();

                var startPoint = new Point(Random.Next(0, width), Random.Next(0, height));
                var endPoint = new Point(Random.Next(0, width), Random.Next(0, height));
                g.DrawLine(linePen, startPoint, endPoint);
            }
        }

        /// <summary>
        /// 获取干扰颜色
        /// </summary>
        /// <returns></returns>
        private Color GetDisorderColor()
        {
            if (!RandomColor)
            {
                return Color.Black;
            }

            Random rnd = Random;
            return IsBgLight()
                ? Color.FromArgb(rnd.Next(130, 200), rnd.Next(130, 200), rnd.Next(130, 200))
                : Color.FromArgb(rnd.Next(70, 150), rnd.Next(70, 150), rnd.Next(70, 150));
        }

        /// <summary>
        /// 是否背景高亮
        /// </summary>
        /// <returns></returns>
        private bool IsBgLight()
        {
            int flag = 255 / 2;
            bool isBgLight = (Background.R + Background.G + Background.B) / 3 > flag;
            return isBgLight;
        }

        /// <summary>
        /// 绘制干扰点
        /// </summary>
        /// <param name="g">绘制器</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        private void DrawDisorderPoint(Graphics g, int width, int height)
        {
            var pointPen = new Pen(Color.Black, 0);
            int x = 0;
            int y = 0;
            for (var i = 0; i < (int)(width * height * RandomPointPercent / 100); i++)
            {
                x = Random.Next(0, width);
                y = Random.Next(0, height);
                pointPen.Color = GetDisorderColor();
                g.DrawRectangle(pointPen, x, y, 1, 1);
            }
        }

        /// <summary>
        /// 绘制文本
        /// </summary>
        /// <param name="g">绘制器</param>
        /// <param name="code">验证码</param>
        /// <param name="height">高度</param>
        private void DrawText(Graphics g, string code, int height)
        {
            for (int i = 0; i < code.Length; i++)
            {
                var position = GetPosition(i, height);
                var point = new PointF(position.Item1, position.Item2);
                Brush brush = new SolidBrush(GetTextColor());
                var font = GetFont(code);
                FontItalic(g);
                g.DrawString($"{code[i]}", font, brush, point);
                g.ResetTransform();
            }
        }

        /// <summary>
        /// 获取文本颜色
        /// </summary>
        /// <returns></returns>
        private Color GetTextColor()
        {
            if (!RandomColor)
            {
                return Color.FromArgb(255 - Background.R, 255 - Background.G, 255 - Background.B);
            }

            Random rnd = Random;
            int r, g, b;
            if (!IsBgLight())
            {
                r = rnd.Next(255 - Background.R);
                g = rnd.Next(255 - Background.G);
                b = rnd.Next(255 - Background.B);
                if ((r + g + b) / 3 < 255 / 2)
                {
                    r = 255 - r;
                    g = 255 - g;
                    b = 255 - b;
                }
            }
            else
            {
                r = rnd.Next(Background.R);
                g = rnd.Next(Background.G);
                b = rnd.Next(Background.B);
                if ((r + g + b) / 3 > 255 / 2)
                {
                    r = 255 - r;
                    g = 255 - g;
                    b = 255 - b;
                }
            }

            return Color.FromArgb(r, g, b);
        }

        /// <summary>
        /// 获取文本字体
        /// </summary>
        /// <param name="code">验证码</param>
        /// <returns></returns>
        private Font GetFont(string code)
        {
            var fontName = Regex.IsMatch(code, @"[\u4e00-\u9fa5]+", RegexOptions.IgnoreCase)
                ? FontNamesForChinese[Random.Next(FontNamesForChinese.Count)]
                : FontNames[Random.Next(FontNames.Count)];
            var font = new Font(fontName, FontSize, FontStyle.Bold);
            return font;
        }

        /// <summary>
        /// 获取位置
        /// </summary>
        /// <param name="index">当前字符索引</param>
        /// <param name="height">图片高度</param>
        /// <returns></returns>
        private Tuple<int, int> GetPosition(int index, int height)
        {
            int x = FontWidth / 4 + FontWidth * index;
            int y = 3;
            if (RandomPosition)
            {
                x = Random.Next(FontWidth / 4) + FontWidth * index;
                y = Random.Next(height / 5);
            }
            return new Tuple<int, int>(x, y);
        }

        /// <summary>
        /// 倾斜字体
        /// </summary>
        /// <param name="g">绘制器</param>
        private void FontItalic(Graphics g)
        {
            if (RandomItalic)
            {
                g.TranslateTransform(0, 0);
                Matrix transform = g.Transform;
                transform.Shear(Convert.ToSingle(Random.Next(2, 9) / 10d - 0.5), 0.001f);
                g.Transform = transform;
            }
        }

        /// <summary>
        /// 获取指定长度的验证码图片
        /// </summary>
        /// <param name="length">长度</param>
        /// <param name="code">验证码</param>
        /// <param name="captchaType">验证码类型</param>
        /// <returns></returns>
        public Bitmap CreateImage(int length, out string code,
            CaptchaType captchaType = CaptchaType.NumberAndLetter)
        {
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            length = length < 1 ? 1 : length;
            code = GetCode(length, captchaType);
            if (code.Length > length)
            {
                code = code.Substring(0, length);
            }

            return CreateImage(code);
        }

        #endregion
    }
}
