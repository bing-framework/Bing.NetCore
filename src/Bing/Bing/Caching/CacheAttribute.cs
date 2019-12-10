using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AspectCore.DependencyInjection;
using AspectCore.DynamicProxy;
using Bing.Aspects.Base;
using Bing.Exceptions;
using Bing.Utils.Threading.Asyncs;

namespace Bing.Caching
{
    /// <summary>
    /// 缓存 属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CacheAttribute : InterceptorBase
    {
        /// <summary>
        /// 缓存键值。可以附加参数，例如：UserInfo{model:Nmae}_{type}
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 过期时间间隔
        /// </summary>
        public TimeSpan? Expiration { get; set; }

        /// <summary>
        /// 拼接缓存键参数
        /// </summary>
        private static readonly ConcurrentDictionary<MethodInfo, List<string>> AppendKeyParameters;

        /// <summary>
        /// 异步结果方法
        /// </summary>
        private static readonly MethodInfo TaskResultMethod;

        /// <summary>
        /// 异步锁
        /// </summary>
        private readonly AsyncLock _lock = new AsyncLock();

        /// <summary>
        /// 缓存
        /// </summary>
        [FromServiceContext]
        public ICache Cache { get; set; }

        /// <summary>
        /// 静态构造函数
        /// </summary>
        static CacheAttribute()
        {
            AppendKeyParameters = new ConcurrentDictionary<MethodInfo, List<string>>();
            TaskResultMethod = typeof(Task).GetMethods()
                .FirstOrDefault(p => p.Name == "FromResult" && p.ContainsGenericParameters);
        }

        /// <summary>
        /// 执行
        /// </summary>
        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
            List<string> appendKeyArray = null;
            if (string.IsNullOrWhiteSpace(Key))
            {
                Key =
                    $"{context.ServiceMethod.DeclaringType}.{context.ImplementationMethod.Name}:{context.ServiceMethod.ToString()}";
            }
            else
            {
                if (!AppendKeyParameters.TryGetValue(context.ImplementationMethod, out appendKeyArray))
                {
                    if (Key.IndexOf("{", StringComparison.Ordinal) > -1)
                    {
                        appendKeyArray = new List<string>();
                        var matchs = Regex.Matches(Key, @"\{\w*\:?\w*\}", RegexOptions.None);
                        foreach (Match match in matchs)
                        {
                            if (match.Success)
                            {
                                appendKeyArray.Add(match.Value.TrimStart('{').TrimEnd('}'));
                            }
                        }
                    }

                    AppendKeyParameters.TryAdd(context.ImplementationMethod, appendKeyArray);
                }
            }

            var currentCacheKey = Key;

            if (appendKeyArray != null && appendKeyArray.Count > 0)
            {
                // 获取方法的参数
                var pars = context.ProxyMethod.GetParameters();

                // 设置参数名和值，加入字典
                var dicValue = new Dictionary<string, object>();
                for (var i = 0; i < pars.Length; i++)
                {
                    dicValue.Add(pars[i].Name, context.Parameters[i]);
                }

                foreach (var key in appendKeyArray)
                {
                    if (key.Contains(":"))
                    {
                        var arr = key.Split(':');
                        var keyFirst = arr[0];
                        var keySecond = arr[1];

                        if (!dicValue.TryGetValue(keyFirst, out var value))
                        {
                            throw new Warning(
                                $"Cache {context.ServiceMethod.DeclaringType}.{context.ImplementationMethod.Name} 不包含参数 {keyFirst}");
                        }

                        var ob = Internal.Helper.ToDictionary(value);
                        if (!ob.TryGetValue(keySecond, out object tokenValue))
                        {
                            throw new Warning(
                                $"Cache {context.ServiceMethod.DeclaringType}.{context.ImplementationMethod.Name} {keyFirst} 不包含参数 {keySecond}");
                        }

                        currentCacheKey = currentCacheKey.Replace("{" + key + "}", tokenValue.ToString());
                    }
                    else
                    {
                        if (!dicValue.TryGetValue(key, out var value))
                        {
                            throw new Warning(
                                $"Cache {context.ServiceMethod.DeclaringType}.{context.ImplementationMethod.Name} 不包含参数 {key}");
                        }

                        currentCacheKey = currentCacheKey.Replace("{" + key + "}", value.ToString());
                    }
                }
            }

            // 返回值类型
            var returnType = context.IsAsync()
                ? context.ServiceMethod.ReturnType.GetGenericArguments().First()
                : context.ServiceMethod.ReturnType;

            // 从缓存取值
            var cacheValue = await Cache.GetAsync(currentCacheKey, returnType);
            if (cacheValue != null)
            {
                context.ReturnValue = context.IsAsync()
                    ? TaskResultMethod.MakeGenericMethod(returnType).Invoke(null, new object[] {cacheValue})
                    : cacheValue;
                return;
            }

            using (await _lock.LockAsync())
            {
                cacheValue = await Cache.GetAsync(currentCacheKey, returnType);
                if (cacheValue != null)
                {
                    context.ReturnValue = context.IsAsync()
                        ? TaskResultMethod.MakeGenericMethod(returnType).Invoke(null, new object[] { cacheValue })
                        : cacheValue;
                }
                else
                {
                    await next(context);
                    dynamic returnValue =
                        context.IsAsync() ? await context.UnwrapAsyncReturnValue() : context.ReturnValue;
                    Cache.TryAdd(currentCacheKey, returnValue, Expiration);
                }
            }
        }
    }
}
