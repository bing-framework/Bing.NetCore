using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.MockData.Core
{
    /// <summary>
    /// 随机数生成器
    /// </summary>
    public class RandomBuilder
    {
        #region 字段

        /// <summary>
        /// 随机数操作
        /// </summary>
        private readonly Core.Random _random;

        /// <summary>
        /// 重复数
        /// </summary>
        private int _repeat = 0;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="RandomBuilder"/>类型的实例
        /// </summary>
        public RandomBuilder()
        {
            _random=new Random();
        }

        /// <summary>
        /// 初始化一个<see cref="RandomBuilder"/>类型的实例
        /// </summary>
        /// <param name="seed">种子数</param>
        public RandomBuilder(int seed)
        {
            _random=new Random(seed);
        }

        #endregion

        #region GenerateNumber(生成随机数字)

        /// <summary>
        /// 生成随机数字
        /// </summary>
        /// <param name="length">长度</param>
        /// <returns></returns>
        public string GenerateNumber(int length)
        {
            StringBuilder sb=new StringBuilder();
            long ticks = DateTime.Now.Ticks + this._repeat;
            this._repeat++;
            Random random=new Random((int)((ulong)ticks & 0xffffffffL) | (int)(ticks >> this._repeat));
            for (int i = 0; i < length; i++)
            {
                int num = random.GetInt();
                string temp = ((char)(0x30 + (ushort)(num % 10))).ToString();
                sb.Append(temp);
            }
            return sb.ToString();
        }

        #endregion

        #region GenerateInt(生成随机整数)

        /// <summary>
        /// 生成随机整数
        /// </summary>
        /// <param name="maxValue">最大值，包含最大值</param>
        /// <returns></returns>
        public int GenerateInt(int maxValue)
        {
            return GenerateInt(0, maxValue + 1);
        }

        /// <summary>
        /// 生成随机整数
        /// </summary>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值，不包含最大值</param>
        /// <returns></returns>
        public int GenerateInt(int minValue, int maxValue)
        {
            return _random.GetInt(minValue, maxValue);
        }

        #endregion

        #region GenerateChinese(生成随机常用汉字)

        /// <summary>
        /// 生成随机常用汉字
        /// </summary>
        /// <param name="length">文本长度</param>
        /// <returns></returns>
        public string GenerateChinese(int length)
        {
            return GenerateText(length, Const.SIMPLIFIED_CHINESE);
        }

        #endregion

        #region GenerateRandomLengthChinese(生成随机长度常用汉字)

        /// <summary>
        /// 生成随机长度常用汉字
        /// </summary>
        /// <param name="maxLength">最大长度</param>
        /// <returns></returns>
        public string GenerateRandomLengthChinese(int maxLength)
        {
            return GenerateRandomLengthText(maxLength, Const.SIMPLIFIED_CHINESE);
        }

        /// <summary>
        /// 生成随机长度常用汉字
        /// </summary>
        /// <param name="minLength">最小长度</param>
        /// <param name="maxLength">最大长度</param>
        /// <returns></returns>
        public string GenerateRandomLengthChinese(int minLength, int maxLength)
        {
            return GenerateRandomLengthText(minLength, maxLength, Const.SIMPLIFIED_CHINESE);
        }

        #endregion

        #region GenerateText(生成随机文本)

        /// <summary>
        /// 生成随机文本
        /// </summary>
        /// <param name="length">文本长度</param>
        /// <param name="text">随机内容</param>
        /// <returns></returns>
        public string GenerateText(int length, string text)
        {
            StringBuilder sb=new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                sb.Append(GetRandomChar(text));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 获取随机字符
        /// </summary>
        /// <param name="text">随机内容</param>
        /// <returns></returns>
        private string GetRandomChar(string text)
        {
            var index = _random.GetInt(0, text.Length);
            return text[index].ToString();
        }

        #endregion

        #region GenerateRandomLengthString(生成随机长度文本)

        /// <summary>
        /// 生成随机长度文本
        /// </summary>
        /// <param name="maxLength">最大长度</param>
        /// <param name="text">随机内容</param>
        /// <returns></returns>
        public string GenerateRandomLengthText(int maxLength, string text)
        {
            var length = GenerateInt(1, maxLength);
            return GenerateText(length, text);
        }

        /// <summary>
        /// 生成随机长度文本
        /// </summary>
        /// <param name="minLength">最小长度</param>
        /// <param name="maxLength">最大长度</param>
        /// <param name="text">随机内容</param>
        /// <returns></returns>
        public string GenerateRandomLengthText(int minLength, int maxLength, string text)
        {
            var length = GenerateInt(minLength, maxLength);
            return GenerateText(length, text);
        }

        #endregion

        #region GenerateAlphanumeric(生成随机字母数字)

        /// <summary>
        /// 生成随机字母数字
        /// </summary>
        /// <param name="maxLength">最大长度</param>
        /// <param name="hasUppercase">是否包含大写字母,true:是,false:否</param>
        /// <returns></returns>
        public string GenerateAlphanumeric(int maxLength, bool hasUppercase = false)
        {
            string text = hasUppercase
                ? Const.LOWERCASE + Const.UPPERCASE + Const.ARABIC_NUMBERS
                : Const.LOWERCASE + Const.ARABIC_NUMBERS;

            return GenerateText(maxLength, text);
        }

        #endregion

        #region GenerateDate(生成随机日期)

        /// <summary>
        /// 生成随机日期
        /// </summary>
        /// <param name="beginYear">起始年份</param>
        /// <param name="endYear">结束年份</param>
        /// <returns></returns>
        public DateTime GenerateDate(int beginYear = 1970, int endYear = 2080)
        {
            var year = GenerateInt(beginYear, endYear);
            var month = GenerateInt(1, 13);
            var day = GenerateInt(1, (month == 2 && IsLeapYear(year) ? 29 : 28) + 1);
            var hour = GenerateInt(0, 24);
            var minute = GenerateInt(0, 60);
            var second = GenerateInt(0, 60);
            return new DateTime(year, month, day, hour, minute, second);
        }

        /// <summary>
        /// 是否闰年
        /// </summary>
        /// <param name="year">年份</param>
        /// <returns></returns>
        private bool IsLeapYear(int year)
        {
            if (year % 4 == 0 && year % 100 != 0 || year % 400 == 0)
            {
                return true;
            }

            return false;
        }

        #endregion

        #region GenerateBool(生成随机布尔值)

        /// <summary>
        /// 生成随机布尔值
        /// </summary>
        /// <returns></returns>
        public bool GenerateBool()
        {
            var random = _random.GetInt(1, 100);
            if (random % 2 == 0)
            {
                return false;
            }

            return true;
        }

        #endregion
    }
}
