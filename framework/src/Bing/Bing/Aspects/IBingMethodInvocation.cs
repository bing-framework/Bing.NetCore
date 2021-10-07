using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Bing.Aspects
{
    /// <summary>
    /// 方法调用
    /// </summary>
    public interface IBingMethodInvocation
    {
        /// <summary>
        /// 目标对象
        /// </summary>
        object TargetObject { get; }

        /// <summary>
        /// 方法
        /// </summary>
        MethodInfo Method { get; }

        /// <summary>
        /// 参数数组
        /// </summary>
        object[] Parameters { get; }

        /// <summary>
        /// 返回值
        /// </summary>
        object ReturnValue { get; set; }

        /// <summary>
        /// 属性
        /// </summary>
        IDictionary<string, object> Properties { get; }

        /// <summary>
        /// 处理并继续
        /// </summary>
        Task ProceedAsync();
    }
}
