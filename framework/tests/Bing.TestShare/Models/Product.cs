using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Data;
using Bing.Domain.Entities;

namespace Bing.Tests.Models;

/// <summary>
/// 表示用于数据访问测试的产品实体。
/// </summary>
[Description("产品")]
public class Product : AggregateRoot<Product>, ISoftDelete, IAuditedObject//, IExtraProperties
{
    /// <summary>
    /// 初始化一个 <see cref="Product"/> 类型的实例。
    /// </summary>
    public Product() : this(Guid.Empty)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="Product"/> 类型的实例。
    /// </summary>
    /// <param name="id">产品标识</param>
    public Product(Guid id) : base(id)
    {
        //TestProperty2 = new ProductItem();
        //TestProperty4 = new ProductItem2();
        //TestProperties = new List<ProductItem>();
    }

    /// <summary>
    /// 获取或设置产品编码。
    /// </summary>
    [Description("产品编码")]
    [MaxLength(50)]
    public string Code { get; set; }
    /// <summary>
    /// 获取或设置产品名称。
    /// </summary>
    [Description("产品名称")]
    [MaxLength(500)]
    public string Name { get; set; }
    /// <summary>
    /// 获取或设置产品价格。
    /// </summary>
    [Description("价格")]
    public decimal Price { get; set; }
    /// <summary>
    /// 获取或设置以 <see langword="int"/> 表示的产品价格。
    /// </summary>
    [Description("价格")]
    public int IntPrice { get; set; }
    /// <summary>
    /// 获取或设置以 <see langword="long"/> 表示的产品价格。
    /// </summary>
    [Description("价格")]
    public long LongPrice { get; set; }
    /// <summary>
    /// 获取或设置以 <see langword="float"/> 表示的产品价格。
    /// </summary>
    [Description("价格")]
    public float FloatPrice { get; set; }
    /// <summary>
    /// 获取或设置产品描述。
    /// </summary>
    [Description("描述")]
    public string Description { get; set; }
    /// <summary>
    /// 获取或设置产品是否启用。
    /// </summary>
    [Description("启用")]
    public bool Enabled { get; set; }
    /// <inheritdoc />
    [Description("创建时间")]
    public DateTime? CreationTime { get; set; }
    /// <inheritdoc />
    [Description("创建人")]
    public Guid? CreatorId { get; set; }
    /// <inheritdoc />
    [Description("最后修改时间")]
    public DateTime? LastModificationTime { get; set; }
    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置产品最后修改者标识。
    /// </remarks>
    [Description("最后修改人")]
    public Guid? LastModifierId { get; set; }
    /// <inheritdoc />
    [Description("是否删除")]
    public bool IsDeleted { get; set; }

    ///// <summary>
    ///// 简单扩展属性
    ///// </summary>
    //[NotMapped]
    //public string TestProperty1 {
    //    get => ExtraProperties.GetProperty<string>( nameof( TestProperty1 ) );
    //    set => ExtraProperties.SetProperty( nameof(TestProperty1), value );
    //}

    //private readonly ExtraProperty<ProductItem> _property2 = new(nameof( TestProperty2 ) );
    ///// <summary>
    ///// 对象扩展属性
    ///// </summary>
    //[NotMapped]
    //public ProductItem TestProperty2 {
    //    get => _property2.GetProperty( ExtraProperties );
    //    set => _property2.SetProperty( ExtraProperties,value );
    //}

    ///// <summary>
    ///// 枚举扩展属性
    ///// </summary>
    //[NotMapped]
    //public ProductEnum? TestProperty3 {
    //    get => ExtraProperties.GetProperty<ProductEnum?>( nameof( TestProperty3 ) );
    //    set => ExtraProperties.SetProperty( nameof( TestProperty3 ), value );
    //}

    //private readonly ExtraProperty<ProductItem2> _property4 = new( nameof( TestProperty4 ) );
    ///// <summary>
    ///// 不可变对象扩展属性
    ///// </summary>
    //[NotMapped]
    //public ProductItem2 TestProperty4 {
    //    get => _property4.GetProperty( ExtraProperties );
    //    set => _property4.SetProperty( ExtraProperties, value );
    //}

    //private readonly ExtraProperty<List<ProductItem>>  _properties = new( nameof( TestProperties ) );

    ///// <summary>
    ///// 对象集合扩展属性
    ///// </summary>
    //[NotMapped]
    //public List<ProductItem> TestProperties {
    //    get => _properties.GetProperty( ExtraProperties );
    //    set => _properties.SetProperty( ExtraProperties,value );
    //}

    //private readonly ExtraProperty<ApplicationExtend> _property5 = new( nameof( TestProperty5 ) );
    ///// <summary>
    ///// 不可变对象扩展属性
    ///// </summary>
    //[NotMapped]
    //public ApplicationExtend TestProperty5 {
    //    get => _property5.GetProperty( ExtraProperties );
    //    set => _property5.SetProperty( ExtraProperties, value );
    //}

    /// <inheritdoc />
    protected override void AddChanges(Product other)
    {
        AddChange(t => t.Code, other.Code);
        AddChange(t => t.Name, other.Name);
        AddChange(t => t.Price, other.Price);
        AddChange(t => t.Description, other.Description);
        AddChange(t => t.Enabled, other.Enabled);
        AddChange(t => t.CreationTime, other.CreationTime);
        AddChange(t => t.CreatorId, other.CreatorId);
        AddChange(t => t.LastModificationTime, other.LastModificationTime);
        AddChange(t => t.LastModifierId, other.LastModifierId);
    }
}
