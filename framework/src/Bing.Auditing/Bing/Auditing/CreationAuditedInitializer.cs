using System;
using Bing.Extensions;

namespace Bing.Auditing
{
    /// <summary>
    /// 创建操作审计初始化器
    /// </summary>
    public sealed class CreationAuditedInitializer
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
        /// 操作时间
        /// </summary>
        private readonly DateTime? _dateTime;

        /// <summary>
        /// 初始化一个<see cref="CreationAuditedInitializer"/>类型的实例
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="userId">用户标识</param>
        /// <param name="userName">用户名称</param>
        /// <param name="dateTime">操作时间</param>
        private CreationAuditedInitializer(object entity, string userId, string userName, DateTime? dateTime)
        {
            _entity = entity;
            _userId = userId;
            _userName = userName;
            _dateTime = dateTime;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="userId">用户标识</param>
        /// <param name="userName">用户名称</param>
        public static void Init(object entity, string userId, string userName) => new CreationAuditedInitializer(entity, userId, userName, null).Init();

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="userId">用户标识</param>
        /// <param name="userName">用户名称</param>
        /// <param name="dateTime">操作时间</param>
        public static void Init(object entity, string userId, string userName, DateTime? dateTime) => new CreationAuditedInitializer(entity, userId, userName,dateTime).Init();

        /// <summary>
        /// 初始化
        /// </summary>
        public void Init()
        {
            if (_entity == null)
                return;
            InitCreationTime();
            InitCreator();
            InitCreatorId();
        }

        /// <summary>
        /// 初始化创建时间
        /// </summary>
        private void InitCreationTime()
        {
            if (_entity is IHasCreationTime result)
                result.CreationTime = _dateTime.HasValue ? _dateTime.SafeValue() : DateTime.Now;
        }

        /// <summary>
        /// 初始化创建人
        /// </summary>
        private void InitCreator()
        {
            if (string.IsNullOrWhiteSpace(_userName))
                return;
            if (_entity is IHasCreator result)
                result.Creator = _userName;
        }

        /// <summary>
        /// 初始化创建人标识
        /// </summary>
        private void InitCreatorId()
        {
            if (string.IsNullOrWhiteSpace(_userId))
                return;
            switch (_entity)
            {
                case ICreationAuditedObject<Guid> _:
                    InitGuid();
                    return;

                case ICreationAuditedObject<Guid?> _:
                    InitNullableGuid();
                    return;

                case ICreationAuditedObject<int> _:
                    InitInt();
                    return;

                case ICreationAuditedObject<int?> _:
                    InitNullableInt();
                    return;

                case ICreationAuditedObject<string> _:
                    InitString();
                    return;

                case ICreationAuditedObject<long> _:
                    InitLong();
                    return;

                case ICreationAuditedObject<long?> _:
                    InitNullableLong();
                    return;
            }
        }

        /// <summary>
        /// 初始化Guid
        /// </summary>
        private void InitGuid()
        {
            var result = (ICreationAuditedObject<Guid>)_entity;
            result.CreatorId = _userId.ToGuid();
        }

        /// <summary>
        /// 初始化可空Guid
        /// </summary>
        private void InitNullableGuid()
        {
            var result = (ICreationAuditedObject<Guid?>)_entity;
            result.CreatorId = _userId.ToGuidOrNull();
        }

        /// <summary>
        /// 初始化int
        /// </summary>
        private void InitInt()
        {
            var result = (ICreationAuditedObject<int>)_entity;
            result.CreatorId = _userId.ToInt();
        }

        /// <summary>
        /// 初始化可空int
        /// </summary>
        private void InitNullableInt()
        {
            var result = (ICreationAuditedObject<int?>)_entity;
            result.CreatorId = _userId.ToIntOrNull();
        }

        /// <summary>
        /// 初始化Long
        /// </summary>
        private void InitLong()
        {
            var result = (ICreationAuditedObject<long>)_entity;
            result.CreatorId = _userId.ToLong();
        }

        /// <summary>
        /// 初始化可空Long
        /// </summary>
        private void InitNullableLong()
        {
            var result = (ICreationAuditedObject<long?>)_entity;
            result.CreatorId = _userId.ToLongOrNull();
        }

        /// <summary>
        /// 初始化字符串
        /// </summary>
        private void InitString()
        {
            var result = (ICreationAuditedObject<string>)_entity;
            result.CreatorId = _userId.SafeString();
        }
    }
}
