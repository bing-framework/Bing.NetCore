using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bing.Dapper.Tests.Samples;

/// <summary>
/// 测试样例2
/// </summary>
[DisplayName("测试样例2")]
public class Sample2
{
    /// <summary>
    /// 获取或设置描述。
    /// </summary>
    [Description("描述")]
    public string Description { get; set; }
    /// <summary>
    /// 获取或设置显示名。
    /// </summary>
    [DisplayName("显示名")]
    public string DisplayName { get; set; }
    /// <summary>
    /// 获取或设置显示描述。
    /// </summary>
    [Display(Description = "显示描述")]
    public string Display { get; set; }
    /// <summary>
    /// 获取或设置字符串值。
    /// </summary>
    public string StringValue { get; set; }
    /// <summary>
    /// 获取或设置 int 值。
    /// </summary>
    public int IntValue { get; set; }
    /// <summary>
    /// 获取或设置导航属性。
    /// </summary>
    [NotMapped]
    public Sample3 Test3 { get; set; }
}
