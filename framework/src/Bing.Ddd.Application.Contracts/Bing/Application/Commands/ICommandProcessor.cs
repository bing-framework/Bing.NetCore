using System.Threading;
using System.Threading.Tasks;

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
    Task<TResult> ProcessAsync<TResult>(ICommand<TResult> request, CancellationToken cancellationToken = default);
}