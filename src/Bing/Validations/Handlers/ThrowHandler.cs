using System.Linq;
using Bing.Configurations;
using Bing.Validations.Abstractions;

namespace Bing.Validations.Handlers
{
    /// <summary>
    /// 验证失败，抛出异常 - 默认验证处理器
    /// </summary>
    public class ThrowHandler : IValidationHandler
    {
        /// <summary>
        /// 处理验证错误
        /// </summary>
        /// <param name="results">验证错误集合</param>
        public void Handle(ValidationResultCollection results)
        {
            if (results.IsValid)
                return;
            BingConfig.Current.ValidationHandler(results.First().ErrorMessage);
            //throw new Warning(results.First().ErrorMessage);
        }
    }
}
