using Bing.Mapping;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Api.Controllers
{
    /// <summary>
    /// 对象映射 控制器
    /// </summary>
    [Route("api/[controller]/[action]")]
    public class MapperController:ApiControllerBase
    {
        /// <summary>
        /// 对象映射
        /// </summary>
        /// <param name="mapper">对象</param>
        /// <returns></returns>
        [HttpPost]
        public MapperSample2 MapTo(MapperSample1 mapper)
        {
            return mapper.MapTo<MapperSample2>();
        }
    }
    
    public class MapperSample1
    {
        public string StringValue { get; set; }

        public decimal DecimalValue { get; set; }
    }

    public class MapperSample2
    {
        public string StringValue { get; set; }
        public decimal DecimalValue { get; set; }
    }
}
