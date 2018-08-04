using Bing.Offices.Excels.Enums;

namespace Bing.Offices.Excels.Models
{
    /// <summary>
    /// Excel 导出参数
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ExportParams<T> : ExcelBaseParams<T>
    {
        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 标题高度
        /// </summary>
        public short TitleHeight { get; set; } = 10;

        /// <summary>
        /// 二级标题
        /// </summary>
        public string SecondTitle { get; set; }

        /// <summary>
        /// 工作表名称
        /// </summary>
        public string SheetName { get; set; }

        /// <summary>
        /// 过滤属性
        /// </summary>
        public string[] Exclusions { get; set; }

        /// <summary>
        /// 是否添加序号
        /// </summary>
        public string IndexName { get; set; } = "序号";

        /// <summary>
        /// 冻结列
        /// </summary>
        public int FreezeCol { get; set; }

        /// <summary>
        /// 表头颜色
        /// </summary>
        public short Color { get; set; }

        /// <summary>
        /// 属性说明行的颜色
        /// </summary>
        public short HeaderColor { get; set; }

        /// <summary>
        /// Excel 导出版本
        /// </summary>
        public ExcelType Type { get; set; } = ExcelType.Xlsx;

        /// <summary>
        /// 导出样式
        /// </summary>
        public ExcelStyleType Style { get; set; } = ExcelStyleType.None;

        /// <summary>
        /// 表头高度
        /// </summary>
        public double HeaderHeight { get; set; } = 9;

        /// <summary>
        /// 是否创建表头
        /// </summary>
        public bool IsCreateHeadRows { get; set; } = true;

        /// <summary>
        /// 是否动态获取数据
        /// </summary>
        public bool IsDynamicData { get; set; } = false;

        /// <summary>
        /// 是否追加图形
        /// </summary>
        public bool IsAppendGraph { get; set; } = true;

        /// <summary>
        /// 单工作表最大值。03版本默认6W行,07默认100W
        /// </summary>
        public int MaxNum { get; set; } = 0;

        /// <summary>
        /// 导出时在excel中每个列的高度 单位为字符，一个汉字=2个字符
        /// 全局设置,优先使用
        /// </summary>
        public short Height { get; set; } = 0;

        public ExportParams() { }

        public ExportParams(string title, string sheetName)
        {
            Title = title;
            SheetName = sheetName;
        }

        public ExportParams(string title, string sheetName, ExcelType type)
        {
            Title = title;
            Type = type;
            SheetName = sheetName;
        }

        public ExportParams(string title, string secondTitle, string sheetName)
        {
            Title = title;
            SecondTitle = secondTitle;
            SheetName = sheetName;
        }
    }
}
