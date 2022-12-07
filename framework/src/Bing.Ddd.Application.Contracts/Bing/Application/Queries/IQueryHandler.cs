using System.Threading;
using System.Threading.Tasks;

namespace Bing.Application.Queries;

/// <summary>
/// 定义查询处理程序
/// </summary>
public interface IQueryHandler
{
}

/// <summary>
/// 定义查询处理程序
/// </summary>
/// <typeparam name="TQuery">查询类型</typeparam>
public interface IQueryHandler<in TQuery> : IQueryHandler
    where TQuery : IQuery
{
    /// <summary>
    /// 处理
    /// </summary>
    /// <param name="query">查询对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// 定义查询处理程序
/// </summary>
/// <typeparam name="TQuery">查询类型</typeparam>
/// <typeparam name="TResult">结果类型</typeparam>
public interface IQueryHandler<in TQuery, TResult> : IQueryHandler
    where TQuery : IQuery<TResult>
{
    /// <summary>
    /// 处理
    /// </summary>
    /// <param name="query">查询对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}