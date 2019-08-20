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
}
