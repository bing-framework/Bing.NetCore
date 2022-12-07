using System;

namespace Bing.Data.Attributes;

/// <summary>
/// 自定义 Decimal 类型的精度
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public class DecimalPrecisionAttribute : Attribute
{
    /// <summary>
    /// 精确度，默认：18
    /// </summary>
    public byte Precision { get; set; }

    /// <summary>
    /// 保留小数位数，默认：4
    /// </summary>
    public byte Scale { get; set; }

    /// <summary>
    /// 初始化一个<see cref="DecimalPrecisionAttribute"/>类型的实例
    /// </summary>
    public DecimalPrecisionAttribute() : this(18, 4)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="DecimalPrecisionAttribute"/>类型的实例
    /// </summary>
    /// <param name="precision">精确度</param>
    /// <param name="scale">保留小数位数</param>
    public DecimalPrecisionAttribute(byte precision, byte scale)
    {
        Precision = precision;
        Scale = scale;
    }
}