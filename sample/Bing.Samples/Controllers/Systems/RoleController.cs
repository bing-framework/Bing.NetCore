using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Samples.Service.Abstractions.Systems;
using Bing.Samples.Service.Dtos.Systems;
using Bing.Samples.Service.Queries.Systems;
using Bing.Webs.Controllers.Trees;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Controllers.Systems
{
    /// <summary>
    /// 角色控制器
    /// </summary>
    public class RoleController : TreeControllerBase<RoleDto, RoleDto, RoleQuery>
    {
        /// <summary>
        /// 初始化角色控制器
        /// </summary>
        /// <param name="service">角色服务</param>
        public RoleController(IRoleService service) : base(service)
        {
            RoleService = service;
        }

        /// <summary>
        /// 角色服务
        /// </summary>
        public IRoleService RoleService { get; set; }

        /// <summary>
        /// 转换为树型结果
        /// </summary>
        /// <param name="data">数据列表</param>
        /// <param name="async">是否异步</param>
        protected override RoleDto ToResult(List<RoleDto> data, bool async = false)
        {
            return data.FirstOrDefault();
        }

        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="request">请求</param>
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateRoleRequest request)
        {
            var id = await RoleService.CreateAsync(request);
            return Success(id);
        }
    }

    /// <summary>
    /// 角色控制器
    /// </summary>
    public class RoleTableController : TreeTableControllerBase<RoleDto, RoleQuery>
    {
        /// <summary>
        /// 初始化角色控制器
        /// </summary>
        /// <param name="service">角色服务</param>
        public RoleTableController(IRoleService service) : base(service)
        {
        }

        /// <summary>
        /// 获取树型表格结果
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="async">是否异步</param>
        protected override ITreeTableResult<RoleDto> GetTreeTableResult(IEnumerable<RoleDto> data, bool async)
        {
            throw new NotImplementedException();
        }
    }
}
