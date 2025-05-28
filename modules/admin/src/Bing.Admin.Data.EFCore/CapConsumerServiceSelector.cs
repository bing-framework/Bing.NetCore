using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using DotNetCore.CAP.Internal;

namespace Bing.Admin.Data
{
    /// <summary>
    /// Cap 消费者服务选择器
    /// </summary>
    public class CapConsumerServiceSelector : ConsumerServiceSelector
    {
        /// <summary>
        /// 初始化一个<see cref="CapConsumerServiceSelector"/>类型的实例
        /// </summary>
        public CapConsumerServiceSelector(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        /// <summary>
        /// 查找消费者。通过接口类型中查找
        /// </summary>
        /// <param name="provider">服务提供程序</param>
        protected override IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromInterfaceTypes(IServiceProvider provider)
        {
            var result = base.FindConsumersFromInterfaceTypes(provider).ToList();
            if (!NeedReplaceContext(result, provider))
                return result;
            foreach (var item in result)
                ReplaceContext(item, provider);
            return result;
        }

        /// <summary>
        /// 是否需要替换上下文
        /// </summary>
        /// <param name="consumerExecutorDescriptors">消费者执行器定义者</param>
        /// <param name="provider">服务提供程序</param>
        private bool NeedReplaceContext(IEnumerable<ConsumerExecutorDescriptor> consumerExecutorDescriptors, IServiceProvider provider)
        {
            var result = consumerExecutorDescriptors.FirstOrDefault();
            if (result == null)
                return false;
            var aspectCoreType = provider.GetService(result.ServiceTypeInfo);
            if (aspectCoreType.GetType().GetTypeInfo().FullName.StartsWith("AspectCore.DynamicGenerated"))
                return true;
            return false;
        }

        /// <summary>
        /// 替换上下文
        /// </summary>
        /// <param name="item">消费者执行器定义者</param>
        /// <param name="provider">服务提供程序</param>
        private void ReplaceContext(ConsumerExecutorDescriptor item, IServiceProvider provider)
        {
            try
            {
                var aspectCoreType = provider.GetService(item.ServiceTypeInfo);
                item.ImplTypeInfo = aspectCoreType.GetType().GetTypeInfo();
                ReplaceMethodInfo(item);
                Debug.WriteLine($"AspectCoreType: {aspectCoreType}");
                Debug.WriteLine($"服务类型:{item.ServiceTypeInfo}, 实现类型: {item.ImplTypeInfo}, 方法: {item.MethodInfo}, 特性: {item.Attribute}");
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}] Type: {item.ServiceTypeInfo}, Exception: {e}");
            }
        }

        /// <summary>
        /// 替换参数
        /// </summary>
        /// <param name="item">项</param>
        private void ReplaceMethodInfo(ConsumerExecutorDescriptor item)
        {
            var oldMethod = item.MethodInfo;
            var methodInfos = item.ImplTypeInfo.GetMethods();
            var methodInfo = methodInfos.FirstOrDefault(x => x.ToString() == oldMethod.ToString());
            item.MethodInfo = methodInfo ?? oldMethod;
        }
    }
}
