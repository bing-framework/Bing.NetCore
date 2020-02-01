using Bing.Tests.Samples;
using Xunit;

namespace Bing.Tests.Domains
{
    /// <summary>
    /// 测试值对象
    /// </summary>
    public class ValueObjectBaseTest
    {
        /// <summary>
        /// 值对象测试样例1
        /// </summary>
        private ValueObjectSample _sample;

        /// <summary>
        /// 值对象测试样例2
        /// </summary>
        private ValueObjectSample _sample2;

        /// <summary>
        /// 值对象测试样例3
        /// </summary>
        private ValueObjectSample _sample3;

        /// <summary>
        /// 值对象测试样例4
        /// </summary>
        private ValueObjectSample _sample4;

        /// <summary>
        /// 值对象测试样例5
        /// </summary>
        private ValueObjectSample _sample5;

        /// <summary>
        /// 值对象测试样例6
        /// </summary>
        private ValueObjectSample _sample6;

        /// <summary>
        /// 值对象测试样例7
        /// </summary>
        private ValueObjectSample _sample7;

        /// <summary>
        /// 聚合根测试样例
        /// </summary>
        private AggregateRootSample _aggregateRootSample;

        /// <summary>
        /// 初始化一个<see cref="ValueObjectBaseTest"/>类型的实例
        /// </summary>
        public ValueObjectBaseTest()
        {
            _sample = new ValueObjectSample("a", "b");
            _sample2 = new ValueObjectSample("a", "b");
            _sample3 = new ValueObjectSample("1", "");
            _aggregateRootSample = new AggregateRootSample();
            _sample4 = new ValueObjectSample("a", "b", _aggregateRootSample);
            _sample5 = new ValueObjectSample("a", "b", _aggregateRootSample);
            _sample6 = new ValueObjectSample("a", "b", _aggregateRootSample, new ValueObjectSample("a", "b"));
            _sample7 = new ValueObjectSample("a", "b", _aggregateRootSample, new ValueObjectSample("a", "b"));
        }

        /// <summary>
        /// 测试 - 对象相等性 - 判空
        /// </summary>
        [Fact]
        public void Test_Equals_Null()
        {
            Assert.False(_sample.Equals(null));
            Assert.False(_sample == null);
            Assert.False(null == _sample);
            Assert.True(_sample != null);

            _sample2 = null;
            Assert.False(_sample.Equals(_sample2));

            _sample = null;
            Assert.True(_sample == _sample2);
            Assert.True(_sample2 == _sample);
        }

        /// <summary>
        /// 测试 - 哈希
        /// </summary>
        [Fact]
        public void Test_GetHashCode()
        {
            Assert.True(_sample.GetHashCode() == _sample2.GetHashCode());
        }

        /// <summary>
        /// 测试 - 克隆
        /// </summary>
        [Fact]
        public void Test_Clone()
        {
            _sample3 = _sample.Clone();
            Assert.NotSame(_sample, _sample3);
            Assert.True(_sample == _sample3);
            Assert.Equal("a", _sample3.City);

            _sample = _sample6.Clone();
            Assert.True(_sample == _sample6);
            Assert.Equal("a", _sample.Child.City);
            Assert.Same(_sample.Child, _sample6.Child);
        }
    }
}
