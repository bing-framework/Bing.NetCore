namespace Bing.Data.Sql.Builders.Conditions
{
    /// <summary>
    /// Is Null查询条件
    /// </summary>
    public class IsNullCondition : ICondition
    {
        /// <summary>
        /// 列名
        /// </summary>
        private readonly string _name;

        /// <summary>
        /// 初始化一个<see cref="IsNullCondition"/>类型的实例
        /// </summary>
        /// <param name="name">列名</param>
        public IsNullCondition(string name) => _name = name;

        /// <summary>
        /// 获取查询条件
        /// </summary>
        public string GetCondition() => string.IsNullOrWhiteSpace(_name) ? null : $"{_name} Is Null";
    }
}
