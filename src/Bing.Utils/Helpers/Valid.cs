using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Bing.Utils.Extensions;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 验证 操作
    /// </summary>
    public static class Valid
    {
        #region IsEmail(是否邮箱)

        /// <summary>
        /// 是否邮箱
        /// </summary>
        /// <param name="value">邮箱地址</param>
        /// <param name="isRestrict">是否按严格模式验证</param>
        /// <returns></returns>
        public static bool IsEmail(string value, bool isRestrict = false)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            string pattern = isRestrict
                ? @"^(?("")("".+?""@)|(([0-9a-zA-Z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-zA-Z])@))(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-zA-Z][-\w]*[0-9a-zA-Z]\.)+[a-zA-Z]{2,6}))$"
                : @"^[\w-]+(\.[\w-]+)*@[\w-]+(\.[\w-]+)+$";

            return value.IsMatch(pattern, RegexOptions.IgnoreCase);
        }

        #endregion

        #region HasEmail(是否存在邮箱)

        /// <summary>
        /// 是否存在邮箱
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="isRestrict">是否按严格模式验证</param>
        /// <returns></returns>
        public static bool HasEmail(string value, bool isRestrict = false)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            string pattern = isRestrict
                ? @"^(?("")("".+?""@)|(([0-9a-zA-Z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-zA-Z])@))(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-zA-Z][-\w]*[0-9a-zA-Z]\.)+[a-zA-Z]{2,6}))$"
                : @"^[\w-]+(\.[\w-]+)*@[\w-]+(\.[\w-]+)+$";
            return value.IsMatch(pattern, RegexOptions.IgnoreCase);
        }

        #endregion

        #region IsPhoneNumber(是否合法的手机号码)
        /// <summary>
        /// 是否合法的手机号码
        /// </summary>
        /// <param name="value">手机号码</param>
        /// <returns></returns>
        public static bool IsPhoneNumber(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^(0|86|17951)?(13[0-9]|15[012356789]|18[0-9]|14[57]|17[678])[0-9]{8}$");
        }
        #endregion

        #region IsMobileNumber(是否手机号码)
        /// <summary>
        /// 是否手机号码
        /// </summary>
        /// <param name="value">手机号码</param>
        /// <param name="isRestrict">是否按严格模式验证</param>
        /// <returns></returns>
        public static bool IsMobileNumberSimple(string value, bool isRestrict = false)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            string pattern = isRestrict ? @"^[1][3-8]\d{9}$" : @"^[1]\d{10}$";
            return value.IsMatch(pattern);
        }
        /// <summary>
        /// 是否手机号码
        /// </summary>
        /// <param name="value">手机号码</param>
        /// <returns></returns>
        public static bool IsMobileNumber(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            value = value.Trim().Replace("^", "").Replace("$", "");
            /*
             * 手机号码: 
             * 13[0-9], 14[5,7], 15[0, 1, 2, 3, 5, 6, 7, 8, 9], 17[6, 7, 8], 18[0-9], 170[0-9]
             * 移动号段: 134,135,136,137,138,139,150,151,152,157,158,159,182,183,184,187,188,147,178,1705
             * 联通号段: 130,131,132,155,156,185,186,145,176,1709
             * 电信号段: 133,153,180,181,189,177,1700
             */
            return value.IsMatch(@"^1(3[0-9]|4[57]|5[0-35-9]|8[0-9]|70)\d{8}$");
        }

        /// <summary>
        /// 是否存在手机号码
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="isRestrict">是否按严格模式验证</param>
        /// <returns></returns>
        public static bool HasMobileNumberSimple(string value, bool isRestrict = false)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            string pattern = isRestrict ? @"[1][3-8]\d{9}" : @"[1]\d{10}";
            return value.IsMatch(pattern);
        }
        #endregion

        #region IsChinaMobilePhone(是否中国移动号码)
        /// <summary>
        /// 是否中国移动号码
        /// </summary>
        /// <param name="value">手机号码</param>
        /// <returns></returns>
        public static bool IsChinaMobilePhone(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }

            /*
             * 中国移动：China Mobile
             * 134,135,136,137,138,139,150,151,152,157,158,159,182,183,184,187,188,147,178,1705
             */
            return value.IsMatch(@"(^1(3[4-9]|4[7]|5[0-27-9]|7[8]|8[2-478])\d{8}$)|(^1705\d{7}$)");
        }
        #endregion

        #region IsChinaUnicomPhone(是否中国联通号码)
        /// <summary>
        /// 是否中国联通号码
        /// </summary>
        /// <param name="value">手机号码</param>
        /// <returns></returns>
        public static bool IsChinaUnicomPhone(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            /*
             * 中国联通：China Unicom
             * 130,131,132,155,156,185,186,145,176,1709
             */
            return value.IsMatch(@"(^1(3[0-2]|4[5]|5[56]|7[6]|8[56])\d{8}$)|(^1709\d{7}$)");
        }
        #endregion

        #region IsChinaTelecomPhone(是否中国电信号码)
        /// <summary>
        /// 是否中国电信号码
        /// </summary>
        /// <param name="value">手机号码</param>
        /// <returns></returns>
        public static bool IsChinaTelecomPhone(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            /*
             * 中国电信：China Telecom
             * 133,153,180,181,189,177,1700
             */
            return value.IsMatch(@"(^1(33|53|77|8[019])\d{8}$)|(^1700\d{7}$)");
        }
        #endregion

        #region IsTel(是否中国电话)
        /// <summary>
        /// 是否中国电话，格式：010-85849685
        /// </summary>
        /// <param name="value">电话</param>
        /// <returns></returns>
        public static bool IsTel(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^\d{3,4}-?\d{6,8}$", RegexOptions.IgnoreCase);
        }
        #endregion

        #region IsIdCard(是否身份证号码)
        /// <summary>
        /// 是否身份证号码
        /// </summary>
        /// <param name="value">身份证</param>
        /// <returns></returns>
        public static bool IsIdCard(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            if (value.Length == 15)
            {
                return value.IsMatch(@"^[1-9]\d{7}((0\d)|(1[0-2]))(([0|1|2]\d)|3[0-1])\d{3}$");
            }
            return value.Length == 0x12 &&
                   value.IsMatch(@"^[1-9]\d{5}[1-9]\d{3}((0\d)|(1[0-2]))(([0|1|2]\d)|3[0-1])((\d{4})|\d{3}[Xx])$",
                       RegexOptions.IgnoreCase);
        }
        #endregion

        #region IsBase64String(是否Base64编码)
        /// <summary>
        /// 是否Base64编码
        /// </summary>
        /// <param name="value">Base64字符串</param>
        /// <returns></returns>
        public static bool IsBase64String(string value)
        {
            return value.IsMatch(@"[A-Za-z0-9\+\/\=]");
        }
        #endregion

        #region IsGuid(是否Guid)
        /// <summary>
        /// 是否Guid
        /// </summary>
        /// <param name="guid">Guid字符串</param>
        /// <returns></returns>
        public static bool IsGuid(string guid)
        {
            if (guid.IsEmpty())
            {
                return false;
            }
            return guid.IsMatch(@"[A-F0-9]{8}(-[A-F0-9]{4}){3}-[A-F0-9]{12}|[A-F0-9]{32}", RegexOptions.IgnoreCase);
        }
        #endregion

        #region IsUrl(是否Url地址)
        /// <summary>
        /// 是否Url地址（统一资源定位）
        /// </summary>
        /// <param name="value">url地址</param>
        /// <returns></returns>
        public static bool IsUrl(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return
                value.IsMatch(
                    @"^(http|https)\://([a-zA-Z0-9\.\-]+(\:[a-zA-Z0-9\.&%\$\-]+)*@)*((25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9])\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[0-9])|localhost|([a-zA-Z0-9\-]+\.)*[a-zA-Z0-9\-]+\.(com|edu|gov|int|mil|net|org|biz|arpa|info|name|pro|aero|coop|museum|[a-zA-Z]{1,10}))(\:[0-9]+)*(/($|[a-zA-Z0-9\.\,\?\'\\\+&%\$#\=~_\-]+))*$",
                    RegexOptions.IgnoreCase);
        }
        #endregion

        #region IsUri(是否Uri)
        /// <summary>
        /// 是否Uri（统一资源标识）
        /// </summary>
        /// <param name="value">uri</param>
        /// <returns></returns>
        public static bool IsUri(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            if (value.IndexOf(".", StringComparison.OrdinalIgnoreCase) == -1)
            {
                return false;
            }
            var schemes = new[]
            {
                "file",
                "ftp",
                "gopher",
                "http",
                "https",
                "ldap",
                "mailto",
                "net.pipe",
                "net.tcp",
                "news",
                "nntp",
                "telnet",
                "uuid"
            };

            bool hasValidSchema = false;
            foreach (string scheme in schemes)
            {
                if (hasValidSchema)
                {
                    continue;
                }
                if (value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                {
                    hasValidSchema = true;
                }
            }
            if (!hasValidSchema)
            {
                value = "http://" + value;
            }
            return Uri.IsWellFormedUriString(value, UriKind.Absolute);
        }
        #endregion

        #region IsMainDomainUrl(是否主域名Url地址)
        /// <summary>
        /// 是否主域名或者www开头的域名
        /// </summary>
        /// <param name="value">url地址</param>
        /// <returns></returns>
        public static bool IsMainDomainUrl(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return
                value.IsMatch(
                    @"^http(s)?\://((www.)?[a-zA-Z0-9\-]+\.(com|edu|gov|int|mil|net|org|biz|arpa|info|name|pro|aero|coop|museum|[a-zA-Z]{1,10}))(\:[0-9]+)*(/($|[a-zA-Z0-9\.\,\?\'\\\+&%\$#\=~_\-]+))*$");
        }
        #endregion

        #region IsMainDomain(是否主域名)

        /// <summary>
        /// 是否主域名
        /// </summary>
        /// <param name="value">域名字符串</param>
        /// <returns></returns>
        public static bool IsMainDomain(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(
                @"^((www.)?[a-zA-Z0-9\-]+\.(com|edu|gov|int|mil|net|org|biz|arpa|info|name|pro|aero|coop|museum|[a-zA-Z]{1,10}))(\:[0-9]+)*$",
                RegexOptions.IgnoreCase);
        }

        #endregion

        #region IsDomain(是否域名)

        /// <summary>
        /// 是否域名
        /// </summary>
        /// <param name="value">域名字符串</param>
        /// <returns></returns>
        public static bool IsDomain(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(
                @"^(([a-zA-Z0-9\-]+\.)?[a-zA-Z0-9\-]+\.(com|edu|gov|int|mil|net|org|biz|arpa|info|name|pro|aero|coop|museum|[a-zA-Z]{1,10}))(\:[0-9]+)*$",
                RegexOptions.IgnoreCase);
        }

        #endregion

        #region IsMac(是否Mac地址)
        /// <summary>
        /// 是否Mac地址
        /// </summary>
        /// <param name="value">Mac地址</param>
        /// <returns></returns>
        public static bool IsMac(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^([0-9A-F]{2}-){5}[0-9A-F]{2}$") || value.IsMatch(@"^[0-9A-F]{12}$");
        }
        #endregion

        #region IsPositiveInteger(是否大于0的正整数)
        /// <summary>
        /// 是否大于0的正整数
        /// </summary>
        /// <param name="value">正整数</param>
        /// <returns></returns>
        public static bool IsPositiveInteger(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^[1-9]+\d*$");
        }
        #endregion

        #region IsInt32(是否Int32类型)
        /// <summary>
        /// 是否Int32类型
        /// </summary>
        /// <param name="value">整数</param>
        /// <returns></returns>
        public static bool IsInt32(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^[0-9]*$");
        }
        #endregion

        #region IsDouble(是否Double类型，如果带有.默认为1位0)
        /// <summary>
        /// 是否Double类型
        /// </summary>
        /// <param name="value">小数</param>
        /// <returns></returns>
        public static bool IsDouble(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^\d[.]?\d?$");
        }
        /// <summary>
        /// 是否Double类型
        /// </summary>
        /// <param name="value">小数</param>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值</param>
        /// <param name="digit">小数位数，如果是0则不检测</param>
        /// <returns></returns>
        public static bool IsDouble(string value, double minValue, double maxValue, int digit)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            string patten = string.Format(@"^\d[.]?\d{0}$", "{0,10}");
            if (digit > 0)
            {
                patten = string.Format(@"^\d[.]?\d{0}$", "{" + digit + "}");
            }
            if (value.IsMatch(patten))
            {
                double val = Convert.ToDouble(value);
                if (val >= minValue && val <= maxValue)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region IsPassword(是否密码格式)
        /// <summary>
        /// 是否密码One的格式，6-25位包含特殊字符
        /// </summary>
        /// <param name="value">密码</param>
        /// <returns></returns>
        public static bool IsPasswordOne(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^[\@A-Za-z0-9\!\#\$\%\^\&\*\.\~_]{6,25}$");
        }
        /// <summary>
        /// 是否密码One的格式，指定密码长度，包含特殊字符
        /// </summary>
        /// <param name="value">密码</param>
        /// <param name="min">最小长度</param>
        /// <param name="max">最大长度</param>
        /// <returns></returns>
        public static bool IsPasswordOne(string value, int min, int max)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(string.Format(@"^[\@A-Za-z0-9\!\#\$\%\^\&\*\.\~_]{0},{1}$", "{" + min, max + "}"));
        }
        /// <summary>
        /// 是否密码Two的格式，6-25位包含特殊字符
        /// </summary>
        /// <param name="value">密码</param>
        /// <returns></returns>
        public static bool IsPasswordTwo(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return
                value.IsMatch(
                    @"(?=^.{6,25}$)(?=(?:.*?\d){1})(?=.*[a-z])(?=(?:.*?[A-Z]){1})(?=(?:.*?[!@#$%*()_+^&}{:;?.]){1})(?!.*\s)[0-9a-zA-Z!@#$%*()_+^&]*$");
        }
        #endregion

        #region IsLoginName(是否登录账号)
        /// <summary>
        /// 是否登录账号，6-30位长度
        /// </summary>
        /// <param name="value">登录账号</param>
        /// <returns></returns>
        public static bool IsLoginName(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^(?![^a-zA-Z]+$)[A-Za-z0-9]{6,30}$");
        }
        /// <summary>
        /// 是否登录账号
        /// </summary>
        /// <param name="value">登录账号</param>
        /// <param name="min">最小长度</param>
        /// <param name="max">最大长度</param>
        /// <returns></returns>
        public static bool IsLoginName(string value, int min, int max)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(string.Format(@"^(?![^a-zA-Z]+$)[A-Za-z0-9]{0},{1}$", "{" + min, max + "}"));
        }
        #endregion

        #region IsBankCard(是否银行卡号)
        /// <summary>
        /// 是否银行卡号，16位或19位银行卡（简单校验）
        /// </summary>
        /// <param name="value">银行卡号</param>
        /// <returns></returns>
        public static bool IsBandCard(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^(\d{16}|\d{19})$");
        }
        #endregion

        #region IsSafeSqlString(是否安全Sql语句)
        /// <summary>
        /// 是否安全Sql语句
        /// </summary>
        /// <param name="value">sql语句</param>
        /// <returns></returns>
        public static bool IsSafeSqlString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.IsMatch(@"[-|;|,|\/|\(|\)|\[|\]|\}|\{|%|@|\*|!|\']") || value.IsMatch(
                    @"select|insert|delete|from|count(|drop table|update|truncate|asc(|mid(|Char(|xp_cmdshell|exec master|netlocalgroup administrators|:|net user|""|or|and")
            )
            {
                return true;
            }

            return false;
        }
        #endregion

        #region IsVersion(是否有效的版本号)
        /// <summary>
        /// 是否有效版本号，范例：1.3,1.1.5,1.25.256
        /// </summary>
        /// <param name="value">版本号</param>
        /// <param name="length">长度</param>
        /// <returns></returns>
        public static bool IsVersion(string value, int length = 5)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            value = value.Replace("^", "").Replace("$", "");
            return value.IsMatch(string.Format(@"^{0}{1}{2}$", @"\d{0,4}\.(\d{1,4}\.){0,", length, @"}\d{1,4}"));
        }
        #endregion

        #region IsChineseWord(是否中文字符)

        /// <summary>
        /// 是否中文字符
        /// </summary>
        /// <param name="value">中文字符</param>
        /// <returns></returns>
        public static bool IsChineseWord(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IsMatch(@"^[\u4e00-\u9fa5]{0,}$");
        }

        #endregion

        #region IsChinese(是否中文)

        /// <summary>
        /// 是否中文
        /// </summary>
        /// <param name="value">中文</param>
        /// <returns></returns>
        public static bool IsChinese(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^[\u4e00-\u9fa5]+$", RegexOptions.IgnoreCase);
        }

        #endregion

        #region IsContainsChinese(是否包含中文)

        /// <summary>
        /// 是否包含中文
        /// </summary>
        /// <param name="value">中文</param>
        /// <returns></returns>
        public static bool IsContainsChinese(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"[\u4e00-\u9fa5]+", RegexOptions.IgnoreCase);
        }

        #endregion

        #region IsContainsNumber(是否包含数字)

        /// <summary>
        /// 是否包含数字
        /// </summary>
        /// <param name="value">数字</param>
        /// <returns></returns>
        public static bool IsContainsNumber(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"[0-9]+");
        }

        #endregion

        #region IsIpAddress(是否IP地址)
        /// <summary>
        /// 是否IP地址
        /// </summary>
        /// <param name="value">ip地址</param>
        /// <returns>结果</returns>
        public static bool IsIpAddress(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^(\d(25[0-5]|2[0-4][0-9]|1?[0-9]?[0-9])\d\.){3}\d(25[0-5]|2[0-4][0-9]|1?[0-9]?[0-9])\d$");
        }
        #endregion

        #region IsInteger(是否整数)
        /// <summary>
        /// 是否整数
        /// </summary>
        /// <param name="value">值</param>
        /// <returns>结果</returns>
        public static bool IsInteger(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^\-?[0-9]+$");
        }
        #endregion

        #region IsUnicode(是否Unicode字符串)
        /// <summary>
        /// 是否Unicode字符串
        /// </summary>
        /// <param name="value">unicode字符串</param>
        /// <returns>结果</returns>
        public static bool IsUnicode(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return
                value.IsMatch(
                    @"^(http|https|ftp|rtsp|mms):(\/\/|\\\\)[A-Za-z0-9%\-_@]+\.[A-Za-z0-9%\-_@]+[A-Za-z0-9\.\/=\?%\-&_~`@:\+!;]*$");
        }
        #endregion

        #region IsLengthStr(字符串长度是否在指定范围内)
        /// <summary>
        /// 字符串长度是否在指定范围内，一个中文为2个字符
        /// </summary>
        /// <param name="value">字符串</param>
        /// <param name="begin">开始</param>
        /// <param name="end">结束</param>
        /// <returns></returns>
        public static bool IsLengthStr(string value, int begin, int end)
        {
            int length = Regex.Replace(value, @"[^\x00-\xff]", "OK").Length;
            if ((length <= begin) && (length >= end))
            {
                return false;
            }
            return true;
        }
        #endregion

        #region IsPostalCode(是否中国邮政编码)
        /// <summary>
        /// 是否中国邮政编码，6位数字
        /// </summary>
        /// <param name="value">邮政编码</param>
        /// <returns></returns>
        public static bool IsChinesePostalCode(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^[1-9]\d{5}$", RegexOptions.IgnoreCase);
        }
        #endregion

        #region IsNormalChar(是否正常字符，字母、数字、下划线的组合)
        /// <summary>
        /// 是否正常字符，字母、数字、下划线的组合
        /// </summary>
        /// <param name="value">字符串</param>
        /// <returns></returns>
        public static bool IsNormalChar(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"[\w\d_]+", RegexOptions.IgnoreCase);
        }
        #endregion

        #region IsPostfix(是否指定后缀)
        /// <summary>
        /// 是否指定后缀
        /// </summary>
        /// <param name="value">字符串</param>
        /// <param name="postfixs">后缀名数组</param>
        /// <returns></returns>
        public static bool IsPostfix(string value, string[] postfixs)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            string postfix = string.Join("|", postfixs);
            return value.IsMatch(string.Format(@".(?i:{0})$", postfix));
        }
        #endregion

        #region IsDecimal(是否数字型)
        /// <summary>
        /// 是否数字型
        /// </summary>
        /// <param name="value">数字</param>
        /// <returns></returns>
        public static bool IsDecimal(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^([0-9])[0-9]*(\.\w*)?$");
        }
        #endregion

        #region IsRepeat(是否重复)
        /// <summary>
        /// 是否重复，范例：112,返回true
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool IsRepeat(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            var array = value.ToCharArray();
            return array.Any(c => array.Count(t => t == c) > 1);
        }
        #endregion

        #region IsQQ(是否合法QQ号码)
        /// <summary>
        /// 是否合法QQ号码
        /// </summary>
        /// <param name="value">QQ号码</param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public static bool IsQQ(string value)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            return value.IsMatch(@"^[1-9][0-9]{4,9}$");
        }
        #endregion

        #region IsColorValue(是否颜色值)

        /// <summary>
        /// 是否颜色值。检查颜色值是否为3/6位的合法颜色值。例如：FFF、FFFFFF
        /// </summary>
        /// <param name="value">颜色值</param>
        /// <returns></returns>
        public static bool IsColorValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            value = value.Trim().Trim('#');
            if (value.Length != 33 && value.Length != 6)
            {
                return false;
            }

            // 不包含0-9 a-f以外的字符
            return !value.IsMatch(@"[^0-9a-f]", RegexOptions.IgnoreCase);
        }

        #endregion

        #region IsWideWord(是否全角字符)

        /// <summary>
        /// 是否全角字符，包含汉字在内
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool IsWideWord(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IsMatch(@"[^/x00-/xff]");
        }

        #endregion

        #region IsNarrowWord(是否半角字符)

        /// <summary>
        /// 是否半角字符，包含汉字在内
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool IsNarrowWord(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IsMatch(@"[/x00-/xff]");
        }

        #endregion

        #region IsOnlyNumber(是否数字)

        /// <summary>
        /// 是否数字。0-9的数字，不包含符号"."和"-"
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool IsOnlyNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IsMatch(@"^\d+$");
        }

        #endregion

        #region IsUpperCaseChar(是否大写英文字母)

        /// <summary>
        /// 是否大写英文字母组成的字符串
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool IsUpperCaseChar(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IsMatch(@"^[A-Z]+$");
        }

        /// <summary>
        /// 是否大写英文字母
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool IsUpperCaseChar(char value)
        {
            return IsUpperCaseChar(value.ToString());
        }

        #endregion

        #region IsLowerCaseChar(是否小写英文字母)

        /// <summary>
        /// 是否小写英文字母组成的字符串
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool IsLowerCaseChar(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IsMatch(@"^[a-z]+$");
        }

        /// <summary>
        /// 是否小写英文字母
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool IsLowerCaseChar(char value)
        {            
            return IsLowerCaseChar(value.ToString());
        }

        #endregion

        #region 日期类型

        #region IsTime(是否时间格式)

        /// <summary>
        /// 是否时间格式
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static bool IsTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IsMatch(@"^((([0-1]?[0-9])|(2[0-3])):([0-5]?[0-9])(:[0-5]?[0-9])?)$");
        }

        #endregion

        #region IsDate(是否日期)

        /// <summary>
        /// 是否日期
        /// </summary>
        /// <param name="value">日期字符串</param>
        /// <param name="isRegex">是否正则验证</param>
        /// <returns></returns>
        public static bool IsDate(string value, bool isRegex = false)
        {
            if (value.IsEmpty())
            {
                return false;
            }
            if (isRegex)
            {
                //考虑到4年一度的366天，还有特殊的2月的日期
                return
                    value.IsMatch(
                        @"^((((1[6-9]|[2-9]\d)\d{2})-(0?[13578]|1[02])-(0?[1-9]|[12]\d|3[01]))|(((1[6-9]|[2-9]\d)\d{2})-(0?[13456789]|1[012])-(0?[1-9]|[12]\d|30))|(((1[6-9]|[2-9]\d)\d{2})-0?2-(0?[1-9]|1\d|2[0-8]))|(((1[6-9]|[2-9]\d)(0[48]|[2468][048]|[13579][26])|((16|[2468][048]|[3579][26])00))-0?2-29-)) (20|21|22|23|[0-1]?\d):[0-5]?\d:[0-5]?\d$");
            }
            return DateTime.TryParse(value, out var minValue);
        }

        /// <summary>
        /// 是否日期
        /// </summary>
        /// <param name="value">日期字符串</param>
        /// <param name="format">格式化字符串</param>
        /// <returns></returns>
        public static bool IsDate(string value, string format)
        {
            return IsDate(value, format, null, DateTimeStyles.None);
        }

        /// <summary>
        /// 是否日期
        /// </summary>
        /// <param name="value">日期字符串</param>
        /// <param name="format">格式化字符串</param>
        /// <param name="provider">格式化提供者</param>
        /// <param name="styles">日期格式</param>
        /// <returns></returns>
        public static bool IsDate(string value, string format, IFormatProvider provider, DateTimeStyles styles)
        {
            if (value.IsEmpty())
            {
                return false;
            }

            return DateTime.TryParseExact(value, format, provider, styles, out var minValue);
        }
        #endregion

        #region IsDateTime(是否有效时间)
        /// <summary>
        /// 是否大于最小时间
        /// </summary>
        /// <param name="value">时间</param>
        /// <param name="min">最小时间</param>
        /// <returns></returns>
        public static bool IsDateTimeMin(string value, DateTime min)
        {
            if (value.IsEmpty())
            {
                return false;
            }

            if (DateTime.TryParse(value, out var dateTime))
            {
                if (DateTime.Compare(dateTime, min) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 是否小于最大时间
        /// </summary>
        /// <param name="value">时间</param>
        /// <param name="max">最大时间</param>
        /// <returns></returns>
        public static bool IsDateTimeMax(string value, DateTime max)
        {
            if (value.IsEmpty())
            {
                return false;
            }

            if (DateTime.TryParse(value, out var dateTime))
            {
                if (DateTime.Compare(max, dateTime) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #endregion
    }
}
