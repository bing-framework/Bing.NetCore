using System;
using System.Collections.Generic;

namespace Bing.Application.Dtos
{
    /// <summary>
    /// 列表结果 - 数据传输对象
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    [Serializable]
    public class ListResultDto<T> : IListResult<T>
    {
        /// <summary>
        /// 列表
        /// </summary>
        private IReadOnlyList<T> _items;

        /// <summary>
        /// 列表
        /// </summary>
        public IReadOnlyList<T> Items
        {
            get =>_items ??= new List<T>(); 
            set => _items = value;
        }

        /// <summary>
        /// 初始化一个<see cref="ListResultDto{T}"/>类型的实例
        /// </summary>
        public ListResultDto() { }

        /// <summary>
        /// 初始化一个<see cref="ListResultDto{T}"/>类型的实例
        /// </summary>
        /// <param name="items">列表</param>
        public ListResultDto(IReadOnlyList<T> items) => Items = items;
    }
}
