using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Admin.Service.Shared.Responses.Systems
{
    /// <summary>
    /// 菜单响应
    /// </summary>
    public class MenuResponse
    {
        /// <summary>
        /// 标识
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 父标识
        /// </summary>
        public string ParentId { get; set; }

        /// <summary>
        /// 层级
        /// </summary>
        public int? Level { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 地址
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 是否外部地址
        /// </summary>
        public bool External { get; set; }

        /// <summary>
        /// 图标
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// 启用
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        public int? SortId { get; set; }
    }
}
