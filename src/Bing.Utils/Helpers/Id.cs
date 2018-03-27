using System;
using System.Collections.Generic;
using System.Text;
using Bing.Utils.IdGenerators.Abstractions;
using Bing.Utils.IdGenerators.Core;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// Id 生成器
    /// </summary>
    public static class Id
    {
        /// <summary>
        /// Id
        /// </summary>
        private static string _id;

        /// <summary>
        /// Guid 生成器
        /// </summary>
        public static IGuidGenerator GuidGenerator { get; set; } = SequentialGuidGenerator.Current;

        /// <summary>
        /// Long 生成器
        /// </summary>
        public static ILongGenerator LongGenerator { get; set; } = SnowflakeIdGenerator.Current;

        /// <summary>
        /// String 生成器
        /// </summary>
        public static IStringGenerator StringGenerator { get; set; } = TimestampIdGenerator.Current;

        /// <summary>
        /// 设置Id
        /// </summary>
        /// <param name="id">Id</param>
        public static void SetId(string id)
        {
            _id = id;
        }

        /// <summary>
        /// 重置Id
        /// </summary>
        public static void Reset()
        {
            _id = null;
        }

        /// <summary>
        /// 创建 Guid ID
        /// </summary>
        /// <returns></returns>
        public static Guid GuidId()
        {
            return GuidGenerator.Create();
        }

        /// <summary>
        /// 创建 Long ID
        /// </summary>
        /// <returns></returns>
        public static long LongId()
        {
            return LongGenerator.Create();
        }

        /// <summary>
        /// 创建 String ID
        /// </summary>
        /// <returns></returns>
        public static string StringId()
        {
            return StringGenerator.Create();
        }
    }
}
