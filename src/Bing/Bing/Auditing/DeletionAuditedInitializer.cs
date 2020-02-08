using System;
using Bing.Extensions;

namespace Bing.Auditing
{
    /// <summary>
    /// 删除操作审计初始化器
    /// </summary>
    public sealed class DeletionAuditedInitializer
    {
        /// <summary>
        /// 实体
        /// </summary>
        private readonly object _entity;

        /// <summary>
        /// 用户标识
        /// </summary>
        private readonly string _userId;

        /// <summary>
        /// 用户名称
        /// </summary>
        private readonly string _userName;

        /// <summary>
        /// 初始化一个<see cref="DeletionAuditedInitializer"/>类型的实例
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="userId">用户标识</param>
        /// <param name="userName">用户名称</param>
        private DeletionAuditedInitializer(object entity, string userId, string userName)
        {
            _entity = entity;
            _userId = userId;
            _userName = userName;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="userId">用户标识</param>
        /// <param name="userName">用户名称</param>
        public static void Init(object entity, string userId, string userName) => new DeletionAuditedInitializer(entity, userId, userName).Init();

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init()
        {
            if (_entity == null)
                return;
            InitDeletionTime();
            InitDeleter();
            InitDeleterId();
        }

        /// <summary>
        /// 初始化删除时间
        /// </summary>
        private void InitDeletionTime()
        {
            if (_entity is IHasDeletionTime result)
                result.DeletionTime = DateTime.Now;
        }

        /// <summary>
        /// 初始化删除人
        /// </summary>
        private void InitDeleter()
        {
            if (_entity is IHasDeleter result)
                result.Deleter = _userName;
        }

        /// <summary>
        /// 初始化删除人标识
        /// </summary>
        private void InitDeleterId()
        {
            if (string.IsNullOrWhiteSpace(_userId))
                return;
            switch (_entity)
            {
                case IDeletionAuditedObject<Guid> _:
                    InitGuid();
                    return;

                case IDeletionAuditedObject<Guid?> _:
                    InitNullableGuid();
                    return;

                case IDeletionAuditedObject<int> _:
                    InitInt();
                    return;

                case IDeletionAuditedObject<int?> _:
                    InitNullableInt();
                    return;

                case IDeletionAuditedObject<string> _:
                    InitString();
                    return;

                case IDeletionAuditedObject<long> _:
                    InitLong();
                    return;

                case IDeletionAuditedObject<long?> _:
                    InitNullableLong();
                    return;
            }
        }

        /// <summary>
        /// 初始化Guid
        /// </summary>
        private void InitGuid()
        {
            var result = (IDeletionAuditedObject<Guid>)_entity;
            result.DeleterId = _userId.ToGuid();
        }

        /// <summary>
        /// 初始化可空Guid
        /// </summary>
        private void InitNullableGuid()
        {
            var result = (IDeletionAuditedObject<Guid?>)_entity;
            result.DeleterId = _userId.ToGuidOrNull();
        }

        /// <summary>
        /// 初始化int
        /// </summary>
        private void InitInt()
        {
            var result = (IDeletionAuditedObject<int>)_entity;
            result.DeleterId = _userId.ToInt();
        }

        /// <summary>
        /// 初始化可空int
        /// </summary>
        private void InitNullableInt()
        {
            var result = (IDeletionAuditedObject<int?>)_entity;
            result.DeleterId = _userId.ToIntOrNull();
        }

        /// <summary>
        /// 初始化Long
        /// </summary>
        private void InitLong()
        {
            var result = (IDeletionAuditedObject<long>)_entity;
            result.DeleterId = _userId.ToLong();
        }

        /// <summary>
        /// 初始化可空Long
        /// </summary>
        private void InitNullableLong()
        {
            var result = (IDeletionAuditedObject<long?>)_entity;
            result.DeleterId = _userId.ToLongOrNull();
        }

        /// <summary>
        /// 初始化字符串
        /// </summary>
        private void InitString()
        {
            var result = (IDeletionAuditedObject<string>)_entity;
            result.DeleterId = _userId.SafeString();
        }
    }
}
