using System;
using Bing.Extensions;

namespace Bing.Auditing
{
    /// <summary>
    /// 修改操作审计初始化器
    /// </summary>
    public sealed class ModificationAuditedInitializer
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
        /// 初始化一个<see cref="ModificationAuditedInitializer"/>类型的实例
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="userId">用户标识</param>
        /// <param name="userName">用户名称</param>
        private ModificationAuditedInitializer(object entity, string userId, string userName)
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
        public static void Init(object entity, string userId, string userName) => new ModificationAuditedInitializer(entity, userId, userName).Init();

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init()
        {
            if (_entity == null)
                return;
            InitLastModificationTime();
            InitLastModifier();
            InitLastModifierId();
        }

        /// <summary>
        /// 初始化修改时间
        /// </summary>
        private void InitLastModificationTime()
        {
            if (_entity is IHasModificationTime result)
                result.LastModificationTime = DateTime.Now;
        }

        /// <summary>
        /// 初始化修改人
        /// </summary>
        private void InitLastModifier()
        {
            if (string.IsNullOrWhiteSpace(_userName))
                return;
            if (_entity is IHasModifier result)
                result.LastModifier = _userName;
        }

        /// <summary>
        /// 初始化修改人标识
        /// </summary>
        private void InitLastModifierId()
        {
            if (string.IsNullOrWhiteSpace(_userId))
                return;
            switch (_entity)
            {
                case IModificationAuditedObject<Guid> _:
                    InitGuid();
                    return;
                case IModificationAuditedObject<Guid?> _:
                    InitNullableGuid();
                    return;
                case IModificationAuditedObject<int> _:
                    InitInt();
                    return;
                case IModificationAuditedObject<int?> _:
                    InitNullableInt();
                    return;
                case IModificationAuditedObject<string> _:
                    InitString();
                    return;
                case IModificationAuditedObject<long> _:
                    InitLong();
                    return;
                case IModificationAuditedObject<long?> _:
                    InitNullableLong();
                    return;
            }
        }

        /// <summary>
        /// 初始化Guid
        /// </summary>
        private void InitGuid()
        {
            var result = (IModificationAuditedObject<Guid>)_entity;
            result.LastModifierId = _userId.ToGuid();
        }

        /// <summary>
        /// 初始化可空Guid
        /// </summary>
        private void InitNullableGuid()
        {
            var result = (IModificationAuditedObject<Guid?>)_entity;
            result.LastModifierId = _userId.ToGuidOrNull();
        }

        /// <summary>
        /// 初始化int
        /// </summary>
        private void InitInt()
        {
            var result = (IModificationAuditedObject<int>)_entity;
            result.LastModifierId = _userId.ToInt();
        }

        /// <summary>
        /// 初始化可空int
        /// </summary>
        private void InitNullableInt()
        {
            var result = (IModificationAuditedObject<int?>)_entity;
            result.LastModifierId = _userId.ToIntOrNull();
        }

        /// <summary>
        /// 初始化Long
        /// </summary>
        private void InitLong()
        {
            var result = (IModificationAuditedObject<long>)_entity;
            result.LastModifierId = _userId.ToLong();
        }

        /// <summary>
        /// 初始化可空Long
        /// </summary>
        private void InitNullableLong()
        {
            var result = (IModificationAuditedObject<long?>)_entity;
            result.LastModifierId = _userId.ToLongOrNull();
        }

        /// <summary>
        /// 初始化字符串
        /// </summary>
        private void InitString()
        {
            var result = (IModificationAuditedObject<string>)_entity;
            result.LastModifierId = _userId.SafeString();
        }
    }
}
