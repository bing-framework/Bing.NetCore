namespace Bing.Uow;

/// <summary>
/// 表示带有操作级取消令牌的工作单元范围。
/// </summary>
public interface IUnitOfWorkExecutionScope : IUnitOfWorkScope
{
    /// <summary>
    /// 获取调用方取消与工作单元超时合成后的令牌。
    /// </summary>
    CancellationToken CancellationToken { get; }
}
