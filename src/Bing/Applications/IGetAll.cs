using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Applications.Dtos;

namespace Bing.Applications
{
    /// <summary>
    /// 获取全部数据
    /// </summary>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    public interface IGetAll<TDto> where TDto:IDto,new()
    {
        /// <summary>
        /// 获取全部
        /// </summary>
        /// <returns></returns>
        List<TDto> GetAll();

        /// <summary>
        /// 获取全部
        /// </summary>
        /// <returns></returns>
        Task<List<TDto>> GetAllAsync();
    }
}
