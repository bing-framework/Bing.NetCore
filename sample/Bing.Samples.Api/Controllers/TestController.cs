using System.Threading.Tasks;
using Bing.AspNetCore.Uploads;
using Bing.AspNetCore.Uploads.Params;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Api.Controllers
{
    /// <summary>
    /// 测试控制器
    /// </summary>
    public class TestController : ApiControllerBase
    {
        /// <summary>
        /// 文件上传服务
        /// </summary>
        private IFileUploadService _fileUploadService;

        /// <summary>
        /// 初始化一个<see cref="TestController"/>类型的实例
        /// </summary>
        public TestController(IFileUploadService fileUploadService)
        {
            _fileUploadService = fileUploadService;
        }

        /// <summary>
        /// 上传Logo
        /// </summary>
        [HttpPost("uploadLogo")]
        public async Task<IActionResult> UploadLogoAsync(IFormFile formFile)
        {
            var param = new SingleFileUploadParam()
            {
                Request = Request,
                FormFile = formFile,
                RootPath = "D:\\",
                Module = "Test",
                Group = "Logo"
            };
            var result = await _fileUploadService.UploadAsync(param);

            return Success(result);
        }
    }
}
