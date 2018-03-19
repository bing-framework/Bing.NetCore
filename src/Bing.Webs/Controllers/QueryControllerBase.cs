using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Applications;
using Bing.Applications.Dtos;
using Bing.Datas.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Webs.Controllers
{
    public abstract class QueryControllerBase<TDto, TQuery> : WebApiControllerBase
        where TQuery : IQueryParameter 
        where TDto : class, IDto, new()
    {
        /// <summary>
        /// Crud服务
        /// </summary>
        private readonly ICrudService<TDto, TQuery> _service;

        /// <summary>
        /// 初始化一个<see cref="QueryControllerBase{TDto,TQuery}"/>类型的实例
        /// </summary>
        /// <param name="service">Crud服务</param>
        protected QueryControllerBase(ICrudService<TDto, TQuery> service)
        {
            _service = service;
        }

        /// <summary>
        /// 获取单个实例，调用范例：GET URL(/api/customers/1)
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns></returns>
        [HttpGet("{id}")]
        public virtual async Task<IActionResult> GetAsync(object id)
        {
            var result = await _service.GetByIdAsync(id);
            return Success(result);
        }

        /// <summary>
        /// 查询，调用范例：GET URL(/api/customers/query?name=a)
        /// </summary>
        /// <param name="query">查询参数</param>
        /// <returns></returns>
        [HttpGet("Query")]
        public virtual async Task<IActionResult> QueryAsync(TQuery query)
        {
            var result = await _service.QueryAsync(query);
            return Success(result);
        }

        /// <summary>
        /// 分页查询，调用范例：GET URL(/api/customers?name=a)
        /// </summary>
        /// <param name="query">查询参数</param>
        /// <returns></returns>
        [HttpGet]
        public virtual async Task<IActionResult> PagerQueryAsync(TQuery query)
        {
            var result = await _service.PagerQueryAsync(query);
            return Success(result);
        }        
    }
}
