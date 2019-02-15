namespace Bing.Datas.Sql.Builders.Core
{
    /// <summary>
    /// 联合操作项
    /// </summary>
    public class UnionItem
    {
        /// <summary>
        /// 操作
        /// </summary>
        public string Operation { get; }

        /// <summary>
        /// Sql生成器
        /// </summary>
        public ISqlBuilder Builder { get; }

        /// <summary>
        /// 初始化一个<see cref="UnionItem"/>类型的实例
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="builder"></param>
        public UnionItem(string operation, ISqlBuilder builder)
        {
            Operation = operation;
            Builder = builder;
        }
    }
}
