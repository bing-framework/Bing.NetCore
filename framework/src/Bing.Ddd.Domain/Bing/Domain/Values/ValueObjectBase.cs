using System.Reflection;
using Bing.Domain.Entities;
using Bing.Helpers;

namespace Bing.Domain.Values;

/// <summary>
/// 值对象
/// </summary>
/// <remarks>参考地址：https://docs.microsoft.com/zh-cn/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/implement-value-objects</remarks>
/// <typeparam name="TValueObject">值对象类型</typeparam>
public abstract class ValueObjectBase<TValueObject> : DomainObjectBase<TValueObject>, IEquatable<TValueObject> where TValueObject : ValueObjectBase<TValueObject>
{
    /// <inheritdoc />
    public bool Equals(TValueObject other) => this == other;

    /// <inheritdoc />
    /// <remarks>
    /// 相等性比较
    /// </remarks>
    public override bool Equals(object other) => Equals(other as TValueObject);

    /// <summary>
    /// 相等性比较
    /// </summary>
    /// <param name="left">值对象</param>
    /// <param name="right">值对象</param>
    /// <returns>两个值对象的所有属性值均相等时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public static bool operator ==(ValueObjectBase<TValueObject> left, ValueObjectBase<TValueObject> right)
    {
        if ((object)left == null && (object)right == null)
            return true;
        if (!(left is TValueObject) || !(right is TValueObject))
            return false;
        var properties = left.GetType().GetTypeInfo().GetProperties();
        return properties.All(property => property.GetValue(left) == property.GetValue(right));
    }

    /// <summary>
    /// 判断两个值对象是否不相等。
    /// </summary>
    /// <param name="left">值对象</param>
    /// <param name="right">值对象</param>
    /// <returns>值对象不相等时为 <see langword="true"/>。</returns>
    public static bool operator !=(ValueObjectBase<TValueObject> left, ValueObjectBase<TValueObject> right) => !(left == right);

    /// <inheritdoc />
    /// <remarks>
    /// 获取哈希
    /// </remarks>
    public override int GetHashCode()
    {
        var properties = GetType().GetTypeInfo().GetProperties();
        return properties.Select(property => property.GetValue(this))
            .Where(value => value != null)
            .Aggregate(0, (current, value) => current ^ value.GetHashCode());
    }

    /// <summary>
    /// 克隆副本
    /// </summary>
    /// <returns>当前值对象的浅表副本。</returns>
    public virtual TValueObject Clone() => Conv.To<TValueObject>(MemberwiseClone());
}
