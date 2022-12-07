namespace Bing.Application.Queries;

/// <summary>
/// 定义查询
/// </summary>
public interface IQuery
{
}

/// <summary>
/// 定义查询
/// </summary>
/// <typeparam name="TResult">结果类型</typeparam>
public interface IQuery<TResult> : IQuery
{
}