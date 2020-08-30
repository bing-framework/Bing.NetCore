using Bing.Admin.Systems.Domain.Parameters;
using Bing.Exceptions;
using Bing.Extensions;

namespace Bing.Admin.Service.Shared.Requests.Systems.Extensions
{
    /// <summary>
    /// 管理员请求扩展
    /// </summary>
    public static class AdministratorRequestExtensions
    {
        /// <summary>
        /// 转换为参数
        /// </summary>
        /// <param name="request">请求</param>
        public static UserParameter ToParameter(this AdministratorCreateRequest request)
        {
            if (request == null)
                throw new Warning("请求参数不能为空");
            return new UserParameter
            {
                Nickname = request.Nickname,
                UserName = request.UserName,
                Password = request.Password,
                Gender = request.Gender,
                Enabled = request.Enabled.SafeValue(),
                IsSystem = true
            };
        }

        /// <summary>
        /// 转换为参数
        /// </summary>
        /// <param name="request">请求</param>
        public static UserParameter ToParameter(this AdministratorUpdateRequest request)
        {
            if (request == null)
                throw new Warning("请求参数不能为空");
            return new UserParameter
            {
                Nickname = request.Nickname,
                Gender = request.Gender,
                Enabled = request.Enabled.SafeValue(),
                Id = request.Id.ToGuid()
            };
        }
    }
}
