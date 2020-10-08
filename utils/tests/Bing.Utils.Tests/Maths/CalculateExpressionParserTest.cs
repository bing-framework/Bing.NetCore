using System.Linq;
using Bing.Utils.Maths;
using Xunit;

namespace Bing.Utils.Tests.Maths
{
    /// <summary>
    /// 计算表达式解析器
    /// </summary>
    public class CalculateExpressionParserTest
    {
        [Fact]
        public void Test_GetAllNode()
        {
            var parser = new CalculateExpressionParser("( ) + - * / 1");
            var nodes = parser.GetAllNodes().ToList();
            Assert.Equal(7, nodes.Count);
            Assert.Equal(CalculationSymbol.OpenBracket, nodes[0].Symbol);
            Assert.Equal(CalculationSymbol.CloseBracket, nodes[1].Symbol);
            Assert.Equal(CalculationSymbol.Add, nodes[2].Symbol);
            Assert.Equal(CalculationSymbol.Sub, nodes[3].Symbol);
            Assert.Equal(CalculationSymbol.Mul, nodes[4].Symbol);
            Assert.Equal(CalculationSymbol.Div, nodes[5].Symbol);
            Assert.Equal(CalculationSymbol.Number, nodes[6].Symbol);
            Assert.Equal(1, nodes[6].Value);
        }

        [Fact]
        public void Test_GetNode_100()
        {
            var parser = new CalculateExpressionParser("100");
            var nodes = parser.GetAllNodes().ToList();
            Assert.Equal(100.0, nodes[0].Value);
        }

        [Fact]
        public void Test_GetNode_123()
        {
            var parser = new CalculateExpressionParser("123");
            var nodes = parser.GetAllNodes().ToList();
            Assert.Equal(123.0, nodes[0].Value);
        }

        [Fact]
        public void Test_GetNode_1230()
        {
            var parser = new CalculateExpressionParser("123.0");
            var nodes = parser.GetAllNodes().ToList();
            Assert.Equal(123.0, nodes[0].Value);
        }

        [Fact]
        public void Test_GetNode_12301()
        {
            var parser = new CalculateExpressionParser("123.01");
            var nodes = parser.GetAllNodes().ToList();
            Assert.Equal(123.01, nodes[0].Value);
        }

        [Fact]
        public void Test_GetNode_123456()
        {
            var parser = new CalculateExpressionParser("123.456");
            var nodes = parser.GetAllNodes().ToList();
            Assert.Equal(123.456, nodes[0].Value);
        }
    }
}
