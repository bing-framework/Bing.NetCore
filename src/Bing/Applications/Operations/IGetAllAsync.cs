using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bing.Applications.Operations
{
    /// <summary>
    /// 获取全部数据
    /// </summary>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    public interface IGetAllAsync<TDto> where TDto : new()
    {
        /// <summary>
        /// 获取全部
        /// </summary>
        Task<List<TDto>> GetAllAsync();
    }
}
