using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Bing.Utils.Drawing
{
    /// <summary>
    /// 验证码生成器
    /// </summary>
    public class ValidateCoder
    {
        #region 字段

        /// <summary>
        /// 随机数
        /// </summary>
        private static readonly Random Random = new Random();

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
        /// 初始化一个<see cref="ValidateCoder"/>类型的实例
        /// </summary>
        public ValidateCoder()
        {
            FontNames = new List<string> { "Arial", "Batang", "Buxton Sketch", "David", "SketchFlow Print" };
            FontNamesForChinese = new List<string> { "宋体", "幼圆", "楷体", "仿宋", "隶书", "黑体" };
            FontSize = 20;
            FontWidth = FontSize;
            Background = Color.FromArgb(240, 240, 240);
            RandomPointPercent = 0;
        }

        #endregion

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
    }
}
