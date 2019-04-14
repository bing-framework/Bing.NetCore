using System;
using System.ComponentModel.DataAnnotations;
using Bing.Helpers;
using Bing.Properties;
using Bing.Sessions;
using Bing.Utils.Extensions;
using Bing.Validations;

namespace Bing.Domains.Entities
{
    /// <summary>
    /// 领域实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public abstract class EntityBase<TEntity> : EntityBase<TEntity, Guid> where TEntity : IEntity
    {
        /// <summary>
        /// 初始化一个<see cref="EntityBase{TEntity}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected EntityBase(Guid id) : base(id)
        {
        }
    }

    /// <summary>
    /// 领域实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    public abstract class EntityBase<TEntity, TKey> : DomainBase<TEntity>, IEntity<TEntity, TKey>
        where TEntity : IEntity
    {
        /// <summary>
        /// 标识
        /// </summary>
        [Key]
        public TKey Id { get; private set; }

        /// <summary>
        /// 用户会话
        /// </summary>
        protected virtual ISession Session => Ioc.Create<ISession>();

        /// <summary>
        /// 初始化一个<see cref="EntityBase{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected EntityBase(TKey id)
        {
            Id = id;
        }

        /// <summary>
        /// 判断两个实体是否同一数据记录的实体
        /// </summary>
        /// <param name="other">领域实体</param>
        /// <returns></returns>
        public override bool Equals(object other)
        {
            return this == (other as EntityBase<TEntity, TKey>);
        }

        /// <summary>
        /// 用作特定类型的哈希函数。
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return ReferenceEquals(Id, null) ? 0 : Id.GetHashCode();
        }

        /// <summary>
        /// 相等比较
        /// </summary>
        /// <param name="left">领域实体</param>
        /// <param name="right">领域实体</param>
        /// <returns></returns>
        public static bool operator ==(EntityBase<TEntity, TKey> left, EntityBase<TEntity, TKey> right)
        {
            if ((object)left == null && (object)right == null)
            {
                return true;
            }
            if (!(left is TEntity) || !(right is TEntity))
            {
                return false;
            }
            if (Equals(left.Id, null))
            {
                return false;
            }
            if (left.Id.Equals(default(TKey)))
            {
                return false;
            }
            return left.Id.Equals(right.Id);
        }

        /// <summary>
        /// 不相等比较
        /// </summary>
        /// <param name="left">领域实体</param>
        /// <param name="right">领域实体</param>
        /// <returns></returns>
        public static bool operator !=(EntityBase<TEntity, TKey> left, EntityBase<TEntity, TKey> right)
        {
            return !(left == right);
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public virtual void Init()
        {
            InitId();
        }

        /// <summary>
        /// 初始化标识
        /// </summary>
        protected virtual void InitId()
        {
            if (typeof(TKey) == typeof(int) || typeof(TKey) == typeof(long))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id.SafeString()) || Id.Equals(default(TKey)))
            {
                Id = CreateId();
            }
        }

        /// <summary>
        /// 创建标识
        /// </summary>
        /// <returns></returns>
        protected virtual TKey CreateId()
        {
            return Utils.Helpers.Conv.To<TKey>(Guid.NewGuid());
        }

        /// <summary>
        /// 验证
        /// </summary>
        /// <param name="results">验证结果集合</param>
        protected override void Validate(ValidationResultCollection results)
        {
            ValidateId(results);
        }

        /// <summary>
        /// 验证标识
        /// </summary>
        /// <param name="results">验证结果集合</param>
        protected virtual void ValidateId(ValidationResultCollection results)
        {
            if (typeof(TKey) == typeof(int) || typeof(TKey) == typeof(long))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id.SafeString()) || Id.Equals(default(TKey)))
            {
                results.Add(new ValidationResult(R.IdIsEmpty));
            }
        }
    }
}
