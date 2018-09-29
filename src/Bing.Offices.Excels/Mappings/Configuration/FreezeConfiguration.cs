using Bing.Offices.Excels.Mappings.Abstractions;

namespace Bing.Offices.Excels.Mappings.Configuration
{
    /// <summary>
    /// 冻结窗口配置。表示指定实体的Excel冻结窗口配置
    /// </summary>
    public class FreezeConfiguration:IFreezeMap
    {
        /// <summary>
        /// 列号。要拆分的列号，默认:0
        /// </summary>
        public int ColumnSplit { get; internal set; } = 0;

        /// <summary>
        /// 行号。要拆分的行号，默认：1
        /// </summary>
        public int RowSpit { get; internal set; } = 1;

        /// <summary>
        /// 列索引。最左侧的列索引，默认：0
        /// </summary>
        public int LeftMostColumn { get; internal set; } = 0;

        /// <summary>
        /// 行索引。最顶行的行索引，默认：1
        /// </summary>
        public int TopRow { get; internal set; } = 1;
    }
}
