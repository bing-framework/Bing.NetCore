using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Bing.Webs.Filters;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Antiforgery.Internal;
//using Bing.Webs.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Api.Controllers
{
    /// <summary>
    /// Swagger测试信息
    /// </summary>
    //[IgnoreResultHandler]
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class SwaggerTestController : Controller
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

        [HttpGet]
        public IActionResult GetToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

            var token = tokens.RequestToken;
            var tokenName = tokens.HeaderName;

            HttpContext.Response.Cookies.Append(tokenName,token);

            return new ObjectResult(new
            {
                token= token,
                tokenName= tokenName
            });
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
