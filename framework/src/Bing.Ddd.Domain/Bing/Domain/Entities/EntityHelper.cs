using System;
using Bing.Helpers;
using Bing.Reflection;

namespace Bing.Domain.Entities
{
    /// <summary>
    /// 实体帮助类
    /// </summary>
    public static class EntityHelper
    {
        /// <summary>
        /// Guid 生成函数
        /// </summary>
        public static Func<Guid> GuidGenerateFunc { get; set; } = Guid.NewGuid;

        /// <summary>
        /// 是否有默认值
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        public static bool HasDefaultKeys(IEntity entity)
        {
            Check.NotNull(entity, nameof(entity));
            foreach (var key in entity.GetKeys())
            {
                if (!IsDefaultKeyValue(key))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 是否默认主键值
        /// </summary>
        /// <param name="value">值</param>
        /// <returns>true: 默认值, false: 非默认值</returns>
        private static bool IsDefaultKeyValue(object value)
        {
            if (value == null)
                return true;
            var type = value.GetType();
            if (type == typeof(int))
                return Convert.ToInt32(value) <= 0;
            if (type == typeof(long))
                return Convert.ToInt64(value) <= 0;
            return Types.IsDefaultValue(value);
        }
    }
}
