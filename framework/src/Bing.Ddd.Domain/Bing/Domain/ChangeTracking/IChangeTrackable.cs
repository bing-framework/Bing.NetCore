namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 定义变更跟踪接口，用于检测对象属性的变更情况。
/// </summary>
/// <remarks>
/// 该接口主要用于对比两个对象的状态差异，并返回变更详情。<br/>
/// 适用于持久化对象（Entity）、领域对象（DomainObjectBase）等需要变更追踪的场景。
/// </remarks>
public interface IChangeTrackable
{
    /// <summary>
    /// 获取对象的变更信息。
    /// </summary>
    /// <param name="otherObject">要比较的对象。</param>
    /// <returns>
    /// 返回 <see cref="ChangedValueDescriptorCollection"/>，其中包含当前对象与 <paramref name="otherObject"/> 之间的所有属性变更信息。
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// 当 <paramref name="otherObject"/> 的类型与当前对象类型不匹配时，会抛出该异常。
    /// </exception>
    ChangedValueDescriptorCollection GetChanges(object otherObject);
}
