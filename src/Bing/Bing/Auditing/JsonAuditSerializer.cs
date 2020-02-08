using System;
using System.Collections.Generic;
using Bing.Dependency;
using Newtonsoft.Json;

namespace Bing.Auditing
{
    /// <summary>
    /// 基于JSON的审计序列化器
    /// </summary>
    public class JsonAuditSerializer : IAuditSerializer, ITransientDependency
    {
        /// <summary>
        /// 同步锁对象
        /// </summary>
        private static readonly object SyncObj = new object();

        /// <summary>
        /// 共享的JSON序列化器设置
        /// </summary>
        private static JsonSerializerSettings _sharedJsonSerializerSettings;

        /// <summary>
        /// 序列化
        /// </summary>
        /// <param name="obj">对象</param>
        public string Serialize(object obj) => JsonConvert.SerializeObject(obj, GetSharedJsonSerializerSettings());

        /// <summary>
        /// 获取共享的JSON序列化器设置
        /// </summary>
        private JsonSerializerSettings GetSharedJsonSerializerSettings()
        {
            if (_sharedJsonSerializerSettings == null)
            {
                lock (SyncObj)
                {
                    if (_sharedJsonSerializerSettings == null)
                    {
                        _sharedJsonSerializerSettings = new JsonSerializerSettings()
                        {
                            ContractResolver = new AuditingContractResolver(new List<Type>())
                        };
                    }
                }
            }
            return _sharedJsonSerializerSettings;
        }
    }
}
