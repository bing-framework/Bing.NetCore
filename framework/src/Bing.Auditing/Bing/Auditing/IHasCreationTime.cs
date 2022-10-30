using System;

namespace Bing.Auditing;

/// <summary>
/// 创建时间
/// </summary>
public interface IHasCreationTime
{
    /// <summary>
    /// 创建时间
    /// </summary>
    DateTime? CreationTime { get; set; }
}