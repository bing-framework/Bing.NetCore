namespace Bing.Application.Commands;

/// <summary>
/// 定义命令
/// </summary>
public interface ICommand
{
}

/// <summary>
/// 定义命令
/// </summary>
/// <typeparam name="TResult">结果类型</typeparam>
public interface ICommand<TResult>
{
}