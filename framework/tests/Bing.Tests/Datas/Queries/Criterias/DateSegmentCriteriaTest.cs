using System;
using System.Text;
using Bing.Data.Queries;
using Bing.Data.Queries.Conditions;
using Bing.Tests.Samples;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tests.Datas.Queries.Criterias
{
    /// <summary>
    /// 测试日期范围过滤条件
    /// </summary>
    public class DateSegmentCriteriaTest
    {
        /// <summary>
        /// 输出日志
        /// </summary>
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// 最小日期
        /// </summary>
        private readonly DateTime? _min;

        /// <summary>
        /// 最大日期
        /// </summary>
        private readonly DateTime? _max;

        /// <summary>
        /// 初始化一个<see cref="DateSegmentCriteriaTest"/>类型的实例
        /// </summary>
        public DateSegmentCriteriaTest(ITestOutputHelper output)
        {
            _output = output;
            _min = DateTime.Parse("2000-1-1 10:10:10");
            _max = DateTime.Parse("2000-1-3 10:10:10");
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 不包含边界
        /// </summary>
        [Fact]
        public void Test_GetCondition_Neither()
        {
            var result = new StringBuilder();
            result.Append("t => ((t.DateValue >= Convert(Parse(\"2000/1/2 0:00:00\"), DateTime))");
            result.Append(" AndAlso (t.DateValue < Convert(Parse(\"2000/1/3 0:00:00\"), DateTime)))");

            var condition= new DateSegmentCondition<AggregateRootSample, DateTime>(t => t.DateValue, _min, _max, Boundary.Neither);
            _output.WriteLine(condition.GetCondition().ToString());
            Assert.Equal(result.ToString(), condition.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 不包含边界【可空】
        /// </summary>
        [Fact]
        public void Test_GetCondition_Neither_With_Nullable()
        {
            var result = new StringBuilder();
            result.Append("t => ((t.NullableDateValue >= Convert(Parse(\"2000/1/2 0:00:00\"), Nullable`1))");
            result.Append(" AndAlso (t.NullableDateValue < Convert(Parse(\"2000/1/3 0:00:00\"), Nullable`1)))");

            var condition = new DateSegmentCondition<AggregateRootSample, DateTime?>(t => t.NullableDateValue, _min, _max, Boundary.Neither);
            _output.WriteLine(condition.GetCondition().ToString());
            Assert.Equal(result.ToString(), condition.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 包含左边
        /// </summary>
        [Fact]
        public void Test_GetCondition_Left()
        {
            var result = new StringBuilder();
            result.Append("t => ((t.DateValue >= Convert(Parse(\"2000/1/1 0:00:00\"), DateTime))");
            result.Append(" AndAlso (t.DateValue < Convert(Parse(\"2000/1/3 0:00:00\"), DateTime)))");

            var condition = new DateSegmentCondition<AggregateRootSample, DateTime>(t => t.DateValue, _min, _max, Boundary.Left);
            _output.WriteLine(condition.GetCondition().ToString());
            Assert.Equal(result.ToString(), condition.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 包含左边【可空】
        /// </summary>
        [Fact]
        public void Test_GetCondition_Left_With_Nullable()
        {
            var result = new StringBuilder();
            result.Append("t => ((t.NullableDateValue >= Convert(Parse(\"2000/1/1 0:00:00\"), Nullable`1))");
            result.Append(" AndAlso (t.NullableDateValue < Convert(Parse(\"2000/1/3 0:00:00\"), Nullable`1)))");

            var condition = new DateSegmentCondition<AggregateRootSample, DateTime?>(t => t.NullableDateValue, _min, _max, Boundary.Left);
            _output.WriteLine(condition.GetCondition().ToString());
            Assert.Equal(result.ToString(), condition.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 包含右边
        /// </summary>
        [Fact]
        public void Test_GetCondition_Right()
        {
            var result = new StringBuilder();
            result.Append("t => ((t.DateValue >= Convert(Parse(\"2000/1/2 0:00:00\"), DateTime))");
            result.Append(" AndAlso (t.DateValue < Convert(Parse(\"2000/1/4 0:00:00\"), DateTime)))");

            var condition = new DateSegmentCondition<AggregateRootSample, DateTime>(t => t.DateValue, _min, _max, Boundary.Right);
            _output.WriteLine(condition.GetCondition().ToString());
            Assert.Equal(result.ToString(), condition.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 包含右边【可空】
        /// </summary>
        [Fact]
        public void Test_GetCondition_Right_With_Nullable()
        {
            var result = new StringBuilder();
            result.Append("t => ((t.NullableDateValue >= Convert(Parse(\"2000/1/2 0:00:00\"), Nullable`1))");
            result.Append(" AndAlso (t.NullableDateValue < Convert(Parse(\"2000/1/4 0:00:00\"), Nullable`1)))");

            var condition = new DateSegmentCondition<AggregateRootSample, DateTime?>(t => t.NullableDateValue, _min, _max, Boundary.Right);
            _output.WriteLine(condition.GetCondition().ToString());
            Assert.Equal(result.ToString(), condition.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 包含两边
        /// </summary>
        [Fact]
        public void Test_GetCondition_Both()
        {
            var result = new StringBuilder();
            result.Append("t => ((t.DateValue >= Convert(Parse(\"2000/1/1 0:00:00\"), DateTime))");
            result.Append(" AndAlso (t.DateValue < Convert(Parse(\"2000/1/4 0:00:00\"), DateTime)))");

            var condition = new DateSegmentCondition<AggregateRootSample, DateTime>(t => t.DateValue, _min, _max, Boundary.Both);
            _output.WriteLine(condition.GetCondition().ToString());
            Assert.Equal(result.ToString(), condition.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 获取查询条件 - 包含两边【可空】
        /// </summary>
        [Fact]
        public void Test_GetCondition_Both_With_Nullable()
        {
            var result = new StringBuilder();
            result.Append("t => ((t.NullableDateValue >= Convert(Parse(\"2000/1/1 0:00:00\"), Nullable`1))");
            result.Append(" AndAlso (t.NullableDateValue < Convert(Parse(\"2000/1/4 0:00:00\"), Nullable`1)))");

            var condition = new DateSegmentCondition<AggregateRootSample, DateTime?>(t => t.NullableDateValue, _min, _max, Boundary.Both);
            _output.WriteLine(condition.GetCondition().ToString());
            Assert.Equal(result.ToString(), condition.GetCondition().ToString());
        }
    }
}
