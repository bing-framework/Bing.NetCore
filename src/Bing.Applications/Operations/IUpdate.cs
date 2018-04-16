using System;
using System.Collections.Generic;
using System.Text;
using Bing.Applications.Aspects;
using Bing.Applications.Dtos;
using Bing.Validations.Aspects;

namespace Bing.Applications.Operations
{
    /// <summary>
    /// 修改操作
    /// </summary>
    /// <typeparam name="TUpdateRequest">修改参数类型</typeparam>
    public interface IUpdate<in TUpdateRequest> where TUpdateRequest:IRequest,new()
    {
        /// <summary>
        /// 修改
        /// </summary>
        /// <param name="request">请求参数</param>
        [UnitOfWork]
        void Update([Valid] TUpdateRequest request);
    }
}
