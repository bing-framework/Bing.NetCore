using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.Canal.Server.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Canal.Server
{
    internal abstract class CanalClientHostedServiceBase
    {
        /// <summary>
        /// 注册类型列表
        /// </summary>
        private readonly List<Type> _registerTypeList;

        protected CanalClientHostedServiceBase(IServiceScopeFactory serviceScopeFactory
            , CanalConsumeRegister register)
        {
            _registerTypeList = new List<Type>();
            if (register.SingletonConsumeList != null && register.SingletonConsumeList.Any())
                _registerTypeList.AddRange(register.SingletonConsumeList);
            if (register.ConsumeList != null && register.ConsumeList.Any())
                _registerTypeList.AddRange(register.ConsumeList);
            if (!_registerTypeList.Any())
                throw new ArgumentNullException(nameof(_registerTypeList));
        }

        protected void Send(CanalBody canalBody)
        {

        }
    }
}
