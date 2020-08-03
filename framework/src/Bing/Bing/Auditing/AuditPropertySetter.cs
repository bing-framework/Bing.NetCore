using Bing.DependencyInjection;
using Bing.Security.Claims;
using Bing.Sessions;
using Bing.Users;

namespace Bing.Auditing
{
    /// <summary>
    /// 审计属性设置器
    /// </summary>
    public class AuditPropertySetter : IAuditPropertySetter, ITransientDependency
    {
        /// <summary>
        /// 当前用户
        /// </summary>
        protected ICurrentUser CurrentUser { get; }

        /// <summary>
        /// 用户会话
        /// </summary>
        protected ISession Session { get; }

        /// <summary>
        /// 初始化一个<see cref="AuditPropertySetter"/>类型的实例
        /// </summary>
        /// <param name="currentUser">当前用户</param>
        /// <param name="session">用户会话</param>
        public AuditPropertySetter(ICurrentUser currentUser
            , ISession session)
        {
            CurrentUser = currentUser;
            Session = session;
        }

        /// <summary>
        /// 设置创建属性
        /// </summary>
        /// <param name="targetObject">目标对象</param>
        public void SetCreationProperties(object targetObject) => CreationAuditedInitializer.Init(targetObject, CurrentUser.UserId, GetUserName());

        /// <summary>
        /// 获取用户名称
        /// </summary>
        private string GetUserName()
        {
            var name = CurrentUser.FindClaimValue(BingClaimTypes.FullName);
            return string.IsNullOrEmpty(name) ? CurrentUser.UserName : name;
        }

        /// <summary>
        /// 设置修改属性
        /// </summary>
        /// <param name="targetObject">目标对象</param>
        public void SetModificationProperties(object targetObject) => ModificationAuditedInitializer.Init(targetObject, CurrentUser.UserId, GetUserName());

        /// <summary>
        /// 设置删除属性
        /// </summary>
        /// <param name="targetObject">目标对象</param>
        public void SetDeletionProperties(object targetObject) => DeletionAuditedInitializer.Init(targetObject,CurrentUser.UserId,GetUserName());
    }
}
