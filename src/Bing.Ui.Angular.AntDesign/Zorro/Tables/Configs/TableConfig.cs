using System;
using System.Collections.Generic;
using System.Text;
using Bing.Ui.Configs;
using Bing.Ui.Extensions;
using Bing.Ui.TagHelpers;

namespace Bing.Ui.Zorro.Tables.Configs
{
    /// <summary>
    /// 表格配置
    /// </summary>
    public class TableConfig : Config
    {
        /// <summary>
        /// 序号宽度
        /// </summary>
        public static string LineNumberWidth { get; set; } = "60";

        /// <summary>
        /// 复选框宽度
        /// </summary>
        public static string CheckboxWidth { get; set; } = "30";

        /// <summary>
        /// 表格共享键
        /// </summary>
        public const string TableShareKey = "TableShare";

        /// <summary>
        /// 表格包装器标识
        /// </summary>
        public string WrapperId => Context.GetValueFromItems<TableShareConfig>(TableShareKey).TableWrapperId;

        /// <summary>
        /// 表格标识
        /// </summary>
        public string Id => Context.GetValueFromItems<TableShareConfig>(TableShareKey).TableId;

        /// <summary>
        /// 标题集合
        /// </summary>
        public List<ColumnInfo> Columns => Context.GetValueFromItems<TableShareConfig>(TableShareKey).Columns;

        /// <summary>
        /// 是否自动创建行
        /// </summary>
        public bool AutoCreateRow => Context.GetValueFromItems<TableShareConfig>(TableShareKey).AutoCreateRow;

        /// <summary>
        /// 是否自动创建表头
        /// </summary>
        public bool AutoCreateHead => Context.GetValueFromItems<TableShareConfig>(TableShareKey).AutoCreateHead;

        /// <summary>
        /// 是否排序
        /// </summary>
        public bool IsSort => Context.GetValueFromItems<TableShareConfig>(TableShareKey).IsSort;

        /// <summary>
        /// 初始化一个<see cref="TableConfig"/>类型的实例
        /// </summary>
        /// <param name="context">上下文</param>
        public TableConfig(Context context) : base(context)
        {
        }
    }
}
