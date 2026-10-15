using System.Collections.ObjectModel;

namespace Bing.Core.Modularity;

/// <summary>
/// 提供插件重新加载失败时的切换结果。
/// </summary>
public sealed class BingHotPluginReloadFailureInfo
{
    /// <summary>
    /// 异常数据中的诊断键。
    /// </summary>
    public const string ExceptionDataKey = "Bing.HotPluginReloadFailure";

    /// <summary>
    /// 初始化一个 <see cref="BingHotPluginReloadFailureInfo" /> 类型的实例。
    /// </summary>
    /// <param name="attemptId">本次重新加载的标识。</param>
    /// <param name="previousGenerationId">切换前的插件代标识。</param>
    /// <param name="currentGenerationId">失败时当前可用的插件代标识。</param>
    /// <param name="phase">失败阶段。</param>
    /// <param name="switched">新代是否已经发布。</param>
    internal BingHotPluginReloadFailureInfo(Guid attemptId, Guid? previousGenerationId,
        Guid? currentGenerationId, BingHotPluginReloadPhase phase, bool switched)
    {
        AttemptId = attemptId;
        PreviousGenerationId = previousGenerationId;
        CurrentGenerationId = currentGenerationId;
        Phase = phase;
        Switched = switched;
    }

    /// <summary>
    /// 获取本次重新加载的标识。
    /// </summary>
    public Guid AttemptId { get; }

    /// <summary>
    /// 获取切换前的插件代标识。
    /// </summary>
    public Guid? PreviousGenerationId { get; }

    /// <summary>
    /// 获取失败时当前可用的插件代标识。
    /// </summary>
    public Guid? CurrentGenerationId { get; }

    /// <summary>
    /// 获取失败阶段。
    /// </summary>
    public BingHotPluginReloadPhase Phase { get; }

    /// <summary>
    /// 获取新代是否已经发布。
    /// </summary>
    public bool Switched { get; }

    /// <summary>
    /// 从异常中读取重新加载失败信息。
    /// </summary>
    /// <param name="error">重新加载抛出的异常。</param>
    /// <returns>失败信息；异常不包含此信息时返回 null。</returns>
    public static BingHotPluginReloadFailureInfo FromException(Exception error)
    {
        if (error == null) throw new ArgumentNullException(nameof(error));
        return error.Data[ExceptionDataKey] as BingHotPluginReloadFailureInfo;
    }
}
