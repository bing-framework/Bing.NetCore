namespace Bing.Offices.Excels.Mappings.Abstractions
{
    /// <summary>
    /// 冻结映射
    /// </summary>
    public interface IFreezeMap
    {
        /// <summary>
        /// 列号。要拆分的列号，默认:0
        /// </summary>
        int ColumnSplit { get; }

        /// <summary>
        /// 行号。要拆分的行号，默认：1
        /// </summary>
        int RowSpit { get; }

        /// <summary>
        /// 列索引。最左侧的列索引，默认：0
        /// </summary>
        int LeftMostColumn { get; }

        /// <summary>
        /// 行索引。最顶行的行索引，默认：1
        /// </summary>
        int TopRow { get; }
    }
}
