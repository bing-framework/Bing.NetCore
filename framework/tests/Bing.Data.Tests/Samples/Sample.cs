using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System;

namespace Bing.Data.Tests.Samples;

/// <summary>
/// 测试样例
/// </summary>
public class Sample
{
    /// <summary>
    /// 初始化一个 <see cref="Sample"/> 类型的实例。
    /// </summary>
    public Sample()
    {
        StringList = new List<string>();
        StringArray = StringList.ToArray();
    }

    /// <summary>
    /// 获取或设置年龄
    /// </summary>
    public int? Age { get; set; }

    /// <summary>
    /// 获取或设置电话
    /// </summary>
    [Description("电话")]
    public int Tel { get; set; }

    /// <summary>
    /// 获取或设置测试值
    /// </summary>
    [Display(Name = "测试值")]
    [Required(ErrorMessage = "测试值不能为空")]
    [StringLength(20, ErrorMessage = "测试值长度不能超过20")]
    [EmailAddress]
    [Url]
    public string TestValue { get; set; }
    /// <summary>
    /// 获取或设置电子邮件验证
    /// </summary>
    [EmailAddress(ErrorMessage = "电子邮件不正确")]
    public string Email { get; set; }
    /// <summary>
    /// 获取或设置网址验证
    /// </summary>
    [Url(ErrorMessage = "网址不正确")]
    public string Url { get; set; }
    /// <summary>
    /// 获取或设置最大长度
    /// </summary>
    [MaxLength(2, ErrorMessage = "最大长度是2")]
    public string MaxLengthValue { get; set; }
    /// <summary>
    /// 获取或设置长度验证
    /// </summary>
    [StringLength(3, MinimumLength = 2, ErrorMessage = "最大长度是3,最小长度是2")]
    public string StringLengthValue { get; set; }
    /// <summary>
    /// 获取或设置DisplayName
    /// </summary>
    [DisplayName("DisplayNameValue值")]
    public string DisplayNameValue { get; set; }
    /// <summary>
    /// 获取或设置Display
    /// </summary>
    [Display(Name = "DisplayValue值")]
    public string DisplayValue { get; set; }
    /// <summary>
    /// 获取或设置string值
    /// </summary>
    [Display(Name = "字符串值")]
    [StringLength(20, ErrorMessage = "长度不能超过20")]
    [Required(ErrorMessage = "不能为空")]
    public string StringValue { get; set; }
    /// <summary>
    /// 获取或设置decimal值
    /// </summary>
    public decimal DecimalValue { get; set; }
    /// <summary>
    /// 获取或设置可空decimal值
    /// </summary>
    public decimal? NullableDecimalValue { get; set; }
    /// <summary>
    /// 获取或设置float值
    /// </summary>
    public float FloatValue { get; set; }
    /// <summary>
    /// 获取或设置可空float值
    /// </summary>
    public float? NullableFloatValue { get; set; }
    /// <summary>
    /// 获取或设置double值
    /// </summary>
    public double DoubleValue { get; set; }
    /// <summary>
    /// 获取或设置可空double值
    /// </summary>
    public double? NullableDoubleValue { get; set; }
    /// <summary>
    /// 获取或设置bool值
    /// </summary>
    public bool BoolValue { get; set; }
    /// <summary>
    /// 获取或设置可空bool值
    /// </summary>
    public bool? NullableBoolValue { get; set; }
    /// <summary>
    /// 获取或设置DateTime值
    /// </summary>
    public DateTime DateValue { get; set; }
    /// <summary>
    /// 获取或设置可空DateTime值
    /// </summary>
    public DateTime? NullableDateValue { get; set; }
    /// <summary>
    /// 获取或设置int值
    /// </summary>
    [Description("IntValue")]
    public int IntValue { get; set; }
    /// <summary>
    /// 获取或设置可空int值
    /// </summary>
    [Description("TestNullableIntValue")]
    public int? NullableIntValue { get; set; }
    /// <summary>
    /// 获取或设置short值
    /// </summary>
    public short ShortValue { get; set; }
    /// <summary>
    /// 获取或设置可空short值
    /// </summary>
    public short? NullableShortValue { get; set; }
    /// <summary>
    /// 获取或设置long值
    /// </summary>
    public long LongValue { get; set; }
    /// <summary>
    /// 获取或设置可空long值
    /// </summary>
    public long? NullableLongValue { get; set; }
    /// <summary>
    /// 获取或设置字符串列表
    /// </summary>
    public List<string> StringList { get; set; }
    /// <summary>
    /// 获取或设置字符串数据
    /// </summary>
    public string[] StringArray { get; set; }
    /// <summary>
    /// 获取或设置Guid值
    /// </summary>
    public Guid GuidValue { get; set; }

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
}
