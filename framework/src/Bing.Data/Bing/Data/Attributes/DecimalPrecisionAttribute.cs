namespace Bing.Data.Attributes;

/// <summary>
/// 为实体属性指定十进制数据的精度和小数位数。
/// </summary>
/// <remarks>使用无参构造函数时，精度默认为 18，小数位数默认为 4。</remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public class DecimalPrecisionAttribute : Attribute
{
    /// <summary>
    /// 获取或设置十进制数据的总精度。
    /// </summary>
    public byte Precision { get; set; }

    /// <summary>
    /// 获取或设置十进制数据的小数位数。
    /// </summary>
    public byte Scale { get; set; }

    /// <summary>
    /// 初始化一个 <see cref="DecimalPrecisionAttribute"/> 类型的实例。
    /// </summary>
    public DecimalPrecisionAttribute() : this(18, 4)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="DecimalPrecisionAttribute"/> 类型的实例。
    /// </summary>
    /// <param name="precision">精确度</param>
    /// <param name="scale">保留小数位数</param>
    public DecimalPrecisionAttribute(byte precision, byte scale)
    {
        Precision = precision;
        Scale = scale;
    }
}
