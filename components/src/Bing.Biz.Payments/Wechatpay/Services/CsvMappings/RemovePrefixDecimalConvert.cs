﻿using System;
using Bing.Extensions;
using TinyCsvParser.TypeConverter;

namespace Bing.Biz.Payments.Wechatpay.Services.CsvMappings;

/// <summary>
/// 移除前缀`字符的decimal列转换器
/// </summary>
internal class RemovePrefixDecimalConvert : ITypeConverter<decimal>
{
    /// <summary>
    /// 转换
    /// </summary>
    public bool TryConvert(string value, out decimal result)
    {
        result = string.IsNullOrWhiteSpace(value) ? 0 : value.TrimStart('`').ToDecimal();
        return true;
    }

    /// <summary>
    /// 目标类型
    /// </summary>
    public Type TargetType => typeof(decimal);
}