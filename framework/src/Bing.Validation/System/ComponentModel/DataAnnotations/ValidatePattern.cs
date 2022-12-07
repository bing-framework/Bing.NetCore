namespace Bing.Validations.Validators;

/// <summary>
/// 验证正则
/// </summary>
public static class ValidatePattern
{
    /// <summary>
    /// 手机号验证正则表达式
    /// </summary>
    public const string MobilePhonePattern = "^1[0-9]{10}$";

    /// <summary>
    /// 身份证验证正则表达式
    /// </summary>
    public const string IdCardPattern = @"(^\d{15}$)|(^\d{18}$)|(^\d{17}(\d|X|x)$)";

    /// <summary>
    /// 中文验证正则表达式
    /// </summary>
    public const string ChinesePattern = @"^[\u4e00-\u9fa5]+$";

    /// <summary>
    /// Url验证正则表达式
    /// </summary>
    public const string UrlPattern =
        @"^http(s?):\/\/([\w\-]+(\.[\w\-]+)*\/)*[\w\-]+(\.[\w\-]+)*\/?(\?([\w\-\.,@?^=%&:\/~\+#]*)+)?$";

    /// <summary>
    /// 英文字母验证正则表达式
    /// </summary>
    public const string LetterPattern = @"^[a-zA-Z]+$";

    /// <summary>
    /// 车牌号验证正则表达式
    /// </summary>
    public const string PlateNumberOfChinaPatter =
        @"^[京津沪渝冀豫云辽黑湘皖鲁新苏浙赣鄂桂甘晋蒙陕吉闽贵粤青藏川宁琼使领A-Z]{1}[A-Z]{1}[A-Z0-9]{4}[A-Z0-9挂学警港澳]{1}$";

    /// <summary>
    /// 邮政编码验证正则表达式
    /// </summary>
    public const string PostalCodeOfChinaPatter = @"^\d{6}$";

    /// <summary>
    /// QQ验证正则表达式
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public const string QQPatter = @"^[1-9][0-9]{4,10}$";

    /// <summary>
    /// 微信号验证正则表达式
    /// </summary>
    public const string WechatNoPatter = @"^[a-zA-Z]([-_a-zA-Z0-9]{5,19})+$";

    /// <summary>
    /// 固话验证正则表达式
    /// </summary>
    public const string TelNoOfChinaPatter = @"^\d{3,4}-?\d{6,8}$";
}
