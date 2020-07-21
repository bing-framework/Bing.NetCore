using Bing.Tests.Samples;
using Xunit;

namespace Bing.Tests.Domains
{
    /// <summary>
    /// int标识实体测试
    /// </summary>
    public class IntEntityTest
    {
        /// <summary>
        /// 聚合根测试样例
        /// </summary>
        private IntAggregateRootSample _sample;

        /// <summary>
        /// 聚合根测试样例2
        /// </summary>
        private IntAggregateRootSample _sample2;

        /// <summary>
        /// 初始化一个<see cref="IntEntityTest"/>类型的实例
        /// </summary>
        public IntEntityTest()
        {
            _sample = new IntAggregateRootSample();
            _sample2 = new IntAggregateRootSample();
        }

        /// <summary>
        /// 测试 - 实体相等性 - 当两个实体的标识相同，则实体相同
        /// </summary>
        [Fact]
        public void Test_Equals_IdEquals()
        {
            _sample = new IntAggregateRootSample(1);
            _sample2 = new IntAggregateRootSample(1);
            Assert.True(_sample.Equals(_sample2));
            Assert.True(_sample == _sample2);
            Assert.False(_sample != _sample2);
        }

        /// <summary>
        /// 测试 - 实体相等性 - Id为空
        /// </summary>
        [Fact]
        public void Test_Equals_Id_Empty()
        {
            _sample = new IntAggregateRootSample(0);
            _sample2 = new IntAggregateRootSample(1);
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
            _sample = new IntAggregateRootSample { Name = "a" };
            Assert.Equal($"Id:{_sample.Id},姓名:a", _sample.ToString());
        }
    }
}
