using Bing.Datas.Sql.Queries.Builders.Abstractions;

namespace Bing.Datas.Sql.Queries.Builders.Conditions
{
    /// <summary>
    /// 空查询条件
    /// </summary>
    public class NullCondition:ICondition
    {
        /// <summary>
        /// 初始化一个<see cref="NullCondition"/>类型的实例
        /// </summary>
        private NullCondition() { }

        /// <summary>
        /// 空查询条件实例
        /// </summary>
        public static readonly NullCondition Instance = new NullCondition();

        /// <summary>
        /// 获取查询条件
        /// </summary>
        /// <returns></returns>
        public string GetCondition()
        {
            return null;
        }
    }
}
