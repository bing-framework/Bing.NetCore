using System.ComponentModel;
using Bing.Domain.ChangeTracking;
using Bing.Domain.Entities;

namespace DomainAotFixtures;

/// <summary>由另一个程序集独立生成比较钩子，实体自身提供身份供原生消费者匹配。</summary>
public partial class ExternalEntity : DomainObjectBase<ExternalEntity>, IEntity
{
    public int Id { get; set; }
    public object[] GetKeys() => new object[] { Id };
    public void Init() { }
    [Description("外部名称")] public string Name { get; set; } = "";
    [ChangeIgnore] public string Secret => throw new InvalidOperationException("忽略字段不应被读取");
}

/// <summary>外部领域基类，同时验证描述继承和真实 TObject 签名。</summary>
public partial class ExternalLegacyBase : DomainObjectBase<ExternalLegacyBase>
{
    [Description("继承说明")] public string Inherited { get; set; } = "";
    [Description("基类虚属性")] public virtual string Overridden { get; set; } = "";
    [Description("不应串用")] public string Hidden { get; set; } = "";
}
