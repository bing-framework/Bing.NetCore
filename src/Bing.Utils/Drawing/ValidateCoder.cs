using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Utils.Drawing
{
    /// <summary>
    /// 验证码生成类
    /// </summary>
    public class ValidateCoder
    {
        /// <summary>
        /// 随机数
        /// </summary>
        private static readonly Random Random = new Random();

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

        /// <summary>
        /// 获取指定长度的验证码字符串
        /// </summary>
        /// <param name="length">长度</param>
        /// <param name="codeType">验证码类型</param>
        /// <returns></returns>
        public string GetCode(int length, ValidateCodeType codeType = ValidateCodeType.NumberAndLetter)
        {
            length.CheckGreaterThan(nameof(length), 0);

            switch (codeType)
            {
                case ValidateCodeType.Number:
                    return GetRandomNumbers(length);
                case ValidateCodeType.ChineseChar:
                    return GetRandomChinese(length);
                default:
                    return GetRandomNumbersAndLetters(length);
            }
        }

        /// <summary>
        /// 获取指定字符串的验证码图片
        /// </summary>
        /// <param name="code">验证码</param>
        /// <returns></returns>
        public Bitmap CreateImage(string code)
        {
            code.CheckNotNullOrEmpty(nameof(code));

            var width = FontWidth * code.Length + FontWidth;
            var height = FontSize + FontSize / 2;
            const int flag = 255 / 2;
            bool isBgLight = (Background.R + Background.G + Background.B) / 3 > flag;

            Bitmap image = new Bitmap(width, height);
            Graphics graphics = Graphics.FromImage(image);
            graphics.Clear(Background);
            Brush brush = new SolidBrush(Color.FromArgb(255 - Background.R, 255 - Background.G, 255 - Background.B));
            int x, y = 3;
            if (HasBorder)
            {
                graphics.DrawRectangle(new Pen(Color.Silver), 0, 0, image.Width - 1, image.Height - 1);
            }

            Random rnd = Random;

            // 绘制干扰线
            for (var i = 0; i < RandomLineCount; i++)
            {
                x = rnd.Next(image.Width);
                y = rnd.Next(image.Height);
                int m = rnd.Next(image.Width);
                int n = rnd.Next(image.Height);
                Color lineColor = !RandomColor
                    ? Color.FromArgb(90, 90, 90)
                    : isBgLight
                        ? Color.FromArgb(rnd.Next(130, 200), rnd.Next(130, 200), rnd.Next(130, 200))
                        : Color.FromArgb(rnd.Next(70, 150), rnd.Next(70, 150), rnd.Next(70, 150));
                Pen pen = new Pen(lineColor, 2);
                graphics.DrawLine(pen, x, y, m, n);
            }

            // 绘制干扰点
            for (var i = 0; i < (int) (image.Width * image.Height * RandomPointPercent / 100); i++)
            {
                x = rnd.Next(image.Width);
                y = rnd.Next(image.Height);
                Color pointColor = isBgLight
                    ? Color.FromArgb(rnd.Next(30, 80), rnd.Next(30, 80), rnd.Next(30, 80))
                    : Color.FromArgb(rnd.Next(150, 200), rnd.Next(150, 200), rnd.Next(150, 200));
                image.SetPixel(x, y, pointColor);
            }

            // 绘制文字
            for (var i = 0; i < code.Length; i++)
            {
                rnd = Random;
                x = FontWidth / 4 + FontWidth * i;
                if (RandomPosition)
                {
                    x = rnd.Next(FontWidth / 4) + FontWidth * i;
                    y = rnd.Next(image.Flags / 5);
                }

                PointF point = new PointF(x, y);
                if (RandomColor)
                {
                    int r, g, b;
                    if (!isBgLight)
                    {
                        r = rnd.Next(255 - Background.R);
                        g = rnd.Next(255 - Background.G);
                        b = rnd.Next(255 - Background.B);
                        if ((r + g + b) / 3 < flag)
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
                        if ((r + g + b) / 3 > flag)
                        {
                            r = 255 - r;
                            g = 255 - g;
                            b = 255 - b;
                        }
                    }

                    brush = new SolidBrush(Color.FromArgb(r, g, b));
                }

                var fontName = Valid.IsContainsChinese(code)
                    ? FontNamesForChinese[rnd.Next(FontNamesForChinese.Count)]
                    : FontNames[rnd.Next(FontNames.Count)];
                var font = new Font(fontName, FontSize, FontStyle.Bold);
                if (RandomItalic)
                {
                    graphics.TranslateTransform(0, 0);
                    Matrix transform = graphics.Transform;
                    transform.Shear(Convert.ToSingle(rnd.Next(2, 9) / 10d - 0.5), 0.001f);
                    graphics.Transform = transform;
                }

                graphics.DrawString(code.Substring(i, 1), font, brush, point);
                graphics.ResetTransform();
            }

            return image;
        }

        /// <summary>
        /// 获取指定长度的验证码图片
        /// </summary>
        /// <param name="length">长度</param>
        /// <param name="code">验证码</param>
        /// <param name="codeType">验证码类型</param>
        /// <returns></returns>
        public Bitmap CreateImage(int length, out string code,
            ValidateCodeType codeType = ValidateCodeType.NumberAndLetter)
        {
            length.CheckGreaterThan(nameof(length), 0);
            length = length < 1 ? 1 : length;
            switch (codeType)
            {
                case ValidateCodeType.Number:
                    code = GetRandomNumbers(length);
                    break;                
                case ValidateCodeType.ChineseChar:
                    code = GetRandomChinese(length);
                    break;
                default:
                    code = GetRandomNumbersAndLetters(length);
                    break;
            }

            if (code.Length > length)
            {
                code = code.Substring(0, length);
            }

            return CreateImage(code);
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
            string allChar = $"{Const.ArabicNumbers}{Const.Uppercase}{Const.Lowercase}";
            StringBuilder result = new StringBuilder();
            for (var i = 0; i < length; i++)
            {
                var index = Random.Next(allChar.Length);
                result.Append(allChar[index]);
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

            byte[] bs = {b1, b2};
            return encoding.GetString(bs);
        }
    }
}
