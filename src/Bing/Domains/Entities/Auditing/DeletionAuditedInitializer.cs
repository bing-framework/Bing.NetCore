using System;
using Bing.Configurations;
using Bing.Sessions;
using Bing.Utils.Extensions;

namespace Bing.Domains.Entities.Auditing
{
    /// <summary>
    /// 删除操作审计初始化器
    /// </summary>
    public class DeletionAuditedInitializer
    {
        /// <summary>
        /// 实体
        /// </summary>
        private readonly object _entity;

        /// <summary>
        /// 用户会话
        /// </summary>
        private readonly ISession _session;

        /// <summary>
        /// 初始化一个<see cref="DeletionAuditedInitializer"/>类型的实例
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="session">用户会话</param>
        private DeletionAuditedInitializer(object entity, ISession session)
        {
            _entity = entity;
            _session = session;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="session">用户会话</param>
        public static void Init(object entity, ISession session)
        {
            new DeletionAuditedInitializer(entity, session).Init();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init()
        {
            if (_entity is IDeletionAudited<Guid>)
            {
                InitGuid();
                return;
            }
            if (_entity is IDeletionAudited<Guid?>)
            {
                InitNullableGuid();
                return;
            }
            if (_entity is IDeletionAudited<int>)
            {
                InitInt();
                return;
            }
            if (_entity is IDeletionAudited<int?>)
            {
                InitNullableInt();
                return;
            }
            if (_entity is IDeletionAudited<string>)
            {
                InitString();
                return;
            }
            if (_entity is IDeletionAudited<long>)
            {
                InitLong();
                return;
            }
            if (_entity is IDeletionAudited<long?>)
            {
                InitNullableLong();
                return;
            }
        }

        /// <summary>
        /// 初始化Guid
        /// </summary>
        private void InitGuid()
        {
            var result = (IDeletionAudited<Guid>)_entity;
            result.DeletionTime = DateTime.Now;
            result.DeleterId = _session.UserId.ToGuid();
        }

        /// <summary>
        /// 初始化可空Guid
        /// </summary>
        private void InitNullableGuid()
        {
            var result = (IDeletionAudited<Guid?>)_entity;
            result.DeletionTime = DateTime.Now;
            result.DeleterId = _session.UserId.ToGuidOrNull();
        }

        /// <summary>
        /// 初始化int
        /// </summary>
        private void InitInt()
        {
            var result = (IDeletionAudited<int>)_entity;
            result.DeletionTime = DateTime.Now;
            result.DeleterId = _session.UserId.ToInt();
        }

        /// <summary>
        /// 初始化可空int
        /// </summary>
        private void InitNullableInt()
        {
            var result = (IDeletionAudited<int?>)_entity;
            result.DeletionTime = DateTime.Now;
            result.DeleterId = _session.UserId.ToIntOrNull();
        }

        /// <summary>
        /// 初始化Long
        /// </summary>
        private void InitLong()
        {
            var result = (IDeletionAudited<long>)_entity;
            result.DeletionTime = DateTime.Now;
            result.DeleterId = _session.UserId.ToLong();
        }

        /// <summary>
        /// 初始化可空Long
        /// </summary>
        private void InitNullableLong()
        {
            var result = (IDeletionAudited<long?>)_entity;
            result.DeletionTime = DateTime.Now;
            result.DeleterId = _session.UserId.ToLongOrNull();
        }

        /// <summary>
        /// 初始化字符串
        /// </summary>
        private void InitString()
        {
            var result = (IDeletionAudited<string>)_entity;
            result.DeletionTime = DateTime.Now;
            if (result.DeleterId.IsEmpty())
            {
                result.DeleterId = BingConfig.Current.EnabledUserName
                    ? _session.UserName.SafeString()
                    : _session.UserId.SafeString();
            }
        }
    }
}
