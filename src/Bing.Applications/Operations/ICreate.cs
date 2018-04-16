using System;
using System.Collections.Generic;
using System.Text;
using Bing.Applications.Aspects;
using Bing.Applications.Dtos;
using Bing.Validations.Aspects;

namespace Bing.Applications.Operations
{
    /// <summary>
    /// 创建操作
    /// </summary>
    /// <typeparam name="TCreateRequest">创建参数类型</typeparam>
    public interface ICreate<in TCreateRequest> where TCreateRequest:IRequest,new()
    {
        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="request">请求参数</param>
        [UnitOfWork]
        void Create([Valid] TCreateRequest request);
    }
}
