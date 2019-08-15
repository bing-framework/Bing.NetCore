// ***********************************************************************
// Assembly         : Utilities
// Author           : zhuzhiqing
// Created          : 06-27-2014
//
// Last Modified By : zhuzhiqing
// Last Modified On : 10-17-2014
/* * * * * * * * * * * * * * * * * * * * * * * * * * *
 * 作者：朱志清
 * 日期：2009-05-30
 * 描述：
 * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System.Text.RegularExpressions;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 正则表达式
    /// </summary>
    public struct RegexEnum
    {
        /// <summary>
        /// The ASCII
        /// </summary>
        public static string Ascii = "^[\\x00-\\xFF]+$"; //仅ACSII字符

        /// <summary>
        /// The chinese
        /// </summary>
        public static string Chinese = "^[\\u4E00-\\u9FA5\\uF900-\\uFA2D]+$"; //仅中文

        /// <summary>
        /// The color
        /// </summary>
        public static string Color = "^[a-fA-F0-9]{6}$"; //颜色

        /// <summary>
        /// The date
        /// </summary>
        public static string Date = @"^\\d{4}(\\-|\\/|\.)\\d{1,2}\\1\\d{1,2}$"; //日期

        /// <summary>
        /// The decmal
        /// </summary>
        public static string Decmal = "^([+-]?)\\d*\\.\\d+$"; //浮点数

        /// <summary>
        /// The decmal1
        /// </summary>
        public static string Decmal1 = "^[1-9]\\d*.\\d*|0.\\d*[1-9]\\d*$"; //正浮点数

        /// <summary>
        /// The decmal2
        /// </summary>
        public static string Decmal2 = "^-([1-9]\\d*.\\d*|0.\\d*[1-9]\\d*)$"; //负浮点数

        /// <summary>
        /// The decmal3
        /// </summary>
        public static string Decmal3 = "^-?([1-9]\\d*.\\d*|0.\\d*[1-9]\\d*|0?.0+|0)$"; //浮点数

        /// <summary>
        /// The decmal4
        /// </summary>
        public static string Decmal4 = "^[1-9]\\d*.\\d*|0.\\d*[1-9]\\d*|0?.0+|0$"; //非负浮点数（正浮点数 + 0）

        /// <summary>
        /// The decmal5
        /// </summary>
        public static string Decmal5 = "^(-([1-9]\\d*.\\d*|0.\\d*[1-9]\\d*))|0?.0+|0$"; //非正浮点数（负浮点数 + 0）

        /// <summary>
        /// The email
        /// </summary>
        public static string Email = "^\\w+((-\\w+)|(\\.\\w+))*\\@[A-Za-z0-9]+((\\.|-)[A-Za-z0-9]+)*\\.[A-Za-z0-9]+$";

        //邮件

        /// <summary>
        /// The has zero int
        /// </summary>
        public static string HasZeroInt = "^-?[1-9]\\d*|0$"; //包含0的整数, -1，0，1

        /// <summary>
        /// The idcard
        /// </summary>
        public static string Idcard = "^[1-9]([0-9]{14}|[0-9]{17})$|^\\d{17}(\\d|x)$"; //身份证

        /// <summary>
        /// The intege
        /// </summary>
        public static string Intege = "^-?[1-9]\\d*$"; //非0整数, -1，1

        /// <summary>
        /// The intege1
        /// </summary>
        public static string Intege1 = "^[1-9]\\d*$"; //正整数

        /// <summary>
        /// The intege2
        /// </summary>
        public static string Intege2 = "^-[1-9]\\d*$"; //负整数

        /// <summary>
        /// The ip4
        /// </summary>
        public static string Ip4 =
            "^(25[0-5]|2[0-4]\\d|[0-1]\\d{2}|[1-9]?\\d)\\.(25[0-5]|2[0-4]\\d|[0-1]\\d{2}|[1-9]?\\d)\\.(25[0-5]|2[0-4]\\d|[0-1]\\d{2}|[1-9]?\\d)\\.(25[0-5]|2[0-4]\\d|[0-1]\\d{2}|[1-9]?\\d)$";

        //ip地址

        /// <summary>
        /// The letter
        /// </summary>
        public static string Letter = "^[A-Za-z]+$"; //字母

        /// <summary>
        /// The letter l
        /// </summary>
        public static string LetterL = "^[a-z]+$"; //小写字母

        /// <summary>
        /// The letter u
        /// </summary>
        public static string LetterU = "^[A-Z]+$"; //大写字母

        /// <summary>
        /// The mobile
        /// </summary>
        public static string Mobile = "^(13|15)[0-9]{9}$"; //手机

        /// <summary>
        /// The more zero int
        /// </summary>
        public static string MoreZeroInt = "^[1-9]\\d*$"; //正整数，不包含0。 例如1,2,

        /// <summary>
        /// The notempty
        /// </summary>
        public static string Notempty = "^\\S+$"; //非空

        /// <summary>
        /// The no zero int
        /// </summary>
        public static string NoZeroInt = "^-?[1-9]\\d*$"; //非0整数, -1，1

        /// <summary>
        /// The number
        /// </summary>
        public static string Num = "^([+-]?)\\d*\\.?\\d+$"; //数字

        /// <summary>
        /// The num1
        /// </summary>
        public static string Num1 = "^[1-9]\\d*$|^0$"; //正数（正整数 + 0）

        /// <summary>
        /// The num2
        /// </summary>
        public static string Num2 = "^-[1-9]\\d*$|^0$"; //负数（负整数 + 0）

        /// <summary>
        /// The picture
        /// </summary>
        public static string Picture = "(.*)\\.(jpg|bmp|gif|ico|pcx|jpeg|tif|png|raw|tga)$"; //图片

        /// <summary>
        /// The qq
        /// </summary>
        public static string Qq = "^[1-9]*[1-9][0-9]*$"; //QQ号码

        /// <summary>
        /// The rar
        /// </summary>
        public static string Rar = "(.*)\\.(rar|zip|7zip|tgz)$"; //压缩文件

        /// <summary>
        /// The tel
        /// </summary>
        public static string Tel = "^(([0\\+]\\d{2,3}-)?(0\\d{2,3})-)?(\\d{7,8})(-(\\d{3,}))?$";

        //电话号码的函数(包括验证国内区号,国际区号,分机号)

        /// <summary>
        /// The telphone
        /// </summary>
        public static string Telphone = @"^\\d{10,11}$|^((\(\d{3,4}\)|\\d{3,4}-)?\\d{7,8})$";

        //电话号码的函数(包括验证国内区号,国际区号,分机号)"^\\d{10,11}|((\(\d{3,4}\)|\\d{3,4}-)?\\d{7,8})$"

        /// <summary>
        /// The URL
        /// </summary>
        public static string Url = "^http[s]?:\\/\\/([\\w-]+\\.)+[\\w-]+([\\w-./?%&=]*)?$"; //url

        /// <summary>
        /// The username
        /// </summary>
        public static string Username = "^\\w+$"; //用来用户注册。匹配由数字、26个英文字母或者下划线组成的字符串

        /// <summary>
        /// The zipcode
        /// </summary>
        public static string Zipcode = "^\\d{6}$"; //邮编

        /// <summary>
        /// The special character
        /// </summary>
        public static string SpecialChar = "^[^!@#$%*^()?`{}~！·；：“”‘’。，、？￥（）《》【】……——\\[\\]\\+\\-\\=<>&/'|]+$"; //特殊字符

        /// <summary>
        /// The unique identifier
        /// </summary>
        public static string Guid = @"^(\{){0,1}[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}(\}){0,1}$";//是否是Guid

        /// <summary>
        /// 验证端口，非负整数
        /// </summary>
        public static string IsPortFormat = @"^[1-9]\d*|0$";//验证端口，非负整数

        /// <summary>
        /// 判断名称是否正确
        /// </summary>
        public static string IsNameString = @"^[^\\\/\:\·*<>?？:|}*\?\"""" <>\|]+$";//判断名称是否正确
    }

    /// <summary>
    /// 数据验证
    /// </summary>
    public class RegexValidateHelper
    {
        /// <summary>
        /// 验证端口，正整数（范围：1—65535）
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsPortFormat(string str)
        {
            bool flag = false;
            if (str == "" || str == null)
            {
                flag = false;
            }
            else
            {
                if (Match(RegexEnum.IsPortFormat, str))
                {
                    int port = ConvertToInt(str);
                    if (port <= 0 || port > 65535)
                    {
                        flag = false;
                    }
                    else
                    {
                        flag = true;
                    }
                }
            }
            return flag;
        }

        /// <summary>
        /// 字符串转化为数字
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int ConvertToInt(string value)
        {
            int tempInt = 0;
            int.TryParse(value, out tempInt);
            return tempInt;
        }

        /// <summary>
        /// 判断名称是否正确
        /// </summary>
        /// <param name="str"></param>
        /// <param name="strGeneric">传入的正则表达式</param>
        /// <returns></returns>
        public static bool IsNameString(string str)
        {
            return Match(RegexEnum.IsNameString, str);
        }

        /// <summary>
        /// 是否为特殊字符
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public static bool SpecialChar(string value)
        {
            return Match(RegexEnum.SpecialChar, value);
        }

        /// <summary>
        /// 是否为ACSII字符
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is ASCII; otherwise, <c>false</c>.</returns>
        public static bool IsAscii(string value)
        {
            return Match(RegexEnum.Ascii, value);
        }

        /// <summary>
        /// 是否为汉字
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is chinese; otherwise, <c>false</c>.</returns>
        public static bool IsChinese(string value)
        {
            return Match(RegexEnum.Chinese, value);
        }

        /// <summary>
        /// 是否为颜色字符串
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is color; otherwise, <c>false</c>.</returns>
        public static bool IsColor(string value)
        {
            return Match(RegexEnum.Color, value);
        }

        /// <summary>
        /// 是否为时间格式
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is date; otherwise, <c>false</c>.</returns>
        public static bool IsDate(string value)
        {
            return Match(RegexEnum.Date, value);
        }

        /// <summary>
        /// 是否为浮点数
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is decmal; otherwise, <c>false</c>.</returns>
        public static bool IsDecmal(string value)
        {
            return Match(RegexEnum.Decmal, value);
        }

        /// <summary>
        /// 是否为正浮点数
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is decmal1; otherwise, <c>false</c>.</returns>
        public static bool IsDecmal1(string value)
        {
            return Match(RegexEnum.Decmal1, value);
        }

        /// <summary>
        /// 是否为负浮点数
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is decmal2; otherwise, <c>false</c>.</returns>
        public static bool IsDecmal2(string value)
        {
            return Match(RegexEnum.Decmal2, value);
        }

        /// <summary>
        /// 是否浮点数
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is decmal3; otherwise, <c>false</c>.</returns>
        public static bool IsDecmal3(string value)
        {
            return Match(RegexEnum.Decmal3, value);
        }

        /// <summary>
        /// 是否是非负浮点数（正浮点数 + 0）
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is decmal4; otherwise, <c>false</c>.</returns>
        public static bool IsDecmal4(string value)
        {
            return Match(RegexEnum.Decmal4, value);
        }

        /// <summary>
        /// 是否是非正浮点数（负浮点数 + 0）
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is decmal5; otherwise, <c>false</c>.</returns>
        public static bool IsDecmal5(string value)
        {
            return Match(RegexEnum.Decmal5, value);
        }

        /// <summary>
        /// 是否为整数(包括0)
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is int; otherwise, <c>false</c>.</returns>
        public static bool IsInt(string value)
        {
            return Match(RegexEnum.HasZeroInt, value);
        }

        /// <summary>
        /// 是否为整数(不包括0)
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if [is no zero int] [the specified value]; otherwise, <c>false</c>.</returns>
        public static bool IsNoZeroInt(string value)
        {
            return Match(RegexEnum.NoZeroInt, value);
        }

        /// <summary>
        /// 是否为 正整数，不包含0。 例如1,2,
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if [is more zero int] [the specified value]; otherwise, <c>false</c>.</returns>
        public static bool IsMoreZeroInt(string value)
        {
            return Match(RegexEnum.MoreZeroInt, value);
        }

        /// <summary>
        /// 是否是Email
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is email; otherwise, <c>false</c>.</returns>
        public static bool IsEmail(string value)
        {
            return Match(RegexEnum.Email, value);
        }

        /// <summary>
        /// 是否是身份证号
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is idcard; otherwise, <c>false</c>.</returns>
        public static bool IsIdcard(string value)
        {
            return Match(RegexEnum.Idcard, value);
        }

        /// <summary>
        /// 是否是 非0整数, -1，1
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is intege; otherwise, <c>false</c>.</returns>
        public static bool IsIntege(string value)
        {
            return Match(RegexEnum.Intege, value);
        }

        /// <summary>
        /// 是否是 正整数
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is intege1; otherwise, <c>false</c>.</returns>
        public static bool IsIntege1(string value)
        {
            return Match(RegexEnum.Intege1, value);
        }

        /// <summary>
        /// 是否是 负整数
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is intege2; otherwise, <c>false</c>.</returns>
        public static bool IsIntege2(string value)
        {
            return Match(RegexEnum.Intege2, value);
        }

        /// <summary>
        /// 是否是 IP(V4)
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if [is ip v4] [the specified value]; otherwise, <c>false</c>.</returns>
        public static bool IsIpV4(string value)
        {
            return Match(RegexEnum.Ip4, value);
        }

        /// <summary>
        /// 是否是 英文字母
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is letter; otherwise, <c>false</c>.</returns>
        public static bool IsLetter(string value)
        {
            return Match(RegexEnum.Letter, value);
        }

        /// <summary>
        /// 是否是 小写字母
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if [is letter l] [the specified value]; otherwise, <c>false</c>.</returns>
        public static bool IsLetterL(string value)
        {
            return Match(RegexEnum.LetterL, value);
        }

        /// <summary>
        /// 是否是 大写字母
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if [is letter u] [the specified value]; otherwise, <c>false</c>.</returns>
        public static bool IsLetterU(string value)
        {
            return Match(RegexEnum.LetterU, value);
        }

        /// <summary>
        /// 是否是 手机号码
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is mobile; otherwise, <c>false</c>.</returns>
        public static bool IsMobile(string value)
        {
            return Match(RegexEnum.Mobile, value);
        }

        /// <summary>
        /// 是否为数字
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is number; otherwise, <c>false</c>.</returns>
        public static bool IsNum(string value)
        {
            return Match(RegexEnum.Num, value);
        }

        /// <summary>
        /// 是否为正数
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is num1; otherwise, <c>false</c>.</returns>
        public static bool IsNum1(string value)
        {
            return Match(RegexEnum.Num1, value);
        }

        /// <summary>
        /// 是否为负数
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is num2; otherwise, <c>false</c>.</returns>
        public static bool IsNum2(string value)
        {
            return Match(RegexEnum.Num2, value);
        }

        /// <summary>
        /// 是否为正确的图片格式
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is picture; otherwise, <c>false</c>.</returns>
        public static bool IsPicture(string value)
        {
            return Match(RegexEnum.Picture, value);
        }

        /// <summary>
        /// 是否为QQ号
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is qq; otherwise, <c>false</c>.</returns>
        public static bool IsQq(string value)
        {
            return Match(RegexEnum.Qq, value);
        }

        /// <summary>
        /// 是否为压缩文件
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is rar; otherwise, <c>false</c>.</returns>
        public static bool IsRar(string value)
        {
            return Match(RegexEnum.Rar, value);
        }

        /// <summary>
        /// 是否为电话号码(包括验证国内区号,国际区号,分机号)
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is tel; otherwise, <c>false</c>.</returns>
        public static bool IsTel(string value)
        {
            return Match(RegexEnum.Tel, value);
        }

        /// <summary>
        /// 是否为链接地址
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is URL; otherwise, <c>false</c>.</returns>
        public static bool IsUrl(string value)
        {
            return Match(RegexEnum.Url, value);
        }

        /// <summary>
        /// 是否为正确的用户名
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is username; otherwise, <c>false</c>.</returns>
        public static bool IsUsername(string value)
        {
            return Match(RegexEnum.Username, value);
        }

        /// <summary>
        /// 是否为正确的邮政编码
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is zipcode; otherwise, <c>false</c>.</returns>
        public static bool IsZipcode(string value)
        {
            return Match(RegexEnum.Zipcode, value);
        }

        /// <summary>
        /// 是否为正确的Guid
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if the specified value is unique identifier; otherwise, <c>false</c>.</returns>
        public static bool IsGuid(string value)
        {
            return Match(RegexEnum.Guid, value);
        }

        /// <summary>
        /// 正则表达式验证
        /// </summary>
        /// <param name="regexStr">The regex string.</param>
        /// <param name="value">The value.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public static bool Match(string regexStr, string value)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(regexStr))
            {
                return true;
            }
            var regex = new Regex(regexStr);
            return regex.Match(value).Success;
        }
    }
}
