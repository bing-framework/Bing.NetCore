using System.Threading.Tasks;

namespace Bing.Application.Services
{
    /// <summary>
    /// 删除应用服务
    /// </summary>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public interface IDeleteAppService<in TKey> : IApplicationService
    {
        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="id">实体标识</param>
        void Delete(TKey id);

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="id">实体标识</param>
        Task DeleteAsync(TKey id);
    }
}
