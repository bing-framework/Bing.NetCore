using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Applications;
using Bing.Applications.Dtos;
using Bing.Datas.Queries;
using Bing.Exceptions;
using Bing.Utils.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Webs.Controllers
{
    public abstract class CrudControllerBase<TDto, TQuery> : QueryControllerBase<TDto, TQuery>
        where TQuery : IQueryParameter
        where TDto : DtoBase, new()
    {
        /// <summary>
        /// Crud服务
        /// </summary>
        private readonly ICrudService<TDto, TQuery> _service;

        /// <summary>
        /// 初始化一个<see cref="CrudControllerBase{TDto,TQuery}"/>类型的实例
        /// </summary>
        /// <param name="service">Crud服务</param>
        protected CrudControllerBase(ICrudService<TDto, TQuery> service) : base(service)
        {
            _service = service;
        }

        /// <summary>
        /// 创建，调用范例：POST URL(/api/customers) BODY({name:'a',age:2})
        /// </summary>
        /// <param name="dto">数据传输对象</param>
        /// <returns></returns>
        public virtual async Task<IActionResult> CreateAsync([FromBody] TDto dto)
        {
            if (dto == null)
            {
                return Fail("请求参数不能为空");
            }
            CreateBefore(dto);
            await _service.CreateAsync(dto);
            var result = await _service.GetByIdAsync(dto.Id);
            return Success(result);
        }

        /// <summary>
        /// 创建前操作
        /// </summary>
        /// <param name="dto">数据传输对象</param>
        protected virtual void CreateBefore(TDto dto) { }

        /// <summary>
        /// 修改，调用范例：PUT URL(/api/customers/1 或/api/customers) BODY({id:1,name:'a'})
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="dto">数据传输对象</param>
        /// <returns></returns>
        [HttpPut("{id?}")]
        public virtual async Task<IActionResult> UpdateAsync(string id, [FromBody] TDto dto)
        {
            if (dto == null)
            {
                return Fail("请求参数不能为空");
            }

            if (id.IsEmpty() && dto.Id.IsEmpty())
            {
                throw new Warning("Id不能为空");
            }

            if (dto.Id.IsEmpty())
            {
                dto.Id = id;
            }
            UpdateBefore(dto);
            await _service.UpdateAsync(dto);
            var result = await _service.GetByIdAsync(dto.Id);
            return Success(result);
        }

        /// <summary>
        /// 修改前操作
        /// </summary>
        /// <param name="dto">数据传输对象</param>
        protected virtual void UpdateBefore(TDto dto) { }

        /// <summary>
        /// 保存，调用范例：POST URL(/api/customers/save) BODY({name:'a',age:2})
        /// </summary>
        /// <param name="dto">数据传输对象</param>
        /// <returns></returns>
        [HttpPost("save")]
        public virtual async Task<IActionResult> SaveAsync([FromBody] TDto dto)
        {
            if (dto == null)
            {
                return Fail("请求参数不能为空");
            }
            SaveBefore(dto);
            await _service.SaveAsync(dto);
            var result = await _service.GetByIdAsync(dto.Id);
            return Success(result);
        }

        /// <summary>
        /// 保存前操作
        /// </summary>
        /// <param name="dto">数据传输对象</param>
        protected virtual void SaveBefore(TDto dto) { }

        /// <summary>
        /// 删除，调用范例：DELETE URL(/api/customers/1)，
        /// 注意：该方法用于删除单个实体，批量删除请使用POST请求，否则可能失败
        /// </summary>
        /// <param name="id">标识</param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public virtual async Task<IActionResult> DeleteAsync(string id)
        {
            await _service.DeleteAsync(id);
            return Success();
        }

        /// <summary>
        /// 批量删除，调用范例：POST URL(/api/customers/delete) BODY("'1,2,3'")，
        /// 注意：body参数需要添加引号，"'1,2,3'"而不是"1,2,3"
        /// </summary>
        /// <param name="ids">标识列表，多个Id用逗号分隔，范例：1,2,3</param>
        /// <returns></returns>
        [HttpPost("delete")]
        public virtual async Task<IActionResult> BatchDeleteAsync([FromBody] string ids)
        {
            await _service.DeleteAsync(ids);
            return Success();
        }
    }
}
