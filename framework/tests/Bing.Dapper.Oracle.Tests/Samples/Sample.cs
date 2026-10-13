using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Bing.Dapper.Tests.Samples;

/// <summary>
/// 测试样例
/// </summary>
[Description("测试样例")]
public class Sample
{
    /// <summary>
    /// 初始化一个 <see cref="Sample"/> 类型的实例。
    /// </summary>
    public Sample()
    {
        StringList = new List<string>();
    }

    /// <summary>
    /// 获取或设置测试值。
    /// </summary>
    [Display(Name = "测试值")]
    [Required(ErrorMessage = "测试值不能为空")]
    [StringLength(20, ErrorMessage = "测试值长度不能超过20")]
    [EmailAddress]
    [Url]
    public string TestValue { get; set; }

    /// <summary>
    /// 获取或设置电子邮件地址。
    /// </summary>
    [EmailAddress(ErrorMessage = "电子邮件不正确")]
    public string Email { get; set; }

    /// <summary>
    /// 获取或设置网址。
    /// </summary>
    [Url(ErrorMessage = "网址不正确")]
    public string Url { get; set; }

    /// <summary>
    /// 获取或设置最大长度测试值。
    /// </summary>
    [MaxLength(2, ErrorMessage = "最大长度是2")]
    public string MaxLengthValue { get; set; }

    /// <summary>
    /// 获取或设置长度验证值。
    /// </summary>
    [StringLength(3, MinimumLength = 2, ErrorMessage = "最大长度是3,最小长度是2")]
    public string StringLengthValue { get; set; }

    /// <summary>
    /// 获取或设置 DisplayName 值。
    /// </summary>
    [DisplayName("DisplayNameValue值")]
    public string DisplayNameValue { get; set; }

    /// <summary>
    /// 获取或设置 Display 值。
    /// </summary>
    [Display(Name = "DisplayValue值")]
    public string DisplayValue { get; set; }

    /// <summary>
    /// 获取或设置字符串值。
    /// </summary>
    [Display(Name = "字符串值")]
    [StringLength(20, ErrorMessage = "长度不能超过20")]
    [Required(ErrorMessage = "不能为空")]
    public string StringValue { get; set; }

    /// <summary>
    /// 获取或设置 decimal 值。
    /// </summary>
    public decimal DecimalValue { get; set; }

    /// <summary>
    /// 获取或设置可空 decimal 值。
    /// </summary>
    public decimal? NullableDecimalValue { get; set; }

    /// <summary>
    /// 获取或设置 float 值。
    /// </summary>
    public float FloatValue { get; set; }

    /// <summary>
    /// 获取或设置可空 float 值。
    /// </summary>
    public float? NullableFloatValue { get; set; }

    /// <summary>
    /// 获取或设置 double 值。
    /// </summary>
    public double DoubleValue { get; set; }

    /// <summary>
    /// 获取或设置可空 double 值。
    /// </summary>
    public double? NullableDoubleValue { get; set; }

    /// <summary>
    /// 获取或设置 bool 值。
    /// </summary>
    public bool BoolValue { get; set; }

    /// <summary>
    /// 获取或设置可空 bool 值。
    /// </summary>
    public bool? NullableBoolValue { get; set; }

    /// <summary>
    /// 获取或设置 DateTime 值。
    /// </summary>
    public DateTime DateValue { get; set; }

    /// <summary>
    /// 获取或设置可空 DateTime 值。
    /// </summary>
    public DateTime? NullableDateValue { get; set; }

    /// <summary>
    /// 获取或设置不可空枚举值。
    /// </summary>
    public LogLevel LogLevel { get; set; }

    /// <summary>
    /// 获取或设置可空枚举值。
    /// </summary>
    public LogLevel? NullableLogLevel { get; set; }

    /// <summary>
    /// 获取或设置 int 值。
    /// </summary>
    [Description("IntValue")]
    public int IntValue { get; set; }

    /// <summary>
    /// 获取或设置可空 int 值。
    /// </summary>
    public int? NullableIntValue { get; set; }

    /// <summary>
    /// 获取或设置 short 值。
    /// </summary>
    public short ShortValue { get; set; }

    /// <summary>
    /// 获取或设置可空 short 值。
    /// </summary>
    public short? NullableShortValue { get; set; }

    /// <summary>
    /// 获取或设置 long 值。
    /// </summary>
    public long LongValue { get; set; }

    /// <summary>
    /// 获取或设置可空 long 值。
    /// </summary>
    public long? NullableLongValue { get; set; }

    /// <summary>
    /// 获取或设置字符串列表。
    /// </summary>
    public List<string> StringList { get; set; }

    /// <summary>
    /// 获取或设置 Guid 值。
    /// </summary>
    public Guid GuidValue { get; set; }

    /// <summary>
    /// 获取或设置可空 Guid 值。
    /// </summary>
    public Guid? NullableGuidValue { get; set; }

    /// <summary>
    /// 获取静态字符串。
    /// </summary>
    public static string StaticString => "TestStaticString";

    /// <summary>
    /// 创建测试实例1
    /// </summary>
    /// <returns>StringValue 为 A 的测试实例。</returns>
    public static Sample Create1() => new Sample { StringValue = "A" };

    /// <summary>
    /// 创建测试实例2
    /// </summary>
    /// <returns>StringValue 为 B 的测试实例。</returns>
    public static Sample Create2() => new Sample { StringValue = "B" };

    /// <summary>
    /// 获取或设置删除标记。
    /// </summary>
    public bool IsDeleted { get; set; }
}
