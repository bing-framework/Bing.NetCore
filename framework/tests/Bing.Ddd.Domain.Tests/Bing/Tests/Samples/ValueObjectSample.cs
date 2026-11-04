using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Domain.Values;

namespace Bing.Tests.Samples;

/// <summary>
/// 值对象测试样例。
/// </summary>
public partial class ValueObjectSample : ValueObjectBase<ValueObjectSample>
{
    /// <summary>
    /// 初始化一个 <see cref="ValueObjectSample"/> 类型的实例。
    /// </summary>
    public ValueObjectSample() { }

    /// <summary>
    /// 初始化一个 <see cref="ValueObjectSample" /> 类型的实例。
    /// </summary>
    /// <param name="city">值对象所使用的城市名称。</param>
    /// <param name="street">值对象所使用的街道名称。</param>
    public ValueObjectSample(string city, string street) : this(city, street, null)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="ValueObjectSample" /> 类型的实例。
    /// </summary>
    /// <param name="city">值对象所使用的城市名称。</param>
    /// <param name="street">值对象所使用的街道名称。</param>
    /// <param name="sample">当前测试使用的聚合根样例。</param>
    public ValueObjectSample(string city, string street, AggregateRootSample sample) : this(city, street, sample, null)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="ValueObjectSample" /> 类型的实例。
    /// </summary>
    /// <param name="city">值对象所使用的城市名称。</param>
    /// <param name="street">值对象所使用的街道名称。</param>
    /// <param name="sample">当前测试使用的聚合根样例。</param>
    /// <param name="child">当前节点的子对象。</param>
    public ValueObjectSample(string city, string street, AggregateRootSample sample, ValueObjectSample child)
    {
        City = city;
        Street = street;
        AggregateRoot = sample;
        Child = child;
    }

    /// <summary>
    /// 获取城市。
    /// </summary>
    [StringLength(100, ErrorMessage = "城市输入过长，不能超过100位")]
    public string City { get; private set; }

    /// <summary>
    /// 获取街道。
    /// </summary>
    [StringLength(200, ErrorMessage = "街道输入过长，不能超过200位")]
    public string Street { get; private set; }

    /// <summary>
    /// 获取聚合根测试样例。
    /// </summary>
    [NotMapped]
    public AggregateRootSample AggregateRoot { get; private set; }

    /// <summary>
    /// 获取子值对象测试样例。
    /// </summary>
    [NotMapped]
    public ValueObjectSample Child { get; private set; }
}
