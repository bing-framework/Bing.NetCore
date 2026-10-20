using Bing.Data.ObjectExtending;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bing.EntityFrameworkCore.ValueComparers;

/// <summary>
/// 扩展属性字典 值比较器
/// </summary>
public class ExtraPropertyDictionaryValueComparer : ValueComparer<ExtraPropertyDictionary>
{
    /// <summary>
    /// 初始化一个 <see cref="ExtraPropertyDictionaryValueComparer"/> 类型的实例。
    /// </summary>
    public ExtraPropertyDictionaryValueComparer() : base(
        (d1, d2) => d1.SequenceEqual(d2),
        d => d.Aggregate(0, (key, value) => HashCode.Combine(key, value.GetHashCode())),
        d => new ExtraPropertyDictionary(d))
    {
    }
}
