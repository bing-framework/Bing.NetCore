using Bing.Utils.Maths;
using Xunit;

namespace Bing.Utils.Tests.Maths
{
    /// <summary>
    /// 计算器测试
    /// </summary>
    public class MathEvaluatorTest
    {
        /// <summary>
        /// 计算器
        /// </summary>
        private MathEvaluator _evaluator;

        /// <summary>
        /// 初始化一个<see cref="MathEvaluatorTest"/>类型的实例
        /// </summary>
        public MathEvaluatorTest()
        {
            _evaluator = new MathEvaluator();
        }

        [Fact]
        public void Test_Eval_1()
        {
            var result = _evaluator.Eval("1+2");
            Assert.Equal(3.0, result);
        }

        [Fact]
        public void Test_Eval_2()
        {
            var result = _evaluator.Eval("1+2+3");
            Assert.Equal(6.0, result);
        }

        [Fact]
        public void Test_Eval_3()
        {
            var result = _evaluator.Eval("1+2+3-4");
            Assert.Equal(2.0, result);
        }

        [Fact]
        public void Test_Eval_4()
        {
            var result = _evaluator.Eval("2*3");
            Assert.Equal(6.0, result);
        }

        [Fact]
        public void Test_Eval_5()
        {
            var result = _evaluator.Eval("2*3/2");
            Assert.Equal(3.0, result);
        }

        [Fact]
        public void Test_Eval_6()
        {
            var result = _evaluator.Eval("1+2*3/6");
            Assert.Equal(2.0, result);
        }

        [Fact]
        public void Test_Eval_7()
        {
            var result = _evaluator.Eval("2*3 + 1");
            Assert.Equal(7.0, result);
        }

        [Fact]
        public void Test_Eval_8()
        {
            var result = _evaluator.Eval("(1+2)*(2+3)");
            Assert.Equal(15.0, result);
        }

        [Fact]
        public void Test_Eval_9()
        {
            var result = _evaluator.Eval("(1+2)*(2+3)*(3+4)");
            Assert.Equal(105.0, result);
        }

        [Fact]
        public void Test_Eval_10()
        {
            var result = _evaluator.Eval("(1+2)*3");
            Assert.Equal(9.0, result);
        }

        [Fact]
        public void Test_Eval_11()
        {
            var result = _evaluator.Eval("0.5*(1+2)*4");
            Assert.Equal(6.0, result);
        }

        [Fact]
        public void Test_Eval_12()
        {
            var result = _evaluator.Eval("4/2");
            Assert.Equal(2.0, result);
        }
    }
}
