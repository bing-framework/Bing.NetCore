using Bing.Ui.Builders;

namespace Bing.Ui.Zorro.Tables.Builders
{
    /// <summary>
    /// 表格列td生成器
    /// </summary>
    public class TableColumnBuilder : TagBuilder
    {
        /// <summary>
        /// 初始化一个<see cref="TableColumnBuilder"/>类型的实例
        /// </summary>
        public TableColumnBuilder() : base("td")
        {
        }

        /// <summary>
        /// 截断
        /// </summary>
        /// <param name="column">列名</param>
        /// <param name="length">保留长度</param>
        public void Truncate(string column, int length)
        {
            AddAttribute("nz-tooltip");
            AddAttribute("[nzTitle]", $"(row.{column}|isTruncate:{length})?row.{column}:''");
            AppendContent($"{{{{row.{column}|truncate:{length}}}}}");
        }
    }
}
