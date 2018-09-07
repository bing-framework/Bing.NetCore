using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Bing.Extensions.Swashbuckle.Attributes;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Api.Controllers
{
    /// <summary>
    /// Swagger测试信息
    /// </summary>
    public class SwaggerTestController : ApiControllerBase
    {
        private readonly IAntiforgery _antiforgery;

        public SwaggerTestController(IAntiforgery antiforgery)
        {
            _antiforgery = antiforgery;
        }

        /// <summary>
        /// 获取所有信息
        /// </summary>
        /// <returns></returns>
        [HttpGet]        
        public IEnumerable<string> Get()
        {
            return new string[]{"Bing1","Bing2"};
        }

        /// <summary>
        /// 根据ID获取信息
        /// </summary>
        /// <param name="id">系统编号</param>
        /// <returns></returns>
        [ValidateAntiForgeryToken]
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "Bing001";
        }

        /// <summary>
        /// Post一个数据信息
        /// </summary>
        /// <param name="value">值</param>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        /// <summary>
        /// 根据ID put数据
        /// </summary>
        /// <param name="id">系统编号</param>
        /// <param name="value">值</param>
        [HttpPut]
        public void Put(int id, [FromBody] string value)
        {
        }

        /// <summary>
        /// 根据ID删除数据
        /// </summary>
        /// <param name="id">系统编号</param>
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }

        /// <summary>
        /// 复杂数据操作
        /// </summary>
        /// <param name="info">样例信息</param>
        /// <returns></returns>
        [HttpPost]
        public SampleNameValue Test(SampleNameValue info)
        {
            return info;
        }

        /// <summary>
        /// 复杂数据操作2
        /// </summary>
        /// <param name="info">样例信息</param>
        /// <returns></returns>
        //[IgnoreResultHandler]
        [HttpPost]
        public SampleNameValue Test2([FromBody] SampleNameValue info)
        {
            return info;
        }

        //[HttpGet]
        //public IActionResult GetToken()
        //{
        //    var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

        //    var token = tokens.RequestToken;
        //    var tokenName = tokens.HeaderName;

        //    HttpContext.Response.Cookies.Append(tokenName,token);

        //    return new ObjectResult(new
        //    {
        //        token= token,
        //        tokenName= tokenName
        //    });
        //}

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [SwaggerUpload]
        public async Task<IActionResult> TestUpload()
        {
            return Success();
        }

        /// <summary>
        /// 授权信息
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        //[Authorize("Admin")]
        //[Authorize("Customer")]
        public async Task<IActionResult> TestAuthorizeInfo()
        {
            return Success();
        }

        /// <summary>
        /// 请求头
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [SwaggerRequestHeader("Refrence","引用")]
        public async Task<IActionResult> TestRequestHeader()
        {
            return Success();
        }

        /// <summary>
        /// 响应头
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [SwaggerResponseHeader(200,"正常","tt","成功响应")]
        public async Task<IActionResult> TestResponseHeader()
        {
            return Success();
        }
    }

    /// <summary>
    /// 名称-值 样例
    /// </summary>
    public class SampleNameValue
    {
        /// <summary>
        /// 名称
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// 值
        /// </summary>
        public string Value { get; set; }
    }
}
