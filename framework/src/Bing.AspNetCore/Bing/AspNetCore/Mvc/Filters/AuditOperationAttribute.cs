using System;
using System.Collections.Generic;
using System.Security.Claims;
using Bing.Auditing;
using Bing.Security.Extensions;
using Bing.Uow;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Mvc.Filters
{
    /// <summary>
    /// 操作审计拦截器。负责发起并记录功能的操作日志
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class AuditOperationAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// 审计存储器
        /// </summary>
        private IAuditStore _auditStore;

        /// <summary>
        /// 工作单元管理
        /// </summary>
        private IUnitOfWorkManager _unitOfWorkManager;

        /// <summary>
        /// 审计操作信息
        /// </summary>
        private AuditOperationEntry _auditOperationEntry;

        /// <summary>
        /// 初始化一个<see cref="AuditOperationAttribute"/>类型的实例
        /// </summary>
        public AuditOperationAttribute()
        {
            Order = 2000;
        }
        /// <inheritdoc />
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var scope = context.HttpContext.RequestServices.CreateScope();
            _auditStore = scope.ServiceProvider.GetRequiredService<IAuditStore>();
            if (_auditStore == null)
                throw new ArgumentNullException(nameof(_auditStore), $"{nameof(IAuditStore)} 尚未注册");
            _unitOfWorkManager = scope.ServiceProvider.GetService<IUnitOfWorkManager>();
            var path = context.ActionDescriptor.GetActionPath();
            var ua = context.HttpContext.Request.Headers["User-Agent"].ToString();
            var ip = context.GetRemoteIpAddress();
            _auditOperationEntry = new AuditOperationEntry()
            {
                Path = path,
                Ip = ip,
                UserAgent = ua
            };
            if (context.HttpContext.User?.Identity != null && context.HttpContext.User.Identity.IsAuthenticated &&
                context.HttpContext.User.Identity is ClaimsIdentity identity)
            {
                //_auditOperationEntry.UserId = identity.GetUserId();
                //_auditOperationEntry.UserName = identity.GetUserName();
                //_auditOperationEntry.NickName = identity.GetNickName();
            }
            else
            {
                _auditOperationEntry.UserId = "Anonymous";
                _auditOperationEntry.UserName = "Anonymous";
                _auditOperationEntry.NickName = "匿名";
            }
        }

        /// <inheritdoc />
        public override void OnResultExecuted(ResultExecutedContext context)
        {
            var currentUnitOfWorkManager = context.HttpContext.RequestServices.GetService<IUnitOfWorkManager>();
            if (currentUnitOfWorkManager != null)
            {
                var entities = new List<AuditEntityEntry>();
                foreach (var unitOfWork in currentUnitOfWorkManager.GetUnitOfWorks())
                {
                    entities.AddRange(unitOfWork.GetAuditEntities());
                }
                _auditOperationEntry.AddEntities(entities);
            }
            _auditOperationEntry.End();
            _auditStore.Save(_auditOperationEntry);
            _unitOfWorkManager.Commit();
        }
    }
}
