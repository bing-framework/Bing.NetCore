using System;
using System.Collections.Generic;
using System.Text;
using Bing.Applications.Aspects;
using Bing.Applications.Dtos;
using Bing.Validations.Aspects;

namespace Bing.Applications.Operations
{
    /// <summary>
    /// 保存操作
    /// </summary>
    /// <typeparam name="TRequest">参数类型</typeparam>
    public interface ISave<in TRequest> where TRequest:IRequest,IKey,new()
    {
        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="request">请求参数</param>
        [UnitOfWork]
        void Save([Valid] TRequest request);
    }
}
