namespace Bing.Utils.Drawing
{
    /// <summary>
    /// 验证码类型
    /// </summary>
    public enum CaptchaType
    {
        /// <summary>
        /// 纯数值
        /// </summary>
        Number,
        /// <summary>
        /// 数值与字母组合
        /// </summary>
        NumberAndLetter,
        /// <summary>
        /// 汉字
        /// </summary>
        ChineseChar
    }
}
