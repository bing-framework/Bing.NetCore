using System.Threading.Tasks;
using AspectCore.DynamicProxy;
using AspectCore.DynamicProxy.Parameters;
using Bing.Aspects;
using Bing.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Logs.Aspects
{
    /// <summary>
    /// 日志 属性基类
    /// </summary>
    public abstract class LogAttributeBase : InterceptorBase
    {
        /// <summary>
        /// 执行
        /// </summary>
        public override async Task Invoke(AspectContext context, AspectDelegate next)
        {
            var methodName = GetMethodName(context);
            var log = context.ServiceProvider.GetService<ILog>();
            if (!Enabled(log))
                return;
            ExecuteBefore(log, context, methodName);
            await next(context);
            await ExecuteAfter(log, context, methodName);
        }

        /// <summary>
        /// 获取方法名
        /// </summary>
        /// <param name="context">Aspect上下文</param>
        private string GetMethodName(AspectContext context) => $"{context.ServiceMethod.DeclaringType.FullName}.{context.ServiceMethod.Name}";

        /// <summary>
        /// 是否启用
        /// </summary>
        /// <param name="log">日志操作</param>
        protected virtual bool Enabled(ILog log) => true;

        /// <summary>
        /// 执行前
        /// </summary>
        /// <param name="log">日志操作</param>
        /// <param name="context">Aspect上下文</param>
        /// <param name="methodName">方法名</param>
        private void ExecuteBefore(ILog log, AspectContext context, string methodName)
        {
            log
                .Tag(context.ServiceMethod.Name)
                .Caption($"{context.ServiceMethod.Name}方法执行前")
                .Class(context.ServiceMethod.DeclaringType.FullName)
                .Method(methodName);
            foreach (var parameter in context.GetParameters())
                parameter.AppendTo(log);
            WriteLog(log);
        }

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="log">日志操作</param>
        protected abstract void WriteLog(ILog log);

        /// <summary>
        /// 执行后
        /// </summary>
        /// <param name="log">日志操作</param>
        /// <param name="context">Aspect上下文</param>
        /// <param name="methodName">方法名</param>
        private async Task ExecuteAfter(ILog log, AspectContext context, string methodName)
        {
            if (context.ServiceMethod.ReturnType == typeof(Task) ||
                context.ServiceMethod.ReturnType == typeof(void) ||
                context.ServiceMethod.ReturnType == typeof(ValueTask))
                return;
            var returnValue = context.IsAsync() ? await context.UnwrapAsyncReturnValue() : context.ReturnValue;
            var returnType = returnValue.GetType().FullName;
            log.Caption($"{context.ServiceMethod.Name}方法执行后")
                .Method(methodName)
                .Content($"返回类型: {returnType}, 返回值: {returnValue.SafeString()}");
            WriteLog(log);
        }
    }
}
