using System.Threading.Tasks;
using Bing.Applications.Aspects;
using Bing.Applications.Dtos;
using Bing.Validations.Aspects;

namespace Bing.Applications.Operations
{
    /// <summary>
    /// 修改操作
    /// </summary>
    /// <typeparam name="TUpdateRequest">修改参数类型</typeparam>
    public interface IUpdateAsync<in TUpdateRequest> where TUpdateRequest : IRequest, new()
    {
        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [UnitOfWork]
        Task UpdateAsync([Valid] TUpdateRequest request);
    }
}
