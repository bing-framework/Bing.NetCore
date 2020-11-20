using System;

namespace Bing.Domain.Entities.Events
{
    /// <summary>
    /// 领域事件处理器工厂
    /// </summary>
    public interface IDomainHandlerFactory
    {
        /// <summary>
        /// 创建领域事件处理器
        /// </summary>
        /// <param name="handlerType">领域事件处理器类型</param>
        object Create(Type handlerType);
    }
}
