using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bing.Validation;
using Xunit;

namespace Bing.Tests.Validations
{
    /// <summary>
    /// 测试验证结果集合
    /// </summary>
    public class ValidationResultCollectionTest
    {
        /// <summary>
        /// 验证结果集合
        /// </summary>
        private readonly ValidationResultCollection _results;

        /// <summary>
        /// 初始化一个<see cref="ValidationResultCollectionTest"/>类型的实例
        /// </summary>
        public ValidationResultCollectionTest() => _results = new ValidationResultCollection();

        /// <summary>
        /// 测试 - 在默认情况下，结果集合个数为0
        /// </summary>
        [Fact]
        public void Test_Default()
        {
            Assert.Equal(0, _results.Count);
            Assert.True(_results.IsValid);
        }

        /// <summary>
        /// 测试 - 添加null值，直接忽略
        /// </summary>
        [Fact]
        public void Test_Add_Null()
        {
            _results.Add(null);
            Assert.Equal(0, _results.Count);
        }

        /// <summary>
        /// 测试 - 添加验证成功结果，忽略
        /// </summary>
        [Fact]
        public void Test_Add_Success()
        {
            _results.Add(ValidationResult.Success);
            Assert.Equal(0, _results.Count);
        }

        /// <summary>
        /// 测试添加1个验证结果
        /// </summary>
        [Fact]
        public void Test_Add_1Result()
        {
            _results.Add(new ValidationResult("a"));
            Assert.Equal(1, _results.Count);
            Assert.Equal("a", _results.First().ErrorMessage);
            Assert.False(_results.IsValid);
        }

        /// <summary>
        /// 测试 - 添加null验证结果集合，直接忽略
        /// </summary>
        [Fact]
        public void Test_AddRange_Null()
        {
            _results.AddRange(null);
            Assert.Equal(0, _results.Count);
        }

        /// <summary>
        /// 测试 - 添加2个验证结果
        /// </summary>
        [Fact]
        public void Test_AddRange_2Result()
        {
            _results.AddRange(new[] { new ValidationResult("a"), new ValidationResult("b") });
            Assert.Equal(2, _results.Count);
            Assert.Equal("b", _results.Last().ErrorMessage);
        }
    }
}
