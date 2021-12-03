using System;
using System.Linq.Expressions;
using Bing.Data;

namespace Bing.Tests.Samples
{
    /// <summary>
    /// 查询条件对象样例
    /// </summary>
    public class ConditionSample : ICondition<AggregateRootSample>
    {
        /// <summary>
        /// 获取查询条件,返回结果："t => ((t.Name == \"A\") AndAlso (t.Tel == 1))"
        /// </summary>
        public Expression<Func<AggregateRootSample, bool>> GetCondition() => t => t.Name == "A" && t.Tel == 1;
    }
}
