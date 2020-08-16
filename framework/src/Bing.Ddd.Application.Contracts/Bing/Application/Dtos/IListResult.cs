using System.Collections.Generic;

namespace Bing.Application.Dtos
{
    /// <summary>
    /// 定义列表结果
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public interface IListResult<T>
    {
        /// <summary>
        /// 列表
        /// </summary>
        IReadOnlyList<T> Items { get; set; }
    }
}
