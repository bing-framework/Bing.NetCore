using System;

namespace Bing.Application.Dtos;

/// <summary>
/// 可空标识数据传输对象
/// </summary>
/// <typeparam name="TId">标识类型</typeparam>
[Serializable]
public class NullableIdDto<TId> where TId : struct
{
    /// <summary>
    /// 标识
    /// </summary>
    public TId? Id { get; set; }

    /// <summary>
    /// 初始化一个<see cref="NullableIdDto{TId}"/>类型的实例
    /// </summary>
    public NullableIdDto() { }

    /// <summary>
    /// 初始化一个<see cref="NullableIdDto{TId}"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    public NullableIdDto(TId? id) => Id = id;
}

/// <summary>
/// 可空标识数据传输对象
/// </summary>
[Serializable]
public class NullableIdDto : NullableIdDto<Guid>
{
    /// <summary>
    /// 初始化一个<see cref="NullableIdDto"/>类型的实例
    /// </summary>
    public NullableIdDto() { }

    /// <summary>
    /// 初始化一个<see cref="NullableIdDto"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    public NullableIdDto(Guid? id) : base(id) { }
}