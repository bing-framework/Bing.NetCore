using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Api.Controllers
{
    /// <summary>
    /// 测试 IdentityServer4 相关功能
    /// </summary>
    public class TestIdentityServer4Controller:ApiControllerBase
    {
        /// <summary>
        /// 获取授权信息
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Roles = "admin")]
        public IActionResult Get()
        {
            return new JsonResult(from c in User.Claims select new {c.Type, c.Value});
        }

        [Authorize(Roles = "admin,customer")]
        [HttpGet]
        public string GetInfo(int id)
        {
            return id.ToString();
        }
    }
}
