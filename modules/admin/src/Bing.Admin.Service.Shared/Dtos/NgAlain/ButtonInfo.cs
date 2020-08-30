using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Admin.Service.Shared.Dtos.NgAlain
{
    /// <summary>
    /// 按钮信息
    /// </summary>
    public class ButtonInfo
    {
        /// <summary>
        /// 标识
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 文本
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 图标
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// 排序
        /// </summary>
        public int? SortId { get; set; }
    }
}
