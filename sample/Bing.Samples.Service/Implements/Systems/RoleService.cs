using System;
using System.Threading.Tasks;
using Bing.Applications.Trees;
using Bing.Datas.Queries;
using Bing.Datas.Sql;
using Bing.Domains.Repositories;
using Bing.Events.Messages;
using Bing.Extensions;
using Bing.Mapping;
using Bing.Samples.Data;
using Bing.Samples.Domain.Events;
using Bing.Samples.Domain.Models;
using Bing.Samples.Domain.Repositories;
using Bing.Samples.Domain.Services.Abstractions;
using Bing.Samples.Service.Abstractions.Systems;
using Bing.Samples.Service.Dtos.Systems;
using Bing.Samples.Service.Queries.Systems;
using Bing.Utils.Extensions;

namespace Bing.Samples.Service.Implements.Systems
{
    /// <summary>
    /// 角色服务
    /// </summary>
    public class RoleService : TreeServiceBase<Role, RoleDto, RoleQuery>, IRoleService
    {
        /// <summary>
        /// 初始化角色服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="messageEventBus">消息事件总线</param>
        /// <param name="sqlExecutor">Sql执行对象</param>
        /// <param name="roleManager">角色服务</param>
        /// <param name="roleRepository">角色仓储</param>
        public RoleService(ISampleUnitOfWork unitOfWork
            , IMessageEventBus messageEventBus
            , ISqlExecutor sqlExecutor
            , IRoleManager roleManager
            , IRoleRepository roleRepository)
            : base(unitOfWork, roleRepository)
        {
            UnitOfWork = unitOfWork;
            MessageEventBus = messageEventBus;
            SqlExecutor = sqlExecutor;
            RoleManager = roleManager;
            RoleRepository = roleRepository;
        }

        /// <summary>
        /// 工作单元
        /// </summary>
        public ISampleUnitOfWork UnitOfWork { get; set; }

        /// <summary>
        /// 消息事件总线
        /// </summary>
        public IMessageEventBus MessageEventBus { get; set; }

        /// <summary>
        /// Sql执行对象
        /// </summary>
        public ISqlExecutor SqlExecutor { get; set; }

        /// <summary>
        /// 角色服务
        /// </summary>
        public IRoleManager RoleManager { get; set; }

        /// <summary>
        /// 角色仓储
        /// </summary>
        public IRoleRepository RoleRepository { get; set; }

        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="request">创建请求参数</param>
        public async Task<Guid> CreateAsync(CreateRoleRequest request)
        {
            var role = ToEntity(request);
            role.Type = "Role";
            await RoleManager.CreateAsync(role);
            await MessageEventBus.PublishAsync(new CreateRoleMessageEvent(
                new CreateRoleMessage()
                {
                    Code = $"event:{role.Code}",
                    Name = $"event:{role.Name}",
                    Type = $"event:{role.Type}"
                }));
            await SqlExecutor.ExecuteSqlAsync("insert into Systems.Test(Id, Name) Values(@Id, @Name)",
                new {Id = Guid.NewGuid(), Name = "隔壁老王"});
            await UnitOfWork.CommitAsync();
            return role.Id;
        }

        /// <summary>
        /// 转换为实体
        /// </summary>
        private Role ToEntity(CreateRoleRequest request)
        {
            return request.MapTo<Role>();
        }

        /// <summary>
        /// 修改角色
        /// </summary>
        /// <param name="request">修改角色请求参数</param>
        public async Task UpdateAsync(UpdateRoleRequest request)
        {
            var entity = await RoleRepository.FindAsync(request.Id.ToGuid());
            request.MapTo(entity);
            await RoleManager.UpdateAsync(entity);
            await UnitOfWork.CommitAsync();
        }

        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<Role> CreateQuery(RoleQuery param)
        {
            return new Query<Role>(param)
                .Or(t => t.Name.Contains(param.Keyword),
                    t => t.Code.Contains(param.Keyword),
                    t => t.PinYin.Contains(param.Keyword));
        }
    }
}
