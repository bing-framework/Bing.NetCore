using System;
using System.Collections.Generic;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
    }

    /// <summary>
    /// 类型相似度
    /// </summary>
    public enum TypeSimilarity
    {
        Any,
        Same,
        MayorLong,
        MinorLong,
    }
}
