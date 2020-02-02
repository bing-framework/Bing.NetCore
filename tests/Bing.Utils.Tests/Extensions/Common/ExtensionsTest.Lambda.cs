using System;
using System.Linq.Expressions;
using Bing.Tests.Samples;
using Xunit.Abstractions;
using Bing.Utils.Extensions;
using Xunit;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Tests.Extensions
{
    /// <summary>
    /// 系统扩展测试 - Lambda表达式扩展
    /// </summary>
    public partial class ExtensionsTest
    {
        /// <summary>
        /// 参数表达式
        /// </summary>
        private readonly ParameterExpression _parameterExpression;

        /// <summary>
        /// 表达式1
        /// </summary>
        private Expression _expression1;

        /// <summary>
        /// 表达式2
        /// </summary>
        private Expression _expression2;

        /// <summary>
        /// 初始化一个<see cref="ExtensionsTest"/>类型的实例
        /// </summary>
        public ExtensionsTest(ITestOutputHelper output) : base(output)
        {
            _parameterExpression = Expression.Parameter(typeof(Sample), "t");
            _expression1 = _parameterExpression.Property("StringValue").Call("Contains", Expression.Constant("A"));
            //_expression1 = _parameterExpression.Property("StringValue").Call("Contains", new[] { typeof(string) }, "A");
            _expression2 = _parameterExpression.Property("NullableDateValue")
                .Property("Value")
                .Property("Year")
                .Greater(Expression.Constant(2000));
        }

        /// <summary>
        /// 测试 - And方法
        /// </summary>
        [Fact]
        public void Test_And()
        {
            var andExpression = _expression1.And(_expression2).ToLambda<Func<Sample, bool>>(_parameterExpression);
            Expression<Func<Sample, bool>> expected = t => t.StringValue.Contains("A") && t.NullableDateValue.Value.Year > 2000;
            Assert.Equal(expected.ToString(), andExpression.ToString());

            Expression<Func<Sample, bool>> left = t => t.StringValue == "A";
            Expression<Func<Sample, bool>> right = t => t.StringValue == "B";
            expected = t => t.StringValue == "A" && t.StringValue == "B";
            Assert.Equal(expected.ToString(), left.And(right).ToString());
        }

        /// <summary>
        /// 测试 - Or方法
        /// </summary>
        [Fact]
        public void Test_Or()
        {
            var andExpression = _expression1.Or(_expression2).ToLambda<Func<Sample, bool>>(_parameterExpression);
            Expression<Func<Sample, bool>> expected = t => t.StringValue.Contains("A") || t.NullableDateValue.Value.Year > 2000;
            Assert.Equal(expected.ToString(), andExpression.ToString());
        }

        /// <summary>
        /// 测试 - Or方法 - 左表达式为空
        /// </summary>
        [Fact]
        public void Test_Or_LeftIsNull()
        {
            _expression1 = null;
            var andExpression = _expression1.Or(_expression2).ToLambda<Func<Sample, bool>>(_parameterExpression);
            Expression<Func<Sample, bool>> expected = t => t.NullableDateValue.Value.Year > 2000;
            Assert.Equal(expected.ToString(), andExpression.ToString());
        }

        /// <summary>
        /// 测试 - Or方法 - 右表达式为空
        /// </summary>
        [Fact]
        public void Test_Or_RightIsNull()
        {
            _expression2 = null;
            var andExpression = _expression1.Or(_expression2).ToLambda<Func<Sample, bool>>(_parameterExpression);
            Expression<Func<Sample, bool>> expected = t => t.StringValue.Contains("A");
            Assert.Equal(expected.ToString(), andExpression.ToString());
        }

        /// <summary>
        /// 测试 - Or方法
        /// </summary>
        [Fact]
        public void Test_Or_2()
        {
            Expression<Func<Sample, bool>> left = t => t.StringValue == "A";
            Expression<Func<Sample, bool>> right = t => t.StringValue == "B";
            Expression<Func<Sample, bool>> expected = t => t.StringValue == "A" || t.StringValue == "B";
            Assert.Equal(expected.ToString(), left.Or(right).ToString());
        }

        /// <summary>
        /// 测试 - Or方法 - 左表达式为空
        /// </summary>
        [Fact]
        public void Test_Or_2_LeftIsNull()
        {
            Expression<Func<Sample, bool>> left = null;
            Expression<Func<Sample, bool>> right = t => t.StringValue == "B";
            Expression<Func<Sample, bool>> expected = t => t.StringValue == "B";
            Assert.Equal(expected.ToString(), left.Or(right).ToString());
        }

        /// <summary>
        /// 测试 - Or方法 - 右表达式为空
        /// </summary>
        [Fact]
        public void Test_Or_2_RightIsNull()
        {
            Expression<Func<Sample, bool>> left = t => t.StringValue == "A";
            Expression<Func<Sample, bool>> right = null;
            Expression<Func<Sample, bool>> expected = t => t.StringValue == "A";
            Assert.Equal(expected.ToString(), left.Or(right).ToString());
        }

        /// <summary>
        /// 获取lambda表达式成员值
        /// </summary>
        [Fact]
        public void Test_Value_LambdaExpression()
        {
            Expression<Func<Sample, bool>> expression = Test_ => Test_.StringValue == "A";
            Assert.Equal("A", expression.Value());
        }

        /// <summary>
        /// 测试 - 相等
        /// </summary>
        [Fact]
        public void Test_Equal()
        {
            _expression1 = _parameterExpression.Property("IntValue").Equal(1);
            Assert.Equal("t => (t.IntValue == 1)",
                _expression1.ToLambda<Func<Sample, bool>>(_parameterExpression).ToString());
        }

        /// <summary>
        /// 测试 - 不相等
        /// </summary>
        [Fact]
        public void Test_NotEqual()
        {
            _expression1 = _parameterExpression.Property("NullableIntValue").NotEqual(1);
            Assert.Equal("t => (t.NullableIntValue != 1)",
                _expression1.ToLambda<Func<Sample, bool>>(_parameterExpression).ToString());
        }

        /// <summary>
        /// 测试 - 大于
        /// </summary>
        [Fact]
        public void Test_Greater()
        {
            _expression1 = _parameterExpression.Property("NullableIntValue").Greater(1);
            Assert.Equal("t => (t.NullableIntValue > 1)",
                _expression1.ToLambda<Func<Sample, bool>>(_parameterExpression).ToString());
        }

        /// <summary>
        /// 测试 - 大于等于
        /// </summary>
        [Fact]
        public void Test_GreaterEqual_Nullable()
        {
            _expression1 = _parameterExpression.Property("NullableIntValue").GreaterEqual(1);
            Assert.Equal("t => (t.NullableIntValue >= 1)",
                _expression1.ToLambda<Func<Sample, bool>>(_parameterExpression).ToString());
        }

        /// <summary>
        /// 测试 - 小于
        /// </summary>
        [Fact]
        public void Test_Less()
        {
            _expression1 = _parameterExpression.Property("NullableIntValue").Less(1);
            Assert.Equal("t => (t.NullableIntValue < 1)",
                _expression1.ToLambda<Func<Sample, bool>>(_parameterExpression).ToString());
        }

        /// <summary>
        /// 测试 - 小于等于
        /// </summary>
        [Fact]
        public void Test_LessEqual()
        {
            _expression1 = _parameterExpression.Property("NullableIntValue").LessEqual(1);
            Assert.Equal("t => (t.NullableIntValue <= 1)",
                _expression1.ToLambda<Func<Sample, bool>>(_parameterExpression).ToString());
        }

        /// <summary>
        /// 测试 - 头匹配
        /// </summary>
        [Fact]
        public void Test_StartsWith()
        {
            _expression1 = _parameterExpression.Property("StringValue").StartsWith("a");
            Assert.Equal("t => t.StringValue.StartsWith(\"a\")",
                _expression1.ToLambda<Func<Sample, bool>>(_parameterExpression).ToString());
        }

        /// <summary>
        /// 测试 - 尾匹配
        /// </summary>
        [Fact]
        public void Test_EndsWith()
        {
            _expression1 = _parameterExpression.Property("StringValue").EndsWith("a");
            Assert.Equal("t => t.StringValue.EndsWith(\"a\")",
                _expression1.ToLambda<Func<Sample, bool>>(_parameterExpression).ToString());
        }

        /// <summary>
        /// 测试 - 模糊匹配
        /// </summary>
        [Fact]
        public void Test_Contains()
        {
            _expression1 = _parameterExpression.Property("StringValue").Contains("a");
            Assert.Equal("t => t.StringValue.Contains(\"a\")",
                _expression1.ToLambda<Func<Sample, bool>>(_parameterExpression).ToString());
        }
    }
}
