using System;

namespace Bing.Application.Dtos
{
    /// <summary>
    /// 实体 - 数据传输对象
    /// </summary>
    /// <typeparam name="TKey">标识类型</typeparam>
    [Serializable]
    public abstract class EntityDto<TKey> : IEntityDto<TKey>
    {
        /// <summary>
        /// 标识
        /// </summary>
        public TKey Id { get; set; }

        /// <summary>
        /// 输出字符串
        /// </summary>
        public override string ToString() => $"[DTO: {GetType().Name}] Id = {Id}";
    }
}
