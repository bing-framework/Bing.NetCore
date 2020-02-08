using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bing.Utils.Maths
{
    /// <summary>
    /// 计算器
    /// </summary>
    public class MathEvaluator
    {
        /// <summary>
        /// 计算表达式解析器
        /// </summary>
        private CalculateExpressionParser _parser;

        /// <summary>
        /// 计算节点
        /// </summary>
        private CalculateNode _node;

        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="expressionStr">表达式字符串</param>
        public double Eval(string expressionStr)
        {
            _parser = new CalculateExpressionParser(expressionStr);
            NextNode();
            double result = 0;
            Exp1(ref result);
            return result;
        }

        /// <summary>
        /// 表达式1
        /// </summary>
        /// <param name="result">结果</param>
        private void Exp1(ref double result)
        {
            Exp2(ref result);
            if (_node.Symbol == CalculationSymbol.Add || _node.Symbol == CalculationSymbol.Sub)
            {
                var op = _node;
                NextNode();
                double right = 0;
                Exp1(ref right);
                if (op.Symbol == CalculationSymbol.Add)
                    result += right;
                else if (op.Symbol == CalculationSymbol.Sub)
                    result -= right;
            }
        }

        /// <summary>
        /// 表达式2
        /// </summary>
        /// <param name="result">结果</param>
        private void Exp2(ref double result)
        {
            if (_node.Symbol == CalculationSymbol.OpenBracket)
            {
                NextNode();
                Exp1(ref result);
                if (_node.Symbol == CalculationSymbol.CloseBracket)
                    NextNode();
                else
                    throw new MathEvaluatorException();
            }
            Atom(ref result);
            if (_node.Symbol == CalculationSymbol.Mul || _node.Symbol == CalculationSymbol.Div)
            {
                var op = _node;
                NextNode();
                double right = 0;
                Exp2(ref right);
                if (op.Symbol == CalculationSymbol.Mul)
                    result *= right;
                else if (op.Symbol == CalculationSymbol.Div)
                    result /= right;
            }
        }

        /// <summary>
        /// 数值处理
        /// </summary>
        /// <param name="result">结果</param>
        private void Atom(ref double result)
        {
            if (_node.Symbol == CalculationSymbol.Number)
            {
                result = _node.Value;
                NextNode();
            }
        }

        /// <summary>
        /// 获取下一个计算节点
        /// </summary>
        private void NextNode() => _node = _parser.GetNextNode();
    }

    /// <summary>
    /// 计算表达式解析器
    /// </summary>
    public class CalculateExpressionParser
    {
        /// <summary>
        /// 表达式字符串
        /// </summary>
        private readonly string _expressionStr;

        /// <summary>
        /// 当前索引
        /// </summary>
        private int _currentIndex;

        /// <summary>
        /// 正值
        /// </summary>
        private readonly double[,] _m1 = {
            {1,10,100,1000,10000,100000,1000000},
            {2,20,200,2000,20000,200000,2000000},
            {3,30,300,3000,30000,300000,3000000},
            {4,40,400,4000,40000,400000,4000000},
            {5,50,500,5000,50000,500000,5000000},
            {6,60,600,6000,60000,600000,6000000},
            {7,70,700,7000,70000,700000,7000000},
            {8,80,800,8000,80000,800000,8000000},
            {9,90,900,9000,90000,900000,9000000},
        };

        /// <summary>
        /// 反转值
        /// </summary>
        private readonly double[,] _m2 = {
            {.1,.01,.001,.0001,.00001,.000001,.0000001},
            {.2,.02,.002,.0002,.00002,.000002,.0000002},
            {.3,.03,.003,.0003,.00003,.000003,.0000003},
            {.4,.04,.004,.0004,.00004,.000004,.0000004},
            {.5,.05,.005,.0005,.00005,.000005,.0000005},
            {.6,.06,.006,.0006,.00006,.000006,.0000006},
            {.7,.07,.007,.0007,.00007,.000007,.0000007},
            {.8,.08,.008,.0008,.00008,.000008,.0000008},
            {.9,.09,.009,.0009,.00009,.000009,.0000009},
        };

        /// <summary>
        /// 初始化一个<see cref="CalculateExpressionParser"/>类型的实例
        /// </summary>
        /// <param name="expressionStr">表达式字符串</param>
        public CalculateExpressionParser(string expressionStr)
        {
            if (string.IsNullOrWhiteSpace(expressionStr))
                throw new ArgumentNullException(nameof(expressionStr));
            _expressionStr = expressionStr;
            _currentIndex = 0;
        }

        /// <summary>
        /// 获取所有计算节点
        /// </summary>
        public IEnumerable<CalculateNode> GetAllNodes()
        {
            var node = GetNextNode();
            while (node.Symbol != CalculationSymbol.EOF)
            {
                yield return node;
                node = GetNextNode();
            }
        }

        /// <summary>
        /// 获取下一个计算节点
        /// </summary>
        public CalculateNode GetNextNode()
        {
            char ch;
            switch (1)
            {
                case 1:
                    if (_currentIndex == _expressionStr.Length)
                        return new CalculateNode(CalculationSymbol.EOF);
                    ch = _expressionStr[_currentIndex++];
                    if (ch == ' ')
                        goto case 1;
                    else if (ch == '(')
                        return new CalculateNode(CalculationSymbol.OpenBracket);
                    else if (ch == ')')
                        return new CalculateNode(CalculationSymbol.CloseBracket);
                    else if (ch == '+')
                        return new CalculateNode(CalculationSymbol.Add);
                    else if (ch == '-')
                        return new CalculateNode(CalculationSymbol.Sub);
                    else if (ch == '*')
                        return new CalculateNode(CalculationSymbol.Mul);
                    else if (ch == '/')
                        return new CalculateNode(CalculationSymbol.Div);
                    else if (char.IsDigit(ch))
                    {
                        _currentIndex--;
                        goto case 2;
                    }
                    else
                        throw new MathEvaluatorException();

                case 2:
                    var node = new CalculateNode(CalculationSymbol.Number);
                    var dotIndex = _currentIndex + 1;
                    while (dotIndex < _expressionStr.Length && char.IsDigit(_expressionStr[dotIndex])) dotIndex++;

                    while (_currentIndex < _expressionStr.Length && char.IsDigit(ch = _expressionStr[_currentIndex]))
                    {
                        var digit = ch - 48;
                        if (digit > 0)
                            node.Value += _m1[digit - 1, dotIndex - _currentIndex - 1];
                        _currentIndex++;
                    }

                    if (ch == ',' || ch == '.')
                    {
                        _currentIndex++;
                        while (_currentIndex < _expressionStr.Length && char.IsDigit(ch = _expressionStr[_currentIndex]))
                        {
                            var digit = ch - 48;
                            if (digit > 0)
                                node.Value += _m2[digit - 1, _currentIndex - dotIndex - 1];
                            _currentIndex++;
                        }
                    }
                    return node;
            }
        }
    }

    /// <summary>
    /// 计算节点
    /// </summary>
    public struct CalculateNode
    {
        /// <summary>
        /// 计算符号
        /// </summary>
        public CalculationSymbol Symbol;

        /// <summary>
        /// 值
        /// </summary>
        public double Value;

        /// <summary>
        /// 初始化一个<see cref="CalculateNode"/>类型的实例
        /// </summary>
        /// <param name="symbol">计算符号</param>
        public CalculateNode(CalculationSymbol symbol)
        {
            Symbol = symbol;
            Value = 0;
        }

        /// <summary>
        /// 初始化一个<see cref="CalculateNode"/>类型的实例
        /// </summary>
        /// <param name="symbol">计算符号</param>
        /// <param name="value">值</param>
        public CalculateNode(CalculationSymbol symbol, double value)
        {
            Symbol = symbol;
            Value = value;
        }

        /// <summary>
        /// 输出字符串
        /// </summary>
        public override string ToString()
        {
            if (Symbol == CalculationSymbol.Number)
                return Value.ToString(CultureInfo.InvariantCulture);
            return Symbol.ToString();
        }
    }

    /// <summary>
    /// 计算符号
    /// </summary>
    public enum CalculationSymbol
    {
        /// <summary>
        /// 未知
        /// </summary>
        Unknown,

        /// <summary>
        /// 括号"("
        /// </summary>
        OpenBracket,

        /// <summary>
        /// 括号")"
        /// </summary>
        CloseBracket,

        /// <summary>
        /// 加 +
        /// </summary>
        Add,

        /// <summary>
        /// 减 -
        /// </summary>
        Sub,

        /// <summary>
        /// 乘 *
        /// </summary>
        Mul,

        /// <summary>
        /// 除 /
        /// </summary>
        Div,

        /// <summary>
        /// 数值
        /// </summary>
        Number,

        /// <summary>
        /// 等于
        /// </summary>
        // ReSharper disable once InconsistentNaming
        EOF
    }

    public class MathEvaluatorException : Exception
    {
    }
}
