using System;
using System.Collections.Generic;
using Bing.Offices.Excels.Enums;

namespace Bing.Offices.Excels.Models.Parameters
{
    /// <summary>
    /// Excel 导入导出基础对象
    /// </summary>
    public class ExcelBaseEntity
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 分组名
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// 类型
        /// </summary>
        public EntityType Type { get; set; } = EntityType.String;

        /// <summary>
        /// 数据库格式
        /// </summary>
        public string DbFormat { get; set; }

        /// <summary>
        /// 导出日期格式
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// 值替换
        /// </summary>
        public string[] Replace { get; set; }

        /// <summary>
        /// 字典名称
        /// </summary>
        public string Dict { get; set; }

        /// <summary>
        /// 是否超链接
        /// </summary>
        public bool IsHyperlink { get; set; }

        /// <summary>
        /// 固定列索引
        /// </summary>
        public int FixedIndex { get; set; }

        /// <summary>
        /// 方法
        /// </summary>
        public List<Action> Methods { get; set; }
    }
}
