using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Admin.Service.Shared.Responses.Systems
{
    /// <summary>
    /// 按钮响应
    /// </summary>
    public class ButtonResponse
    {
        /// <summary>
        /// 标识
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 模块标识
        /// </summary>
        public string ModuleId { get; set; }

        /// <summary>
        /// 编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

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
