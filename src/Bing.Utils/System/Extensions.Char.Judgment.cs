namespace System
{
    /// <summary>
    /// 基础类型扩展
    /// </summary>
    public static partial class BaseTypeExtensions
    {
        /// <summary>
        /// 判断当前字符是否在目标字符数组中
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="values">字符数组</param>
        public static bool In(this char @this, params char[] values) => Array.IndexOf(values, @this) != -1;

        /// <summary>
        /// 判断当前字符是否不在目标字符数组中
        /// </summary>
        /// <param name="this">字符</param>
        /// <param name="values">字符数组</param>
        public static bool NotIn(this char @this, params char[] values) => Array.IndexOf(values, @this) == -1;

        /// <summary>
        /// 判断当前字符是否空格字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsWhiteSpace(this char c) => char.IsWhiteSpace(c);

        /// <summary>
        /// 判断当前字符是否控制字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsControl(this char c) => char.IsControl(c);

        /// <summary>
        /// 判断当前字符是否十进制数字字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsDigit(this char c) => char.IsDigit(c);

        /// <summary>
        /// 判断当前字符是否英文字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsLetter(this char c) => char.IsLetter(c);

        /// <summary>
        /// 判断当前字符是否英文或十进制数字字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsLetterOrDigit(this char c) => char.IsLetterOrDigit(c);

        /// <summary>
        /// 判断当前字符是否小写英文字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsLower(this char c) => char.IsLower(c);

        /// <summary>
        /// 判断当前字符是否数字字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsNumber(this char c) => char.IsNumber(c);

        /// <summary>
        /// 判断当前字符是否标点符号
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsPunctuation(this char c) => char.IsPunctuation(c);

        /// <summary>
        /// 判断当前字符是否分隔符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsSeparator(this char c) => char.IsSeparator(c);

        /// <summary>
        /// 判断当前字符是否符号字符
        /// </summary>
        /// <param name="c">字符</param>
        public static bool IsSymbol(this char c) => char.IsSymbol(c);
    }
}
