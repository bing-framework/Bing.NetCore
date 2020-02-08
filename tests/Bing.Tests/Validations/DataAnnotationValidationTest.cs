using System.Linq;
using Bing.Tests.Samples;
using Bing.Validations;
using Xunit;

namespace Bing.Tests.Validations
{
    /// <summary>
    /// 测试验证操作
    /// </summary>
    public class DataAnnotationValidationTest
    {
        /// <summary>
        /// 聚合根测试样例
        /// </summary>
        private readonly AggregateRootSample _sample;

        /// <summary>
        /// 初始化一个<see cref="DataAnnotationValidationTest"/>类型的实例
        /// </summary>
        public DataAnnotationValidationTest() => _sample = AggregateRootSample.CreateSample();

        /// <summary>
        /// 测试 - 验证
        /// </summary>
        [Fact]
        public void Test_Validate()
        {
            _sample.Name = null;
            _sample.EnglishName = "  ";
            var result = DataAnnotationValidation.Validate(_sample);
            Assert.Equal(2, result.Count);
        }

        /// <summary>
        /// 测试 - 验证 - 名称为必填项
        /// </summary>
        [Fact]
        public void Test_Validate_Name_Required()
        {
            _sample.Name = null;
            var result = DataAnnotationValidation.Validate(_sample);
            Assert.Equal("姓名不能为空", result.First().ErrorMessage);
        }

        /// <summary>
        /// 测试 - 验证 - 从资源文件中加载错误消息
        /// </summary>
        [Fact]
        public void Test_Validate_Resource()
        {
            _sample.EnglishName = null;
            var result = DataAnnotationValidation.Validate(_sample);
            Assert.Equal("英文名不能为空", result.First().ErrorMessage);
        }
    }
}
