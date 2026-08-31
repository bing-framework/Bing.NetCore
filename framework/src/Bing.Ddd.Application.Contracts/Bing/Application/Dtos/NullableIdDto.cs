namespace Bing.Application.Dtos;

/// <summary>
/// 提供可选标识字段的数据传输对象。
/// </summary>
/// <typeparam name="TId">标识值类型，必须为值类型。</typeparam>
[Serializable]
public class NullableIdDto<TId> where TId : struct
{
    /// <summary>
    /// 获取或设置可空的对象标识。
    /// </summary>
    public TId? Id { get; set; }

    /// <summary>
    /// 初始化一个不包含标识值的 <see cref="NullableIdDto{TId}"/> 实例。
    /// </summary>
    public NullableIdDto() { }

    /// <summary>
    /// 使用指定标识初始化一个 <see cref="NullableIdDto{TId}"/> 实例。
    /// </summary>
    /// <param name="id">可空的对象标识。</param>
    public NullableIdDto(TId? id) => Id = id;
}

/// <summary>
/// 提供可空 GUID 标识的兼容数据传输对象。
/// </summary>
[Serializable]
public class NullableIdDto : NullableIdDto<Guid>
{
    /// <summary>
    /// 初始化一个不包含标识值的 <see cref="NullableIdDto"/> 实例。
    /// </summary>
    public NullableIdDto() { }

    /// <summary>
    /// 使用指定 GUID 标识初始化一个 <see cref="NullableIdDto"/> 实例。
    /// </summary>
    /// <param name="id">可空的 GUID 标识。</param>
    public NullableIdDto(Guid? id) : base(id) { }
}