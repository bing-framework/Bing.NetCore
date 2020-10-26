using System.Threading;
using System.Threading.Tasks;

namespace Bing.Application.Queries
{
    /// <summary>
    /// 定义查询处理器
    /// </summary>
    public interface IQueryProcessor
    {
        /// <summary>
        /// 查询处理
        /// </summary>
        /// <param name="request">查询请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task ProcessAsync(IQuery request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 查询处理
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="request">查询请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task<TResult> ProcessAsync<TResult>(IQuery<TResult> request, CancellationToken cancellationToken = default);
    }
}
