using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Bing.Caching.Internal;

namespace Bing.Caching.Default
{
    /// <summary>
    /// 默认缓存键生成器
    /// </summary>
    public class DefaultCacheKeyGenerator:ICacheKeyGenerator
    {
        /// <summary>
        /// 连接符
        /// </summary>
        private char _linkChar = ':';

        /// <summary>
        /// 获取缓存键
        /// </summary>
        /// <param name="methodInfo">方法信息</param>
        /// <param name="args">参数</param>
        /// <param name="prefix">前缀</param>
        /// <returns></returns>
        public string GetCacheKey(MethodInfo methodInfo, object[] args, string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                var typeName = methodInfo?.DeclaringType?.Name;
                var methodName = methodInfo?.Name;

                var methodArguments = this.FormatArgumentsToPartOfCacheKey(args);

                return this.GenerateCacheKey(typeName, methodName, methodArguments);
            }
            else
            {
                var methodArguments = this.FormatArgumentsToPartOfCacheKey(args);
                return this.GenerateCacheKey(string.Empty, prefix, methodArguments);
            }
        }

        /// <summary>
        /// 获取缓存键前缀
        /// </summary>
        /// <param name="methodInfo">方法信息</param>
        /// <param name="prefix">前缀</param>
        /// <returns></returns>
        public string GetCacheKeyPrefix(MethodInfo methodInfo, string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                var typeName = methodInfo?.DeclaringType?.Name;
                var methodName = methodInfo?.Name;

                return this.GenerateCacheKeyPrefix(typeName, methodName);
            }
            return this.GenerateCacheKeyPrefix(string.Empty, prefix);
        }

        /// <summary>
        /// 生成缓存键前缀
        /// </summary>
        /// <param name="first">参数1</param>
        /// <param name="second">参数2</param>
        /// <returns></returns>
        private string GenerateCacheKeyPrefix(string first, string second)
        {
            return string.Concat(first, _linkChar, second, _linkChar).TrimStart(_linkChar);
        }

        /// <summary>
        /// 格式化部分缓存键的参数
        /// </summary>
        /// <param name="methodArguments">方法参数</param>
        /// <returns></returns>
        private IList<string> FormatArgumentsToPartOfCacheKey(object[] methodArguments)
        {
            if (methodArguments != null && methodArguments.Length > 0)
            {
                return methodArguments.Select(this.GetArgumentValue).ToList();
            }
            return new List<string> {"0"};
        }

        /// <summary>
        /// 生成缓存键
        /// </summary>
        /// <param name="typeName">类型名</param>
        /// <param name="methodName">方法名</param>
        /// <param name="parameters">参数列表</param>
        /// <returns></returns>
        private string GenerateCacheKey(string typeName, string methodName, IList<string> parameters)
        {
            var builder = new StringBuilder();
            builder.Append(this.GenerateCacheKeyPrefix(typeName, methodName));
            foreach (var parameter in parameters)
            {
                builder.Append(parameter);
                builder.Append(_linkChar);
            }
            return builder.ToString().TrimEnd(_linkChar);
        }

        /// <summary>
        /// 获取参数值
        /// </summary>
        /// <param name="arg">参数</param>
        /// <returns></returns>
        private string GetArgumentValue(object arg)
        {
            switch (arg)
            {
                case int _:
                case long _:
                case string _:
                    return arg.ToString();
                case DateTime time:
                    return time.ToString("yyyyMMddHHmmss");
                case ICachable cachable:
                    return cachable.CacheKey;
            }
            return null;
        }
    }
}
