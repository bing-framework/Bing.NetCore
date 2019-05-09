namespace Bing.Ui.Builders
{
    /// <summary>
    /// 表格标题列th生成器
    /// </summary>
    public class TableHeadColumnBuilder : TagBuilder
    {
        /// <summary>
        /// 初始化一个<see cref="TableHeadColumnBuilder"/>类型的实例
        /// </summary>
        public TableHeadColumnBuilder() : base("th")
        {
        }

        /// <summary>
        /// 设置标题
        /// </summary>
        /// <param name="title">标题</param>
        public void Title(string title)
        {
            AppendContent(title);
        }
    }
}
