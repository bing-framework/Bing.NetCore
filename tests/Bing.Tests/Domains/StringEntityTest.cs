using Bing.Exceptions;
using Bing.Tests.Samples;
using Bing.Tests.XUnitHelpers;
using Xunit;

namespace Bing.Tests.Domains
{
    /// <summary>
    /// string标识实体测试
    /// </summary>
    public class StringEntityTest
    {
        /// <summary>
        /// 聚合根测试样例
        /// </summary>
        private StringAggregateRootSample _sample;

        /// <summary>
        /// 聚合根测试样例2
        /// </summary>
        private StringAggregateRootSample _sample2;

        /// <summary>
        /// 初始化一个<see cref="StringEntityTest"/>类型的实例
        /// </summary>
        public StringEntityTest()
        {
            _sample = new StringAggregateRootSample();
            _sample2 = new StringAggregateRootSample();
        }

        /// <summary>
        /// 测试 - 实体相等性 -当两个实体的标识相同，则实体相同
        /// </summary>
        [Fact]
        public void Test_Equals_IdEquals()
        {
            _sample = new StringAggregateRootSample("a");
            _sample2 = new StringAggregateRootSample("a");
        }

        /// <summary>
        /// 测试 - 实体相等性 - Id为空
        /// </summary>
        [Fact]
        public void Test_Equals_Id_Null()
        {
            _sample = new StringAggregateRootSample(null);
            _sample2 = new StringAggregateRootSample("a");
            Assert.False(_sample.Equals(_sample2));
            Assert.False(_sample == _sample2);
            Assert.False(_sample2 == _sample);

            _sample = new StringAggregateRootSample("a");
            _sample2 = new StringAggregateRootSample(null);
            Assert.False(_sample.Equals(_sample2));
            Assert.False(_sample == _sample2);
            Assert.False(_sample2 == _sample);
        }

        /// <summary>
        /// 测试 - 状态输出
        /// </summary>
        [Fact]
        public void Test_ToString()
        {
            _sample = new StringAggregateRootSample { Name = "a" };
            Assert.Equal($"Id:{_sample.Id},姓名:a", _sample.ToString());
        }

        /// <summary>
        /// 测试 - 验证 - Id为空，无法通过
        /// </summary>
        [Fact]
        public void Test_Validate_IdIsEmpty()
        {
            AssertHelper.Throws<Warning>(() =>
            {
                _sample = new StringAggregateRootSample(null);
                _sample.Validate();
            }, "Id");
            AssertHelper.Throws<Warning>(() =>
            {
                _sample = new StringAggregateRootSample("");
                _sample.Validate();
            }, "Id");
        }
    }
}
