using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 按对象引用比较对象实例。
/// </summary>
internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    /// <summary>
    /// 提供按对象引用比较相等性的共享比较器。
    /// </summary>
    internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
    /// <summary>
    /// 初始化一个 <see cref="ReferenceEqualityComparer" /> 类型的实例。
    /// </summary>
    private ReferenceEqualityComparer() { }
    /// <inheritdoc />
    public new bool Equals(object x, object y) => ReferenceEquals(x, y);
    /// <inheritdoc />
    public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
