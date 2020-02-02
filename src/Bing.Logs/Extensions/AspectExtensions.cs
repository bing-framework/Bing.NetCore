using System.Collections.Generic;
using System.Linq;
using AspectCore.DynamicProxy.Parameters;
using Bing.Helpers;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Logs.Extensions
{
    /// <summary>
    /// Aop扩展
    /// </summary>
    public static partial class AspectExtensions
    {
        /// <summary>
        /// 添加日志参数
        /// </summary>
        /// <param name="parameter">参数</param>
        /// <param name="log">日志</param>
        public static void AppendTo(this Parameter parameter, ILog log)
        {
            log.Params( parameter.Name, GetParameterValue(parameter), parameter.ParameterInfo.ParameterType.FullName);
        }

        /// <summary>
        /// 获取参数值
        /// </summary>
        /// <param name="parameter">参数</param>
        /// <returns></returns>
        private static string GetParameterValue(Parameter parameter)
        {
            if (Reflection.IsGenericCollection(parameter.RawType) == false)
            {
                return parameter.Value.SafeString();
            }
            if (!(parameter.Value is IEnumerable<object> list))
            {
                return parameter.Value.SafeString();
            }

            return list.Select(t => t.SafeString()).Join();
        }
    }
}
