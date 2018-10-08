using System;
using System.Collections.Generic;
using Bing.Offices.Excels.Core.Styles;
using Bing.Offices.Excels.Mappings.Configuration;

namespace Bing.Offices.Excels.Mappings
{
    /// <summary>
    /// Excel设置
    /// </summary>
    public class ExcelSetting
    {
        /// <summary>
        /// 公司名称
        /// </summary>
        public string Company { get; set; } = "简玄冰";

        /// <summary>
        /// 作者
        /// </summary>
        public string Author { get; set; } = "简玄冰";

        /// <summary>
        /// 主题
        /// </summary>
        public string Subject { get; set; } = "Bing.Offices.Excels 扩展封装，提供导入导出数据";

        /// <summary>
        /// 是否使用创建信息
        /// </summary>
        public bool UseCreateInfo { get; set; } = false;

        /// <summary>
        /// 是否使用*.xlsx文件扩展名
        /// </summary>
        public bool UseXlsx { get; set; } = true;

        /// <summary>
        /// 导出数据数量
        /// </summary>
        public int ExportCount { get; set; } = 10000;

        /// <summary>
        /// 导出路径
        /// </summary>
        public string ExportPath { get; set; }

        /// <summary>
        /// 是否自动调整列的大小。如果数据量很大，建议禁止此功能以解决性能问题
        /// </summary>
        public bool AutoSizeColumnsEnabled { get; set; } = true;

        /// <summary>
        /// 标题单元格样式应用程序
        /// </summary>
        public Action<CellStyle> TitleCellStyleApplier { get; set; } = DefaultTitleCellStyleApplier;

        /// <summary>
        /// Fluent 配置
        /// </summary>
        public IDictionary<Type, IFluentConfiguration> FluentConfigs { get; } =
            new Dictionary<Type, IFluentConfiguration>();

        /// <summary>
        /// 默认 Excel 设置
        /// </summary>
        public static ExcelSetting Default { get; set; } = new ExcelSetting();

        /// <summary>
        /// 获取指定实体 Fluent 配置入口点
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="refreshCache">是否刷新缓存</param>
        /// <returns></returns>
        public FluentConfiguration<TEntity> For<TEntity>(bool refreshCache = false) where TEntity : class
        {
            var type = typeof(TEntity);
            if (!FluentConfigs.TryGetValue(type, out var mc) || refreshCache)
            {
                mc = new FluentConfiguration<TEntity>();
                FluentConfigs[type] = mc;
            }

            return mc as FluentConfiguration<TEntity>;
        }

        /// <summary>
        /// 默认标题单元格样式应用程序
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        internal static void DefaultTitleCellStyleApplier(CellStyle cellStyle)
        {
            cellStyle.Alignment = HorizontalAlignment.Center;
            cellStyle.VerticalAlignment = VerticalAlignment.Center;
            cellStyle.FontBoldWeight = 700;
        }
    }
}
