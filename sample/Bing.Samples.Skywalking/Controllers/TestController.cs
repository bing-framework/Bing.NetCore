using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Skywalking.Controllers
{
    /// <summary>
    /// 测试控制器
    /// </summary>
    public class TestController:Controller
    {
        /// <summary>
        /// 获取结果
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetResult(string name)
        {
            return name;
        }
    }
}
