namespace Bing.Tests.Models;

/// <summary>
/// 表示扩展属性测试使用的产品项数据。
/// </summary>
public class ProductItem
{
    /// <summary>
    /// 获取或设置产品项编码。
    /// </summary>
    public int Code { get; set; }
    /// <summary>
    /// 获取或设置产品项名称。
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 获取或设置产品项的测试枚举值。
    /// </summary>
    public ProductEnum EnumValue { get; set; }
}
