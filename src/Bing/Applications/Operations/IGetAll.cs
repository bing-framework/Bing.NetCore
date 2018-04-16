using System.Collections.Generic;
using Bing.Applications.Dtos;

namespace Bing.Applications.Operations
{
    /// <summary>
    /// 获取全部数据
    /// </summary>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    public interface IGetAll<TDto> where TDto: IResponse, new()
    {
        /// <summary>
        /// 获取全部
        /// </summary>
        /// <returns></returns>
        List<TDto> GetAll();        
    }
}
