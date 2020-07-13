using System.Threading.Tasks;
using Bing.Applications.Aspects;
using Bing.Applications.Dtos;
using Bing.Validations.Aspects;

namespace Bing.Applications.Operations
{
    /// <summary>
    /// 保存操作
    /// </summary>
    /// <typeparam name="TRequest">参数类型</typeparam>
    public interface ISaveAsync<in TRequest> where TRequest : IRequest, IKey, new()
    {
        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="request">请求参数</param>
        [UnitOfWork]
        Task SaveAsync([Valid] TRequest request);
    }
}
