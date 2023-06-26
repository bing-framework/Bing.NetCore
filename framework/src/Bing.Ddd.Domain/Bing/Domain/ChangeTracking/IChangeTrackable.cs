using Bing.Domain.Entities;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 变更跟踪
/// </summary>
/// <typeparam name="TObject">领域对象类型</typeparam>
public interface IChangeTrackable<in TObject> where TObject : IDomainObject
{
    /// <summary>
    /// 获取变更值集合
    /// </summary>
    /// <param name="otherObj">其它对象</param>
    ChangedValueDescriptorCollection GetChanges(TObject otherObj);
}
