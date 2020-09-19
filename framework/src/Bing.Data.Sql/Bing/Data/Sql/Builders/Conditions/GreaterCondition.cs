namespace Bing.Data.Sql.Builders.Conditions
{
    /// <summary>
    /// Sql大于查询条件
    /// </summary>
    public class GreaterCondition : ICondition
    {
        /// <summary>
        /// 左操作数
        /// </summary>
        private readonly string _left;

        /// <summary>
        /// 右操作数
        /// </summary>
        private readonly string _right;

        /// <summary>
        /// 初始化一个<see cref="GreaterCondition"/>类型的实例
        /// </summary>
        /// <param name="left">左操作数</param>
        /// <param name="right">右操作数</param>
        public GreaterCondition(string left, string right)
        {
            _left = left;
            _right = right;
        }

        /// <summary>
        /// 获取查询条件
        /// </summary>
        public string GetCondition() => $"{_left}>{_right}";
    }
}
