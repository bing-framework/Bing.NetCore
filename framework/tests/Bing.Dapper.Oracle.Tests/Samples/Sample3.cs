using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bing.Dapper.Tests.Samples;

/// <summary>
/// 测试样例3
/// </summary>
[DisplayName("测试样例3")]
public class Sample3
{
    /// <summary>
    /// 获取或设置字符串值。
    /// </summary>
    public string StringValue { get; set; }

    /// <summary>
    /// 获取或设置删除标记。
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 获取或设置是否跳过。
    /// </summary>
    [NotMapped]
    public bool Ignore { get; set; }
}
