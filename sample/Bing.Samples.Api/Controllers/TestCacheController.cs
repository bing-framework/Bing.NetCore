using Bing.Caching;
using Bing.Domains.Services;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Api.Controllers
{
    /// <summary>
    /// 测试缓存控制器
    /// </summary>
    [Route("api/[controller]/[action]")]
    public class TestCacheController : ApiControllerBase
    {
        public ITestCacheService TestCacheService { get; }

        public TestCacheController(ITestCacheService testCacheService)
        {
            TestCacheService = testCacheService;
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        [HttpPost]
        public UserInfoCache GetUserInfo([FromBody] UserInfoCache userInfo)
        {
            return TestCacheService.GetUserInfo(userInfo);
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        [HttpPost]
        public UserInfoCache GetUserInfoById([FromBody] UserInfoCache userInfo)
        {
            return TestCacheService.GetUserInfoById(userInfo);
        }
    }

    public interface ITestCacheService : IDomainService
    {
        UserInfoCache GetUserInfo(UserInfoCache userInfo);

        UserInfoCache GetUserInfoById(UserInfoCache userInfo);
    }

    public class TestCacheService : DomainServiceBase, ITestCacheService
    {
        public TestCacheService()
        {

        }

        [Cache]
        public UserInfoCache GetUserInfo(UserInfoCache userInfo)
        {
            return userInfo;
        }

        [Cache(Key = "UserInfo{userInfo:UserId}_{userInfo:UserName}")]
        public UserInfoCache GetUserInfoById(UserInfoCache userInfo)
        {
            return userInfo;
        }
    }

    /// <summary>
    /// 用户信息缓存
    /// </summary>
    public class UserInfoCache
    {
        /// <summary>
        /// 用户编号
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
    }
}
