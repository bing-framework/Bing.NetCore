using Bing.Data.Queries;
using Bing.Data.Queries.Conditions;
using Bing.Tests.Samples;
using Xunit;

namespace Bing.Tests.Datas.Queries.Criterias
{
    /// <summary>
    /// 测试整数范围过滤条件
    /// </summary>
    public class IntSegmentCriteriaTest
    {
        /// <summary>
        /// 测试 - 获取查询条件
        /// </summary>
        [Fact]
        public void Test_GetPredicate()
        {
            IntSegmentCondition<AggregateRootSample, int> condition = new IntSegmentCondition<AggregateRootSample, int>(t => t.Tel, 1, 10);
            Assert.Equal("t => ((t.Tel >= 1) AndAlso (t.Tel <= 10))", condition.GetCondition().ToString());

            IntSegmentCondition<AggregateRootSample, int?> criteria2 = new IntSegmentCondition<AggregateRootSample, int?>(t => t.Age, 1, 10);
            Assert.Equal("t => ((t.Age >= 1) AndAlso (t.Age <= 10))", criteria2.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 设置边界
        /// </summary>
        [Fact]
        public void Test_GetPredicate_Boundary()
        {
            IntSegmentCondition<AggregateRootSample, int> condition = new IntSegmentCondition<AggregateRootSample, int>(t => t.Tel, 1, 10, Boundary.Neither);
            Assert.Equal("t => ((t.Tel > 1) AndAlso (t.Tel < 10))", condition.GetCondition().ToString());

            condition = new IntSegmentCondition<AggregateRootSample, int>(t => t.Tel, 1, 10, Boundary.Left);
            Assert.Equal("t => ((t.Tel >= 1) AndAlso (t.Tel < 10))", condition.GetCondition().ToString());

            IntSegmentCondition<AggregateRootSample, int?> criteria2 = new IntSegmentCondition<AggregateRootSample, int?>(t => t.Age, 1, 10, Boundary.Right);
            Assert.Equal("t => ((t.Age > 1) AndAlso (t.Age <= 10))", criteria2.GetCondition().ToString());

            criteria2 = new IntSegmentCondition<AggregateRootSample, int?>(t => t.Age, 1, 10, Boundary.Both);
            Assert.Equal("t => ((t.Age >= 1) AndAlso (t.Age <= 10))", criteria2.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 最小值大于最大值，则交换大小值的位置
        /// </summary>
        [Fact]
        public void Test_GetPredicate_MinGreaterMax()
        {
            IntSegmentCondition<AggregateRootSample, int> condition = new IntSegmentCondition<AggregateRootSample, int>(t => t.Tel, 10, 1);
            Assert.Equal("t => ((t.Tel >= 1) AndAlso (t.Tel <= 10))", condition.GetCondition().ToString());

            IntSegmentCondition<AggregateRootSample, int?> criteria2 = new IntSegmentCondition<AggregateRootSample, int?>(t => t.Age, 10, 1);
            Assert.Equal("t => ((t.Age >= 1) AndAlso (t.Age <= 10))", criteria2.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 最小值为空，忽略最小值条件
        /// </summary>
        [Fact]
        public void Test_GetPredicate_MinIsNull()
        {
            IntSegmentCondition<AggregateRootSample, int> condition = new IntSegmentCondition<AggregateRootSample, int>(t => t.Tel, null, 10);
            Assert.Equal("t => (t.Tel <= 10)", condition.GetCondition().ToString());

            IntSegmentCondition<AggregateRootSample, int?> criteria2 = new IntSegmentCondition<AggregateRootSample, int?>(t => t.Age, null, 10);
            Assert.Equal("t => (t.Age <= 10)", criteria2.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 最大值为空，忽略最大值条件
        /// </summary>
        [Fact]
        public void Test_GetPredicate_MaxIsNull()
        {
            IntSegmentCondition<AggregateRootSample, int> condition = new IntSegmentCondition<AggregateRootSample, int>(t => t.Tel, 1, null);
            Assert.Equal("t => (t.Tel >= 1)", condition.GetCondition().ToString());

            IntSegmentCondition<AggregateRootSample, int?> criteria2 = new IntSegmentCondition<AggregateRootSample, int?>(t => t.Age, 1, null);
            Assert.Equal("t => (t.Age >= 1)", criteria2.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 最大值和最小值均为null,忽略所有条件
        /// </summary>
        [Fact]
        public void Test_BothNull()
        {
            IntSegmentCondition<AggregateRootSample, int> condition = new IntSegmentCondition<AggregateRootSample, int>(t => t.Tel, null, null);
            Assert.Null(condition.GetCondition());

            IntSegmentCondition<AggregateRootSample, int?> criteria2 = new IntSegmentCondition<AggregateRootSample, int?>(t => t.Age, null, null);
            Assert.Null(criteria2.GetCondition());
        }
    }
}
