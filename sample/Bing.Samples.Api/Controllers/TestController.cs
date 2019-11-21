using System.Threading.Tasks;
using Bing.AspNetCore.Mvc;
using Bing.AspNetCore.Uploads;
using Bing.AspNetCore.Uploads.Params;
using Bing.Webs.Controllers;
using Bing.Webs.Filters;
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
        /// Api接口服务
        /// </summary>
        private IApiInterfaceService _apiInterfaceService;

        /// <summary>
        /// 初始化一个<see cref="TestController"/>类型的实例
        /// </summary>
        public TestController(IFileUploadService fileUploadService, IApiInterfaceService apiInterfaceService)
        {
            _fileUploadService = fileUploadService;
            _apiInterfaceService = apiInterfaceService;
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

        /// <summary>
        /// 获取所有控制器
        /// </summary>
        [AntiDuplicateRequest(Interval = 1)]
        [HttpGet("getAllController")]
        public virtual Task<IActionResult> GetAllControllerAsync()
        {
            return Task.FromResult(Success(_apiInterfaceService.GetAllController()));
        }

        /// <summary>
        /// 获取所有操作
        /// </summary>
        [HttpGet("getAllAction")]
        public Task<IActionResult> GetAllActionAsync()
        {
            return Task.FromResult(Success(_apiInterfaceService.GetAllAction()));
        }
    }
}
