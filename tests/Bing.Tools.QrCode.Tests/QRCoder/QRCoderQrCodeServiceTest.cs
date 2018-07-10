using System.Drawing;
using Bing.Tools.QrCode.QRCoder;
using Bing.Utils.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tools.QrCode.Tests.QRCoder
{
    /// <summary>
    /// 
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class QRCoderQrCodeServiceTest:TestBase
    {
        /// <summary>
        /// 二维码服务
        /// </summary>
        private IQrCodeService _service;

        public QRCoderQrCodeServiceTest(ITestOutputHelper output) : base(output)
        {
            _service = new QRCoderQrCodeService();
        }

        /// <summary>
        /// 测试输出基础二维码
        /// </summary>
        [Fact]
        public void Test_Output_BaseCode()
        {
            var bytes = _service.Size(10).Correction(ErrorCorrectionLevel.Q).CreateQrCode("Test Name");
            var result = bytes.ToBase64String();
            //Output.WriteLine(result);
        }

        /// <summary>
        /// 测试输出带有LOGO的二维码
        /// </summary>
        [Fact]
        public void Test_Output_LogoCode()
        {
            var bytes = _service.Size(10).Correction(ErrorCorrectionLevel.Q).Logo("D:\\Resources\\icon6.png")
                .CreateQrCode("我要测试一下带有LOGO的二维码");
            var result = bytes.ToBase64String();
            //Output.WriteLine(result);
        }

        /// <summary>
        /// 测试输出自定义颜色的二维码
        /// </summary>
        [Fact]
        public void Test_Output_CustomColor()
        {
            var bytes = _service.Size(10)
                .Foreground(Color.Black)
                .Background(Color.White)
                .Correction(ErrorCorrectionLevel.Q)
                .CreateQrCode("我要测试一下自定义颜色的二维码");
            var result = bytes.ToBase64String();
            Output.WriteLine(result);
        }
    }
}
