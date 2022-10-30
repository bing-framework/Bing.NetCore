using System.Threading;
using System.Threading.Tasks;

namespace Bing.Application.Commands;

/// <summary>
/// 定义命令处理程序
/// </summary>
public interface ICommandHandler
{
}

/// <summary>
/// 定义命令处理程序
/// </summary>
/// <typeparam name="TCommand">命令类型</typeparam>
public interface ICommandHandler<in TCommand> : ICommandHandler
    where TCommand : ICommand
{
    /// <summary>
    /// 处理
    /// </summary>
    /// <param name="command">命令对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// 定义命令处理程序
/// </summary>
/// <typeparam name="TCommand">命令类型</typeparam>
/// <typeparam name="TResult">结果类型</typeparam>
public interface ICommandHandler<in TCommand, TResult> : ICommandHandler 
    where TCommand : ICommand<TResult>
{
    /// <summary>
    /// 处理
    /// </summary>
    /// <param name="command">命令对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}