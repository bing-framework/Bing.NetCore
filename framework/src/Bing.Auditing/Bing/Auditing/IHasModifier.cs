namespace Bing.Auditing;

/// <summary>
/// 定义修改人
/// </summary>
public interface IHasModifier : IHasModifier<string> { }

/// <summary>
/// 定义修改人
/// </summary>
public interface IHasModifier<TModifier>
{
    /// <summary>
    /// 最后修改人
    /// </summary>
    TModifier LastModifier { get; set; }
}