using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Bing.Admin.Commons.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.Admin.Systems.Domain.Models;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 管理员 查询服务
    /// </summary>
    public class QueryAdministratorService : Bing.Application.Services.AppServiceBase, IQueryAdministratorService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 管理员仓储
        /// </summary>
        protected IAdministratorRepository AdministratorRepository { get; set; }

        /// <summary>
        /// 初始化一个<see cref="QueryAdministratorService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="administratorRepository">管理员仓储</param>
        public QueryAdministratorService( ISqlQuery sqlQuery
            , IAdministratorRepository administratorRepository)
        {
            SqlQuery = sqlQuery;
            AdministratorRepository = administratorRepository;
        }

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        public async Task<PagerList<AdministratorResponse>> PagerQueryAsync(AdministratorQuery parameter)
        {
            if (parameter == null)
                return new PagerList<AdministratorResponse>();
            Debug.WriteLine($"当前用户: {CurrentUser.UserId}, {CurrentUser.UserName}");
            var query = SqlQuery.Query()
                .Select("a.Id,a.Nickname,a.UserName,a.LastModificationTime,a.LastModifier,b.Gender,c.Enabled")
                .From("Users", "a")
                .Join("UserInfo", "b")
                .AppendOn("a.Id = b.Id")
                .Join("Administrator", "c")
                .AppendOn("a.Id = c.Id");
            if (!parameter.Nickname.IsEmpty())
                query = query.Where("a.Nickname", parameter.Nickname, Operator.Contains);
            if (parameter.Enabled.HasValue)
                query = query.Where("c.Enabled", parameter.Enabled.Value);
            if (!parameter.RoleId.IsEmpty())
            {
                var roleQuery = SqlQuery.Query<UserRole>()
                    .Select("UserId")
                    .From("UserRole")
                    .Where("RoleId", parameter.RoleId);
                query = query.Join(roleQuery, "d").AppendOn("a.Id = d.UserId");
            }
            var result = await query.OrderBy("a.LastModificationTime").ToPageAsync<AdministratorResponse>(parameter);
            var userIds = result.Data.Select(x => Conv.ToGuid(x.Id)).ToList();
            var userRoles = await GetUserRolesAsync(userIds);
            if (userRoles.Any())
                result.Data.ForEach(x => { x.Roles = userRoles.Where(y => y.UserId.ToString() == x.Id).ToList(); });
            return result;
        }

        /// <summary>
        /// 通过标识获取
        /// </summary>
        /// <param name="id">用户标识</param>
        public async Task<AdministratorResponse> GetById(Guid id)
        {
            var result = await SqlQuery.Query()
                .Select("a.Id,a.Nickname,a.UserName,a.LastModificationTime,a.LastModifier,b.Gender,c.Enabled")
                .From("Users", "a")
                .Join("UserInfo", "b")
                .AppendOn("a.Id = b.Id")
                .Join("Administrator", "c")
                .AppendOn("a.Id = c.Id")
                .Where("a.Id", id)
                .FirstOrDefaultAsync<AdministratorResponse>();
            result.Roles = await GetUserRolesAsync(new List<Guid>() { id });
            return result;
        }

        /// <summary>
        /// 获取用户角色列表
        /// </summary>
        /// <param name="userIds">用户编号列表</param>
        private async Task<List<UserRoleResponse>> GetUserRolesAsync(List<Guid> userIds)
        {
            var userRoles = await SqlQuery.Query()
                .Select("a.UserId,b.Id,b.Name,b.Code")
                .From("UserRole", "a")
                .Join("Role", "b")
                .AppendOn("a.RoleId = b.Id")
                .Where("a.UserId", userIds, Operator.In)
                .ToListAsync<UserRoleResponse>();
            return userRoles;
        }
    }
}
