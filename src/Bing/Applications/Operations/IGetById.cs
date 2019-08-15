using System.Collections.Generic;

namespace Bing.Applications.Operations
{
    /// <summary>
    /// 获取指定标识实体
    /// </summary>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    public interface IGetById<TDto> where TDto : new()
    {
        /// <summary>
        /// 通过编号获取
        /// </summary>
        /// <param name="id">实体编号</param>
        TDto GetById(object id);

        /// <summary>
        /// 通过编号列表获取
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表，范例："1,2"</param>
        List<TDto> GetByIds(string ids);
    }
}
