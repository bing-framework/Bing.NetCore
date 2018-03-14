using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bing.Configuration;
using Bing.Exceptions;

namespace Bing.Validations.Handlers
{
    /// <summary>
    /// 验证失败，抛出异常 - 默认验证处理器
    /// </summary>
    public class ThrowHandler:IValidationHandler
    {
        /// <summary>
        /// 处理验证错误
        /// </summary>
        /// <param name="results">验证错误集合</param>
        public void Handle(ValidationResultCollection results)
        {
            if (results.IsValid)
            {
                return;
            }

            //new BingConfig().ValidationHandler(results.First().ErrorMessage);
            throw new Warning(results.First().ErrorMessage);
        }
    }
}
