namespace Bing.Application.Commands;

/// <summary>
/// 定义命令处理器
/// </summary>
public interface ICommandProcessor
{
    /// <summary>
    /// 命令处理
    /// </summary>
    /// <param name="request">命令请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ProcessAsync(ICommand request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 命令处理
    /// </summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="request">命令请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示命令处理结果的异步操作。</returns>
    Task<TResult> ProcessAsync<TResult>(ICommand<TResult> request, CancellationToken cancellationToken = default);
}