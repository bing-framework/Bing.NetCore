namespace Bing.Offices.Excels.Mappings
{
    /// <summary>
    /// 行数据校验器
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="rowIndex">行索引</param>
    /// <param name="rowData">当前行数据</param>
    /// <returns></returns>
    public delegate bool RowDataValidator<T>(int rowIndex, T rowData) where T : class;

    /// <summary>
    /// 行数据校验器
    /// </summary>
    /// <param name="rowIndex">行索引</param>
    /// <param name="rowData">当前行数据</param>
    /// <returns></returns>
    public delegate bool RowDataValidator(int rowIndex, object rowData);

    /// <summary>
    /// 单元格值校验器
    /// </summary>
    /// <param name="rowIndex">行索引</param>
    /// <param name="columnIndex">列索引</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    public delegate bool CellValueValidator(int rowIndex, int columnIndex, object value);

    /// <summary>
    /// 单元格值转换器
    /// </summary>
    /// <param name="rowIndex">行索引</param>
    /// <param name="columnIndex">列索引</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    public delegate object CellValueConverter(int rowIndex, int columnIndex, object value);
}
