using System.Text;
using Bing.Drawing;
using Bing.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.Drawing
{
    public class CaptchaBuilderTest : TestBase
    {
        private CaptchaBuilder _coder;

        public CaptchaBuilderTest(ITestOutputHelper output) : base(output)
        {
            _coder = new CaptchaBuilder();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void Test_GetCode_NumberAndLetter()
        {
            var result = _coder.GetCode(10);
            Output.WriteLine(result);
        }

        [Fact]
        public void Test_GetCode_Number()
        {
            var result = _coder.GetCode(10, CaptchaType.Number);
            Output.WriteLine(result);
        }

        [Fact]
        public void Test_GetCode_ChineseChar()
        {
            var result = _coder.GetCode(10, CaptchaType.ChineseChar);
            Output.WriteLine(result);
        }

        [Fact]
        public void Test_CreateImage()
        {
            var code = "";
            _coder.RandomPointPercent = 5;
            _coder.Height = 50;
            _coder.RandomColor = true;
            using (var image = _coder.CreateImage(4, out code, CaptchaType.ChineseChar))
            {
                image.Save("D:\\test.png");
            }
            Output.WriteLine(code);
        }
    }
}
