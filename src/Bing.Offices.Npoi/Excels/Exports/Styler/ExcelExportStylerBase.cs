using Bing.Offices.Excels.Models.Parameters;
using NPOI.SS.UserModel;

namespace Bing.Offices.Npoi.Excels.Exports.Styler
{
    /// <summary>
    /// Excel导出样式器基类
    /// </summary>
    public abstract class ExcelExportStylerBase:IExcelExportStyler
    {
        /// <summary>
        /// 字符串单行样式
        /// </summary>
        protected ICellStyle StringNoneStyle { get; set; }

        /// <summary>
        /// 字符串单行换行样式
        /// </summary>
        protected ICellStyle StringNoneWrapStyle { get; set; }

        /// <summary>
        /// 字符串间隔行样式
        /// </summary>
        protected ICellStyle StringSeptailStyle { get; set; }

        /// <summary>
        /// 字符串间隔行换行样式
        /// </summary>
        protected ICellStyle StringSeptailWrapStyle { get; set; }

        /// <summary>
        /// 工作簿
        /// </summary>
        protected IWorkbook Workbook { get; set; }

        /// <summary>
        /// 字符串格式化
        /// </summary>
        protected static short StringFormat = (short) BuiltinFormats.GetBuiltinFormat("TEXT");

        /// <summary>
        /// 创建样式
        /// </summary>
        /// <param name="workbook">工作簿</param>
        protected void CreateStyles(IWorkbook workbook)
        {
            this.StringNoneStyle = workbook.CreateCellStyle();
            this.StringNoneWrapStyle = workbook.CreateCellStyle();
            this.StringSeptailStyle = workbook.CreateCellStyle();
            this.StringSeptailWrapStyle = workbook.CreateCellStyle();
            this.Workbook = workbook;
        }

        public ICellStyle GetHeaderStyle(short color)
        {
            throw new System.NotImplementedException();
        }

        public ICellStyle GetTitleStyle(short color)
        {
            throw new System.NotImplementedException();
        }

        public ICellStyle GetStyles(bool parity, ExcelExportEntity entity)
        {
            if (parity && (entity == null || entity.IsWarp))
            {
                return StringNoneWrapStyle;
            }

            if (parity)
            {
                return StringNoneStyle;
            }

            if (parity == false && (entity == null || entity.IsWarp))
            {
                return StringSeptailWrapStyle;
            }

            return StringSeptailWrapStyle;
        }

        public ICellStyle GetStyles(ICell cell, int dataRow, ExcelExportEntity entity, object obj, object data)
        {
            return GetStyles(dataRow % 2 == 1, entity);
        }
    }
}
