using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Admin.Service.Shared.Dtos.NgAlain
{
    /// <summary>
    /// 菜单信息
    /// </summary>
    public class MenuInfo
    {
        /// <summary>
        /// 初始化一个<see cref="MenuInfo"/>类型的实例
        /// </summary>
        public MenuInfo()
        {
            Children = new List<MenuInfo>();
            Buttons = new List<ButtonInfo>();
            Group = true;
        }

        /// <summary>
        /// 标识
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 文本
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 图标
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// 链接
        /// </summary>
        public string Link { get; set; }

        /// <summary>
        /// 外部链接
        /// </summary>
        public string ExternalLink { get; set; }

        /// <summary>
        /// 链接目标
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// 组
        /// </summary>
        public bool Group { get; set; }

        /// <summary>
        /// 不要在面包屑导航中显示
        /// </summary>
        public bool HideInBreadcrumb { get; set; }

        /// <summary>
        /// 子菜单
        /// </summary>
        public List<MenuInfo> Children { get; set; }

        /// <summary>
        /// 按钮列表
        /// </summary>
        public List<ButtonInfo> Buttons { get; set; }
    }
}
